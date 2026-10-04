# Robustness fixes

Known defects and loose ends in the tree as it stands. Mostly independent of each other and of
[single-console-logging.md](single-console-logging.md) — each can be done, tested and committed on
its own, except where an item says otherwise.

Items are indexed in [TODO.md](TODO.md) — summaries, severities and the suggested order of work
live there, along with the conventions these codes follow. This file holds only the detail.

---

## DRV — Log drive failure kills all console output

**Severity:** the bot goes quiet with no indication why — and the trigger is a condition this project
already knows it hits.

`ConsoleLogger.ColoredLineAsync` awaits the **file** write before the console/pipe write:

```csharp
string message = $"[{TimeStamp()}] {text}";
await LogToFileAsync(message);      // <-- throws here and nothing below ever runs
await _writeLock.WaitAsync();
```

`LogToFileAsync` does `Directory.CreateDirectory(LogDirectory)`, which is
`<BotOutput>\AssistantLogs` (`D:\Repositories\Stream-Resources\Bot Output\AssistantLogs`), then
`File.AppendAllTextAsync`. If `D:`
is disconnected, read-only or **full**, that throws. Because `ColoredLine` is fire-and-forget
(`_ = ColoredLineAsync(...)`), the exception is captured into a discarded `Task`: no console output,
no file entry, no stack trace. The only symptom is the log window going quiet while the bot keeps
running.

Note the irony: `DiskSpace.cs` exists specifically because this machine runs low on disk, and
`AssistantLogs` is at 33 MB across 133 files with `Custom/` adding 585 more and no pruning (see
[ROT](#rot--log-directories-grow-without-bound)). Low disk space is the exact condition that disables
the logging that would tell you about it.

**Fix** — make the two sinks independent, so neither can take the other down:

```csharp
static async Task ColoredLineAsync(ConsoleColor color, object text) {
    if (text == null) return;
    string message = $"[{TimeStamp()}] {text}";
    _ = LogToFileAsync(message);    // no longer awaited; cannot block or kill the console path
    ...
}
```

and wrap the body of `LogToFileAsync` in `try/catch`, reporting the failure with `Debug.WriteLine`
only — **never** by calling back into `LogToFile`, or a dead drive becomes an infinite feedback loop.
Hoist `Directory.CreateDirectory` out of the per-line path into `Start()`.

This leaves file writes ordered among themselves (still serialised by `_semaphore`) while removing the
ordering dependency between the file sink and the console sink.

> If [single-console-logging.md](single-console-logging.md) is going ahead soon, that change fixes
> this structurally via two independent channels, and this item should be skipped rather than done
> twice. Do it here only if that plan stays banked.

**Verify:** point `BotOutput` in `paths.json` at a path on a non-existent drive (`Z:\nope\`) and
confirm the app
still starts, still prints to the log window, and still connects to Twitch.

---

## NRE — `NullReferenceException` inside the error handler

**Severity:** only triggers when the log viewer fails to launch — but then it fails confusingly.

Three null-dereferences in `ConsoleLogger`, all on the failure path, and all three already flagged by
the compiler as `CS8602` (`ConsoleLogger.cs` lines 127, 137 and 153):

- `ColoredLineAsync` calls `await RestartAsync()` and then uses `_writer.Write(...)` without
  re-checking null. `_writer` is declared `BinaryWriter?`.
- The `catch` block does the same thing again, so the handler itself throws.
- `RestartAsync` does `if (!_process.HasExited)` on a `Process?`. `Process.Start` with
  `UseShellExecute = true` legitimately returns `null`, and does so here whenever
  `logger/StreamAssistantLog.exe` is missing — exactly what happens after a `dotnet clean`, or if the
  `PublishLogger` target fails. It also catches only `InvalidOperationException`, while `Kill(true)`
  can throw `Win32Exception`.

Net effect: a missing viewer executable produces an unobserved `NullReferenceException` per log line
instead of one clear "could not start the log viewer" message.

**Fix** — null-check `_process` before `HasExited`, null-check `_writer` after `RestartAsync` and bail
with a `Debug.WriteLine` if it is still null, broaden the `catch` to `Exception`, and don't retry the
write inside the `catch` (let the next line attempt a fresh reconnect).

> Same caveat as [DRV](#drv--log-drive-failure-kills-all-console-output): the single-console change
> deletes all of this code. Skip if that is happening soon.

**Verify:** delete `bin/Debug/net8.0/logger/` and run. Expect one clear diagnostic and a functioning
bot, not a silent stall.

---

## WRN — Make the build warning-clean

**Severity:** housekeeping. None of the warnings are in `code/Color/`.

- [ ] `ChatHandler.ChatMessage` is declared and referenced nowhere (confirmed by grep — the only hit
      is its own declaration). Its three fields raise `CS8618`; they used to raise `CS0649` too, but
      the `InternalsVisibleTo` added for the test project hides that one (a friend assembly could
      assign them). Delete it.
- [ ] `Cheers.MONEY_PER_BIT` is assigned but never used (`CS0414`) — the only consumer is the
      commented-out `Money.Current` line.
- [ ] The `CA1416` warnings from `TextToSpeech` are Windows-only SAPI calls seen through a
      platform-neutral TFM. Retargeting to `net8.0-windows` would silence them honestly — the app is
      Windows-only anyway (SAPI, OBS, drive letters, `user32.dll` P/Invokes).
- Leave alone: the `CS0162` "unreachable code" warning in `TwitchEventSub` is just the
  `const bool IS_TEST = false` switch. Not a defect.
- See also [NRE](#nre--nullreferenceexception-inside-the-error-handler) for the three `CS8602`
  warnings, and note the single-console change deletes that code.

---

## ROT — Log directories grow without bound

**Severity:** housekeeping — but see
[DRV](#drv--log-drive-failure-kills-all-console-output) for why it is not purely cosmetic.

`AssistantLogs/` holds 133 files / 33 MB, and `AssistantLogs/Custom/` holds 585 files / 2.5 MB — one
per sub, resub, gift and community-gift event ever received, each written by `LogToCustomFile` with
its own `Directory.CreateDirectory` call. Nothing prunes either.

**Options**, in increasing effort: do nothing and revisit; add a `Clock` job at startup that deletes
files older than N days (`Clock.AddGenericJobs` is the place, and `Clock` already supports one-shot
jobs); or move `Custom/` writes into the SQLite database that `Database.cs` already manages, which is
a better fit for per-event records than 585 loose files.

---

## CHN — One exception logged on the wrong channel

**Severity:** trivial.

`Subscriptions.cs` logs a caught exception with `ConsoleLogger.ColorType.None` (white) and passes the
`Exception` object directly, making it the only `catch` in the tree that doesn't follow the
`ColoredLine(ColorType.Error, "Error <code>")` + `LogToFile(ex)` convention. Bring it in line.
