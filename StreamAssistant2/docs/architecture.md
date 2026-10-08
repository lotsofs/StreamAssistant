# Architecture

The shape of the bot: what starts when, which thread things run on, who talks to whom, and where every
file fits. Read this first when you are new to the tree; the other docs go deep on one area each.

Everything here was read from the code. Where a behaviour was also run, it says so.

## What it is

A single-user Windows desktop program (WPF, `net8.0-windows`, `WinExe`) that sits beside OBS while the
channel `lotsofs` streams. It listens to Twitch, and reacts by speaking, playing sounds, posting in chat,
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
                         │  DiskSpace, TwitchUptime, Ads: own loops   │
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
   1. `Coloring.Load()`: colour tables and schemes from disk ([color.md](color.md)).
   2. `Database.InitAsync()`: opens `streamAssistant.db`, creates the `flushes` table if missing.
   3. Logs `STARTED!`.
   4. `DiskSpace.Start()`, `TwitchUptime.Start()` and `ConnectionHealth.Start()`: the periodic loops.
   5. `EnableBot()`: `ObsConnection.Connect()`, `TwitchIRCManager.Connect()` plus
      `OnMessage += ChatHandler.ProcessMessage`, `TwitchHelixApi.Init()`, `TwitchEventSub.Connect()`,
      `LayoutColoring.StartWorker()`.
   6. `TextToSpeech.ReportStart()`, which is the first touch of `TextToSpeech`, so its static
      constructor (and the `Microsoft Catherine` voice lookup) runs only now, after everything else
      is already live.
   Any exception logs `Error PRG1` and the bot stays half-started.
6. Closing the window ends `Application.Run`. `Main` then posts `🍂 Shutting Down` to chat, logs
   `SHUTDOWN!` and calls `DisableBot()` (OBS disconnect, IRC disconnect, EventSub cancel). The chat
   post is fire-and-forget and the IRC socket is closed straight after, so that message can be lost.
   Read, not reproduced.

Stopping the debugger kills the process without running any of the shutdown path.

## Threading model

- **UI thread**: only the WPF window and its 250 ms status timer. Bot code never touches WPF controls
  ([dashboard.md](dashboard.md)).
- **Everything else is the thread pool.** Each long-lived loop is started as `_ = Task.Run(...)` or
  `_ = SomethingAsync()` from `StartBotAsync`'s context: the IRC connection and listen loops, the
  EventSub session loop, the OBS reconnect loop, the colour worker, the TTS worker, and the
  periodic loops in `DiskSpace` and `TwitchUptime`.
- **Handlers run on the read loops.** `TwitchIRCManager.OnMessage` is invoked synchronously inside the
  IRC listen loop, and `TwitchEventHandler.Handle` synchronously inside the EventSub listen loop. A
  slow handler stalls that transport, so handlers return fast and push long work into a fire-and-forget
  async method (`_ = Handler(...)`). The first part of such a method up to its first `await` still runs
  on the read loop; a handler that blocks and reads no JSON starts with `await Task.Yield()`, as the
  train does.
- **Static mutable state with no locks** is the norm: `Subscriptions._giftBombs`,
  `ChatterList._list`. Most are touched from one thread in practice, but see the
  items in [TODO.md](TODO.md) for the ones that aren't.

## The module pattern

Nearly every module is a `static class` holding its own state and started once from `Program`. Follow
that rather than introducing instances. Visibility is `internal` for most, `public` for the Twitch
transport classes and the colour code (the test project reaches internals through `InternalsVisibleTo`).

## Where everything lives

### Root (older layout, mid-migration)

| File | Role | Detail |
|---|---|---|
| [Program.cs](../Program.cs) | Entry point, startup order, shutdown | above |
| [Config.cs](../Config.cs) | Loads `paths.json` + `secrets.json` into `Config.Data` | [infrastructure.md](infrastructure.md#config) |
| [ConsoleLogger.cs](../ConsoleLogger.cs) | All logging: dated file + `LineLogged` event | [infrastructure.md](infrastructure.md#logging) |
| [Database.cs](../Database.cs) | SQLite, one table (`flushes`) | [infrastructure.md](infrastructure.md#database) |
| [Sound.cs](../Sound.cs) | NAudio alert-sound playback | [infrastructure.md](infrastructure.md#sound) |
| [TextToSpeech.cs](../TextToSpeech.cs) | SAPI speech queue | [infrastructure.md](infrastructure.md#text-to-speech) |
| [LanguageFilter.cs](../LanguageFilter.cs) | Naive bad-word replacement before TTS | [infrastructure.md](infrastructure.md#language-filter) |
| [DiskSpace.cs](../DiskSpace.cs) | Free-space watchdog for drive `A:\` | [infrastructure.md](infrastructure.md#disk-space) |
| [TwitchUptime.cs](../TwitchUptime.cs) | Scrapes uptime from decapi.me; midnight date post | [infrastructure.md](infrastructure.md#uptime-and-clock-check) |
| [LayoutColoring.cs](../LayoutColoring.cs) | Serialised colour-change queue and the OBS animation | [obs.md](obs.md#layout-recolouring) |
| [Obs.cs](../Obs.cs) | Thin OBS wrappers (rest of the file is commented out) | [obs.md](obs.md) |
| [ObsConnection.cs](../ObsConnection.cs) | OBS websocket and its reconnect loop | [obs.md](obs.md#connection) |
| [Util.cs](../Util.cs) | `TrueModulo` overloads; used only by `ColorUtil.HsvToRgb` | |
| [Money.cs](../Money.cs), [ISaveable.cs](../ISaveable.cs) | Unused leftovers | [legacy.md](legacy.md) |
| [Games.cs](../Games.cs), [Donations.cs](../Donations.cs), [LeftPanel.cs](../LeftPanel.cs) | Entirely commented out | [legacy.md](legacy.md) |

### `code/Twitch/`

| File | Role |
|---|---|
| [TwitchIRCManager.cs](../code/Twitch/TwitchIRCManager.cs) | Raw TCP IRC: read chat, send chat |
| [ChatHandler.cs](../code/Twitch/ChatHandler.cs) | Parses IRC lines; admin commands; (stubbed) public commands |
| [ChatCommand.cs](../code/Twitch/ChatCommand.cs) | Data holder for a public command |
| [ChatterList.cs](../code/Twitch/ChatterList.cs) | First-time-chatter list |
| [TwitchEventSub.cs](../code/Twitch/TwitchEventSub.cs) | WebSocket session loop and subscribing |
| [ConnectionHealth.cs](../code/Twitch/ConnectionHealth.cs) | Shared IRC/EventSub age thresholds; logs a connection going bad and recovering |
| [TwitchEventSubSubscription.cs](../code/Twitch/TwitchEventSubSubscription.cs) | The table of subscriptions to create |
| [TwitchEventHandler.cs](../code/Twitch/TwitchEventHandler.cs) | Dispatch on event type |
| [TwitchHelixApi.cs](../code/Twitch/TwitchHelixApi.cs) | Outbound REST (redemption status only) |
| [ChannelPoints.cs](../code/Twitch/ChannelPoints.cs) | What each channel-point reward does |
| [Ads.cs](../code/Twitch/Ads.cs) | Ad-break handling and chat warnings |
| [Cheers.cs](../code/Twitch/Cheers.cs) | Bits alert |
| [Subscriptions.cs](../code/Twitch/Subscriptions.cs) | Sub, resub, gift and gift-bomb alerts: sound, delay, speech; the gift-bomb list (plus a large commented-out legacy block) |
| [SubscriptionMessages.cs](../code/Twitch/SubscriptionMessages.cs) | The spoken sentences for those alerts |
| [CommunityGiftSub.cs](../code/Twitch/CommunityGiftSub.cs) | Collects the recipients of one gift bomb |
| [TestEvents/](../code/Twitch/TestEvents/) | `!test` scripts: simulated events built in code (`TestEventRunner`, `TestArgs`, one file per area) |

All of this is covered in [twitch.md](twitch.md) and [events.md](events.md).

### `code/Timing/`

[ClockMarks.cs](../code/Timing/ClockMarks.cs): pure wall-clock arithmetic (window numbering on clock marks, time to the
next minute, next local midnight) for the periodic loops. See [infrastructure.md](infrastructure.md#periodic-work).

### `code/Util/`

[FireForget.cs](../code/Util/FireForget.cs): `FireForget.Run(code, what, work)` starts an async body
without awaiting it and logs a throw as `Error <code>: <what> failed`. Used for EventSub handlers and chat sends.

[JsonElementExtensions.cs](../code/Util/JsonElementExtensions.cs): `ReadString`, `ReadInt`, `ReadBool` and `ReadElement` on
`JsonElement`, reading by dotted path (`"sub.sub_tier"`) and returning a fallback for a missing step, a
`null` or the wrong kind. `ReadInt` and `ReadBool` also accept numeric and boolean strings. Every EventSub
payload read goes through these.

### `code/Color/`

See [color.md](color.md), which is the authority on them.

### `ui/`

[MainWindow.xaml](../ui/MainWindow.xaml) and its code-behind: the dashboard. See
[dashboard.md](dashboard.md).

## Data flows worth knowing

**A chat message.** IRC line → `TwitchIRCManager.ListenLoop` → `OnMessage` →
`ChatHandler.ProcessMessage` → `HandlePrivMsg` → log it, `ChatterList.AddChatter`, then admin commands
(for `lotsofs`/`botsofs`) or public commands (everyone else; replies are stubbed out).

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
- **No Twitch SDK.** Both transports are hand-rolled; see [twitch.md](twitch.md).
