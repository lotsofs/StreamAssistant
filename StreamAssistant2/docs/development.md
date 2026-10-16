# Development

How to build, run, test and verify, how to work next to a bot that is already running, and recipes for
the changes this codebase sees most. Conventions (formatting, error handling, the warning-free build)
are in [CLAUDE.md](../CLAUDE.md) and aren't repeated here.

## Prerequisites

- Windows, and the .NET 8 SDK. The app is `net8.0-windows` WPF; SAPI speech, drive letters and the
  Windows-only `System.Drawing` colour names make it Windows-only.
- The `Microsoft Catherine` SAPI voice installed (see [infrastructure.md](infrastructure.md#text-to-speech)).
- A `secrets.json` in `Directories.BotInput`; the first run creates it from the template and stops.
- Optional at runtime: OBS with its websocket server on port 4455. The bot copes with OBS being closed.

## Commands

Run all of these from `StreamAssistant2/` (the project folder). The app resolves `paths.json` and
`secrets.json.example` relative to the working directory.

```bash
dotnet build StreamAssistant2.csproj                 # bin/<Config>/net8.0-windows/
dotnet build StreamAssistant2.csproj -c Release
dotnet run --project StreamAssistant2.csproj
dotnet test ../StreamAssistant2.Tests/StreamAssistant2.Tests.csproj
```

VS Code's `build` task and the `.NET Console Launch` configuration in `.vscode/` do the same. The launch
configuration uses the internal console (the app has no console of its own) and sets `cwd` to the
workspace folder.

## Working next to a running bot

The owner runs the bot from the editor with F5, often while a stream is on. So:

- **Never stop or relaunch the running bot.** While it runs it holds `bin/` open, so a normal build
  can't overwrite the DLL.
- **Build to a scratch folder** when the bot may be running:
  `dotnet build StreamAssistant2.csproj -p:BaseOutputPath=<scratch>/`. It still shares `obj/`, which is
  harmless.
- **Don't open an extra window.** Only `Program.Main` creates the dashboard; scratch programs that
  reference the assembly never do. Don't launch the bot to look at it.
- **Writing to the real log folder is fine.**
- **Scratch programs and tests must never touch Twitch or OBS.** Anything that reaches
  `TwitchIRCManager.SendMessage`, `LayoutColoring` (which posts to chat *and* drives OBS) or
  `TestEventRunner.Run` (which runs real handlers) is off limits unless the owner asks.
- **Don't run `git add`, `commit` or `push`** unless explicitly asked. Reading git state is fine.

## Verification, by area

`code/Color/`, the scheduling logic (disk alerts, the clock's text format, ad warnings), the IRC and
EventSub timeouts, the EventSub and StreamElements session loops, the tip sentences and the FAQ commands
have automated tests.
Everything else is checked by running the app and reading the dashboard.

### Unit tests

xUnit project `../StreamAssistant2.Tests` (a sibling folder so the bot's default file globbing doesn't
compile it). The test project references the bot's project, and the bot grants it
`InternalsVisibleTo`. Its files sit in folders that mirror `code/` (`Color/`, `Twitch/`, `Timing/`, …), so
`code/Games/Games.cs` is tested in `Games/GamesTests.cs`. `ColorData.cs` and `Fixtures/` stay at its root:
`ColorData.cs` holds the assembly-wide attributes and finds the bot's `paths.json` two folders up from
itself.

| File | Covers |
|---|---|
| `ColorNameComparerTests` | strict equality (case, spacing, punctuation) and `Fold` |
| `ColorUtilTests` | hex normalisation, `Darken`/`Lighten`, `ToHex`, `ToOBS`, `HsvToRgb` |
| `RegistryTests` | table and scheme loading, duplicates, prefix forms, random pick, split colours lowercased and a bad one dropped, sign schemes with an unknown trend or scheme dropped |
| `SingleColorParserTests` | every input form, loose matching, `random`, misses |
| `TripleColorParserTests` | the separator-weight rule, slot padding, the token cap |
| `RealDataSmokeTest` | loads the real colour data named in `paths.json`, and checks `gameschemes`' `none` has every split colour and SWAT4 a sign scheme for every trend; skipped if absent |
| `ColorData.cs` | fixture loading helpers and the `RealDataFact` attribute |
| `DiskAlertTests` | `DiskSpace.AlertState`: immediate first and worsening alerts, repeats on the clock marks, the early-wake case, recovery |
| `ClockMarksTests` | `ClockMarks`: window numbering on the marks, `UntilNextMinute` and its floor |
| `ClockTests` | `Clock.ClockText`: the format holds under a culture with other separators. Never runs the loop, which writes to OBS and chat |
| `AdsScheduleTests` | `Ads.RunScheduleAsync`: order, cancelling, a throwing sender, isolation from other work. Uses fake senders, never `SendMessage` |
| `IrcReadTimeoutTests` | `TwitchIRCManager.ReadLineOrTimeoutAsync` over a loopback `TcpListener`: silence times out, outer cancellation stays a cancellation, lines and a close come through |
| `EventSubReceiveTimeoutTests` | `TwitchEventSub.ReceiveFullMessage` over a loopback `HttpListener` WebSocket: silence and a stall mid-message time out, split frames join, a Close frame and a dead socket give their reasons, outer cancellation throws. A raw-TCP WebSocket (handshake by hand) drops the connection with a TCP reset or a plain close, which gives `ConnectionLost` |
| `EventSubConnectTimeoutTests` | `TwitchEventSub.ConnectOrTimeoutAsync`: a connect to a non-routable address times out, outer cancellation stays a cancellation, a loopback `HttpListener` connects |
| `GiftlessRecipientTests` | bomb recipients whose bomb never arrives are reported once (through the real `HandleSubGiftNotif` bomb path, with a short `GiftlessWait`), and a bomb that did arrive isn't |
| `SubscriptionMessageTests` | the `SubscriptionMessages.Build…Message` sentences (sub, resub, targeted gift, bomb, including the month separator, the bomb tier and the "and N other people" count, and the giftless chat line), `CommunityGiftSub` waiting (recipients before or after the count, and a timeout with a partial list), and `SweepOldBombs` |
| `ConnectionHealthTests` | `ConnectionHealth`: band boundaries for all three connections, dead equals the reconnect timeouts, and the tracker (silent without data, one line on going bad, none for wobble or Warn alone, one recovery line with the longest silence, the reconnect gap ignored) |
| `EventSubLoopTests` | the real `TwitchEventSub.StartConnectionLoop` against a loopback fake of Twitch (WebSocket sessions plus the subscribe endpoint), through the `EventSubUrl` / `SubscriptionsUrl` / `RetryDelay` / `NotificationHandler` overrides: fresh session, planned reconnect, refused reconnect, a dropped connection (`ConnectionLost`, no `TES1`/`TES3`), 409 and 500 on subscribe. Events go to a capture, never the real handlers |
| `EventSubReconnectTests` | `TwitchEventSub.ConnectToReconnectUrlAsync` and `DrainOldSocketAsync` against a scripted loopback server: welcome, 4007 close, no welcome, wrong first message, missing id, hung connect, cancellation; draining handles notifications and stops on close or the limit |
| `IrcConnectTimeoutTests` | `TwitchIRCManager.ConnectOrTimeoutAsync`: a connect to a non-routable address times out, outer cancellation stays a cancellation, a loopback listener connects |
| `StreamElementsLoopTests` | the real `StreamElementsSocket.StartConnectionLoop` against a loopback fake of Astro, through the `AstroUrl` / `Times` / `NotificationHandler` overrides and `Config.Data.StreamElements`: the subscribe frame (topic, room, token), acknowledgement and delivery, repeating probes, a refused probe keeping the session, `reconnect` resuming with its token once, `err_unauthorized` stopping for good, a refused subscribe retrying, silence and a server close reconnecting, a quiet cancel, and no window line carrying the JWT or reconnect token. Events go to a capture, never `Tips` |
| `StreamElementsRetryTests` | `StreamElementsSocket.RetryDelay` doubling to its cap, and the default silence limit matching the health deadline |
| `GamesTests` | `Games`: `games.json` parsing (slot objects, the `default` volume, `Aspect`, case), `ResolveCategory`, `Lookup`, `ResolvePanel` (`!changepanel`), the change commands (`!changegame`, and `!changecapture` / `!changepanel` with nothing loaded), going live and boot (Helix fetch and the OBS wait replaced), `RunStep`; `GameCapture`'s slot assignment, volumes and log lines; `GameBackground.PathFor`, `GameSidebar.Choose` (the index + 1, the highest below when missing, the lowest when none is below, any case, the second series numbered on its own, a game without images) and `GameBorder.Files` (the aspect's own pair, 16x9 without one, the plain pair when the aspect has none, none without either or the directory); `GameScene.Named` and `Choose` (layout and games scenes by prefix, the aspect's scene, 16x9 without one, the rest hidden, none for an unknown aspect); `LayoutColoring.Filters` (borders and attachments take inner and outer, the scene the text, twins and real) and `LayoutScenes`; `ChannelPoints.RedemptionLine`. Never reaches OBS |
| `LeftPanelTests` | `LeftPanel.GroupName` (each scene's own prefix), `Numbered`, `Parse` (the file shape, case, missing and null parts, skipped and pause panels, group-name clean-up, entries that name another entry, chains, dead ends and loops, `Sign`, `Keep`, the defining `Entry`), `Which` (the default, an entry named by another, the game's own) and `Lookup` (any case, the `default` fallback). `LeftPanelRotationTests`: `Transition` and `Missing`, and the real `RunAsync` against fake OBS calls (a step's order with the copy listing and the reference item hidden, timings and the wrap, waiting for OBS, one panel and none, cancelling, missing groups, a throwing call, and splits: the `Previous` panels then the interrupted panel again, a split during them, none without `Previous`, a single panel waiting for splits; skips in the loop, during a split and on a single panel; a kept family staying up while the others move; split colours: the highlights on the first step only, each `Previous` step's colours by its sign before showing, and none without a sign, without colours or with an unreadable sign; plus the panel indexes each step reports); never reaches OBS. `LeftPanelPlanTests`: `Plan` on a layout like the real one (the families move together to the lowest copy hidden in each, and copy sources take its number; remembered values come along only with the families they were set with; consecutive `Splits Current` panels alternate; the fallback when no number is free in every family; an `Always` family keeps its copy; a kept family that's up stays, the others move avoiding its number unless they can't, a kept family that isn't up moves, `Keep` alone shows its groups, and with every family kept the sources go on the kept copy; numbered names stand for their family; unnumbered sources get the copy's number too; panels without copies are set as written) and `Exists`. The rotation tests' fake OBS has copies, the reference item and a wrong-prefix group per scene. `LeftPanelStatusLinesTests`: the dashboard column's lines from `StatusLines` (sections, markers, `Always` on once shown, the timer and where there is none, panel names, lines with missing groups flagged, kept groups listed and flagged, the title, with the entry a game shares panels with, `(blank)` and `(none)`). `LeftPanelStopTests`: `Stop`, `Resume`, `Start` and `OnObsConnected` together, on panels loaded from a temporary `leftpanel.json` with OBS never ready; a stopped rotation isn't restarted by OBS connecting until resumed or started again, and `Status` follows `Start` and `Stop`; `Next` only logs without a rotation, after a stop, or with one panel; the start line's split-colours part. A real-data test checks the real `leftpanel.json` against `games.json`, that its name entries lead to panels and that no panel is skipped, skipped itself when the file is absent |
| `SplitWatcherTests` | `SplitWatcher.IsSplit` (up by exactly one, not the run's start, undo, reset or a repeat), `IsSkip` (only an empty `-` time) and `ParseIndex`; the `FileSystemWatcher` and the wait for the sign file aren't tested |
| `SplitColorsTests` | `SplitColors.SignKeys` (every sign LiveSplit writes, case, padding, unknown and blank), `Trend` (gold first, then the run's side, none for the rest), `GameColor.ChooseScheme` (the trend's scheme, else the default, none without sign schemes), `Resolve` (the game's colours over `none`'s, a game without any, nothing at all), `HighlightFilters` and `PreviousFilters` (sources, filters, OBS values, a key without a colour left out) |
| `TipsTests` | `Tips.FormatAmount` (a dot under `nl-NL`), `Money`, `Sentence` with and without a message, `ChatLine`, `IsNew`, and `StreamElementsEventHandler.Redact` dropping only the email from StreamElements' documented payload. Never calls `Tips.Process`, which plays, posts and speaks |
| `ObsInputsTests` | `ObsInputs`: the `Store` (a later success clearing a failure, one entry per source and setting, `Snapshot` a copy), `KindOf` (a `file` an image only for an image type, else a text), `ColorHex` undoing `ColorUtil.ToOBS`, `Shown` per kind (direct texts quoted, files by name), a text read from file listed under Text with its path, `Ago`, and `StatusLines` (group order, empty groups left out, a volume joining its capture or else going under Other, the unused captures on one dim `… → none` line under the kinds' short names and a refused one kept apart, images and texts newest first, the label before the arrow, `Abbreviate` and image names shortened to a width, the ages in s, m, h and d, texts and images dim while not showing in `Basic Stream Layout` (and plain before the first check, red when failed), `ShowingIn` (following enabled nested scenes and groups, each listed once even in a loop, null when the scene can't be listed), chip tooltips without an age, rows dim while their group is hidden (layout groups by name, left-panel groups through prefix and copy), the `Layout`/`Fade`/`Highlights`/`Previous` swatch rows with their codes, attachments joining their swatch or listed verbosely when another colour, unset and refused swatches, unknown filters verbose, failures, image paths on hover, the not-connected title, `Nothing set yet`, and `StatusLine` equality with chips). Uses its own `Store`, never reaches OBS |
| `MoneyGoalTests` | `MoneyGoal.ToCents` (half a cent rounds away from zero), `Text` (`<total> / <goal> EUR`, the default goal) and `ParseGoal` (dot or comma decimals; zero, negative, words, units, thousands separators and an overflow refused). Never calls `Credit`, `TrySetGoal` or `Reset`, which reach the database and OBS |
| `FaqCommandTests` | `ChatHandler.FindCommand` (each `!name` anywhere and in any case, a question per command, question words inside other words not counting, the first match winning), `Reply`, and `Cooldowns` (per user and command, ending at 60 s, a refused try not extending it). Never calls `CheckForCommands`, which posts to chat |

Fixtures live in `Fixtures/Colors/` and `Fixtures/ColorSchemes/`. Because the registries are static,
test parallelism is off for the assembly; a test that loads other data must reload the fixtures in a
`finally`. Full rules, including `Skip = "<code>: …"` for tests of open bugs, are in
[color.md](color.md#tests-and-verification).

### Everything else: scratch programs

To exercise the real compiled assembly without running the bot:

1. Build the bot to a scratch output (above).
2. Create a throwaway console project (in the scratchpad, not the repo) that references
   `StreamAssistant2.dll`. Target `net8.0-windows`.
3. Internal types need reflection (`Assembly.Load("StreamAssistant2")`, `GetType("StreamAssistant2.Ads")`,
   and so on); public members such as `ConsoleLogger`, `Coloring` and the parsers can be called directly.
4. Read results from the console, from `ConsoleLogger.LineLogged`, or from the log file.

The "reproduced" label on a TODO item means exactly this: run through the real assembly, not just read.

Static state is process-wide, so one scenario per process.

## Recipes

### Add an admin chat command

Add a `case` to `ChatHandler.CheckForAdminCommands`. The command is the first word, case-sensitive; the
argument is the rest, trimmed. Reach the bot's capabilities through the modules
(`LayoutColoring`, `TwitchIRCManager.SendMessage`, `TextToSpeech.EnqueueSpeech`).

### Add a public chat command

Add a `ChatCommand(name, regex, output)` to `ChatHandler._commands`. List order is priority: a message
gets only the first command it matches. Follow the existing patterns (a `!name` branch, and a question
branch of lookaheads whose words each start with `\b`), then add lines that should and shouldn't match to
`FaqCommandTests`. The reply format and the cooldown are shared ([twitch.md](twitch.md#public-commands)).

### Add an EventSub event

Two edits (descriptor, then handler case); see [twitch.md](twitch.md#adding-an-eventsub-event). Then make
a replay file for it ([twitch.md](twitch.md#the-test-harness)) and run `!test <name>` from the
broadcaster account.

### Add a channel-point reward

1. Create the reward. If the bot must fulfil or cancel redemptions, create it **through the API with
   the bot's client id** ([color.md](color.md)); a dashboard-made reward gets a 403.
2. Redeem it once. The bot logs `Unhandled channel point reward redemption id: <id>` and the payload.
3. Add a `REWARD_ID_…` constant and a `case` in `ChannelPoints.ProcessAdd`.
4. Close the redemption with `TwitchHelixApi.UpdateRedemption` if the bot is responsible for it.

### Add an alert sound

Add a member to `Sound.Sounds`, a `case` in `PlaySound(Sounds)` with the file name and a volume, and put
the file in the alert-sounds folder.

### Schedule something

There is no scheduler. Write an `async` loop in the module that owns the work (`DiskSpace.WatchAsync` and
`TwitchUptime.UptimeLoopAsync` are the models): wait, run the work inside a try/catch that logs an `Error`
code, repeat; and start it with `_ = …` from a `Start()` that `Program.StartBotAsync` calls. For one-shot
delayed work, `await Task.Delay` in an async method; to make a group of delayed actions cancellable, give them
one `CancellationTokenSource` as `Ads` does. Keep the *decision* (is it time? what is due?) in a pure function
so it can be tested without waiting.

### Add a log channel

Add the enum member to `ConsoleLogger.ColorType`, then a colour in `MainWindow._logColors`. Update the
tables in [infrastructure.md](infrastructure.md#colour-channels) and [dashboard.md](dashboard.md).

### Add a database table

Add its `CREATE TABLE IF NOT EXISTS` to `Database.InitAsync` and a method per operation, parameterised,
each opening its own connection. Altering an existing table needs an explicit migration step: the
`IF NOT EXISTS` won't do it.

### Add an OBS action

Add a wrapper to `Obs.cs` with the `IsConnected()` guard. Wrap callers that run outside the colour worker
in their own try/catch, since the wrappers don't catch.

### Add a dashboard panel

See [dashboard.md](dashboard.md#adding-a-panel).

### Add colour data

Drop a JSON file into the tables or schemes folder named in `paths.json`; no code. The rules and the
loader's log lines are in [color.md](color.md).

### Add a module

A `static class` under `code/<Area>/` (WPF windows and panels go in `ui/`). Start it from `Program`,
in `StartBotAsync` or `EnableBot`, in the right order. Its tests go in the matching folder of the test
project.

## When you change something

- Keep the build at zero warnings, bot and test project.
- Update the docs that describe it. A new or removed hardcoded value goes in
  [reference.md](reference.md); a new error code goes in its table.
- A finished TODO item is **deleted** (index row and detail section), not marked done.
