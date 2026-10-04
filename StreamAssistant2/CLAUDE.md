# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A personal Twitch stream-automation bot for the channel `lotsofs`. It talks to Twitch IRC and EventSub directly (over raw TCP / WebSocket, no Twitch SDK), drives OBS over obs-websocket, speaks notifications via SAPI TTS, plays alert sounds, and persists a little state in SQLite. Single-user software: channel names, OBS source names, channel-point reward GUIDs, drive letters, and resource paths are hardcoded throughout by design.

## Docs

**[docs/TODO.md](docs/TODO.md) is the master index — start there.** It lists every tracked item by 3-letter code with its area, a one-line summary, severity, and a link to the detail. A code can be resolved from the index alone; the detail docs do not need to be opened or searched to find one. The user refers to items by code ("do RAN"). One item has a clock on it: `SEC`, credential rotation.

Detail lives in the other files in [docs/](docs/), which record findings expensive to rediscover and are the right place to add new ones:

- [docs/robustness-fixes.md](docs/robustness-fixes.md) — known bugs, loose ends and verification tasks, one `##` section per code.
- [docs/color.md](docs/color.md) — how colour requests are resolved (`code/Color/`): the files, the data file formats, the single and triple parsing rules, loose matching, and how to test it. **Read it before changing anything in `code/Color/`.**
- [docs/single-console-logging.md](docs/single-console-logging.md) — `SCL`, a designed-but-unstarted change that would delete the two-process logging described below, plus eight deferred items of its own.

Conventions that keep the index trustworthy:

- Codes are three letters, stable for the life of the item, and unique across *all* files, so one code always means one thing while it exists. A code may be reused once its item is gone.
- **Adding or removing an item means editing both places** — the index row in `docs/TODO.md` *and* the detail section in its doc. They are the one permitted duplication; keep them in step.
- A finished item is **deleted outright**, not annotated as done and not moved to a "fixed" list. Git history is the record; the docs describe only what is still true.
- Items state whether a behaviour was **reproduced** by running the real compiled assembly, or only read. Don't promote a read-only observation to "reproduced" without running it.

## Commands

```bash
# Build (also builds + publishes the logger into bin/<Config>/net8.0/logger/)
dotnet build StreamAssistant2.csproj

# Run — must run from the project dir: secrets.json and icon.ico are resolved relative to cwd
dotnet run --project StreamAssistant2.csproj

# Release
dotnet build StreamAssistant2.csproj -c Release

# Tests (sibling project ../StreamAssistant2.Tests, also in ../StreamAssistant2.sln)
dotnet test ../StreamAssistant2.Tests/StreamAssistant2.Tests.csproj
```

VS Code's `build` task and `.NET Console Launch` config (external terminal, `cwd` = workspace folder) do the same.

Only `code/Color/` has tests (xUnit, in `../StreamAssistant2.Tests`, a sibling folder so the bot's default file globbing doesn't compile them); see [docs/color.md § Tests and verification](docs/color.md#tests-and-verification) for how they work. Everything else is verified manually: run the app and watch the logger window.

`secrets.json` is gitignored (it is the entire `.gitignore`); a `BeforeBuild` target copies `secrets.json.example` over it if missing, so a fresh clone builds but will fail at `Config.Load()` until real values are filled in. The example file documents the full shape.

## Two-process architecture

The app runs as **two console processes**:

1. `StreamAssistant2` — the bot. Its own console is owned by [Dashboard.cs](code/Ui/Dashboard.cs), which repaints a one-line status bar at cursor (0,0) every 15 ms showing IRC ping age and EventSub keepalive age, colour-coded as they approach timeout. **Never `Console.Write` from anywhere else in this process** — it will be trampled by the dashboard loop.
2. `StreamAssistantLog` (`../StreamAssistantLog`) — the log viewer. Spawned as a child by [ConsoleLogger.cs](ConsoleLogger.cs), fed over a named pipe `S.StreamAssistant.<pid>`. Wire format per line: 1 byte `ConsoleColor`, `int32` length, UTF-8 bytes. The viewer exits when it receives a message ending in `SHUTDOWN!`.

So all human-readable output goes through `ConsoleLogger.ColoredLine(ColorType.X, text)`, which both appends to the daily log file and ships the line to the viewer. If the pipe is dead it kills and respawns the child process, then retries. `ColorType` is a semantic channel (`ChatIncoming`, `EventSubNotification`, `Helix`, `AdNotification`, `Error`, `Important`, …) backed by a `ConsoleColor` value — pick the channel that matches the event, not the colour you want. `ConsoleLogger.LogToFile` is file-only (use it for raw JSON payloads and exceptions); `LogToCustomFile` writes one file per event under `AssistantLogs/Custom/`.

## Startup and wiring

[Program.cs](Program.cs) is the whole composition root — there is no DI, no service registry. Order matters:

`Config.Load()` → `ConsoleLogger.Start()` → `Dashboard.Start()` → 1 s wait for the viewer to connect → `Coloring.Load()` → `Database.InitAsync()` → `Clock.Start()` + `Clock.AddGenericJobs()` → `EnableBot()` (OBS connect, IRC connect + `OnMessage += ChatHandler.ProcessMessage`, Helix init, EventSub connect, `LayoutColoring.StartWorker()`), then await Ctrl+C / ProcessExit and `DisableBot()`.

Anything that logs via `ColoredLine` must run after that wait: before it, `ConsoleLogger` has no viewer process, and `RestartAsync` dereferences the null `_process` (see NRE in TODO.md). That is why `Coloring.Load()`, which logs skipped colour entries, sits there.

Nearly every module is a `static class` holding its own state, started once from here. This is the dominant pattern in the codebase; follow it rather than introducing instances.

## Twitch: two independent transports

- **IRC** ([TwitchIRCManager.cs](code/Twitch/TwitchIRCManager.cs)) — raw `TcpClient` to `irc.chat.twitch.tv:6667`, used for *reading chat messages and sending chat messages*. Owns a reconnect loop; answers `PING` itself; everything else is raised on `OnMessage` as the raw IRC line. `SendMessage` is the only way the bot speaks in chat and is called from all over the codebase.
- **EventSub** ([TwitchEventSub.cs](code/Twitch/TwitchEventSub.cs)) — `ClientWebSocket` to `wss://eventsub.wss.twitch.tv/ws`, used for *everything else* (subs, cheers, redemptions, follows, ad breaks). Session loop: `session_welcome` → POST each subscription to Helix with the session id → dispatch `notification` messages → honour `session_reconnect` → treat >20 s without keepalive as dead. Exit reasons are tracked in `SessionExitReason` so the reconnect log says why the session died.

Message parsing is hand-rolled on both sides: [ChatHandler.ProcessMessage](code/Twitch/ChatHandler.cs) splits the IRC line by spaces into tags/prefix/command/body and switches on the command, and EventSub payloads are walked as `JsonElement` with `GetProperty` rather than deserialized into models.

**Adding an EventSub event requires two edits:** add the subscription descriptor to `TwitchEventSubSubscription.Subscriptions` (type, version, and which condition ids it needs), and add a `case` in [TwitchEventHandler.Handle](code/Twitch/TwitchEventHandler.cs). Unhandled types log on the `EventSubConfusion` channel instead of failing. `channel.chat.notification` fans out a second level in `HandleChannelChatNotification` on `notice_type` (`sub`, `resub`, `sub_gift`, `community_sub_gift`) into [Subscriptions.cs](Subscriptions.cs).

`TwitchEventSub.SendTest("<name>")` replays a saved payload from `Stream-Resources\Input\Bot\Tests\<name>.txt` straight into the handler — this is the test harness, triggered in chat by the broadcaster with `!test <name>`. `IS_TEST` + `TestBroadcasterId` redirect user-scoped subscriptions to another channel.

[TwitchHelixApi.cs](code/Twitch/TwitchHelixApi.cs) is the outbound REST side (currently only redemption status updates). Note EventSub keeps its own `HttpClient` for subscribing, separate from this one. Tokens come from `secrets.json` and are never refreshed at runtime despite `RefreshToken` being present.

## Scheduling

[Clock.cs](Clock.cs) is a single cooperative scheduler: a list of `ScheduledJob { GetNextRun, Action, Repeat, Cancellation }`, a loop that sleeps until the earliest `NextRun`, and a `TaskCompletionSource` wake-up so `AddJob` can interrupt the sleep. Use it for anything time-based — delayed sounds (`Sound.PlaySoundDelayed`) and the ad pre-warnings in [Ads.cs](code/Twitch/Ads.cs) both do, and `Ads` keeps handles to its pending jobs so a new ad break can cancel the previous round of warnings. The generic jobs (disk-space check, clock check, uptime poll) are registered in `AddGenericJobs`.

## OBS and the colouring pipeline

[ObsConnection.cs](ObsConnection.cs) holds the `OBSWebsocket` and a reconnect loop to `ws://127.0.0.1:4455`. [Obs.cs](Obs.cs) is a thin wrapper (`SetSourceEnabled`, `SetImageSource`, `SetFilterProperty`) where every call no-ops with a warning if not connected — OBS being closed is a normal state, not an error.

Layout recolouring is a multi-second animated sequence (set filters on the transitionary sources, cross-fade, then set the real ones), so it must not overlap. [LayoutColoring.cs](LayoutColoring.cs) therefore serializes requests through an unbounded `Channel<ColorRequest>` consumed by one worker started at boot. Enqueue via `TryChangeToSingle` / `TryChangeToTriple` / `ChangeToRandom`; don't call `ChangeColorAsync` directly.

Colour *resolution* is separate, under [code/Color/](code/Color/), and documented in full in [docs/color.md](docs/color.md). Read that before touching it: the triple parser's separator-weight rule in particular is not guessable from the code. In short, `SingleColorParser` turns one chat string into a colour (hex, `rgb()`, `hsv()`, bare `r,g,b`, a named scheme or table colour, a system colour, or `random`), with a loose fallback that folds accents and treats odd letters as wildcards. `TripleColorParser` splits a string into up to three such colours. The registries load the colour tables and scheme files named in `secrets.json`. Two invariants bite most often: every hex the colour code produces is lowercase `#rrggbb`, and the triple parser ranks spans by *mean* separator weight; don't "simplify" that to a sum.

## Conventions

- **Formatting**: tabs, and opening brace on the same line (`.editorconfig`: `csharp_new_line_before_open_brace = none`), including for types and methods. `else`/`catch` on the line after the closing brace.
- **Fire-and-forget**: sync entry points wrap async work as `_ = DoThingAsync();` (see `ConsoleLogger.ColoredLine`, `TwitchIRCManager.SendMessage`, `Clock.Start`). Deliberate — handlers must not block the IRC/EventSub read loops. The async body is responsible for catching and logging its own exceptions.
- **Error handling**: `catch` → `ConsoleLogger.ColoredLine(ColorType.Error, "Error <short code>")` + `LogToFile(ex)`, then recover. The short codes (`Error 7`, `Error TES1`, `Error Obs29`) are ad-hoc grep handles, not a scheme.
- **Nullable is enabled** and implicit usings are on, but several files still carry full `using System...` blocks and `_reader!`-style assumptions inside the reconnect loops.
- Chat text is run through [LanguageFilter.ReplaceBadWords](LanguageFilter.cs) before being spoken by TTS. The file documents its own naive substring approach and why that's acceptable here.

## State of the tree

Two migrations are in flight; expect inconsistency and don't "clean up" either without being asked:

1. **WinForms → console.** The old `Form_StreamAssistant.*` UI was deleted and replaced by the `Dashboard` + logger-process pair. `StreamAssistant2.csproj.user` still references the dead form.
2. **Flat root → `code/<Area>/`.** [code/Color/](code/Color/), [code/Twitch/](code/Twitch/) and [code/Ui/](code/Ui/) are the new home. The folder is lowercase `code/`; on Windows a case-only rename of it has to go through `git mv` (git runs with `core.ignorecase`), and VS Code holds a handle on the folder itself, so move its subfolders rather than renaming it. Put new files under `code/<Area>/`; a sizeable set of modules (`Subscriptions.cs`, `LayoutColoring.cs`, `Clock.cs`, `Obs.cs`, …) is still at the root.

Beyond that, the repo carries a lot of commented-out code from the era when this tool was a helper for Streamer.Bot and communicated through a `MsgQueue` (~24 references, all commented). [Games.cs](Games.cs), [Donations.cs](Donations.cs), and [LeftPanel.cs](LeftPanel.cs) are entirely commented out, and the public command replies in `ChatHandler.CheckForCommands` are stubbed the same way — they parse and match, then don't send. The admin commands in `CheckForAdminCommands` work (`!test`, and the colour commands `!changecolor`, `!changecolors`, `!changecolorrandom`, which mirror the three colour rewards), except the `!stoppaneltimer` stub. `MsgQueue` no longer exists; porting one of these means routing it to `TwitchIRCManager.SendMessage`, `TextToSpeech.EnqueueSpeech`, or `LayoutColoring` instead.

## External dependencies at runtime

Hardcoded absolute paths, mostly under `D:\Repositories\Stream-Resources\`: logs (`Bot Data\AssistantLogs\`), SQLite db (`Bot Data\streamAssistant.db`), alert sounds (`Alert Sounds\`), EventSub test payloads (`Input\Bot\Tests\`). Colour tables, colour schemes, and train images are configurable in `secrets.json`. [DiskSpace.cs](DiskSpace.cs) watches drive `A:\` and falls back to the first drive if absent. [TwitchUptime.cs](TwitchUptime.cs) scrapes stream uptime from `decapi.me` rather than Helix, and rate-limits itself to one request per 5 s. TTS requires the `Microsoft Catherine` SAPI voice to be installed — `SpeechSynthesizer.SelectVoice` throws in the static constructor if it is missing.
