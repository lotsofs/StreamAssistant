# Architecture

The shape of the bot: what starts when, which thread things run on, who talks to whom, and where every
file fits. Read this first when you are new to the tree; the other docs go deep on one area each.

Everything here was read from the code. Where a behaviour was also run, it says so.

## What it is

A single-user Windows desktop program (WPF, `net8.0-windows`, `WinExe`) that sits beside OBS while the
channel `lotsofs` streams. It listens to Twitch (and to StreamElements, for tips), and reacts by speaking, playing sounds, posting in chat,
recolouring the OBS layout and recording a little state. There is no server, no installer and no
second user, so channel names, OBS source names, reward GUIDs, drive letters and resource paths are
hardcoded on purpose ([reference.md](reference.md) lists them).

```
                         ┌────────────────────────────────────────────┐
  Twitch IRC (TCP) ─────►│ TwitchIRCManager ──► ChatHandler            │
        ▲                │        ▲                 │ admin commands   │
        │ SendMessage    │        │                 ▼                  │
        │                │        │          LayoutColoring ◄──┐       │
  Twitch EventSub (WS) ─►│ TwitchEventSub ──► TwitchEventHandler      │
                         │                      │  │  │  │            │
                         │   Ads ◄──────────────┘  │  │  └► Cheers    │
                         │   ChannelPoints ◄───────┘  └► Subscriptions│
                         │      │      │                    │  │      │
                         │      │      └► Database (SQLite) │  │      │
                         │      ▼                           ▼  ▼      │
                         │  TwitchHelixApi            Sound  TextToSpeech
                         │                                            │
                         │  DiskSpace, TwitchUptime, Clock, Ads:      │
                         │    own loops                               │
  StreamElements (WS) ──►│  StreamElementsSocket ──► Tips: sound,     │
                         │    chat, TextToSpeech, MoneyGoal (SQLite,  │
                         │    OBS text)                               │
                         │  Obs / ObsConnection ◄── LayoutColoring    │
                         │  ConsoleLogger ──► log file + MainWindow   │
                         └────────────────────────────────────────────┘
```

## Startup and shutdown

[Program.cs](../Program.cs) is the whole composition root. There is no dependency injection and no
service registry.

1. `Main` is a synchronous `[STAThread]` method. WPF needs the STA thread, and an `async Main` would
   lose it after the first `await`.
2. `Config.Load()` runs first. A failure shows a message box (there is no console) and exits.
3. `new Application()`, `new MainWindow()`, then `app.Run(window)`.
4. When the window has loaded, `StartBotAsync` runs **on the thread pool** (`Task.Run`). It is
   load-bearing: started on the UI thread, every fire-and-forget loop would capture WPF's
   synchronization context and run its continuations on the UI thread.
5. `StartBotAsync`, in order:
   1. Logs `STARTED!`, so it is the first line of each run in the log.
   2. `Coloring.Load()`: colour tables and schemes from disk ([color.md](color.md)).
   3. `Games.Load()`: `<BotInput>\games.json`, the game behind each Twitch category. Never throws.
   4. `LeftPanel.Load()`: `<BotInput>\leftpanel.json`, each game's left-panel panels
      ([obs.md](obs.md#left-panel)). Never throws.
   5. `Database.InitAsync()`: opens `streamAssistant.db`, creates the `flushes` and `money_goal` tables if
      missing.
   6. Logs `Data loaded`.
   7. `DiskSpace.Start()`, `TwitchUptime.Start()`, `Clock.Start()` and `ConnectionHealth.Start()`: the
      periodic loops. `Clock` doesn't need OBS yet: it skips its OBS write until OBS is ready. Then
      `SplitWatcher.Start()`, the watcher on LiveSplit's split index, which never throws, and
      `ObsInputs.Start()`, the dashboard's check of which images and texts are showing in `Basic Stream Layout`, idle
      until OBS is ready.
   8. `EnableBot()`: `ObsConnection.Connect()`, `TwitchIRCManager.Connect()` plus
      `OnMessage += ChatHandler.ProcessMessage`, `TwitchHelixApi.Init()`, `TwitchEventSub.Connect()`,
      `StreamElementsSocket.Connect()` (only logs "tips are off" without credentials),
      `LayoutColoring.StartWorker()`.
   9. `Games.OnBootAsync`, fire-and-forget (`Error GMS_boot`): stores the current category from Helix and
      applies the game once, when OBS is first connected ([events.md](events.md#category-change-and-going-live)).
   10. `TextToSpeech.ReportStart()`, which is the first touch of `TextToSpeech`, so its static
      constructor (and the `Microsoft Catherine` voice lookup) runs only now, after everything else
      is already live.
   Any exception logs `Error PRG1` and the bot stays half-started.
6. Closing the window ends `Application.Run`. `Main` then posts `🍂 Shutting Down` to chat, logs
   `SHUTDOWN!` and calls `DisableBot()` (OBS disconnect, IRC disconnect, EventSub and StreamElements cancel). The chat
   post is fire-and-forget and the IRC socket is closed straight after, so that message can be lost.
   Read, not reproduced.

Stopping the debugger kills the process without running any of the shutdown path.

## Threading model

- **UI thread**: only the WPF window and its 250 ms status timer. Bot code never touches WPF controls
  ([dashboard.md](dashboard.md)).
- **Everything else is the thread pool.** Each long-lived loop is started as `_ = Task.Run(...)` or
  `_ = SomethingAsync()` from `StartBotAsync`'s context: the IRC connection and listen loops, the
  EventSub session loop, the StreamElements session loop and its per-session sender task, the OBS
  reconnect loop, the colour worker, the TTS worker, and the periodic loops in `DiskSpace`,
  `TwitchUptime` and `Clock`.
- **Handlers run on the read loops.** `TwitchIRCManager.OnMessage` is invoked synchronously inside the
  IRC listen loop, `TwitchEventHandler.Handle` synchronously inside the EventSub listen loop, and
  `StreamElementsEventHandler.Handle` inside the StreamElements one. A
  slow handler stalls that transport, so handlers return fast and push long work into a fire-and-forget
  async method (`_ = Handler(...)`). The first part of such a method up to its first `await` still runs
  on the read loop; a handler that blocks and reads no JSON starts with `await Task.Yield()`, as the
  train does.
- **Static mutable state with no locks** is the norm: `Subscriptions._giftBombs`, for one. Most is
  touched from one thread in practice. `ChatterList` is the exception: its set is locked, since IRC
  adds to it and OBS's stream-start event resets it.

## The module pattern

Nearly every module is a `static class` holding its own state and started once from `Program`. Follow
that rather than introducing instances. Visibility is `internal` for most, `public` for the Twitch
transport classes and the colour code (the test project reaches internals through `InternalsVisibleTo`).

## Where everything lives

Everything is under `code/<Area>/`, except [Program.cs](../Program.cs) (the entry point, startup order
and shutdown, above) at the root and the window in [ui/](#ui). The test project mirrors these folders.

### `code/Core/`

| File | Role | Detail |
|---|---|---|
| [Config.cs](../code/Core/Config.cs) | Loads `paths.json` + `secrets.json` into `Config.Data` | [infrastructure.md](infrastructure.md#config) |
| [ConsoleLogger.cs](../code/Core/ConsoleLogger.cs) | All logging: dated file + `LineLogged` event | [infrastructure.md](infrastructure.md#logging) |
| [Database.cs](../code/Core/Database.cs) | SQLite: the toilet rewards' `flushes` and the `money_goal` row | [infrastructure.md](infrastructure.md#database) |

### `code/Audio/`

| File | Role | Detail |
|---|---|---|
| [Sound.cs](../code/Audio/Sound.cs) | NAudio alert-sound playback | [infrastructure.md](infrastructure.md#sound) |
| [TextToSpeech.cs](../code/Audio/TextToSpeech.cs) | SAPI speech queue | [infrastructure.md](infrastructure.md#text-to-speech) |
| [LanguageFilter.cs](../code/Audio/LanguageFilter.cs) | Naive bad-word replacement before TTS | [infrastructure.md](infrastructure.md#language-filter) |

### `code/Twitch/`

| File | Role |
|---|---|
| [TwitchIRCManager.cs](../code/Twitch/TwitchIRCManager.cs) | Raw TCP IRC: read chat, send chat |
| [ChatHandler.cs](../code/Twitch/ChatHandler.cs) | Parses IRC lines; admin commands; public FAQ commands and their cooldown |
| [ChatCommand.cs](../code/Twitch/ChatCommand.cs) | Data holder for a public command |
| [ChatterList.cs](../code/Twitch/ChatterList.cs) | First-time-chatter list |
| [TwitchEventSub.cs](../code/Twitch/TwitchEventSub.cs) | WebSocket session loop and subscribing |
| [TwitchEventSubSubscription.cs](../code/Twitch/TwitchEventSubSubscription.cs) | The table of subscriptions to create |
| [TwitchEventHandler.cs](../code/Twitch/TwitchEventHandler.cs) | Dispatch on event type |
| [TwitchHelixApi.cs](../code/Twitch/TwitchHelixApi.cs) | Outbound REST: redemption status, channel category |
| [ChannelPoints.cs](../code/Twitch/ChannelPoints.cs) | What each channel-point reward does |
| [Ads.cs](../code/Twitch/Ads.cs) | Ad-break handling and chat warnings |
| [Cheers.cs](../code/Twitch/Cheers.cs) | Bits alert |
| [Raids.cs](../code/Twitch/Raids.cs) | Raid chat line |
| [StreamEvents.cs](../code/Twitch/StreamEvents.cs) | Logs Twitch's stream online and offline |
| [Subscriptions.cs](../code/Twitch/Subscriptions.cs) | Sub, resub, gift and gift-bomb alerts: sound, delay, speech; the gift-bomb list |
| [SubscriptionMessages.cs](../code/Twitch/SubscriptionMessages.cs) | The spoken sentences for those alerts |
| [CommunityGiftSub.cs](../code/Twitch/CommunityGiftSub.cs) | Collects the recipients of one gift bomb |
| [TestEvents/](../code/Twitch/TestEvents/) | `!test` scripts: simulated events built in code (`TestEventRunner`, `TestArgs`, one file per area), StreamElements tips included |

All of this is covered in [twitch.md](twitch.md) and [events.md](events.md).

### `code/StreamElements/`

| File | Role |
|---|---|
| [StreamElementsSocket.cs](../code/StreamElements/StreamElementsSocket.cs) | WebSocket session loop to StreamElements' Astro gateway: subscribe, probes, reconnect |
| [StreamElementsEventHandler.cs](../code/StreamElements/StreamElementsEventHandler.cs) | Dispatch on topic; the `EventSubs\se.…` dump without the tipper's email |
| [Tips.cs](../code/StreamElements/Tips.cs) | Tip alert: anthem, chat line, speech; counts the tip toward the money goal |

Covered in [streamelements.md](streamelements.md) and [events.md](events.md#tips).

### `code/Money/`

[MoneyGoal.cs](../code/Money/MoneyGoal.cs): the tips' running total toward a goal, kept in the database,
written to OBS's `Text: Money Goal`, and changed by `!moneygoal` and `!resetmoney`. See
[obs.md](obs.md#money-goal).

### `code/Obs/`

[ObsConnection.cs](../code/Obs/ObsConnection.cs): the OBS websocket, its reconnect loop, and the
stream-state handler ([obs.md](obs.md#connection)). [Obs.cs](../code/Obs/Obs.cs): the thin OBS request
wrappers ([obs.md](obs.md#the-wrapper)). [LayoutColoring.cs](../code/Obs/LayoutColoring.cs): the
serialised colour-change queue and the OBS animation ([obs.md](obs.md#layout-recolouring)).
[ObsInputs.cs](../code/Obs/ObsInputs.cs): what the wrappers last set on each source (or OBS's refusal),
and the lines the dashboard's OBS sources list shows from it ([dashboard.md](dashboard.md#obs-sources)).

### `code/Games/`

[Games.cs](../code/Games/Games.cs): loads `<BotInput>\games.json` (Twitch category id → game name and
game, window and audio capture executables) and resolves a category to its game, falling back to id `0`. `HandleUpdate` takes
`channel.update` and calls `Apply`, the per-game setup, when the category changes;
`OnStreamStartedAsync` calls it when OBS starts streaming ([events.md](events.md#category-change-and-going-live)). `Apply` sets the background, borders, layout and games scenes, sidebar, colour, left panel and capture sources; the capture sources only for a category in `games.json`.

[GameBackground.cs](../code/Games/GameBackground.cs): `Set(gameName)` points `Image: Background` at the
game's PNG in `Directories.Backgrounds`, else `Template.png` there, or logs and leaves it when neither exists.
[GameBorder.cs](../code/Games/GameBorder.cs): `Set(game)` points the inner and outer `Border: Colorable` sources,
and their fade twins, at the border images for the game's `Aspect` (16x9 by default) in `Directories.Layout`,
or the plain ones when that aspect has none of its own.
[GameScene.cs](../code/Games/GameScene.cs): `Set(game)` shows the `!Scene: Layout <aspect>` and
`!Scene: Games <aspect>` items of `Basic Stream Layout` for the game's aspect (16x9 by default) and hides the
others of each kind there; `GamesScenes()` lists the games scenes for `GameCapture`, which shows and hides
its slots in each.
[GameSidebar.cs](../code/Games/GameSidebar.cs): `SetGame(name)` makes the game the one whose
`Sidebar: Dynamic 1` and `2` images follow LiveSplit's split index; `OnIndexChanged`, from `SplitWatcher`, and
`OnObsConnected`, from `ObsConnection`, show the image for the current index.
[GameColor.cs](../code/Games/GameColor.cs): `Set(gameName)` queues the game's `gameschemes` colour scheme
through `LayoutColoring`, or logs and leaves the colour when there is none.
[GameCapture.cs](../code/Games/GameCapture.cs): `Set(game)` points the three numbered game, window and
audio capture sources at the game's executables, `none` for unused slots. All three in
[obs.md](obs.md#per-game-setup).

### `code/LeftPanel/`

[LeftPanel.cs](../code/LeftPanel/LeftPanel.cs): loads `<BotInput>\leftpanel.json` (game name → the groups always
shown and the panels to take turns) and finds a game's entry, falling back to `default`. `Start(game)`,
called from `Games.Apply`, rotates those panels in OBS until the next `Start`; `OnObsConnected`, from
`ObsConnection`'s `Connected` handler, restarts it after an OBS restart; `Stop` and `Resume` back the
`!stoppaneltimer` / `!startpaneltimer` admin commands; `OnSplit`, from `SplitWatcher`, plays a game's
previous-split panels; `Status` and `StatusLines` feed the dashboard's left-panel column. Each rotation
colours LiveSplit's split sources through `SplitColors`. See [obs.md](obs.md#left-panel).

### `code/LiveSplit/`

[SplitWatcher.cs](../code/LiveSplit/SplitWatcher.cs): watches LiveSplit's split index
(from `Number.txt` and `Completed.txt` in `<LivesplitGenerated>\CurrentSplit\AllSplits`) and calls `LeftPanel.OnSplit` when it rises by one
split. See [obs.md](obs.md#splits-from-livesplit).

[SplitColors.cs](../code/LiveSplit/SplitColors.cs): a game's split colours from `gameschemes.json` (over
`none`'s), the sign → colour table, and the filter colours for the split list highlights and the previous
split's differences, which the left-panel rotation sets. See [obs.md](obs.md#split-colours).

### `code/Timing/`

[ClockMarks.cs](../code/Timing/ClockMarks.cs): pure wall-clock arithmetic (window numbering on clock marks, time to the
next minute) for the periodic loops. See [infrastructure.md](infrastructure.md#periodic-work).

[Clock.cs](../code/Timing/Clock.cs): the per-minute loop that writes the OBS clock text and posts the date to chat
at local midnight. See [infrastructure.md](infrastructure.md#clock).

[DiskSpace.cs](../code/Timing/DiskSpace.cs): the free-space watchdog for drive `A:\`
([infrastructure.md](infrastructure.md#disk-space)). [TwitchUptime.cs](../code/Timing/TwitchUptime.cs):
scrapes the uptime from decapi.me and writes it to OBS ([infrastructure.md](infrastructure.md#uptime)).
[ConnectionHealth.cs](../code/Timing/ConnectionHealth.cs): the shared IRC, EventSub and StreamElements age
thresholds, and the once-a-second check that logs a connection going bad and recovering
([infrastructure.md](infrastructure.md#connection-health)).

### `code/Util/`

[FireForget.cs](../code/Util/FireForget.cs): `FireForget.Run(code, what, work)` starts an async body
without awaiting it and logs a throw as `Error <code>: <what> failed`. Used for EventSub handlers and chat sends.

[JsonElementExtensions.cs](../code/Util/JsonElementExtensions.cs): `ReadString`, `ReadInt`, `ReadDecimal`, `ReadBool` and `ReadElement` on
`JsonElement`, reading by dotted path (`"sub.sub_tier"`) and returning a fallback for a missing step, a
`null` or the wrong kind. `ReadInt`, `ReadDecimal` and `ReadBool` also accept numeric and boolean strings
(`ReadDecimal` only with a dot). Every EventSub and StreamElements payload read goes through these.

[Util.cs](../code/Util/Util.cs): `TrueModulo` overloads, used only by `ColorUtil.HsvToRgb`.

### `code/Color/`

See [color.md](color.md), which is the authority on them.

### `ui/`

[MainWindow.xaml](../ui/MainWindow.xaml) and its code-behind: the dashboard (status bar, log, and on the
right a column of the OBS sources the bot has set and the left-panel column). See [dashboard.md](dashboard.md).

## Data flows worth knowing

**A chat message.** IRC line → `TwitchIRCManager.ListenLoop` → `OnMessage` →
`ChatHandler.ProcessMessage` → `HandlePrivMsg` → log it, `ChatterList.AddChatter`, then admin commands
(for `lotsofs`/`botsofs`) or the public FAQ commands (everyone else), which answer in chat through
`TwitchIRCManager.SendMessage`.

**A channel-point redemption.** EventSub `notification` →
`TwitchEventHandler.Handle` → `ChannelPoints.ProcessAdd` → switch on reward id → sound, database,
OBS, or `LayoutColoring` → for colour rewards, `TwitchHelixApi.UpdateRedemption` closes it.

**A colour request.** `LayoutColoring.TryChangeTo…` → parser ([color.md](color.md)) →
`ColorEntry` → `ChangeColor`: enqueue a `ColorRequest` and post the chat line → the worker runs the
nine-second OBS animation ([obs.md](obs.md#layout-recolouring)).

**A sub alert.** EventSub `channel.chat.notification` → `HandleChannelChatNotification` (dumps the
payload to a custom log file) → `Subscriptions.Handle…` → sound, delay, `TextToSpeech.EnqueueSpeech`.

**An ad break.** EventSub `channel.ad_break.begin` → `Ads.Process`: waits out the ad, cancels any
earlier pre-warnings, starts a new `RunScheduleAsync` with four warnings.

**A tip.** StreamElements `message` on `channel.tips` → `StreamElementsEventHandler.Handle` (dumps it,
email removed) → `Tips.Process` → `MoneyGoal.Credit` (database, then `Text: Money Goal`, in the
background), anthem, `💸` chat line, 6 s, speech.

**A log line.** Anywhere → `ConsoleLogger.ColoredLine` → async file append **and** `LineLogged` event →
`MainWindow` marshals to the UI thread and appends to the list.

## Cross-cutting rules

- **Output goes through the logger.** Human-readable output uses `ConsoleLogger.ColoredLine`, picking
  the `ColorType` that matches the event, never the colour you want.
- **Speech and sound are fire-and-forget.** `TextToSpeech.EnqueueSpeech` and `Sound.PlaySound` return
  immediately.
- **Chat output is `TwitchIRCManager.SendMessage`**, only. It silently does nothing while the IRC
  writer is null.
- **Errors**: catch, log `Error <code>`, write the exception to the file, recover. Codes are
  catalogued in [reference.md](reference.md#error-codes).
- **No Twitch SDK.** Both Twitch transports are hand-rolled, and so is the StreamElements one; see
  [twitch.md](twitch.md) and [streamelements.md](streamelements.md).
