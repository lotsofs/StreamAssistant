# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A personal Twitch stream-automation bot for the channel `lotsofs`. It talks to Twitch IRC and EventSub directly (over raw TCP / WebSocket, no Twitch SDK), takes tips from StreamElements over a third WebSocket, drives OBS over obs-websocket, speaks notifications via SAPI TTS, plays alert sounds, and persists a little state in SQLite. Single-user software: channel names, OBS source names, channel-point reward GUIDs, drive letters, and resource paths are hardcoded throughout by design.

## Docs

**[docs/TODO.md](docs/TODO.md) is the master index — start there.** It lists every tracked item by 3-letter code with its area, a one-line summary, severity, and a link to the detail. A code can be resolved from the index alone; the detail docs do not need to be opened or searched to find one. The user refers to items by code ("do RAN").

Detail lives in the other files in [docs/](docs/), which record findings expensive to rediscover and are the right place to add new ones. Read the one for the area you are touching before changing it:

- [docs/architecture.md](docs/architecture.md) — startup order, threading model, file-by-file map, data flows. Start here when new to the tree.
- [docs/twitch.md](docs/twitch.md) — IRC, EventSub and Helix in depth: credentials, session lifecycle, chat parsing, the subscription table, the `!test` harness.
- [docs/eventsub-flow.md](docs/eventsub-flow.md) — Mermaid flow charts of `TwitchEventSub`: the connection loop, the message loop, subscribing, the planned reconnect, and message reading.
- [docs/streamelements.md](docs/streamelements.md) — the StreamElements connection for tips: the Astro protocol, credentials, the probe that stands in for keepalives, session lifecycle and exit reasons.
- [docs/events.md](docs/events.md) — what each event and channel-point reward does: subs, gift bombs, cheers, tips, ads, flush/train/colour rewards.
- [docs/obs.md](docs/obs.md) — OBS connection, the wrappers, and the layout-recolouring queue and animation.
- [docs/infrastructure.md](docs/infrastructure.md) — config, logging, periodic work, database, sound, TTS, language filter, disk-space and uptime watchdogs.
- [docs/dashboard.md](docs/dashboard.md) — the WPF window, its status thresholds, and how to add a panel.
- [docs/reference.md](docs/reference.md) — error-code catalogue and every hardcoded value (paths, URLs, OBS names, timings).
- [docs/development.md](docs/development.md) — build/run/test, working beside a running bot, scratch-program verification, recipes for common changes.
- [docs/gotchas.md](docs/gotchas.md) — the things that bite, one line each, with pointers.
- [docs/legacy.md](docs/legacy.md) — the Streamer.Bot-era history, what of it was rebuilt, and the code that is compiled but has no caller.
- [docs/robustness-fixes.md](docs/robustness-fixes.md) — known bugs, loose ends and verification tasks, one `##` section per code.
- [docs/color.md](docs/color.md) — how colour requests are resolved (`code/Color/`): the files, the data file formats, the single and triple parsing rules, loose matching, and how to test it. **Read it before changing anything in `code/Color/`.**
- [docs/live-checks.md](docs/live-checks.md) — the owner's checklist of built-but-unseen behaviour to verify in the real bot, OBS and Twitch, tracked as one item, LIV. It is grouped by when a check can be done (any time, with LiveSplit, around an OBS restart, at a real stream, …). Add a line, under the next unused number and in the group for when it can be checked, when something new needs a live look. Delete it once it checks out, without renumbering the rest, and log it as `LIV <n>`. Delete the file and the LIV row when none are left.
- [docs/DONE.md](docs/DONE.md) — the log of finished work, newest first, one row per item (`—` for work without a code), started 2026-10-23; earlier finished items are only in git history.

Only `TODO.md`, `robustness-fixes.md` and `live-checks.md` hold tracked items, and only `DONE.md` holds finished ones; the rest describe how things are. When code changes a described behaviour, hardcoded value or error code, update the doc in the same change.

Conventions that keep the index trustworthy:

- Codes are three letters, stable for the life of the item, and unique across *all* files, so one code always means one thing while it exists. A code may be reused once its item is gone.
- **Adding or removing an item means editing both places** — the index row in `docs/TODO.md` *and* the detail section in its doc. They are the one permitted duplication; keep them in step.
- A finished item **moves to the log**: delete its index row and detail section, and add its row to `docs/DONE.md`, in the same change. A partly done item is rewritten to say what is left, and the done part is logged as `CODE (partial)`. No "done" notes in the index or detail docs: they describe only what is still true. Finished work that never had a code gets a row too.
- Items state whether a behaviour was **reproduced** by running the real compiled assembly, or only read. Don't promote a read-only observation to "reproduced" without running it.

[../CHANGELOG.md](../CHANGELOG.md), at the repository root, is the changelog for the person running the bot, separate from `docs/DONE.md`. Release 1 isn't out yet, so until then its `Unreleased` says only "First release" with a short description; don't add entries. Once release 1 is out, record user-visible changes under `Unreleased`.

## Commands

```bash
# Build (output in bin/<Config>/net8.0-windows/)
dotnet build StreamAssistant2.csproj

# Run — must run from the project dir: paths.json and secrets.json.example are resolved relative to cwd
dotnet run --project StreamAssistant2.csproj

# Release
dotnet build StreamAssistant2.csproj -c Release

# Tests (sibling project ../StreamAssistant2.Tests, also in ../StreamAssistant2.sln)
dotnet test ../StreamAssistant2.Tests/StreamAssistant2.Tests.csproj

# One test class or test (xUnit, by fully qualified name)
dotnet test ../StreamAssistant2.Tests/StreamAssistant2.Tests.csproj --filter "FullyQualifiedName~SplitWatcherTests"

# While the bot is running: it holds bin/ open, so a plain build or test fails copying the DLL (MSB3027)
dotnet build StreamAssistant2.csproj -p:BaseOutputPath=<scratch>/
dotnet test ../StreamAssistant2.Tests/StreamAssistant2.Tests.csproj -p:BaseOutputPath=<scratch>/
```

VS Code's `build` task and `.NET Console Launch` config (internal console, since the app has no console window of its own; `cwd` = workspace folder) do the same. The owner runs the bot that way, with F5, often during a stream; see [docs/development.md § Working next to a running bot](docs/development.md#working-next-to-a-running-bot).

Tests are xUnit, in `../StreamAssistant2.Tests`: a sibling folder, so the bot's default file globbing doesn't compile them, with files in folders that mirror `code/`. They cover the pure logic: decisions pulled out of loops, parsers, sentence builders, and the connection loops against loopback fakes. [docs/development.md § Unit tests](docs/development.md#unit-tests) lists what each test class covers, and [docs/color.md § Tests and verification](docs/color.md#tests-and-verification) explains how they work. Everything else is verified manually: run the app and watch its window.

Configuration is split across two files, both read by [Config.cs](code/Core/Config.cs):

- **`paths.json`**, in the project folder: the `directories` block (`BotInput`, `BotOutput`, `Trains`, `Colors`, `ColorSchemes`, `Backgrounds`, `LivesplitGenerated`, `Layout`, `Sidebar`). It isn't secret, just machine-specific, so it is gitignored and a `BeforeBuild` target creates it from the tracked `paths.json.example` when missing.
- **`secrets.json`**, in `Directories.BotInput` (`D:\Repositories\Stream-Resources\Bot Input`), *outside* this repository: `obs`, `twitchAuth`, `twitchIds`, `streamElements` (`channelId` and `jwt`; without them tips are off and the rest runs) and `twitchTestNames`. On first start, if it's missing, `Config.Load()` copies the tracked `secrets.json.example` there and stops with a message asking for the real values. `Stream-Resources` is its own git repo, so that file must be ignored there. Never read or print its values; check for "set or empty" if needed.

The project's `.gitignore` covers `paths.json` and `secrets.json`; nothing copies either into the build output.

## The dashboard window

The app is a WPF program (`net8.0-windows`, `WinExe`) with one window, [MainWindow](ui/MainWindow.xaml.cs): a status bar of connection ages along the top (IRC ping, EventSub keepalive, StreamElements reply, colour-coded as they near timeout), the coloured log, and on the right two columns: the OBS sources the bot has set this run (`ObsInputs.Current`) and the left-panel rotation (`LeftPanel.Status`). It's a personal dashboard, not something shown on stream, and more panels are expected to join it. [docs/dashboard.md](docs/dashboard.md) describes each part in full.

All human-readable output goes through `ConsoleLogger.ColoredLine(ColorType.X, text)`, which appends to the dated log file and raises `ConsoleLogger.LineLogged`. The window subscribes to that event and marshals each line onto the UI thread with `Dispatcher.InvokeAsync`. It keeps the last 5000 lines (the file has everything), follows the end only while scrolled to the bottom, and copies selected lines with Ctrl+C. `ColorType` is a semantic channel (`ChatIncoming`, `EventSubNotification`, `Helix`, `AdNotification`, `Error`, `Important`, …) — pick the channel that matches the event, not the colour you want. The hex colour for each channel lives in `MainWindow`'s `_logColors` table. `ConsoleLogger.LogToFile` is file-only (use it for raw JSON payloads and exceptions); `LogToEventSubFile` writes one file per real EventSub or StreamElements event under `AssistantLogs/EventSubs/`, which `!test replay` reads back. If log files can't be written, `Error LOG1` appears in the window once, until a write succeeds again.

Bot code never touches WPF controls: it logs, and the window reads the static state it displays (`TwitchIRCManager.TimeSinceLastPing`, `TwitchEventSub.KeepAliveTimer`, `StreamElementsSocket.SinceLastMessage`, `LeftPanel.Status`, `ObsInputs.Current`, which the OBS wrappers in `Obs.cs` fill as they set sources). A new panel follows the same pattern: poll on a `DispatcherTimer` or subscribe to an event and marshal with the window's `Dispatcher`.

## Startup and wiring

[Program.cs](Program.cs) is the whole composition root — there is no DI, no service registry. Order matters:

`Main` is a synchronous `[STAThread]` method, because WPF needs the STA thread and an `async Main` loses it after the first `await`. It runs `Config.Load()` (a failure shows a message box and exits, since there is no console to print to), creates the `Application` and `MainWindow`, and runs the window. When the window has loaded, `StartBotAsync` runs **on the thread pool**: `Coloring.Load()` → `Games.Load()` → `LeftPanel.Load()` → `Database.InitAsync()` → `DiskSpace.Start()` + `TwitchUptime.Start()` + `Clock.Start()` + `ConnectionHealth.Start()` + `SplitWatcher.Start()` + `ObsInputs.Start()` → `EnableBot()` (OBS connect, IRC connect + `OnMessage += ChatHandler.ProcessMessage`, Helix init, EventSub connect, StreamElements connect, `LayoutColoring.StartWorker()`) → `Games.OnBootAsync` (fire-and-forget: current category from Helix, applied once, when OBS is first connected) → `TextToSpeech.ReportStart()` (speaks "Text to speech system online"). A startup exception logs `Error PRG1`.

Starting on the thread pool is load-bearing. Started from the UI thread, every fire-and-forget loop would capture WPF's synchronization context and run its continuations on the UI thread. Closing the window ends `Application.Run`; `Main` then posts the shutdown message to chat, logs `SHUTDOWN!`, hides a train still running and calls `DisableBot()`.

Nearly every module is a `static class` holding its own state, started once from here. This is the dominant pattern in the codebase; follow it rather than introducing instances.

## Twitch: two independent transports (and StreamElements, a third)

- **IRC** ([TwitchIRCManager.cs](code/Twitch/TwitchIRCManager.cs)) — raw `TcpClient` to `irc.chat.twitch.tv:6667`, used for *reading chat messages and sending chat messages*. Owns a reconnect loop; answers `PING` itself; everything else is raised on `OnMessage` as the raw IRC line. `SendMessage` is the only way the bot speaks in chat and is called from all over the codebase.
- **EventSub** ([TwitchEventSub.cs](code/Twitch/TwitchEventSub.cs)) — `ClientWebSocket` to `wss://eventsub.wss.twitch.tv/ws`, used for *everything else* (subs, cheers, redemptions, follows, ad breaks). Session loop: `session_welcome` → POST each subscription to Helix with the session id → dispatch `notification` messages → honour `session_reconnect` → treat >20 s without keepalive as dead. Exit reasons are tracked in `SessionExitReason` so the reconnect log says why the session died.
- **StreamElements** ([StreamElementsSocket.cs](code/StreamElements/StreamElementsSocket.cs)) — not Twitch: `ClientWebSocket` to StreamElements' Astro gateway, `wss://astro.streamelements.com`, used only for *tips* (Twitch has none). On `welcome` it subscribes to `channel.tips` with the channel id and JWT from `secrets.json`. It re-sends that subscribe every 30 s as a probe, because Astro's keepalive pings are invisible to .NET 8, and 70 s of silence ends the session. Astro answers each probe with `err_bad_request` / `already subscribed to topic`, which counts as success and is never logged. It also follows `reconnect` with its token, and stops for good on `err_unauthorized`. Events go to [StreamElementsEventHandler](code/StreamElements/StreamElementsEventHandler.cs) and on to [Tips.cs](code/StreamElements/Tips.cs). Posts `🟣 SE Connected` / `💥 SE Disconnected` like EventSub, but not across a planned reconnect or on shutdown. Details: [docs/streamelements.md](docs/streamelements.md).

Message parsing is hand-rolled on both sides: [ChatHandler.ProcessMessage](code/Twitch/ChatHandler.cs) splits the IRC line by spaces into tags/prefix/command/body and switches on the command, and EventSub payloads are walked as `JsonElement` rather than deserialized into models, through the extensions in [code/Util/JsonElementExtensions.cs](code/Util/JsonElementExtensions.cs): `evt.ReadString("sub_gift.recipient_user_login", "Unknown User")`, `ReadInt`, `ReadDecimal`, `ReadBool`, `ReadElement`. They take a dotted path and return the fallback for a missing step, a `null` or the wrong kind, because Twitch sends `null` for absent values (an anonymous gifter's `cumulative_total`). Don't reintroduce `GetProperty` chains.

**Adding an EventSub event requires two edits:** add the subscription descriptor to `TwitchEventSubSubscription.Subscriptions` (type, version, and which condition ids it needs), and add a `case` in [TwitchEventHandler.Handle](code/Twitch/TwitchEventHandler.cs). Unhandled types log on the `EventSubConfusion` channel instead of failing. `channel.chat.notification` fans out a second level in `HandleChannelChatNotification` on `notice_type` (`sub`, `resub`, `sub_gift`, `community_sub_gift`) into [Subscriptions.cs](code/Twitch/Subscriptions.cs), whose spoken sentences come from [SubscriptionMessages.cs](code/Twitch/SubscriptionMessages.cs), and `raid` into [Raids.cs](code/Twitch/Raids.cs), which posts a chat line.

`!test <script> [args]` is the test harness: [code/Twitch/TestEvents/](code/Twitch/TestEvents/) builds simulated events in code (`sub`, `resub`, `gift`, `bomb`, `cheer`, `raid`, `online`, `offline`, and the StreamElements `tip`) or replays a real one saved in `AssistantLogs\EventSubs\` (`replay`) and feeds them straight into `TwitchEventHandler.Handle` (or `StreamElementsEventHandler.Handle` for an `se.` type), running the real handlers; see [docs/twitch.md § The test harness](docs/twitch.md#the-test-harness). `IS_TEST` + `TestBroadcasterId` redirect user-scoped subscriptions to another channel.

[TwitchHelixApi.cs](code/Twitch/TwitchHelixApi.cs) is the outbound REST side (redemption status updates, and the channel's current category for the go-live game setup). Note EventSub keeps its own `HttpClient` for subscribing, separate from this one. Tokens come from `<BotInput>\secrets.json` and are never refreshed at runtime despite `RefreshToken` being present.

## Scheduling

There is no scheduler. Anything time-based is an `async` loop in the module that owns it, started fire-and-forget from `Program.StartBotAsync` and written like the connection loops: wait, work inside a try/catch that logs an `Error` code, repeat. `DiskSpace.Start()` runs the disk-space check on every whole minute (alerting through a pure `AlertState` that fires on clock marks), `TwitchUptime.Start()` runs the uptime poll, `Clock.Start()` writes OBS's clock text (`Text: Time Of Day`) on every whole minute and posts the date to chat on the first minute of a new day, and `ConnectionHealth.Start()` checks the IRC, EventSub and StreamElements ages every second, logging once when one goes bad and once when it recovers (its thresholds also colour the dashboard, and the red one is the reconnect timeout). The ad pre-warnings are one method, `Ads.RunScheduleAsync`, run per ad break on a `CancellationTokenSource`; a new ad break cancels the previous round. The left-panel rotation works the same way: `LeftPanel.Start`, from each game setup and again whenever OBS connects, cancels the running `RunAsync` and starts the game's. Keep the *decision* (is it due?) in a pure function so it can be tested.

## OBS and the colouring pipeline

[ObsConnection.cs](code/Obs/ObsConnection.cs) holds the `OBSWebsocket` and a reconnect loop to `ws://127.0.0.1:4455`. [Obs.cs](code/Obs/Obs.cs) is a thin wrapper (`SetSourceEnabled`, `SetInputSetting`, `SetImageSource`, `SetInputVolumeDb`, `SetFilterProperty`, `GetSceneItemNames`) where every call no-ops with a warning if not connected — OBS being closed is a normal state, not an error.

Layout recolouring is a multi-second animated sequence (set filters on the transitionary sources, cross-fade, then set the real ones, toggling the colour groups in every `!Scene: Layout <aspect>` scene), so it must not overlap. [LayoutColoring.cs](code/Obs/LayoutColoring.cs) therefore serializes requests through an unbounded `Channel<ColorRequest>` consumed by one worker started at boot. Enqueue via `TryChangeToSingle` / `TryChangeToTriple` / `ChangeToRandom`; don't call `ChangeColorAsync` directly.

Colour *resolution* is separate, under [code/Color/](code/Color/), and documented in full in [docs/color.md](docs/color.md). Read that before touching it: the triple parser's separator-weight rule in particular is not guessable from the code. In short, `SingleColorParser` turns one chat string into a colour (hex, `rgb()`, `hsv()`, bare `r,g,b`, a named scheme or table colour, a system colour, or `random`), with a loose fallback that folds accents and treats odd letters as wildcards. `TripleColorParser` splits a string into up to three such colours. The registries load the colour tables and scheme files from the directories in `paths.json`. Two invariants bite most often: every hex the colour code produces is lowercase `#rrggbb`, and the triple parser ranks spans by *mean* separator weight; don't "simplify" that to a sum.

The left-panel rotation colours LiveSplit's sources too, apart from the layout colour: [SplitColors.cs](code/LiveSplit/SplitColors.cs) takes the game's `SplitColors` from `gameschemes.json` (laid over `none`'s), and the rotation sets them on the split list highlights' filters when it starts. On each previous-split panel, it sets them on the previous split's differences, by the `Sign.txt` that the game's `leftpanel.json` entry names. See [docs/obs.md § Split colours](docs/obs.md#split-colours). A `gameschemes` category with `SignSchemes` (SWAT4) also has its layout colour follow the previous-split sign's trend: `GameColor.OnSign`, called by `SplitWatcher` after every index change, queues the scheme for Ahead, Behind or Gold (else the category's `Default`) when it changes, without a chat line.

## Conventions

- **Formatting**: tabs, and opening brace on the same line (`.editorconfig`: `csharp_new_line_before_open_brace = none`), including for types and methods. `else`/`catch` on the line after the closing brace.
- **Fire-and-forget**: sync entry points wrap async work as `_ = DoThingAsync();` (see `ConsoleLogger.ColoredLine`, `TwitchIRCManager.SendMessage`, `DiskSpace.Start`). Deliberate — handlers must not block the IRC/EventSub read loops. The async body is responsible for catching and logging its own exceptions, or the call goes through `FireForget.Run(code, what, () => ...)`, which does it.
- **Error handling**: `catch` → `ConsoleLogger.ColoredLine(ColorType.Error, "Error <short code>")` + `LogToFile(ex)`, then recover. The short codes (`Error 7`, `Error TES1`, `Error Obs29`) are ad-hoc grep handles, not a scheme.
- **The build is warning-free** — bot and test project, 0 warnings — so keep it that way rather than suppressing. Nullable is enabled and implicit usings are on (several files still carry full `using System...` blocks). Both assemblies are marked `[assembly: SupportedOSPlatform("windows")]`, which is what keeps the Windows-only SAPI calls from raising CA1416.
- Chat text is run through [LanguageFilter.ReplaceBadWords](code/Audio/LanguageFilter.cs) before being spoken by TTS. The file documents its own naive substring approach and why that's acceptable here.

## State of the tree

Both migrations are finished. The UI went WinForms → console → WPF; `ConsoleLogger` keeps its name from the console era. The flat project folder moved into `code/<Area>/`:

| Folder | Holds |
|---|---|
| [code/Core/](code/Core/) | config, logging, database, the data-file reload |
| [code/Audio/](code/Audio/) | alert sounds, TTS, the language filter |
| [code/Timing/](code/Timing/) | the periodic loops (clock, disk space, uptime) and connection health |
| [code/Twitch/](code/Twitch/) | IRC, EventSub, Helix, chat and the event handlers; `TestEvents/` for `!test` |
| [code/StreamElements/](code/StreamElements/) | the Astro connection and tips |
| [code/Obs/](code/Obs/) | the OBS connection, the wrappers, layout recolouring |
| [code/Games/](code/Games/) | the per-game setup |
| [code/LeftPanel/](code/LeftPanel/) | the left-panel rotation |
| [code/LiveSplit/](code/LiveSplit/) | the split watcher and split colours |
| [code/Color/](code/Color/) | colour resolution |
| [code/Money/](code/Money/) | the money goal |
| [code/Util/](code/Util/) | `FireForget`, the JSON readers, `TrueModulo` |

Only `Program.cs` stays at the root, and WPF windows and panels (XAML plus code-behind) go in [ui/](ui/). Put a new file in the matching `code/<Area>/` folder and its tests in the same folder of the test project. The folder is lowercase `code/`; on Windows a case-only rename of it has to go through `git mv` (git runs with `core.ignorecase`), and VS Code holds a handle on the folder itself, so move its subfolders rather than renaming it.

No Streamer.Bot-era code is left; [docs/legacy.md](docs/legacy.md) has its history. The chat commands work: the public FAQ commands in `ChatHandler.CheckForCommands` answer in chat (the first match only, at most once a minute per user and command), and the admin commands are in `CheckForAdminCommands` (`!test <script>`, `!train`, and the colour commands `!changecolor`, `!changecolors`, `!changecolorrandom`, which mirror the three colour rewards, `!changegame` / `!changecategory`, which switch the bot's category locally and run the game setup, `!changepanel` / `!changecapture`, which run only the left-panel or only the capture-source part of it, `!stoppaneltimer` / `!startpaneltimer`, which stop and restart the left-panel rotation, `!nextpanelitem`, which moves it on to the next panel now, `!reloaddata`, which reloads the colour data, `games.json` and `leftpanel.json` and runs the current category's game setup again, and `!moneygoal <amount>` / `!resetmoney`, which set the money goal and reset its total).

## External dependencies at runtime

Paths come from `paths.json` and all sit under `D:\Repositories\Stream-Resources\`. Under `BotInput` (`Bot Input\`): `secrets.json`, and `games.json` (game id → name, capture executables and volumes, and an optional `Aspect` (`16x9` by default, SWAT4's `5x4`) that picks the border images and the `!Scene: Layout <aspect>` and `!Scene: Games <aspect>` shown, plus a `default` volume; loaded by `code/Games/Games.cs`), and `leftpanel.json` (game name → the left-panel groups and panels, plus a `default`; loaded and rotated in OBS by `code/LeftPanel/LeftPanel.cs`). Colour tables and schemes have their own `Colors` and `ColorSchemes` entries (currently `Colors\Tables\` and `Colors\Schemes\`). Under `BotOutput` (`Bot Output\`): logs (`AssistantLogs\`) and the SQLite db (`streamAssistant.db`: the toilet rewards' flushes and the `money_goal` row, the tips' total and goal shown in OBS's `Text: Money Goal`). Train images have their own `Trains` entry, game backgrounds a `Backgrounds` entry (`Images\Backgrounds\`), the layout's border images a `Layout` entry (`Images\Layout\`, a `_16x9` pair and a plain one for other aspects, set per game by `code/Games/GameBorder.cs`), the sidebar images a `Sidebar` entry (`Images\Sidebar\`; `Dynamic\<game>_<n>.png` and `Dynamic\<game>_2_<n>.png` follow the split index in `Sidebar: Dynamic 1` and `2`, set by `code/Games/GameSidebar.cs`), and LiveSplit's text output a `LivesplitGenerated` entry (`Text\Livesplit Generated\`), whose split index `code/LiveSplit/SplitWatcher.cs` watches for the previous-split panel. The one path still hard-coded is the alert sounds folder (`Stream-Resources\Alert Sounds\`, in `Sound.cs`). [DiskSpace.cs](code/Timing/DiskSpace.cs) watches drive `A:\` and falls back to the first drive if absent. [TwitchUptime.cs](code/Timing/TwitchUptime.cs) scrapes stream uptime from `decapi.me` rather than Helix, writes it to OBS's `Text: Stream Uptime`, polls at once on going live (OBS's stream start and Twitch's `stream.online`), and never sends two requests within 5 s. TTS requires the `Microsoft Catherine` SAPI voice to be installed — `SpeechSynthesizer.SelectVoice` throws in the static constructor if it is missing.
