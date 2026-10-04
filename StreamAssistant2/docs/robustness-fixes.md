# Robustness fixes

Known defects and loose ends in the tree as it stands. Mostly independent of each other and of
[single-console-logging.md](single-console-logging.md) — each can be done, tested and committed on
its own, except where an item says otherwise.

Items are indexed in [TODO.md](../TODO.md) — summaries, severities and the suggested order of work
live there, along with the conventions these codes follow. This file holds only the detail.

---

## SEC — Credentials in a past transcript

`secrets.json` was read in full during the 2026-10-02 session, so the live Twitch `accessToken` /
`refreshToken` and the OBS `socketPassword` are in that session's transcript. That session confirmed
all four values differ from `secrets.json.example`, i.e. they are real rather than placeholders.

- [ ] Rotate the Twitch token pair in the dev console. The OBS password is only reachable on
      `127.0.0.1`, so it is lower risk.

Gitignoring the file was the right call; this is purely about the transcript. (Later sessions have
read only the `directories` block, so the tokens have not been re-exposed.)

Related: the `refreshToken` is loaded but never used, so the bot cannot survive the ~4 h expiry of a
Twitch user access token without a manual edit. Noted in `CLAUDE.md` as well.

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

`LogToFileAsync` does `Directory.CreateDirectory(LOG_DIRECTORY)` against the hard-coded
`D:\Repositories\Stream-Resources\Bot Data\AssistantLogs\`, then `File.AppendAllTextAsync`. If `D:`
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

**Verify:** point `LOG_DIRECTORY` at a path on a non-existent drive (`Z:\nope\`) and confirm the app
still starts, still prints to the log window, and still connects to Twitch.

## RAN — `random` is case-sensitive

**Severity:** medium, reproduced. `random` → works; `Random` → `Color change request FAILED`.

`SingleColorParser.TryParse` matches the keyword with `input == "random"`. Channel-point `user_input` is
raw viewer text, so capitalisation is a coin flip.

**Fix:** `string.Equals(input, "random", StringComparison.OrdinalIgnoreCase)`.

**Test:** `SingleColorParserTests.Random_IsCaseInsensitive` is written and skipped; remove its `Skip`.

Secondary, defensive only: the keyword is checked *below* the named-table and system lookups, so a
colour-table entry literally named "random" would shadow it. None of the four JSON files under
`Stream-Resources\Input\Bot\Colors` has one today, so moving the keyword above `TryGetNamed` changes
nothing right now.

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

## CAN — System colours report the raw input as their name

**Severity:** low, cosmetic.

The system-colour branch of `SingleColorParser.TryParse` stores the raw `input` as the entry name, while the other
branches store a canonical one (`colorInfo.OriginalName`). `Color.FromName` is case-insensitive and
hands back the canonical spelling in `c.Name`, so using that would make chat say
`Changing to color Control [system]` rather than `… color control [system]`.

**Test:** `SingleColorParserTests.SystemColour_ReportsCanonicalName` is written and skipped; remove
its `Skip`.

---

## RND — `GetRandomColor` hard-codes one table name

**Severity:** latent, reproduced.

```csharp
public static NamedColor GetRandomColor() {
    var colors = Tables["encycolorpedia"].Entries;
```

`encycolorpedia.json` does exist in the Colors directory (52 KB), so this works today. But the key is
the *filename* of a file in an externally-configured directory. Rename or move it and this throws
`KeyNotFoundException` — confirmed by running `GetRandomColor` against an unloaded table set — inside
an `async Task` that `TwitchEventHandler` discards with `_ =`, so the random-colour redemption would
silently do nothing.

Also note `Tables[...].Entries.ElementAt(r)` is O(n) over a 52 KB dictionary on every random
redemption. Irrelevant at this call rate; mentioned only so it isn't mistaken for an index lookup.

**Fix:** fall back to any loaded table (or pick a random table, then a random entry) when the
preferred key is absent, and log `Important` if the preferred table is missing. Alternatively make the
preferred table name a `Config` value alongside the directory itself.

While there, build the pick list once in `LoadTables` as a cached `NamedColor[]`, so a random pick is
an index lookup instead of `ElementAt`. If no tables loaded at all, return a random `#RRGGBB` (source
`random`) rather than throwing.

**Test:** `RegistryTests.GetRandomColor_WorksWithoutEncycolorpedia` is written and skipped (it loads
a fixture set without that table); remove its `Skip`.

---

## CMD — Port the colour chat commands

**Severity:** enabler. Would make every other colour item cheap to test.

`ChatHandler.CheckForAdminCommands` still has `!changecolorrandom` and `!changecolor` as commented
Streamer.Bot-era stubs. Routing them to `LayoutColoring.TryChangeToTriple` / `TryChangeToSingle` /
`ChangeToRandom` would make colour changes testable from chat instead of by spending channel points,
which is what makes [RAN](#ran--random-is-case-sensitive) and
[VFY](#vfy--finish-verifying-the-colour-paths-in-the-app) annoying to check. Broadcaster-gated, like
`!test`.

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

Related: `LOG_DIRECTORY` is a hard-coded absolute path in `ConsoleLogger.cs`, while the comparable
Colors / ColorSchemes / Trains paths all live in `secrets.json` under `directories`. Moving it there
would make DRV testable without editing source, and is a two-line change to `DirectoriesConfig`.

---

## CHN — One exception logged on the wrong channel

**Severity:** trivial.

`Subscriptions.cs` logs a caught exception with `ConsoleLogger.ColorType.None` (white) and passes the
`Exception` object directly, making it the only `catch` in the tree that doesn't follow the
`ColoredLine(ColorType.Error, "Error <code>")` + `LogToFile(ex)` convention. Bring it in line.

---

## VFY — Finish verifying the colour paths in the app

The colour resolution paths have been verified out-of-process against the compiled assembly with real
tables loaded — `hsv()` scaling, `rgb()`, bare `r,g,b`, hex, `random`, `Control` via the system
branch, `cornflowerblue` via `encycolorpedia`, and `asdfgh` correctly failing. What has **not** been
exercised is the same code reached through a real redemption, because `dotnet build` cannot complete
while `bin\Debug\net8.0\StreamAssistant2.dll` is locked by a VS debug session.

- [ ] Redeem the single-colour reward (`REWARD_ID_COLOR_SINGLE` in `ChannelPoints.cs`) with
      `hsv(210,50,80)`. Expect `Color change request fulfilled` in the logger and
      `Changing to color hsv(210°,50%,80%) [hsv]: #6699cc …` in chat.
- [ ] Same reward with `Control`. Expect `[system]` and `#f0f0f0`. Only the WinForms palette names
      (`Control`, `Window`, `Highlight`, `ButtonFace`, `Desktop`) report `[system]` — every web name
      resolves earlier through `htmlcolors.json`.
- [ ] Spot-checks on the same path: `cornflowerblue` → table source, `#A1B2C3` → `hex` and `#a1b2c3`, `random` →
      randomises, `asdfgh` → **FAILED** (not silently black). Every hex in the chat line should be
      lowercase.
- [ ] Accent-free input reaches accented names through the loose fallback. Redeem the single
      reward with `cafe au lait`. Expect `Café au lait [encycolorpedia]` and `#a67b5b` in chat.
- [ ] Optional: add `Stream-Resources\Input\Bot\Tests\Color Single.txt` so `!test` can reach this
      code. None of the 19 existing payloads does — `Channel Points Custom Reward Redemption Add.txt`
      carries a different reward id and `"user_input": "pogchamp"`.

---

