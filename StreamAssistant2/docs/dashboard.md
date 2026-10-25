# The dashboard window

[ui/MainWindow.xaml](../ui/MainWindow.xaml) and [ui/MainWindow.xaml.cs](../ui/MainWindow.xaml.cs): the
bot's only window. A personal control panel beside OBS, not something shown on stream. More panels are
expected to join the status bar and log.

Read from the code. The window was not opened while writing this (the bot's window is only ever opened
by the user, with F5).

## Layout

A `DockPanel` on a near-black background, Consolas 13, default size 1600 × 900. From left to right under
the status bar: the log, the OBS sources column, the left-panel column.

- **Status bar** (docked top): `Twitch IRC time since last ping: mm:ss  |  Event Sub time since last
  keepalive: mm:ss  |  StreamElements time since last reply: mm:ss`. The three ages are `Run` elements
  named `IrcAge`, `EventSubAge` and `StreamElementsAge`.
- **Left-panel column** (docked right, outermost, 360 px, `#141414`): an `ItemsControl` named
  `LeftPanelList` in a vertical `ScrollViewer`, each row a wrapping `TextBlock` in its own colour. See
  [The left panel](#the-left-panel).
- **OBS sources column** (docked right, left of the left-panel column, 440 px, `#141414`): an
  `ItemsControl` named `ObsSourcesList` in a vertical `ScrollViewer`, each row a wrapping `TextBlock`
  with a 10 × 10 colour swatch on its left when it has one and a tooltip when it has one. See
  [OBS sources](#obs-sources).
- **Log** (fills the rest, about 800 px at the default size): a `ListBox` named `LogList`, extended selection, no horizontal scrollbar,
  virtualised with container recycling, bound to an `ObservableCollection<LogLine>`. Each row is a
  wrapping `TextBlock` in that line's colour, with a custom item template that has no hover state and a
  `#264F78` selected background.

## The status bar

A `DispatcherTimer` ticks every 250 ms and calls `UpdateStatus()`, which reads three pieces of static
state straight from the transports (the window polls; the bot never pushes to it) and colours them with
`ConnectionHealth.Classify`:

| Age | Source | Healthy | Yellow after | Orange after | Red after |
|---|---|---|---|---|---|
| IRC | `TwitchIRCManager.TimeSinceLastPing` | grey | 300 s | 360 s | 420 s |
| EventSub | `TwitchEventSub.KeepAliveTimer.Elapsed` | grey | 12 s | 15 s | 20 s |
| StreamElements | `StreamElementsSocket.SinceLastMessage.Elapsed` | grey | 40 s | 50 s | 70 s |

The thresholds live in [ConnectionHealth.cs](../code/Timing/ConnectionHealth.cs), shared with the
connection-health log ([infrastructure.md](infrastructure.md#connection-health)), and red *is* the reconnect
timeout: `TwitchIRCManager.SilenceTimeout`, `TwitchEventSub.KeepAliveTimeout` and
`StreamElementsSocket.SilenceTimeout`. The IRC number is really
*time since any line arrived* ([twitch.md](twitch.md#irc)); Twitch pings about every five minutes, hence the
long thresholds. EventSub's keepalive is about every 10 s, so its yellow starts at 12 s rather than 10 to
avoid flickering every cycle. StreamElements' age is the time since any message, and the bot's own probe
draws a reply every 30 s ([streamelements.md](streamelements.md#keepalive-the-probe)), so it climbs to
about 30 and resets; yellow means a reply is late. With no StreamElements credentials in `secrets.json`,
the segment shows `off` in grey instead of an age.

One quirk in `ShowAge`: the text format is `mm\:ss`, which shows only the minutes component, so an age over
an hour wraps.

Before the first IRC line arrives `_lastPingTime` is `DateTime.MinValue`, so the IRC age starts out huge
(and red) until the first line. EventSub's and StreamElements' stopwatches start at process start.

## The left panel

The right-hand column shows what the left-panel rotation ([obs.md](obs.md#the-rotation)) is doing:

- **How it updates:** the status bar's 250 ms tick also calls `UpdateLeftPanel()`. That builds the lines
  with `LeftPanel.StatusLines(LeftPanel.Status, DateTime.UtcNow)` and redraws only when they differ from
  the last draw. That happens when the rotation moves on (the bot replaces the immutable `RotationStatus`)
  and once a second while a timer counts. `StatusLines` is a pure function in the bot (tested), so the
  window only maps each line's kind to a brush.
- **What it shows**, from top to bottom:
  - a title: `Left panel: <game>`, with ` (default)` when the game has no entry of its own, or the entry
    its panels come from when its entry names another (`SWAT4 (Run_NoSubsplits)`; `LeftPanel.Which`), and
    `, stopped` after `!stoppaneltimer`
  - the `Always` groups
  - the `Loop` panels
  - `Previous split`, only for a game with previous-split panels
- **Each panel's line:** its `Name` from `leftpanel.json` if it has one (`> Per map: Run Delta · 12 / 50 s`).
  Otherwise its groups (`(blank)` for a pause) and the texts it sets in quotes. Then its seconds, for
  example `Splits Key Count · 50 s`.
- **Marking:** the panel up is orange with a `> `. During a previous split, the playing `Previous` panel is
  the orange one, and the loop panel the rotation will return to keeps a white `> `. The `Always` groups
  are orange too once the rotation has shown its first panel, since they stay up throughout.
- **Missing groups:** once the rotation has listed OBS's groups, any line that uses a group OBS doesn't
  have is red: an `Always` group, a loop panel or a previous-split panel. The panel that's up keeps its
  `> ` and timer but is red too. The log names the groups once (`Left panel: <game> has groups OBS
  doesn't: …`).
- **Timer:** the orange panel's seconds read `<elapsed> / <seconds> s`, whole seconds since it was shown,
  stopping at its length. There's no timer when a panel stays up for good: a stopped rotation, or a loop
  with only one panel.
- **No rotation:** before any rotation, and for a game whose panel is left as is, the column reads
  `Left panel: none`.

| Line | Hex |
|---|---|
| Title | `#CCCCCC` |
| Section heading | `#888888` |
| Panel, group, and the marked loop panel during a split | `#EEEEEE` |
| The panel up, and the `Always` groups once shown | `#EE8844` (the `SceneChanges` orange) |
| Any line with a group OBS doesn't have | `#EE4444` (the `Error` red), whatever its kind |

## OBS sources

A column of its own, between the log and the left panel: every OBS source the bot has set this run and
what it last set it to.

- **Where it comes from:** the three OBS wrappers that change a source, `Obs.SetInputSetting` (text, image
  file and capture window, so also `SetImageSource` and the left panel's own calls), `SetInputVolumeDb` and
  `SetFilterProperty`. Each records what it set, or OBS's error, in `ObsInputs.Current`
  ([code/Obs/ObsInputs.cs](../code/Obs/ObsInputs.cs); [obs.md](obs.md#the-wrapper)). `SetSourceEnabled`
  records each item it shows or hides there too, which is how the filter rows know when they're off screen.
  And `ObsInputs.Start()`, from `Program`, works out every 2 s while OBS is ready which image and text sources
  the bot has set are showing in `Basic Stream Layout`, whichever scene is live: `ObsInputs.ShowingIn` lists that
  scene's items (`Obs.SceneItems`, obs-websocket's `GetSceneItemList`, sent raw because the library's own
  scene items leave out the enabled state), and follows each enabled item that is a scene into its items, or
  a group into its (`GetGroupSceneItemList`, raw too, since the library's call for it is broken). A source
  is showing when an enabled item reaches it that way; each scene or group is listed once per pass. When
  `Basic Stream Layout` itself can't be listed, the last answer stays. Nothing is recorded while OBS isn't connected, since
  nothing is set. The list only knows what the bot set: a change made in OBS by hand doesn't show, and a
  source the bot hasn't touched yet this run isn't listed.
- **How it updates:** the same 250 ms tick calls `UpdateObsSources()`, which builds the lines with
  `ObsInputs.StatusLines(Snapshot(), ObsConnection.IsReady, DateTime.UtcNow, Visible(), <characters per
  line>, Showing())` and changes the list only when they differ from the last draw: when something was set,
  shown or hidden, OBS connected or disconnected, or an age reached another second. The rows are kept and
  updated in place (`SourceLine` and `ChipView` raise `PropertyChanged`), so a row isn't rebuilt while you
  hover it and its tooltip stays open; rows are only added or removed at the end. The characters per line
  come from the list's width and the width of a Consolas digit, so they follow the scrollbar coming and
  going. `StatusLines` is a pure function in the bot (tested).
- **What it shows**, from top to bottom, each group left out when it has nothing:
  - a title, `OBS sources`, or `OBS sources (OBS not connected)`, when the last values stay listed
  - `Captures`: each used game, window and audio capture slot, sorted by source, as
    `<source> → <executable>` (the part of OBS's `title:class:executable` value after the last colon), then
    ` · <dB> dB` when a volume was set on it. The slots set to `none` follow on one grey line, their kinds'
    short names (`GameCapture.KindName`) and slot numbers grouped as in the capture log line
    (`GameCapture.SlotGroups`): `Audio capture 1, 2; Game capture 1, 2; Window capture 0, 1, 2 → none · 12 s ago`.
    The numbers are the sources' own, from 0
  - `Filters`: rows of swatches with short codes, then any other filter (see below)
  - `Images`: `<source> → <file name>`, sorted by source. When a line wouldn't fit on one line, the source
    name is shortened word by word from the last, each word to its first letter with its brackets and
    punctuation (`ObsInputs.Abbreviate`): `Border: Colorable Outer (Transitionary)` becomes
    `Border: Colorable Outer (T)`, then `Border: Colorable O (T)`, and so on, until it fits. Room for an age of
    ` · 59 s ago` is always kept, so the name doesn't change as the age grows. Hovering the line shows the
    full name and path. An image last found not showing in `Basic Stream Layout` is grey, and its tooltip
    says so; one not checked yet stays white
  - `Text`: sorted by source. A text set directly shows in quotes, `<source> → "<text>"`, line breaks as
    ` ⏎ `. A text source reading its text from a file (the left panel's LiveSplit texts) shows the file's
    name, `<source> → SegmentTime.txt`, shortened and hovered like an image's. Both come in through
    `SetInputSetting`, as the `text` and `file` keys; a `file` counts as an image only for an image type
    (`.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, `.tga`, `.webp`, `.jxr`, `.psd`; `ObsInputs.KindOf`), any other
    as a text. A text last found not showing in `Basic Stream Layout` is grey, like an image
  - `Other`: any other setting, `<source> · <setting> → <value>`, and a volume on a source with no capture
    line, sorted by source
  - before anything is set, `Nothing set yet`

  On a plain line, the part before the arrow is light blue and the rest white; on a greyed-out line it is a
  greyed blue and the rest grey; on a failed line the whole line is red. Every line ends with
  ` · <n> s ago`, ` · <n> m ago`, ` · <n> h ago` or ` · <n> d ago`, since it was last set (a capture's
  volume counts; the `none` line counts its newest slot).
  Each group heading is bold on a dark blue band (`#1C2733`) across the column, with space above it.
- **Filters** come as up to four rows, each a label, its swatches left to right with their codes, and
  ` · <n> s ago` for its newest. A row is left out until one of its swatches is set; an unset swatch in a
  shown row is hollow with a grey code. Hovering a swatch shows its source, filter, property and hex (no
  age, so the tooltip doesn't change every second).

  | Row | Codes → sources | Shown while |
  |---|---|---|
  | `Layout` | `In` `Border: Colorable Inner`, `Out` `Border: Colorable Outer`, `Txt` `!Scene: All Colorable`, on `Color Correction` | `!Layout: Colorables` is up |
  | `Fade` | the same three with ` (Transitionary)` | `!Layout: Colorables (Transitionary)` is up, during a colour change's fade |
  | `Highlights` | `AG` AheadGained, `AL` AheadLost, `BG` BehindGained, `BL` BehindLost, `Au` Gold: the `Livesplit: SL Highlights <kind>` sources, on `Color Correction 2` | the left panel's `Splits List Highlights` group is up |
  | `Previous` | `Run`, `Seg`, `Au`: `Livesplit: Previous Run`, `Segment`, `Gold`, on `Color Correction` | the left panel's `Splits Previous Values` group is up |

  When the bot last hid a row's group in every scene it showed or hid it in, the row's label and codes are
  grey, and each swatch's tooltip says the group is hidden; the swatches keep their colours. A left-panel
  group matches under either prefix (`_LP: `, `_LPC: `) and in any copy (`LeftPanel.GroupName`,
  `Numbered`). A group the bot hasn't shown or hidden yet this run counts as shown. The source and group
  names come from `LayoutColoring` and `SplitColors`, which set them.

  The Inner and Outer Attachments (and their twins) share their main source's swatch, listed in its
  tooltip, while their colour matches. Any filter not shown in a row, including an attachment of another
  colour, is listed under the rows as `<source> · <filter> · <property> → #rrggbb`, with a small swatch,
  sorted by source. Colours go to OBS as `0xAABBGGRR` integers (`ColorUtil.ToOBS`); `ObsInputs.ColorHex`
  turns them back.
- **Failures:** when OBS refused the last set (a source that doesn't exist, say), the whole line shows the
  value it tried, then ` · failed: <OBS's message>`, in red; a refused swatch gets a red edge and code, with
  the message in its tooltip. A refused `none` slot keeps its own red line. The next successful set clears
  it. The caller still logs its own `Couldn't set …` line as before.

| Line | Hex |
|---|---|
| Title | `#CCCCCC` |
| Group heading, bold | `#7FB2E5` on `#1C2733` |
| Source name (before the arrow), row label | `#B9D6F2` |
| The same on a greyed-out line: the `none` line's kinds, a text or image not showing, a hidden row's label | `#6F8296` |
| Value and age, swatch code | `#EEEEEE` |
| The rest of a greyed-out line (the `none` line, a text or image not showing in `Basic Stream Layout`), a row's age, a hidden row's codes, an unset swatch's code | `#888888` |
| Swatch edge | `#666666` |
| Failed source, a refused swatch's edge and code | `#EE4444` (the `Error` red) |

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
| `ChatOutgoing` | `#EEEE44` (the same yellow as the status bar's warn colour) |
| `Notification` | `#44EE44` |
| `ConnectionNotification` | `#BBEEBB` |
| `Helix` | `#44EEEE` |
| `EventSubNotification` | `#4488EE` |
| `EventSubConfusion` | `#88BBEE` |
| `StreamElementsNotification` | `#44CC99` |
| `StreamElementsConfusion` | `#99DDCC` |
| `AdNotification` | `#0088EE` |
| `Important` | `#EE44EE` |
| `SceneChangesImportant` | `#EEBB88` |
| `SceneChanges` | `#EE8844` |
| `SceneChangesUnimportant` | `#BB8844` |

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
