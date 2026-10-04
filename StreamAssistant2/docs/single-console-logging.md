# Single-console logging with a sticky status footer

**SCL — Status: banked, designed, not started.** Indexed in [TODO.md](TODO.md).

Remove the external `StreamAssistantLog` viewer process and show the colour-coded log in the app's own
console window, **with the terminal's native scrolling and scrollback fully intact**.

## Context

`ConsoleLogger` spawns `StreamAssistantLog.exe` as a child process and pipes colour-tagged lines to it
over a named pipe. That second process exists for exactly one reason: `Dashboard.WriteLoop` owns the
main console, repainting a status bar at row 0 every 15 ms without clearing to end-of-line or
restoring the cursor, so any other write to that console gets trampled. The log had nowhere else to go.

The approach that *would* break the scrollback requirement is a VT scroll region
(`ESC[<top>;<bottom>r`). Terminals implement a margin-constrained line feed as a rectangle move
*inside* the viewport — the top line of the region is overwritten in place and nothing is appended to
history. Windows Terminal and conhost share an `AdaptDispatch` implementation that behaves this way,
as does xterm. **So a top-pinned status line costs the scrollback.** Hence the footer goes at the
bottom: the log then scrolls on the terminal's normal full-viewport path, the one scrollback is built
around.

Decisions already taken:

- Status goes in a **sticky bottom footer**, built for **N lines** (1 initially, 2–3 stat lines later).
- `StreamAssistantLog` is **unwired but kept on disk**.
- A set of adjacent improvements is deliberately **out of scope** — see [Deferred](#deferred).

No VT escapes, no `ENABLE_VIRTUAL_TERMINAL_PROCESSING`, no scroll regions. Positioning uses
`Console.SetCursorPosition`, which `code/Ui/Dashboard.cs` already proves works in this environment.
Avoiding VT also preserves a safety property: chat text is logged verbatim in `ChatHandler`, so
enabling escape interpretation would hand viewers a terminal-injection vector.

## Architecture

**One owner of the console cursor.** This is the invariant the whole design rests on. A single worker
task performs every console write — log lines *and* footer redraws. Nothing else touches `Console`.
If two writers interleave, the footer corrupts.

```
62 ColoredLine call sites ─┐
18 LogToFile call sites ───┤  (source unchanged)
                           ▼
                   ConsoleLogger  (public surface unchanged)
                      │                      │
            TryWrite  │                      │  TryWrite
                      ▼                      ▼
        _consoleQueue: Channel<Entry>   _fileQueue: Channel<string>
                      │                      │
             ONE console worker        ONE file worker
                      ▼                      ▼
          log lines + sticky footer     dated .log append
                      ▲
                      │ SetStatusLines(string[])
                  Dashboard  (never touches Console)
```

Two channels rather than one, so a blocked console write cannot also stall the forensic file log.
Today the file write happens *before* the pipe write, so file logging already survives a stuck viewer —
a shared worker would regress that.

This mirrors the `Channel<T>` + single-worker pattern already in `LayoutColoring.cs`
(`StartWorker` / `ProcessColorChangeQueueAsync`). Reuse that shape, including its double-start guard.

### The existing viewer is the existence proof

`StreamAssistantLog/Program.cs` renders the log today using nothing but `Console.ForegroundColor` +
`Console.WriteLine` — no VT, no escape sequences, no cursor addressing — and scrolling in that window
already works. The no-VT rendering path is therefore empirically confirmed in the real terminal, and
the only genuinely new code here is the footer bookkeeping. Nothing in the viewer needs porting, and
two of its behaviours disappear outright:

- its `catch (ArgumentException)` → `"INVALID CONSOLE COLOR"` guard exists only because the colour
  crosses a process boundary as a raw byte; in-process it is a real enum value;
- `if (message.EndsWith("SHUTDOWN!")) break;` — sniffing log *text* to decide when to exit — is
  replaced by the channel drain in `Dispose()`.

It also never sets `Console.OutputEncoding`, confirming the emoji mangling (deferred **UTF**) is
pre-existing rather than introduced by this change.

## Changes

### 1. `ConsoleLogger.cs`

**Delete:** `_pipeName`, `_pipe`, `_writer`, `_process`, `_writeLock`; `StartAsync`,
`CreateConnectionAsync` (including the leftover `Debug.WriteLine(_process)`), `ColoredLineAsync`,
`RestartAsync`, `DisposePipe`. Drop `using System.IO.Pipes;`, add `using System.Threading.Channels;`.

**Keep byte-identical** so no call site changes: the `ColorType` enum and every member, `Start()`,
`ColoredLine(ColorType, object)`, `Line(object)`, `LogToFile(object, bool = false)`,
`LogToCustomFile(object, string)`, `TimeStamp(bool = false)`, `Dispose()`. Keep `Line()` despite its
zero callers — retaining it keeps the diff purely internal.

**New internals:**

- `readonly record struct Entry(ConsoleColor Color, string Message);`
- Two unbounded channels (`SingleReader = true`, `SingleWriter = false`), plus
  `static Task? _consoleWorker, _fileWorker;`
- `Start()` becomes synchronous and idempotent (log `Important` on double-start, as
  `LayoutColoring.StartWorker` does), launching both workers.
- `ColoredLine` formats on the **caller's** thread — `string message = $"[{TimeStamp()}] {text}";` —
  keeps its existing `if (text == null) return;` guard, then `TryWrite`s to both channels. Formatting
  at enqueue time keeps timestamps in call order and stringifies the object while it still holds the
  state the caller meant. Both `TryWrite`s are lock-free and never block, so the IRC/EventSub read
  loops stay insulated exactly as they are today. A `false` return (only possible after
  `TryComplete`) falls back to `Debug.WriteLine`.
- `LogToFile` keeps its signature and `addTimestamp` semantics; enqueues to the file channel only.
- `FileLoopAsync`: `await foreach` over the reader, each iteration in its own
  `try/catch { Debug.WriteLine(...) }`. **The catch must never re-enqueue** — a dead `D:` drive would
  become an infinite feedback loop. Keep the `+ NewLine + NewLine` so the blank-line-separated file
  format is preserved. Hoist `Directory.CreateDirectory` to `Start()`.
- Keep `_semaphore` **solely** for `LogToCustomFile`, which stays as-is (one call site in
  `TwitchEventHandler`, one file per event). Comment it, or the leftover reads as vestigial.
- Track `_lastColor` as worker-only state and set `Console.ForegroundColor` only when it changes —
  it is a `SetConsoleTextAttribute` P/Invoke, worth skipping on the chat hot path where consecutive
  lines are nearly always `ChatIncoming`.

### 2. New: the sticky footer

Put it in `ConsoleLogger` or a small `code/Ui/StatusFooter.cs` beside `Dashboard`. Driven only by the
console worker.

**State:** `static volatile string[] _statusLines = [];` plus the footer's current height.

**Public entry point:** `SetStatusLines(params string[] lines)` — stores the array and nothing more.
Callable from any thread; does not draw. **Footer height is `_statusLines.Length`**, so going from 1
line to 3 is just passing 3 strings. This is the point of the design.

**Invariant the worker maintains:** the cursor is parked at column 0 of the footer's first row, and
the footer occupies the rows below it.

**Emitting a log line:**

1. Set colour if changed.
2. Write the message + newline at the cursor — this overwrites the footer's first row and advances.
3. Redraw the footer at the new position.
4. Leave the cursor at column 0 of the footer's first row.

**Redrawing the footer:** write each line padded to `Console.WindowWidth - 1` to erase the previous
contents, recomputing the width every redraw so a resize self-heals. Never cache row positions —
recompute from `Console.CursorTop`, because writing near the bottom of the viewport scrolls everything
up. Clamp `SetCursorPosition` coordinates to the buffer and catch `IOException` /
`ArgumentOutOfRangeException`.

**Guards:** if `Console.WindowHeight` is too small for the footer (say `< 2 * lines`), skip the footer
and emit log lines plainly. If `Console.IsOutputRedirected`, skip the footer and all colour entirely —
plain `WriteLine` only.

**Ticking while idle:** race the channel read against a timer so the footer refreshes with no log
traffic:

```csharp
var read = _consoleQueue.Reader.WaitToReadAsync().AsTask();
if (await Task.WhenAny(read, Task.Delay(RefreshMs)) == read) { /* drain and emit */ }
else { RedrawFooter(); }
```

`RefreshMs` of 250–1000 is plenty for second-granularity values. Today's 15 ms loop costs ~66
iterations/sec and ~600 console calls/sec — each a conpty round trip under Windows Terminal — for a
readout that changes once a second.

**On shutdown:** erase the footer rows and leave the cursor below them, so the shell prompt is not
left sitting under a stale status line.

### 3. `code/Ui/Dashboard.cs` — rewrite

Delete the whole `WriteLoop` body, `using System.Text;`, and the dead unused `_input` field. Dashboard
stops touching `Console` completely.

It becomes a loop that computes the status strings and calls `SetStatusLines(...)`. Keep the existing
thresholds verbatim as named constants — IRC 300/360/420 s, EventSub 10/12/15 s. Keep
`public static void Start() { _ = UpdateLoop(); }` per repo convention, but **wrap the loop body in
try/catch** — today it has none, so one `IOException` from `SetCursorPosition` silently kills the
status display for the rest of the session.

Colour inside the footer is possible (the worker owns the cursor, so it can emit colour mid-footer),
but keep v1 monochrome and encode severity as text (`!` / `!!` prefix). Less to get wrong while the
cursor bookkeeping is new.

**Render unknown values as `--:--`.** `TwitchIRCManager._lastPingTime` starts at `DateTime.MinValue`,
so `TimeSinceLastPing` is ~739,000 days at boot; and `TwitchEventSub.KeepAliveTimer` is
`Stopwatch.StartNew()` at static init, so it passes 15 s before EventSub has even connected. Today the
`mm\:ss` format masks both into plausible-looking but meaningless values. Gate on "has this metric
ever been healthy" rather than editing the Twitch classes.

### 4. `Program.cs`

- Move `ConsoleLogger.Start()` **up**, to just after the console setup and before `Config.Load()`.
  Neither loader logs today, but starting the sink first is the correct composition order.
- **Delete `await Task.Delay(1000)`.** Its only purpose was giving the child viewer time to connect to
  the pipe. One second off startup.
- Shutdown order stays as-is, but `Dispose()` now actually drains; add `Console.CursorVisible = true`
  so running from an existing shell does not leave it without a caret.
- Keep `ConsoleHelper.DisableQuickEdit()` — now *more* important, as it is the main mitigation for the
  conhost click-drag stall. The implementation is already correct (quick-edit is an stdin flag).

### 5. `StreamAssistant2.csproj`

- Delete the `ItemGroup` containing the `ProjectReference` to `StreamAssistantLog`. Nothing uses its
  types; the only source reference is the `logger/StreamAssistantLog.exe` path being deleted.
- Delete the `PublishLogger` target. This also removes a nested `dotnet publish` from every build.
- Leave `EnsureSecretsIniExists` and the `secrets.json` item alone. `StreamAssistant2.sln` needs no
  change — it never contained the logger project.
- `.vscode/launch.json` needs no change: keep `"console": "externalTerminal"` (a real console with
  real scrollback) and `cwd` = project dir (`secrets.json` and `icon.ico` are relative).
- **Manual step:** `dotnet clean` will not remove `bin/<cfg>/net8.0/logger/`, because an `Exec`
  produced it. Delete it by hand so a stale `StreamAssistantLog.exe` doesn't later suggest the viewer
  is still wired up.

### 6. Shutdown / flush

`Dispose()` keeps its signature (one call site, last statement in `Main`):

1. `TryComplete()` both writers — late calls then fall through to `Debug.WriteLine` rather than being
   dropped or reordered.
2. Bounded `Task.WaitAll(..., TimeSpan.FromSeconds(1.5))` on both workers, in try/catch. Blocking the
   main thread is safe — the workers are pool threads with no main-thread dependency. **The timeout
   must stay ≤ ~1.5 s:** `AppDomain.CurrentDomain.ProcessExit` also completes the TCS, and the CLR
   gives that handler roughly 2 seconds before killing the process.
3. Erase the footer, `Console.ResetColor()`, restore `CursorVisible` — each guarded.

This also fixes a current race where the final `"SHUTDOWN!"` line reaches neither screen nor file,
because `ColoredLine` is fire-and-forget and `Dispose()` tears the pipe down immediately after.

## Latent bugs fixed as a side effect

Worth noting in the commit message. The first two are described in full in
[robustness-fixes.md](robustness-fixes.md) as **DRV** and **NRE** — **if this change goes ahead,
those two items should be skipped there rather than done twice**, because this design removes the
code they describe.

1. A missing or full log drive silently kills all console output (robustness **DRV**). Fixed
   structurally by the two independent channels.
2. `NullReferenceException` thrown from inside the error handler (robustness **NRE**). The code is
   deleted.
3. **Log ordering is non-deterministic.** Fire-and-forget → `await` file IO under a semaphore → *then*
   take `_writeLock` means two concurrent callers can reach the console in the opposite order from
   their calls, and console and file order can diverge. Single-consumer channels give FIFO per sink.
4. `Dashboard.WriteLoop` has no try/catch, and its status bar never clears to end-of-line and never
   saves/restores the cursor — the root cause of the two-process split.

## Verification

**Scrollback — the non-negotiable.** Raise the buffer first (conhost Properties → Layout → Screen
Buffer Size Height; or Windows Terminal `historySize`). Emit several thousand lines, then confirm **in
both conhost and Windows Terminal**:

- mouse wheel scrolls up through history;
- the scrollbar thumb shrinks as lines accumulate, and dragging works;
- click-drag / Mark select + Enter copies;
- **the oldest line still in the buffer is reachable and its sequence number matches the expected
  buffer depth.** This last one is the actual test — a scroll-region implementation passes the first
  three and fails this;
- the footer does not appear in scrolled-back history (an occasional stray footer row under conpty is
  cosmetic, not a loss of log lines);
- `rg '\\x1b|\\u001b' --type cs` returns nothing, and `SetConsoleMode` is still called only on
  `STD_INPUT_HANDLE`.

**Footer integrity.** Resize narrower and wider while lines flow — no smearing, no duplicated footer,
no stale text past the new width. Shrink the window to a few rows and confirm the short-window guard
trips instead of corrupting. Then call `SetStatusLines` with 1, 2 and 3 strings and confirm the footer
grows and shrinks cleanly with no orphaned rows — this proves the eventual multi-line stats work.

**Colours.** Temporary admin command emitting one `ColoredLine` per `ColorType` member, compared
against the enum. Then alternate two colours rapidly to prove the "set only when changed" optimisation
never prints a line in the previous colour. (`ColorType.ZK = Black` is invisible on black —
pre-existing, and unused by any call site.)

**Ordering.** Fire a few hundred `ColoredLine` calls from several `Task.Run`s with per-thread sequence
numbers; assert per-thread monotonicity on screen, no line split across colours, and that the dated
log file holds the same lines in the same order.

**Nothing stalls.** In legacy conhost, Edit → Mark and hold a selection for 30+ seconds while chat
flows. Confirm the dated log file keeps receiving lines throughout (proving the file worker is
decoupled), IRC stays connected with no reconnect in the log, and on release the backlog flushes in
order with original timestamps.

**Shutdown.** Ctrl+C → `"SHUTDOWN!"` appears on screen *and* is the last line in the log file; exit
under ~2 s; footer erased, colour and caret restored; prompt lands on a clean line. Repeat with the
window close button to exercise the `ProcessExit` path within the CLR's budget.

**Viewer really gone.** No `StreamAssistantLog.exe` in Task Manager; after a clean rebuild
`bin/Debug/net8.0/logger/` is not recreated; build log has no nested `dotnet publish`.

> Scrollback *depth* is a terminal setting, not an app setting. "It doesn't go back far enough" will
> look like an app bug and isn't.

## Deferred

Deliberately out of scope for this change. All eight are registered in [TODO.md](TODO.md), which
also records which of them depend on SCL landing first — **FTR** and **DEL** do; the other six stand
on their own and can be done at any time.

- **ALR — Band-crossing alert lines.** A coloured log line when IRC/EventSub degrades or recovers,
  debounced, with the startup false-positive suppressed. Puts connection health in the dated log for
  the first time. If built: don't copy `DiskSpace.Notify`'s mechanics — it shares one
  `previousPrintTime` across all severities and gates on `now.Second > 0`, silently depending on
  `Clock` firing exactly on minute boundaries.
- **RED — No-console / redirected guards in `Program.cs`.** `Console.CursorVisible = false` throws
  when stdout is redirected, so `StreamAssistant2.exe > out.txt` crashes at startup today. (The
  `IsOutputRedirected` checks inside the *footer* are part of this change; this item is the
  `Program.cs` setup block.)
- **UTF — UTF-8 console output.** `Console.OutputEncoding = Encoding.UTF8` for the emoji the bot sends
  and the chat it logs; also needs a TrueType console font under legacy conhost.
- **BUF — Buffered file writes.** One held `StreamWriter` flushed when the queue drains, replacing
  open/append/close per line on the per-chat-message hot path. Deferred because it trades away
  durability of the log tail on a hard kill.
- **TTL — Metrics in the window title.** The nearest thing to a *top*-anchored readout that costs no
  scrollback, and it keeps ticking even while a conhost selection has the console body blocked. Two
  lines of code.
- **FTR — More footer lines.** 2–3 stat lines are expected eventually. Already supported by
  `SetStatusLines`; just decide what goes in them.
- **DEL — Delete the `StreamAssistantLog` project.** Unwired by this change but kept on disk. If it is
  ever removed, first document the wire format somewhere durable (1 byte `ConsoleColor`, int32 LE
  length, UTF-8 payload); `ConsoleLogger.cs` is currently its only specification.
- **CFG — `LOG_DIRECTORY` into `Config`.** A hard-coded absolute `D:\` path, while the comparable
  Colors / ColorSchemes / Trains paths already live in `secrets.json` under `directories`. See
  **ROT** in [robustness-fixes.md](robustness-fixes.md).

## Follow-up for `CLAUDE.md`

It documents the two-process design, including the "never `Console.Write` from anywhere else in this
process" rule. This change inverts that into "never write to `Console` except from the console
worker". Update it when this lands.
