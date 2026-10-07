# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A personal Twitch stream-automation bot for the channel `lotsofs`. It talks to Twitch IRC and EventSub directly (over raw TCP / WebSocket, no Twitch SDK), drives OBS over obs-websocket, speaks notifications via SAPI TTS, plays alert sounds, and persists a little state in SQLite. Single-user software: channel names, OBS source names, channel-point reward GUIDs, drive letters, and resource paths are hardcoded throughout by design.

## Docs

**[docs/TODO.md](docs/TODO.md) is the master index — start there.** It lists every tracked item by 3-letter code with its area, a one-line summary, severity, and a link to the detail. A code can be resolved from the index alone; the detail docs do not need to be opened or searched to find one. The user refers to items by code ("do RAN").

Detail lives in the other files in [docs/](docs/), which record findings expensive to rediscover and are the right place to add new ones. Read the one for the area you are touching before changing it:

- [docs/architecture.md](docs/architecture.md) — startup order, threading model, file-by-file map, data flows. Start here when new to the tree.
- [docs/twitch.md](docs/twitch.md) — IRC, EventSub and Helix in depth: credentials, session lifecycle, chat parsing, the subscription table, the `!test` harness.
- [docs/events.md](docs/events.md) — what each event and channel-point reward does: subs, gift bombs, cheers, ads, flush/train/colour rewards.
- [docs/obs.md](docs/obs.md) — OBS connection, the wrappers, and the layout-recolouring queue and animation.
- [docs/infrastructure.md](docs/infrastructure.md) — config, logging, periodic work, database, sound, TTS, language filter, disk-space and uptime watchdogs.
- [docs/dashboard.md](docs/dashboard.md) — the WPF window, its status thresholds, and how to add a panel.
- [docs/reference.md](docs/reference.md) — error-code catalogue and every hardcoded value (paths, URLs, OBS names, timings).
- [docs/development.md](docs/development.md) — build/run/test, working beside a running bot, scratch-program verification, recipes for common changes.
- [docs/gotchas.md](docs/gotchas.md) — the things that bite, one line each, with pointers.
- [docs/legacy.md](docs/legacy.md) — commented-out and unused code, its history, and how to port a `MsgQueue` call.
- [docs/robustness-fixes.md](docs/robustness-fixes.md) — known bugs, loose ends and verification tasks, one `##` section per code.
- [docs/color.md](docs/color.md) — how colour requests are resolved (`code/Color/`): the files, the data file formats, the single and triple parsing rules, loose matching, and how to test it. **Read it before changing anything in `code/Color/`.**

Only `TODO.md` and `robustness-fixes.md` hold tracked items; the rest describe how things are. When code changes a described behaviour, hardcoded value or error code, update the doc in the same change.

Conventions that keep the index trustworthy:

- Codes are three letters, stable for the life of the item, and unique across *all* files, so one code always means one thing while it exists. A code may be reused once its item is gone.
- **Adding or removing an item means editing both places** — the index row in `docs/TODO.md` *and* the detail section in its doc. They are the one permitted duplication; keep them in step.
- A finished item is **deleted outright**, not annotated as done and not moved to a "fixed" list. Git history is the record; the docs describe only what is still true.
- Items state whether a behaviour was **reproduced** by running the real compiled assembly, or only read. Don't promote a read-only observation to "reproduced" without running it.

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
```

VS Code's `build` task and `.NET Console Launch` config (internal console, since the app has no console window of its own; `cwd` = workspace folder) do the same.

Only `code/Color/` and the scheduling logic (`DiskSpace.AlertState`, `ClockMarks`, `Ads.RunScheduleAsync`) have tests (xUnit, in `../StreamAssistant2.Tests`, a sibling folder so the bot's default file globbing doesn't compile them); see [docs/color.md § Tests and verification](docs/color.md#tests-and-verification) for how they work. Everything else is verified manually: run the app and watch its window.

Configuration is split across two files, both read by [Config.cs](Config.cs):

- **`paths.json`**, in the project folder: the `directories` block (`BotInput`, `BotOutput`, `Trains`, `Colors`, `ColorSchemes`). It isn't secret, just machine-specific, so it is gitignored and a `BeforeBuild` target creates it from the tracked `paths.json.example` when missing.
- **`secrets.json`**, in `Directories.BotInput` (`D:\Repositories\Stream-Resources\Bot Input`), *outside* this repository: `obs`, `twitchAuth`, `twitchIds` and `twitchTestNames`. On first start, if it's missing, `Config.Load()` copies the tracked `secrets.json.example` there and stops with a message asking for the real values. `Stream-Resources` is its own git repo, so that file must be ignored there. Never read or print its values; check for "set or empty" if needed.

The project's `.gitignore` covers `paths.json` and `secrets.json`; nothing copies either into the build output.

## The dashboard window

The app is a WPF program (`net8.0-windows`, `WinExe`) with one window, [MainWindow](ui/MainWindow.xaml.cs). Along the top is a status bar showing IRC ping age and EventSub keepalive age, refreshed every 250 ms and colour-coded as they approach timeout. Below it is the coloured log. More panels are expected to join it; it's a personal dashboard, not something shown on stream.

All human-readable output goes through `ConsoleLogger.ColoredLine(ColorType.X, text)`, which appends to the dated log file and raises `ConsoleLogger.LineLogged`. The window subscribes to that event and marshals each line onto the UI thread with `Dispatcher.InvokeAsync`. It keeps the last 5000 lines (the file has everything), follows the end only while scrolled to the bottom, and copies selected lines with Ctrl+C. `ColorType` is a semantic channel (`ChatIncoming`, `EventSubNotification`, `Helix`, `AdNotification`, `Error`, `Important`, …) — pick the channel that matches the event, not the colour you want. The hex colour for each channel lives in `MainWindow`'s `_logColors` table. `ConsoleLogger.LogToFile` is file-only (use it for raw JSON payloads and exceptions); `LogToCustomFile` writes one file per event under `AssistantLogs/Custom/`. If log files can't be written, `Error LOG1` appears in the window once, until a write succeeds again.

Bot code never touches WPF controls: it logs, and the window reads the static state it displays (`TwitchIRCManager.TimeSinceLastPing`, `TwitchEventSub.KeepAliveTimer`). A new panel follows the same pattern: poll on a `DispatcherTimer` or subscribe to an event and marshal with the window's `Dispatcher`.

## Startup and wiring

[Program.cs](Program.cs) is the whole composition root — there is no DI, no service registry. Order matters:

`Main` is a synchronous `[STAThread]` method, because WPF needs the STA thread and an `async Main` loses it after the first `await`. It runs `Config.Load()` (a failure shows a message box and exits, since there is no console to print to), creates the `Application` and `MainWindow`, and runs the window. When the window has loaded, `StartBotAsync` runs **on the thread pool**: `Coloring.Load()` → `Database.InitAsync()` → `DiskSpace.Start()` + `TwitchUptime.Start()` → `EnableBot()` (OBS connect, IRC connect + `OnMessage += ChatHandler.ProcessMessage`, Helix init, EventSub connect, `LayoutColoring.StartWorker()`). A startup exception logs `Error PRG1`.

Starting on the thread pool is load-bearing. Started from the UI thread, every fire-and-forget loop would capture WPF's synchronization context and run its continuations on the UI thread. Closing the window ends `Application.Run`; `Main` then posts the shutdown message to chat, logs `SHUTDOWN!` and calls `DisableBot()`.

Nearly every module is a `static class` holding its own state, started once from here. This is the dominant pattern in the codebase; follow it rather than introducing instances.

## Twitch: two independent transports

- **IRC** ([TwitchIRCManager.cs](code/Twitch/TwitchIRCManager.cs)) — raw `TcpClient` to `irc.chat.twitch.tv:6667`, used for *reading chat messages and sending chat messages*. Owns a reconnect loop; answers `PING` itself; everything else is raised on `OnMessage` as the raw IRC line. `SendMessage` is the only way the bot speaks in chat and is called from all over the codebase.
- **EventSub** ([TwitchEventSub.cs](code/Twitch/TwitchEventSub.cs)) — `ClientWebSocket` to `wss://eventsub.wss.twitch.tv/ws`, used for *everything else* (subs, cheers, redemptions, follows, ad breaks). Session loop: `session_welcome` → POST each subscription to Helix with the session id → dispatch `notification` messages → honour `session_reconnect` → treat >20 s without keepalive as dead. Exit reasons are tracked in `SessionExitReason` so the reconnect log says why the session died.

Message parsing is hand-rolled on both sides: [ChatHandler.ProcessMessage](code/Twitch/ChatHandler.cs) splits the IRC line by spaces into tags/prefix/command/body and switches on the command, and EventSub payloads are walked as `JsonElement` with `GetProperty` rather than deserialized into models.

**Adding an EventSub event requires two edits:** add the subscription descriptor to `TwitchEventSubSubscription.Subscriptions` (type, version, and which condition ids it needs), and add a `case` in [TwitchEventHandler.Handle](code/Twitch/TwitchEventHandler.cs). Unhandled types log on the `EventSubConfusion` channel instead of failing. `channel.chat.notification` fans out a second level in `HandleChannelChatNotification` on `notice_type` (`sub`, `resub`, `sub_gift`, `community_sub_gift`) into [Subscriptions.cs](Subscriptions.cs).

`TwitchEventSub.SendTest("<name>")` replays a saved payload from `<BotInput>\Tests\<name>.txt` straight into the handler — this is the test harness, triggered in chat by the broadcaster with `!test <name>`. `IS_TEST` + `TestBroadcasterId` redirect user-scoped subscriptions to another channel.

[TwitchHelixApi.cs](code/Twitch/TwitchHelixApi.cs) is the outbound REST side (currently only redemption status updates). Note EventSub keeps its own `HttpClient` for subscribing, separate from this one. Tokens come from `<BotInput>\secrets.json` and are never refreshed at runtime despite `RefreshToken` being present.

## Scheduling

There is no scheduler. Anything time-based is an `async` loop in the module that owns it, started fire-and-forget from `Program.StartBotAsync` and written like the connection loops: wait, work inside a try/catch that logs an `Error` code, repeat. `DiskSpace.Start()` runs the disk-space check on every whole minute (alerting through a pure `AlertState` that fires on clock marks), and `TwitchUptime.Start()` runs the midnight date post and the uptime poll. The ad pre-warnings are one method, `Ads.RunScheduleAsync`, run per ad break on a `CancellationTokenSource`; a new ad break cancels the previous round. Keep the *decision* (is it due?) in a pure function so it can be tested.

## OBS and the colouring pipeline

[ObsConnection.cs](ObsConnection.cs) holds the `OBSWebsocket` and a reconnect loop to `ws://127.0.0.1:4455`. [Obs.cs](Obs.cs) is a thin wrapper (`SetSourceEnabled`, `SetImageSource`, `SetFilterProperty`) where every call no-ops with a warning if not connected — OBS being closed is a normal state, not an error.

Layout recolouring is a multi-second animated sequence (set filters on the transitionary sources, cross-fade, then set the real ones), so it must not overlap. [LayoutColoring.cs](LayoutColoring.cs) therefore serializes requests through an unbounded `Channel<ColorRequest>` consumed by one worker started at boot. Enqueue via `TryChangeToSingle` / `TryChangeToTriple` / `ChangeToRandom`; don't call `ChangeColorAsync` directly.

Colour *resolution* is separate, under [code/Color/](code/Color/), and documented in full in [docs/color.md](docs/color.md). Read that before touching it: the triple parser's separator-weight rule in particular is not guessable from the code. In short, `SingleColorParser` turns one chat string into a colour (hex, `rgb()`, `hsv()`, bare `r,g,b`, a named scheme or table colour, a system colour, or `random`), with a loose fallback that folds accents and treats odd letters as wildcards. `TripleColorParser` splits a string into up to three such colours. The registries load the colour tables and scheme files from the directories in `paths.json`. Two invariants bite most often: every hex the colour code produces is lowercase `#rrggbb`, and the triple parser ranks spans by *mean* separator weight; don't "simplify" that to a sum.

## Conventions

- **Formatting**: tabs, and opening brace on the same line (`.editorconfig`: `csharp_new_line_before_open_brace = none`), including for types and methods. `else`/`catch` on the line after the closing brace.
- **Fire-and-forget**: sync entry points wrap async work as `_ = DoThingAsync();` (see `ConsoleLogger.ColoredLine`, `TwitchIRCManager.SendMessage`, `DiskSpace.Start`). Deliberate — handlers must not block the IRC/EventSub read loops. The async body is responsible for catching and logging its own exceptions, or the call goes through `FireForget.Run(code, what, () => ...)`, which does it.
- **Error handling**: `catch` → `ConsoleLogger.ColoredLine(ColorType.Error, "Error <short code>")` + `LogToFile(ex)`, then recover. The short codes (`Error 7`, `Error TES1`, `Error Obs29`) are ad-hoc grep handles, not a scheme.
- **The build is warning-free** — bot and test project, 0 warnings — so keep it that way rather than suppressing. Nullable is enabled and implicit usings are on (several files still carry full `using System...` blocks). Both assemblies are marked `[assembly: SupportedOSPlatform("windows")]`, which is what keeps the Windows-only SAPI calls from raising CA1416.
- Chat text is run through [LanguageFilter.ReplaceBadWords](LanguageFilter.cs) before being spoken by TTS. The file documents its own naive substring approach and why that's acceptable here.

## State of the tree

Two migrations are in flight; expect inconsistency and don't "clean up" either without being asked:

1. **WinForms → console → WPF.** The old `Form_StreamAssistant.*` UI was deleted, replaced by a console status bar plus a separate log-viewer process, and then by the WPF window. `ConsoleLogger` keeps its name from the console era.
2. **Flat root → `code/<Area>/`.** [code/Color/](code/Color/) and [code/Twitch/](code/Twitch/) are the new home. The folder is lowercase `code/`; on Windows a case-only rename of it has to go through `git mv` (git runs with `core.ignorecase`), and VS Code holds a handle on the folder itself, so move its subfolders rather than renaming it. Put new files under `code/<Area>/`, except WPF windows and panels (XAML plus its code-behind), which go in [ui/](ui/). A sizeable set of modules (`Subscriptions.cs`, `LayoutColoring.cs`, `Obs.cs`, …) is still at the root.

Beyond that, the repo carries a lot of commented-out code from the era when this tool was a helper for Streamer.Bot and communicated through a `MsgQueue` (~24 references, all commented). [Games.cs](Games.cs), [Donations.cs](Donations.cs), and [LeftPanel.cs](LeftPanel.cs) are entirely commented out, and the public command replies in `ChatHandler.CheckForCommands` are stubbed the same way — they parse and match, then don't send. The admin commands in `CheckForAdminCommands` work (`!test`, and the colour commands `!changecolor`, `!changecolors`, `!changecolorrandom`, which mirror the three colour rewards), except the `!stoppaneltimer` stub. `MsgQueue` no longer exists; porting one of these means routing it to `TwitchIRCManager.SendMessage`, `TextToSpeech.EnqueueSpeech`, or `LayoutColoring` instead.

## External dependencies at runtime

Paths come from `paths.json` and all sit under `D:\Repositories\Stream-Resources\`. Under `BotInput` (`Bot Input\`): `secrets.json` and EventSub test payloads (`Tests\`). Colour tables and schemes have their own `Colors` and `ColorSchemes` entries (currently `Colors\Tables\` and `Colors\Schemes\`). Under `BotOutput` (`Bot Output\`): logs (`AssistantLogs\`) and the SQLite db (`streamAssistant.db`). Train images have their own `Trains` entry. The one path still hard-coded is the alert sounds folder (`Stream-Resources\Alert Sounds\`, in `Sound.cs`). [DiskSpace.cs](DiskSpace.cs) watches drive `A:\` and falls back to the first drive if absent. [TwitchUptime.cs](TwitchUptime.cs) scrapes stream uptime from `decapi.me` rather than Helix, and rate-limits itself to one request per 5 s. TTS requires the `Microsoft Catherine` SAPI voice to be installed — `SpeechSynthesizer.SelectVoice` throws in the static constructor if it is missing.
