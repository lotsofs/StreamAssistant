# The dashboard window

[ui/MainWindow.xaml](../ui/MainWindow.xaml) and [ui/MainWindow.xaml.cs](../ui/MainWindow.xaml.cs): the
bot's only window. A personal control panel beside OBS, not something shown on stream. More panels are
expected to join the status bar and log.

Read from the code. The window was not opened while writing this (the bot's window is only ever opened
by the user, with F5).

## Layout

A `DockPanel` on a near-black background, Consolas 13, default size 1100 × 650:

- **Status bar** (docked top): `Twitch IRC time since last ping: mm:ss  |  Event Sub time since last
  keepalive: mm:ss`. The two ages are `Run` elements named `IrcAge` and `EventSubAge`.
- **Log** (fills the rest): a `ListBox` named `LogList`, extended selection, no horizontal scrollbar,
  virtualised with container recycling, bound to an `ObservableCollection<LogLine>`. Each row is a
  wrapping `TextBlock` in that line's colour, with a custom item template that has no hover state and a
  `#264F78` selected background.

## The status bar

A `DispatcherTimer` ticks every 250 ms and calls `UpdateStatus()`, which reads two pieces of static
state straight from the transports (the window polls; the bot never pushes to it):

| Age | Source | Healthy | Yellow after | Orange after | Red after |
|---|---|---|---|---|---|
| IRC | `TwitchIRCManager.TimeSinceLastPing` | grey | 300 s | 360 s | 420 s |
| EventSub | `TwitchEventSub.KeepAliveTimer.Elapsed` | grey | 10 s | 12 s | 15 s |

The IRC number is really *time since any line arrived* ([twitch.md](twitch.md#irc)). Twitch pings
about every five minutes, hence the long thresholds; at the red one the bot reconnects
(`TwitchIRCManager.SilenceTimeout`). EventSub's keepalive is about every 10 s, and the
bot itself gives up after 20 s.

Two small quirks, both visible in `ShowAge`:

- The doc comment promises "a dark red background past `deadSeconds`", but the code only sets the
  *foreground* to red (the same `#EE4444` as `Error` lines).
- The text format is `mm\:ss`, which shows only the minutes component, so an age over an hour wraps.

Before the first IRC line arrives `_lastPingTime` is `DateTime.MinValue`, so the IRC age starts out huge
(and red) until the first line. EventSub's stopwatch starts at process start.

## The log

`ConsoleLogger.LineLogged` fires on whichever thread logged. `OnLineLogged` hops to the UI thread with
`Dispatcher.InvokeAsync` (and skips if the dispatcher is shutting down); `AddLine` then:

1. decides whether the view is at the bottom (`VerticalOffset >= ScrollableHeight - 1`, or the scroller
   not found yet)
2. appends the line
3. trims the oldest if over `MAX_LINES` = 5000. The file keeps everything
4. scrolls to the end **only if it was already at the bottom**, so scrolling up to read stops the follow

The `ScrollViewer` is found by walking the visual tree on `Loaded`.

Ctrl+C is bound to `CopySelected`. `SelectedItems` is in click order, so it copies the selected lines
in *log* order, joined with newlines, to the clipboard. Clipboard failures are swallowed.

The colour for each channel is looked up in `_logColors`:

| `ColorType` | Hex |
|---|---|
| `None` | `#EEEEEE` |
| `Error` | `#EE4444` |
| `ChatIncoming` | `#EEEEBB` |
| `ChatOutgoing` | `#EEEE88` |
| `Notification` | `#44EE44` |
| `ConnectionNotification` | `#448844` |
| `Helix` | `#44EEEE` |
| `EventSubNotification` | `#4488EE` |
| `EventSubConfusion` | `#88BBEE` |
| `AdNotification` | `#0088EE` |
| `Important` | `#EE44EE` |

A type missing from the table falls back to `None`'s colour. Add a channel by adding the enum member in
`ConsoleLogger` and a row here and in `_logColors`.

## Rules for the window

- **Bot code never touches WPF controls.** It logs; the window reads static state or subscribes to an
  event. Keep it that way so the bot stays testable and the UI thread stays free.
- Every cross-thread update goes through `Dispatcher.InvokeAsync`. Don't `Invoke` (blocking) from a
  logging call: logging happens on the IRC and EventSub read loops.
- Brushes are created once and frozen (`MakeBrush`) so any thread can read them.
- The bot is **started from `Window.Loaded`** on the thread pool ([architecture.md](architecture.md)).
  The window must be able to show log lines before the bot has started, and after it stops.

## Adding a panel

Follow the existing pattern; don't push data in from the bot.

1. Put the XAML in the `DockPanel` (or a new file in `ui/` with its code-behind; WPF windows and panels
   live in `ui/`, not under `code/`).
2. Poll a static value on a `DispatcherTimer` (as the status bar does), **or** subscribe to an event the
   module raises and marshal onto the panel's `Dispatcher`.
3. Unsubscribe in `Closed`, as `MainWindow` does for `LineLogged`.
4. If the panel needs data a module doesn't expose yet, add a read-only static property to the module,
   as `TimeSinceLastPing` and `KeepAliveTimer` were.
