# Infrastructure

The supporting modules that aren't Twitch, OBS or colour: configuration, logging, periodic work, the
database, sound, speech, the language filter and the two periodic watchdogs. Each is a `static class`
holding its own state.

Read from the code. Where something was also run it says so. The scheduling code (disk alerts, midnight
date, ad warnings) has unit tests in `../StreamAssistant2.Tests`.

## Config

[Config.cs](../Config.cs). `Config.Load()` runs before anything else, from `Program.Main`.

1. Reads **`paths.json`** from the working directory (so the app must run from the project folder). It
   holds only `directories`: `BotInput`, `BotOutput`, `Trains`, `Colors`, `ColorSchemes`. It is
   machine-specific, so it is gitignored; a `BeforeBuild` target in the csproj copies it from the tracked
   `paths.json.example` when missing.
2. Reads **`secrets.json`** from `Directories.BotInput`, which is outside this repository. If the file
   is missing, `Load()` copies the tracked `secrets.json.example` there and throws a
   `FileNotFoundException` telling the user to fill it in; `Main` shows that as a message box.
3. Merges them into `Config.Data` (`Directories` from the first, `Obs`, `TwitchAuth`, `TwitchIds` from the
   second).

JSON is read with `System.Text.Json` and `PropertyNameCaseInsensitive`, so `socketPassword` and
`SocketPassword` are the same. Unknown properties are ignored; the example's `twitchTestNames` block
has no matching property and is never read.

`Config.Data` starts as an empty `ConfigModel` before `Load`, which is what lets tests assign
`Config.Data.Directories.Colors` directly. Never print or log anything from `Obs`, `TwitchAuth` or
`TwitchIds`.

## Logging

[ConsoleLogger.cs](../ConsoleLogger.cs). The class keeps its name from the console era; it now feeds the
WPF window ([dashboard.md](dashboard.md)).

| Call | File | Window | Use for |
|---|---|---|---|
| `ColoredLine(type, text)` | yes, timestamped | yes | everything a human should read |
| `Line(text)` | yes | yes (`None`) | uncoloured lines |
| `LogToFile(text, addTimestamp = false)` | yes | no | raw JSON payloads, exceptions |
| `LogToEventSubFile(text, fileName)` | `AssistantLogs\EventSubs\<fileName>` (`EventSubDirectory`) | no | one file per real EventSub event, for `!test replay` |

- **Location**: `<BotOutput>\AssistantLogs\<yyyy-MM-dd HHmmss>.log`, named for when the process first
  used the logger. Event dumps go in `AssistantLogs\EventSubs\`.
- **Format**: `[yyyy-MM-dd HH:mm:ss.fff] text`, each entry followed by a blank line. Event dumps are
  appended without a trailing newline.
- **Writes** are async and fire-and-forget (`_ = LogToFileAsync(...)`), serialised by one
  `SemaphoreSlim`, and each opens, appends and closes the file. That is deliberate: a hard kill
  (stopping the debugger) loses nothing already handed to the logger except lines still waiting on the
  semaphore. Measured (2026-10-07, NVMe, real assembly): 10,000 `ColoredLine` calls return in ~19 ms
  and reach disk in ~850 ms (~85 µs per line, off the caller's thread). A channel plus one held
  `StreamWriter` drained the same 10,000 in ~20 ms, but callers don't wait either way and real
  bursts (a gift bomb) are a few hundred lines, so it wasn't worth the durability trade.
- **The `LineLogged` event** is raised on the *caller's* thread, in call order, wrapped in a try/catch so
  a bad subscriber can't break logging. Subscribers must marshal to their own thread.
- **If file writes fail** (drive full or missing), `Error LOG1` is raised on the event once, until a
  write succeeds again. It never goes to the file, since that is what is failing.
- A `null` text is ignored.

### Colour channels

`ColorType` is a *semantic* channel; the window owns the hex colours (`_logColors` in
[MainWindow.xaml.cs](../ui/MainWindow.xaml.cs)). Pick the channel that matches the event.

| Channel | Used for |
|---|---|
| `None` | plain lines; the gift-bomb debug lines |
| `Error` | every `Error <code>` line ([reference.md](reference.md#error-codes)) |
| `ChatIncoming` | chat messages and unrecognised IRC lines |
| `ChatOutgoing` | `> <message>` for each chat message the bot sends |
| `Notification` | something succeeded or was accepted: flush recorded, colour request fulfilled, layout change started, TTS enqueued |
| `ConnectionNotification` | `STARTED!`, `SHUTDOWN!`, EventSub and OBS connect lines |
| `Helix` | declared and given a colour, but unused |
| `EventSubNotification` | `notification: <type>`, session welcome, subscribe results |
| `EventSubConfusion` | EventSub surprises (unhandled type, reconnect, unknown message) and, oddly, Helix redemption updates |
| `AdNotification` | ad start and end |
| `Important` | warnings and drops the user should notice: colour data skipped, OBS unreachable, colour request failed, unexpected status codes |

## Periodic work

There is no scheduler. Anything time-based is a plain `async` loop in the module that owns it, started
fire-and-forget from `Program.StartBotAsync` and written like the connection loops: wait, do the work in a
try/catch that logs a code and carries on, repeat. A slow job delays only itself, and one job failing
can't stop another.

| Loop | Started by | Runs | Error code |
|---|---|---|---|
| disk-space check | `DiskSpace.Start()` | every whole minute | `DSK1` |
| midnight date post | `TwitchUptime.Start()` | once at each local midnight | `UpT6` |
| uptime poll | `TwitchUptime.Start()` | about when the stream's uptime ticks over a minute | `UpT5` |
| ad warnings | `Ads.Process` | once per ad break, see below | `ADS1` per warning |
| connection health | `ConnectionHealth.Start()` | every second | `CHL1` |

Details of each are under [Disk space](#disk-space), [Uptime and clock check](#uptime-and-clock-check),
[Connection health](#connection-health) and [events.md](events.md#ads). The loops end with the process; there is no shutdown hook.

**Ad warnings** are not a loop but one method, `Ads.RunScheduleAsync`, run per ad break with a
`CancellationTokenSource`. Starting a new round swaps in a fresh source and cancels the previous one, so the
old pending warnings stop; nothing else is affected.

To add something time-based, write a loop in the owning module and start it from `Program`
([development.md](development.md#schedule-something)).

## Database

[Database.cs](../Database.cs) uses `Microsoft.Data.Sqlite`, with the file at
`<BotOutput>\streamAssistant.db`. Every call opens its own connection and disposes it; there is no pool
of your own, no transaction and no WAL setting.

`InitAsync()` runs at startup and issues `CREATE TABLE IF NOT EXISTS` for the single table:

```sql
flushes (
  flush_id   TEXT PRIMARY KEY,   -- the channel-point redemption id
  user_id    TEXT NOT NULL,
  user_login TEXT NOT NULL,
  created_at INTEGER NOT NULL    -- Unix seconds, UTC
)
-- index idx_flush_user_id on flushes(user_id)
```

| Method | Statement |
|---|---|
| `InsertFlushAsync(flushId, userId, userLogin)` | `INSERT`; a duplicate redemption id throws |
| `RetrieveFlushesAsync(userId)` | `SELECT flush_id … WHERE user_id = $user_id` |
| `DeleteFlushesAsync(userId)` | `DELETE … WHERE user_id = $user_id` |

All are parameterised. Only the toilet rewards use it ([events.md](events.md#how-the-flush-pair-works)).
`IF NOT EXISTS` never alters an existing table, so a schema change needs a real migration step in
`InitAsync`, not just an edited `CREATE`.

## Sound

[Sound.cs](../Sound.cs) plays alert clips with NAudio: a fresh `AudioFileReader` and `WaveOutEvent` per
play, both disposed on `PlaybackStopped`, on the default output device. Playback is asynchronous;
`PlaySound` returns immediately and several can overlap.

The folder is the one **hard-coded path not in `paths.json`**: `D:\Repositories\Stream-Resources\Alert
Sounds\`. A missing file throws `FileNotFoundException` into the caller.

| `Sounds` | File | Volume | Played by |
|---|---|---|---|
| `TribalHymn` | `Tribal hymn.mp3` | 0.5 | all sub alerts |
| `TheClap` | `theclap.mp3` | 0.4 | targeted gift subs, gift bombs |
| `Team17Applauds` | `Team17-Applauds.mp3` | 0.75 | cheers |
| `IndianAnthem` | `IndianAnthem.mp3` | 0.4 | only the commented-out donation code |
| `Flush` | `Flush.wav` | 0.4 | the flush reward |
| `Warning` | `Warning.wav` | 0.6 | nothing yet |

There is no delayed variant: handlers `await Task.Delay(...)` between sounds.

## Text to speech

[TextToSpeech.cs](../TextToSpeech.cs) wraps `System.Speech`'s `SpeechSynthesizer` (SAPI) behind a queue.

- The static constructor selects the voice **`Microsoft Catherine`** and starts the worker. If that voice
  isn't installed `SelectVoice` throws, the type fails to initialise, and every later TTS call throws
  `TypeInitializationException`. The first touch is `ReportStart()` at the end of startup (log `Error
  PRG1`), and after that handlers fail silently ([twitch.md](twitch.md#adding-an-eventsub-event)).
- `EnqueueSpeech(text)` logs `TTS Enqueue: …` on `Notification` and calls `SpeakAsync`. `SpeakAsync`
  returns a `Task` that completes when the text has been spoken (the cheer handler discards it).
  Empty or whitespace text is ignored.
- One worker thread drains a `ConcurrentQueue`, woken by a `SemaphoreSlim`, speaking one item at a time
  with `synth.SpeakAsync` and waiting for `SpeakCompleted`. A failure logs `Error 5` and faults that
  item's task.
- `StopSpeech()`, `PurgeQueue()` and `Dispose()` exist and nothing calls them. There is no way to
  interrupt speech from chat yet.

Chat text is run through the [language filter](#language-filter) *before* being spoken only where the
handler does it itself (resub text, cheer text). Usernames and gift recipients are spoken as they are.

## Language filter

[LanguageFilter.cs](../LanguageFilter.cs) holds a dictionary of offensive words from an old Oxford
dictionary export, each with a replacement chosen by the author, and `ReplaceBadWords` runs
`string.Replace(key, value, ignoreCase)` for each in dictionary order.

It is deliberately naive: plain substring replacement, no word boundaries, so innocent words that contain
an entry are changed too (`knockout` becomes `strikeout`). The file states this and why it is acceptable
here; don't "improve" it without being asked. Because replacements run in order, a longer entry must
stay above any shorter entry it contains (`jewess` before `jew`, `faggot` before `fag`).

## Disk space

[DiskSpace.cs](../DiskSpace.cs) watches free space so the VOD recording doesn't run out of room. It looks
at drive `A:\` and falls back to the first drive from `DriveInfo.GetDrives()` if there is no `A:`.

`DiskSpace.Start()` runs a loop that wakes on every whole minute (`ClockMarks.UntilNextMinute`: UTC, recomputed
from the current time each pass, with a 10 ms floor) and checks free bytes against three thresholds (GiB, `1073741824`
bytes):

| Free space | Level | Repeat interval | TTS |
|---|---|---|---|
| under 2 GiB | `Spam` | every minute | yes, with each alert |
| under 15 GiB | `Warn` | every 12 minutes | no |
| under 100 GiB | `Notify` | every 60 minutes | no |

`AlertState.Update(freeBytes, nowUtc)` decides whether a reading alerts:

- **A level that gets worse alerts at once**, including the first crossing.
- **A level that persists alerts on the first check inside each new window of its interval.** The windows are
  `ClockMarks.Window` (`ticks / interval`), and every interval divides an hour, so they start on the clock marks: `Warn` at `:00,
  :12, :24, :36, :48`, `Notify` on the hour. It reads the time *when the check runs*, so a wake that lands a
  hair early (11:59:59.999) sees the old window and does nothing, and the immediate re-check lands on the mark.
- Recovering above 100 GiB resets it, so the next crossing alerts at once. Improving between levels
  doesn't alert early.

A first crossing at, say, 12:11 alerts immediately and then again at the 12:12 mark. The time arithmetic lives in
[ClockMarks.cs](../code/Timing/ClockMarks.cs), pure functions shared with the midnight post, covered by
`ClockMarksTests`. The state is a pure
class (`AlertState`), covered by `DiskAlertTests`. The alert text is the `*_CHAT` constants at the top of the
file; the TTS line is spoken only for `Spam`.

## Uptime and clock check

[TwitchUptime.cs](../TwitchUptime.cs) has two unrelated jobs, started together by `TwitchUptime.Start()`.

**The midnight date post.** At each local midnight it posts the date (`yyyy-MM-dd`) to chat. The loop works out
the next local midnight (`ClockMarks.NextLocalMidnight`, strictly after now), waits in steps of at most ten minutes until
the clock reaches it (so a clock change or a sleep can't leave it asleep past midnight), and posts the
*target's* date, so waking a hair early can't post yesterday's. Errors log `UpT6`. The OBS clock-text update is
a `TODO`, and would be its own per-minute loop.

**The uptime poll** asks `https://decapi.me/twitch/uptime/lotsofs` (a third-party scraper, used instead of
Helix) how long the stream has been live.

- The loop calls `UptimeCheck()` and then waits `SecondsUntilNextMinute`. Its first run is immediate. Errors
  that escape the check log `UpT5`.
- `UptimeCheck` refuses to call twice within 5 s (`UPTIME_READ_TIMEOUT`), logging a warning.
- On success `ProcessUptime` turns the text into `Offline` (the response ends ` is offline`) or
  `<h>h <mm>m`, parsing days, hours, minutes and seconds with one regex. Every group in that regex is
  optional, so it always matches; text it doesn't understand, such as an error message, yields `0h 00m`,
  and the `Regex Error` chat message in the code is unreachable.
- The seconds it read are kept, and the next poll is `60 - seconds` later, so successive polls land just
  after the stream's uptime ticks over to a new minute. HTTP failures set the stored seconds to 0, so a failure
  waits a full minute instead of retrying at once.
- HTTP failures set the value to the status code, or `Error` (log `Error UpT4`).
- The result is stored in `_previousUptime` and **goes nowhere**: writing it to an OBS text source is a
  `TODO`. Right now the feature only generates traffic.

## Connection health

[ConnectionHealth.cs](../code/Twitch/ConnectionHealth.cs) holds the thresholds for the two connection ages
the dashboard shows, and logs when a connection goes stale. The thresholds are one table for both the
window's colours and the log, and **dead is the reconnect timeout itself**:

| Connection | Age | Warn (yellow) | Bad (orange) | Dead (red) = reconnect |
|---|---|---|---|---|
| `Irc` | `TwitchIRCManager.TimeSinceLastPing` | 300 s | 360 s | `SilenceTimeout`, 420 s |
| `EventSub` | `TwitchEventSub.KeepAliveTimer.Elapsed` | 12 s | 15 s | `KeepAliveTimeout`, 20 s |

`ConnectionHealth.Start()` runs a loop that checks both every second (EventSub's bad band is only 5 s wide)
through one `Tracker` per connection. A tracker logs:

- once on entering **Bad** (or jumping straight to Dead), on `Important`:
  `EventSub quiet for 00:16, reconnecting at 00:20`
- once on getting back to **Healthy** after that, on `ConnectionNotification`, with the longest age seen:
  `EventSub recovered after 00:18 of silence`

Nothing else: Warn alone is silent, a wobble between Warn and Bad doesn't repeat the line, and Dead adds
nothing because the transport's own reconnect logs it (`Error TIRC2`, `EventSub session ended:
KeepAliveTimeout`). Readings without data are ignored: before the first IRC line
(`TwitchIRCManager.HasReceivedLine`) and while EventSub has no welcomed session (`TwitchEventSub.IsConnected`).
So startup is silent, and a degradation that ends in a reconnect still gets its recovery line once the new
session is up. Nothing goes to chat. The tracker is pure and tested in `ConnectionHealthTests`.
