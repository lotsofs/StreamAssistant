# Infrastructure

The supporting modules that aren't Twitch, OBS or colour: configuration and settings, logging, periodic work, the
database, sound, speech, the language filter, the two periodic watchdogs and the clock. Each is a `static class`
holding its own state.

Read from the code. Where something was also run it says so. The scheduling code (disk alerts, the clock's
text format, ad warnings) has unit tests in `../StreamAssistant2.Tests`.

## Config

[Config.cs](../code/Core/Config.cs). `Config.Load()` runs before anything else, from `Program.Main`.

1. Reads **`paths.json`** from the working directory (so the app must run from the project folder). It
   holds only `directories.BotInput`, the folder of `secrets.json`, `settings.json`, `games.json` and
   `leftpanel.json`, with environment variables expanded. Every other directory is in `settings.json`
   ([Settings](#settings)); an older `paths.json` that still lists them loads, and they're ignored. Without
   a `BotInput`, `Load()` throws an `InvalidDataException` that `Main` shows as a message box. It is
   machine-specific, so it is gitignored; a `BeforeBuild` target in the csproj copies it from the tracked
   `paths.json.example` when missing, whose `BotInput` is `%APPDATA%\StreamAssistant2\Bot Input`.
2. Reads **`secrets.json`** from `BotInput`, which is outside this repository. If the file
   is missing, `Load()` creates the folder if needed, copies the tracked `secrets.json.example` there and
   throws a `FileNotFoundException` telling the user to fill it in; `Main` shows that as a message box. It
   does the same, with an `InvalidDataException`, when `twitchAuth` has no `login`.
3. Merges them into `Config.Data` (`Directories.BotInput` from the first, `Obs`, `TwitchAuth`, `TwitchBot`,
   `TwitchIds`, `StreamElements` from the second), with both logins lowercased. `twitchAuth` and
   `twitchBot` share a shape (`login`, `accessToken`, `refreshToken`, `clientId`): the broadcaster and
   the account chat goes through ([twitch.md](twitch.md#credentials-and-identity)). Without `twitchBot`,
   chat goes as the broadcaster. `streamElements` holds `channelId` and `jwt` for tips
   ([streamelements.md](streamelements.md#credentials)). A file without either block still loads; the
   values are then empty, and tips are off.

`BotInput` also holds **`settings.json`** ([Settings](#settings)), and **`games.json`** and
**`leftpanel.json`**, which aren't config: `Games.Load()` and
`LeftPanel.Load()` read them later in startup, and again on `!reloaddata`, with the colour data
([architecture.md](architecture.md#codeobs), [obs.md](obs.md#left-panel)). A missing file only logs,
and turns off per-game setup or the left panel; on a reload it keeps what was loaded.

JSON is read with `System.Text.Json` and `PropertyNameCaseInsensitive`, so `socketPassword` and
`SocketPassword` are the same. Unknown properties are ignored; the example's `twitchTestNames` block
has no matching property and is never read.

`Config.Data` starts as an empty `ConfigModel` before `Load`, which is what lets tests assign
`Config.Data.Directories.BotInput` directly. Never print or log anything from `Obs`, `TwitchAuth`,
`TwitchBot`, `TwitchIds` or `StreamElements`.

## Settings

[Settings.cs](../code/Core/Settings.cs) loads **`settings.json`** from `BotInput` into `Settings.Data`, a
[SettingsModel](../code/Core/SettingsModel.cs). It holds the owner's choices: every directory but
`BotInput`, the reward ids, the OBS scene, source and filter names, the train, the sound files and
volumes, the TTS voice, the disk-space thresholds, the ad interval, the FAQ cooldown and the money goal.
[reference.md § Settings](reference.md#settings) lists every key with its default.

- **Every setting is in the file.** The tracked [settings.json.example](../settings.json.example) in the
  project folder lists every key at its default, with comments. On a start with no `settings.json`, `Load`
  copies it into `BotInput` and logs `Created <path> from settings.json.example, every setting at its
  default`, so the file to edit always shows everything. A test checks that the example leaves no key out.
- **Defaults.** Every key defaults to the value the bot used before it was a setting, except the
  directories: `BotOutput` defaults to `%APPDATA%\StreamAssistant2\Bot Output`, and the others to empty,
  which turns their feature off. A section or key the file leaves out (one added by a later version, say)
  keeps its default, and each load logs them: `settings.json lacks <keys>; their defaults apply
  (settings.json.example has every key)`. The bot never writes keys into an existing file, which would lose
  its comments and layout. A sound given only its `File` or its `Volume` keeps the other.
- **Loading.** `Main` calls `Settings.Load(atStartup: true)` right after `Config.Load()` and creating the
  window, so its line shows in the window and nothing is logged before the log's folder, `BotOutput`, is
  known. `!reloaddata` calls `Settings.Load()` first, before the colour data, games and left panels. Each
  load replaces `Settings.Data` whole and logs `Loaded settings.json, all defaults` or
  `Loaded settings.json, changed <section>: <keys>; …`, the keys that differ from the defaults.
- **Read at use.** Code reads `Settings.Data` each time it needs a value and never keeps one, so a reload
  takes effect at the next use: the next train, colour change, game setup, sound, utterance, disk check,
  ad break or FAQ answer, and the dashboard's next refresh. Code that uses several values in one go takes
  one section (`LayoutSettings layout = Settings.Data.Layout`), so a reload can't mix old and new halfway.
- **`BotOutput` waits for a restart.** On a reload a changed `BotOutput` is set back to the running one,
  with `BotOutput changes after a restart`: the log file and the database are already there.
- **Strict parsing.** Keys are read in any case, and comments and trailing commas are allowed. A key no
  setting has, a `null`, bad JSON or a number out of range make the file broken, with a reason that names
  the key. Out of range means a negative time, a volume outside 0 to 1, disk repeat minutes that don't
  divide 60, an ad interval under 5 minutes, no capture slots, or a default goal under 0.01. Directory
  values have environment variables expanded.
- **A broken file** at startup stops the bot with a message box naming the file and the reason, as a bad
  `secrets.json` does: running on the defaults would put the log and a fresh, empty database in the
  default `BotOutput`. On `!reloaddata` it logs `Error SET1: settings.json: <reason>` and keeps the
  settings loaded. A missing file on a reload logs `settings.json not found, keeping the settings loaded`;
  at startup without the example as well, `settings.json not found, using the defaults`.
- **Tests** leave `Settings.Data` at the defaults or point `Settings.Data.Directories` at fixtures
  (`ColorData.Load`). Pure functions that tests feed other values take the settings as a parameter
  (`DiskSpace.Classify(freeBytes, settings)`, `ChannelPoints.RewardOf(id, rewards)`).

## Logging

[ConsoleLogger.cs](../code/Core/ConsoleLogger.cs). The class keeps its name from the console era; it now feeds the
WPF window ([dashboard.md](dashboard.md)).

| Call | File | Window | Use for |
|---|---|---|---|
| `ColoredLine(type, text)` | yes, timestamped | yes | everything a human should read |
| `Line(text)` | yes | yes (`None`) | uncoloured lines |
| `LogToFile(text, addTimestamp = false)` | yes | no | raw JSON payloads, exceptions |
| `LogToEventSubFile(text, fileName)` | `AssistantLogs\EventSubs\<fileName>` (`EventSubDirectory`) | no | one file per real EventSub or StreamElements event, for `!test replay` |

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
| `Notification` | something succeeded or was accepted: flush recorded, a train or colour redemption fulfilled or refunded, TTS enqueued |
| `ConnectionNotification` | `STARTED!`, `Data loaded`, `SHUTDOWN!`, EventSub, StreamElements and OBS connect lines |
| `Helix` | `HELIX> …` lines, one per Helix call, with the response code |
| `EventSubNotification` | `notification: <type>`, session welcome, subscribe results |
| `EventSubConfusion` | EventSub surprises (unhandled type, reconnect, unknown message) |
| `StreamElementsNotification` | `SE notification: <topic>`, `StreamElements welcome`, `StreamElements subscribed to channel.tips`, each `Tip: …` |
| `StreamElementsConfusion` | StreamElements surprises and session ends: `StreamElements session ended: …`, reconnect, close, connection lost, unhandled topic or message type, a repeated tip id |
| `AdNotification` | ad start and end |
| `Important` | warnings and drops the user should notice: colour data skipped, OBS unreachable, colour request failed, unexpected status codes |
| `SceneChangesImportant` | the start of a scene change worth spotting: `Game setup: <name> (<id>)` |
| `SceneChanges` | changes the bot makes to the stream's look and sound: the layout colour animation starting (`Changing layout color to …`), the background (`Image: Background → <file>`), the borders (`Borders → <inner>, <outer>`), each capture slot shown, and a train starting (`Choo choo!`). Warnings about these (no scheme, missing source) stay on `Important` |
| `SceneChangesUnimportant` | routine detail of a scene change: the grouped `→ none, hidden: …` line, and `Sidebar: Dynamic 1|2 → <file>` on each split index change |

## Periodic work

There is no scheduler. Anything time-based is a plain `async` loop in the module that owns it, started
fire-and-forget from `Program.StartBotAsync` and written like the connection loops: wait, do the work in a
try/catch that logs a code and carries on, repeat. A slow job delays only itself, and one job failing
can't stop another.

| Loop | Started by | Runs | Error code |
|---|---|---|---|
| disk-space check | `DiskSpace.Start()` | every whole minute | `DSK1` |
| OBS clock text and midnight date post | `Clock.Start()` | every whole minute | `CLK1`, `CLK2` |
| uptime poll | `TwitchUptime.Start()` | about when the stream's uptime ticks over a minute, and at once on going live | `UpT5` |
| ad warnings | `Ads.Process` | once per ad break, see below | `ADS1` per warning |
| connection health | `ConnectionHealth.Start()` | every second | `CHL1` |
| which image and text sources are showing in the stream scene (`Basic Stream Layout`), for the dashboard ([dashboard.md](dashboard.md#obs-sources)) | `ObsInputs.Start()` | every 2 s while OBS is ready | `OBI1` |

Details of each are under [Disk space](#disk-space), [Clock](#clock), [Uptime](#uptime),
[Connection health](#connection-health) and [events.md](events.md#ads). The loops end with the process; there is no shutdown hook.

**Ad warnings** are not a loop but one method, `Ads.RunScheduleAsync`, run per ad break with a
`CancellationTokenSource`. Starting a new round swaps in a fresh source and cancels the previous one, so the
old pending warnings stop; nothing else is affected.

To add something time-based, write a loop in the owning module and start it from `Program`
([development.md](development.md#schedule-something)).

## Database

[Database.cs](../code/Core/Database.cs) uses `Microsoft.Data.Sqlite`, with the file at
`<BotOutput>\streamAssistant.db`. Every call opens its own connection and disposes it; there is no pool
of your own, no transaction and no WAL setting.

`InitAsync()` runs at startup and issues `CREATE TABLE IF NOT EXISTS` for both tables, then makes sure
the money goal's one row exists:

```sql
flushes (
  flush_id   TEXT PRIMARY KEY,   -- the channel-point redemption id
  user_id    TEXT NOT NULL,
  user_login TEXT NOT NULL,
  created_at INTEGER NOT NULL    -- Unix seconds, UTC
)
-- index idx_flush_user_id on flushes(user_id)

money_goal (
  id            INTEGER PRIMARY KEY CHECK (id = 1),   -- one row
  current_cents INTEGER NOT NULL,                     -- tips since the last !resetmoney
  goal_cents    INTEGER NOT NULL
)
-- INSERT OR IGNORE (1, 0, MoneyGoal.DefaultGoal in cents): with the default settings a first start
-- begins at 0 / 999 EUR; later starts keep the row
```

| Method | Statement |
|---|---|
| `InsertFlushAsync(flushId, userId, userLogin)` | `INSERT`; a duplicate redemption id throws |
| `RetrieveFlushesAsync(userId)` | `SELECT flush_id … WHERE user_id = $user_id` |
| `DeleteFlushesAsync(userId)` | `DELETE … WHERE user_id = $user_id` |
| `GetMoneyAsync()` | `SELECT current_cents, goal_cents`; `(0, <default goal in cents>)` if the row is missing |
| `AddMoneyAsync(cents)` | `UPDATE … SET current_cents = current_cents + $cents` |
| `SetMoneyGoalAsync(cents)` | `UPDATE … SET goal_cents = $cents` |
| `ResetMoneyAsync()` | `UPDATE … SET current_cents = 0` |

All are parameterised. The flush table is for the toilet rewards
([events.md](events.md#how-the-flush-pair-works)), the money one for the money goal
([obs.md](obs.md#money-goal)). Amounts are whole cents, so sums don't drift.
`IF NOT EXISTS` never alters an existing table, so a schema change needs a real migration step in
`InitAsync`, not just an edited `CREATE`.

## Sound

[Sound.cs](../code/Audio/Sound.cs) plays alert clips with NAudio: a fresh `AudioFileReader` and `WaveOutEvent` per
play, both disposed on `PlaybackStopped`, on the default output device. Playback is asynchronous;
`PlaySound` returns immediately and several can overlap.

The folder is `Directories.Sounds` in `settings.json`, and each sound's file and volume are in its `Sounds`
section ([reference.md](reference.md#sounds)). With no folder set, `PlaySound` logs `Sounds not set in
settings.json, <sound> not played` and plays nothing. A missing file throws `FileNotFoundException` into
the caller. The defaults:

| `Sounds` | File | Volume | Played by |
|---|---|---|---|
| `TribalHymn` | `Tribal hymn.mp3` | 0.5 | all sub alerts |
| `TheClap` | `theclap.mp3` | 0.4 | targeted gift subs, gift bombs |
| `Team17Applauds` | `Team17-Applauds.mp3` | 0.75 | cheers |
| `IndianAnthem` | `IndianAnthem.mp3` | 0.4 | StreamElements tips |
| `Flush` | `Flush.wav` | 0.4 | the flush reward |
| `Warning` | `Warning.wav` | 0.6 | the `Spam` disk-space alert |

There is no delayed variant: handlers `await Task.Delay(...)` between sounds.

## Text to speech

[TextToSpeech.cs](../code/Audio/TextToSpeech.cs) wraps `System.Speech`'s `SpeechSynthesizer` (SAPI) behind a queue.

- The static constructor starts the worker. Before each item the worker selects the voice in
  `Speech.Voice` (default **`Microsoft Catherine`**) when it isn't the current one, so `!reloaddata`
  changes it. If that voice isn't installed, `SelectVoice` throws and the item logs `Error 5` unspoken, as
  does every item after it until the setting names an installed voice.
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

[LanguageFilter.cs](../code/Audio/LanguageFilter.cs) holds a dictionary of offensive words from an old Oxford
dictionary export, each with a replacement chosen by the author, and `ReplaceBadWords` runs
`string.Replace(key, value, ignoreCase)` for each in dictionary order.

It is deliberately naive: plain substring replacement, no word boundaries, so innocent words that contain
an entry are changed too (`knockout` becomes `strikeout`). The file states this and why it is acceptable
here; don't "improve" it without being asked. Because replacements run in order, a longer entry must
stay above any shorter entry it contains (`jewess` before `jew`, `faggot` before `fag`).

## Disk space

[DiskSpace.cs](../code/Timing/DiskSpace.cs) watches free space so the VOD recording doesn't run out of room. It looks
at the drive in `DiskSpace.Drive` (`A:\` by default; with or without the backslash, any case) and falls back to the
first drive from `DriveInfo.GetDrives()` if there is no such drive.

`DiskSpace.Start()` runs a loop that wakes on every whole minute (`ClockMarks.UntilNextMinute`: UTC, recomputed
from the current time each pass, with a 10 ms floor) and checks free bytes against three thresholds (GiB, `1073741824`
bytes). The thresholds and repeat intervals are settings (`SpamGiB`, `SpamEveryMinutes`, …, read at each check);
the defaults:

| Free space | Level | Repeat interval | Sound and TTS |
|---|---|---|---|
| under 2 GiB | `Spam` | every minute | yes, with each alert |
| under 15 GiB | `Warn` | every 12 minutes | no |
| under 100 GiB | `Notify` | every 60 minutes | no |

`AlertState.Update(freeBytes, nowUtc)` decides whether a reading alerts:

- **A level that gets worse alerts at once**, including the first crossing.
- **A level that persists alerts on the first check inside each new window of its interval.** The windows are
  `ClockMarks.Window` (`ticks / interval`), and every interval divides an hour (the settings check enforces
  it), so they start on the clock marks: by default `Warn` at `:00, :12, :24, :36, :48`, `Notify` on the hour. It reads the time *when the check runs*, so a wake that lands a
  hair early (11:59:59.999) sees the old window and does nothing, and the immediate re-check lands on the mark.
- Recovering above the `Notify` threshold resets it, so the next crossing alerts at once. Improving between levels
  doesn't alert early.

A first crossing at, say, 12:11 alerts immediately and then again at the 12:12 mark. The time arithmetic lives in
[ClockMarks.cs](../code/Timing/ClockMarks.cs), pure functions shared with the [clock](#clock), covered by
`ClockMarksTests`. The state is a pure
class (`AlertState`), covered by `DiskAlertTests`. The alert text is the `*_CHAT` constants at the top of the
file. Only `Spam` adds more: the `Warning` sound before the chat line and the TTS line after it, with no
delay between them.

## Clock

[Clock.cs](../code/Timing/Clock.cs) does the time-of-day work. `Clock.Start()` runs one loop that wakes on every
whole minute (`ClockMarks.UntilNextMinute`, local time, recomputed each pass) and does two jobs, each in its own
try/catch, so one failing can't stop the other:

- **The OBS clock text.** It writes `yyyy-MM-dd HH:mm` to the text source `Clock.Source` (`Text: Time Of Day`) with
  `Obs.SetInputSetting`. The format quotes its separators (`yyyy'-'MM'-'dd HH':'mm`), because an unquoted `:` is
  the culture's time separator. While OBS isn't ready (`ObsConnection.IsReady`) it skips silently, rather than
  logging the `IsConnected()` warning every minute. There is no write at start or on connect, so after the bot
  starts, or OBS restarts, the text can be up to a minute stale. Errors (a missing source, OBS dropping
  mid-request) log `CLK1`.
- **The midnight date post.** On the first pass of a new local day it posts the date (`yyyy-MM-dd`) to chat.
  `Start()` records the boot date, so starting the bot never posts. Errors log `CLK2`.

**Timing.** The wake lands within one Windows timer tick (about 16 ms) of :00. When it lands early, the pass
rewrites the minute that's still current and sees the old date, then `UntilNextMinute`'s 10 ms floor brings
the real pass. So the OBS text flips, and the date posts, within a few tens of ms of the mark. Waits are
recomputed every pass, so nothing drifts, and a sleep or clock change costs at most one minute.

`ClockText` is pure, covered by `ClockTests`.

## Uptime

[TwitchUptime.cs](../code/Timing/TwitchUptime.cs), started by `TwitchUptime.Start()`, asks
`https://decapi.me/twitch/uptime/<broadcaster login>` (a third-party scraper, used instead of Helix) how long the stream
has been live, and writes the answer to the OBS text source `Uptime.Source` (`Text: Stream Uptime`).

- The loop calls `UptimeCheck()`, then waits `SecondsUntilNextMinute` or until woken, whichever comes
  first. Its first run is immediate. Errors that escape the check log `UpT5`.
- **Go-live wake.** `PollNow()` wakes the loop for an immediate poll. It's called on OBS's stream start
  (`ObsConnection.OnStreamStateChanged`) and on Twitch's `stream.online` (`StreamEvents.Online`). The wake is a
  `SemaphoreSlim(0, 1)`, so any number of calls leaves at most one wake pending (the rest are dropped), and a
  wake during a poll triggers one more poll right after it. If Twitch or decapi still report offline at that
  moment, the next poll is the normal 60 s later.
- **Rate limit.** `UptimeCheck` is private and only the loop calls it, so requests are serial. A request
  less than 5 s (`MIN_GAP`) after the previous one waits out the rest first. A burst of wakes therefore
  never sends decapi two requests within 5 s.
- On success `ProcessUptime` turns the text into `Offline` (the response ends ` is offline`) or
  `<h>h <mm>m`, parsing days, hours, minutes and seconds with one regex. Every group in that regex is
  optional, so it always matches; text it doesn't understand, such as an error message, yields `0h 00m`,
  and the `Regex Error` chat message in the code is unreachable.
- Any failure gives `?`: a non-success status (logged `Uptime request: HTTP Response Code <n>`) or an
  exception (`Error UpT4`).
- **The OBS text** is `U: <value>` (`U: 2h 05m`, `U: Offline`, `U: ?`), written on every poll, so a restarted
  OBS is right again by the next poll. While OBS isn't ready (`ObsConnection.IsReady`) it skips silently,
  rather than logging the `IsConnected()` warning every minute. An OBS error (such as a renamed source)
  escapes to the loop and logs `UpT5`.

**How the poll lands just after each uptime minute.** It's self-correcting rather than guaranteed.
The seconds decapi reports (`37 minutes, 19 seconds`) are kept in `_lastSecondsListed`, and the loop waits
`60 - seconds`. So the next request reaches decapi past the minute by about one round trip, plus the
fraction of a second the whole-number seconds dropped. Each successful poll re-aims the next, so nothing
drifts.

This relies on decapi's seconds being current. Checked 2026-10-10: responses carry `cache-control:
no-cache, private`, and a live channel read `:24`, `:28`, `:33` on calls 4–5 s apart. `Task.Delay` firing up to one
timer tick (~16 ms) early is within the round-trip margin. After a failure, while offline, and on the first
live poll, the seconds are 0, so the next poll is a plain 60 s later and not aimed until a live answer
re-aims it.

## Connection health

[ConnectionHealth.cs](../code/Timing/ConnectionHealth.cs) holds the thresholds for the three connection ages
the dashboard shows, and logs when a connection goes stale. The thresholds are one table for both the
window's colours and the log, and **dead is the reconnect timeout itself**:

| Connection | Age | Warn (yellow) | Bad (orange) | Dead (red) = reconnect |
|---|---|---|---|---|
| `Irc` | `TwitchIRCManager.TimeSinceLastPing` | 300 s | 360 s | `SilenceTimeout`, 420 s |
| `EventSub` | `TwitchEventSub.KeepAliveTimer.Elapsed` | 12 s | 15 s | `KeepAliveTimeout`, 20 s |
| `StreamElements` | `StreamElementsSocket.SinceLastMessage.Elapsed` | 40 s | 50 s | `SilenceTimeout`, 70 s |

StreamElements' age resets on each reply to the 30 s probe ([streamelements.md](streamelements.md#keepalive-the-probe)),
so a healthy connection counts to about 30 s. Warn means one reply is late.

`ConnectionHealth.Start()` runs a loop that checks all three every second (EventSub's bad band is only 5 s wide)
through one `Tracker` per connection. A tracker logs:

- once on entering **Bad** (or jumping straight to Dead), on `Important`:
  `EventSub quiet for 00:16, reconnecting at 00:20`
- once on getting back to **Healthy** after that, on `ConnectionNotification`, with the longest age seen:
  `EventSub recovered after 00:18 of silence`

Nothing else: Warn alone is silent, a wobble between Warn and Bad doesn't repeat the line, and Dead adds
nothing because the transport's own reconnect logs it (`Error TIRC2`, `EventSub session ended:
KeepAliveTimeout`). Readings without data are ignored: before the first IRC line
(`TwitchIRCManager.HasReceivedLine`), while EventSub has no welcomed session (`TwitchEventSub.IsConnected`),
and while StreamElements has no acknowledged subscribe (`StreamElementsSocket.IsConnected`, also false when
tips are off).
So startup is silent, and a degradation that ends in a reconnect still gets its recovery line once the new
session is up. Nothing goes to chat. The tracker is pure and tested in `ConnectionHealthTests`.
