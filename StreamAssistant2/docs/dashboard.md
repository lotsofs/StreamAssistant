# The dashboard window

[ui/MainWindow.xaml](../ui/MainWindow.xaml) and [ui/MainWindow.xaml.cs](../ui/MainWindow.xaml.cs): the
bot's only window. A personal control panel beside OBS, not something shown on stream. More panels are
expected to join the status bar and log.

Read from the code. The window was not opened while writing this (the bot's window is only ever opened
by the user, with F5).

## Layout

A `DockPanel` on a near-black background, Consolas 13, default size 1600 × 900. From left to right under
the status bar: the log, the splits column, and a column with the left panel above the OBS sources.

- **Status bar** (docked top), two rows: `Twitch IRC time since last ping: mm:ss  |  Event Sub time since last
  keepalive: mm:ss  |  StreamElements time since last reply: mm:ss`, whose three ages are `Run` elements
  named `IrcAge`, `EventSubAge` and `StreamElementsAge`; and under it the chat name colours, an
  `ItemsControl` named `NameColorList`. See [Chat name colours](#chat-name-colours).
- **Right-hand column** (docked right, outermost, 440 px, `#141414`), a `Grid` of two rows:
  - **Left panel** (top, as tall as its lines up to 360 px, then it scrolls, with a line under it): an
    `ItemsControl` named `LeftPanelList` in a vertical `ScrollViewer`, each row a wrapping `TextBlock` in its own
    colour. See [The left panel](#the-left-panel).
  - **OBS sources** (the rest of the height): an `ItemsControl` named `ObsSourcesList` in a vertical
    `ScrollViewer`, each row a wrapping `TextBlock` with a 10 × 10 colour swatch on its left when it has one and a
    tooltip when it has one. See [OBS sources](#obs-sources).
- **Splits column** (docked right, left of the right-hand column, `#141414`): a bar that doesn't scroll, with the
  title (`SplitsTitle`) and the list dropdown (`ListChoiceBox`) in `SplitsHead`, then the comparison dropdown
  (`ComparisonBox`), over an `ItemsControl` named `SplitsList`, virtualised, with its vertical `ScrollViewer` in its
  template, as wide as its rows (at least 240 px): each row the name, then a run time and a segment time, nested by
  level. See [Splits](#splits).
- **Scroll bars,** every list's, are dark, from an implicit `ScrollBar` style in the window's resources: 10 px wide,
  no arrow buttons, a clear track, the thumb `#3E3E3E` (`#4E4E4E` under the mouse, `#5E5E5E` while dragged).
- **Dropdowns** are dark too, from implicit `ComboBox` and `ComboBoxItem` styles (WPF's own ignores `Background`): a
  `#1E1E1E` box with a `#3E3E3E` edge and a grey arrow, `#2A2A2A` under the mouse and while open, the list in the same
  colours with the chosen entry orange.
- **Log** (fills the rest): a `ListBox` named `LogList`, extended selection, no horizontal scrollbar,
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

## Chat name colours

The status bar's second row shows the chat name colour of each account the bot changes
([twitch.md](twitch.md#chat-name-colours)): the broadcaster, then the bot account when chat goes through one.

```
Lotsofs ■ Raw umber #826644 (encycolorpedia) 00:42  ·  Botsofs NO TOKEN
```

- **How it updates:** the 250 ms tick calls `UpdateNameColors()`, which builds the parts with
  `NameColor.StatusParts(DateTime.UtcNow)` and changes the row only when they differ from the last draw: a
  colour changed, a note changed, or an age reached another second. The parts are kept and updated in place
  (`NameColorView` raises `PropertyChanged`), so a tooltip stays open. `NameColor.Part` is pure (tested), so
  the window only picks brushes.
- **Each account's part:** its login with the first letter capitalised, a 12 × 12 swatch of its last colour,
  the colour as `<name> <#rrggbb> (<source>)`, and the time since it was set, `mm:ss` (`h:mm:ss` from an hour).
  The source is the colour table the broadcaster's colour came from (`encycolorpedia`, `crayola`, …, or
  `random rgb` with no tables loaded, shown as the hex alone), or `twitch` for the bot's named colour
  (`coral #ff7f50 (twitch)`). The age climbs to the interval and starts again at each change. ` · `
  separates the accounts. Before the bot starts, the row is empty.
- **Notes,** after the age, or alone when there's no colour yet. Hovering the part shows the reason:

  | Note | When | Colour |
  |---|---|---|
  | `waiting` | before the account's first change | grey |
  | `NO TOKEN` | its token lacks `user:manage:chat_color`; left alone until a restart | red |
  | `FAILED` | the last validate or change failed; the last colour and its age stay, and the next pass tries again | red |
  | `OFF` | `NameColor.Enabled` is `false`; the colour is left alone | grey |

| Part | Hex |
|---|---|
| Login | `#B9D6F2` (the OBS sources' label blue) |
| Colour | `#EEEEEE` |
| Age | `#CCCCCC` |
| Separator, grey notes | `#888888` |
| Red notes | `#EE4444` (the `Error` red) |
| Swatch edge | `#666666` |

## The left panel

The top of the right-hand column shows what the left-panel rotation ([obs.md](obs.md#the-rotation)) is doing:

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

The right-hand column under the left panel: every OBS source the bot has set this run and
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

## Splits

A column of its own, between the log and the right-hand column: the splits LiveSplit shows in any of its subsplit
levels' split lists, or all of the run's completed splits, its remaining ones or every split, merged into one list in
run order, each line with its segment time, nested by level like the
names. A split that closes sections gets a line for each, under it and one step further out, so the column reads as a
tree whose sections close below their splits. A section needn't have a `{name}`: a split whose segment at a level
takes in deeper splits closes a section there under its own name. GTASA's 100% splits:

```
Splits [Surrounding ▾]             [Current (Personal Best) ▾]
                                         Run  Segment
            ┌─────── SS39: Pillar    5:26:12  · · · · · · 0:01
            ├─────── Buy Wang Cars   5:26:36  · · · · · · 0:23
         ┌─ Buy Wang Cars            5:26:36  · · · 0:45
         │  ┌─────── Ø28: In front   5:28:52  · · · · · · 0:37
         │  ├─────── Monster         5:33:49  · · · · · · 4:57
         ├─ Monster                  5:33:49  · · · 7:12
      ┌─ Monster                     5:33:49  · · 7:58
      ├─ Puncture Wounds             5:48:57  · · 5:00
   ┌─ Desert First Half              5:48:57  · 23:07
   │     ┌─ House: Tierra Robada     6:05:24  · · · 16:26
   │     ├─ Interdiction             6:11:22  · · · 5:58
   │  ┌─ Interdiction                6:11:22  · · 22:24
   ├─ Non-Linear                     6:11:22  · 22:24
   │  ┌> Learning to Fly             6:20:48  · · 7:54
   ├> Desert Second Half             6:20:48  · 9:25
┌> Desert                            6:20:48  54:57
│     ┌─ N.O.E                       6:24:24  · · 3:36
```

- **Where it comes from:** `SplitList.Start()`, from `Program`, reads LiveSplit's split lists every 500 ms
  ([code/LiveSplit/SplitList.cs](../code/LiveSplit/SplitList.cs)) and keeps the merged rows in `SplitList.Current`.
  Output to File writes one window of splits per level under `<LivesplitGenerated>\SplitList\`: `SansSubsplits`
  (no dashes), `SansSubsplits_Level2` (at most one), `_Level3` and up, and `AllSplits`. Each window shows the splits
  before and after one split: the one scrolled to, else the current one, else the first before the start and the
  last after the finish. That's the `Surrounding` list; the list dropdown can pick another (below). A level is read
  while its `TotalSplits_<level>.txt` has a count (the component empties the files of the levels a run doesn't have),
  from `SansSubsplits` up through each `_Level<k>` in turn, so a level of a list that has no lines yet is still
  read. A read that throws logs `Error LSL1` once, until a read succeeds.
- **The files read**, per level of the list shown: `Names_Raw.txt`, `Names.txt`, each `Names_Indent_<depth>.txt`,
  `Highlight_CurrentSplit.txt`, `Highlight_ScrolledToSplit.txt`, `RealTime\Highlight_Gold.txt`, and in
  `RealTime\<comparison>\`, the folder of the comparison shown (below), the four `Highlight_<kind>.txt`,
  `Delta_Run.txt`, `Time_Run_Upcoming.txt`, `Delta_Segment.txt` and `Time_Segment_Upcoming.txt`, less those the list
  doesn't write (below). For the comparisons, whichever list is shown: the folders in `SplitList\AllSplits\RealTime\`,
  each one's `Time_Run_Upcoming.txt`, and `CurrentComparison.txt`.
  Also, per level, `CurrentSplit\<level>\Number.txt` and
  `TotalSplits_<level>.txt`, for where its window sits in the level (below), and `CurrentSplit\AllSplits\Completed.txt`,
  to tell the finish (`-` with splits completed) from before the start. And `CompletedSplits\AllSplits\Names_Raw.txt`
  with its `Names_Indent_<depth>.txt`, the run's completed splits in order, which place the completed part of the run
  exactly (below); without them the column still works, less exactly. Real time only. Every read allows writers (`FileShare.ReadWrite`), so it never makes LiveSplit's
  write fail ([gotchas.md](gotchas.md)).
- **The comparison:** the dropdown in the bar, over the times, picks the comparison the times and their highlights
  are against. It changes only this column: LiveSplit's selection, OBS and the layout colour stay on LiveSplit's own.
  Output to File writes a folder per comparison ticked in its settings, beside `CurrentComparison`, at every level,
  named without the comparison's spaces (`PersonalBest`), and empties the files of one it no longer writes rather than
  deleting the folder. So a comparison is listed while its `AllSplits` `Time_Run_Upcoming.txt` has text
  (`SplitList.Written`, tested).
  - The entries: `Cycle`, `Current (<name>)` (LiveSplit's own selection, from `CurrentComparison.txt`, followed as it
    changes), then each comparison listed, sorted ignoring case. A folder name is shown with a space wherever a
    lowercase letter or digit meets an uppercase one: `Balanced PB`, `Golf HCP` (`SplitList.ComparisonName`, tested).
  - `Cycle` moves to the next comparison every `SplitList.CycleSeconds` (30) and wraps at the end, and reads
    `Cycle (<name shown>)` while chosen. It goes through every comparison listed once, with LiveSplit's own first
    when it has no folder of its own (`SplitList.ComparisonFolder` names it as Output to File does), or alone when none
    is listed (`SplitList.CycleList`). Choosing it keeps the comparison already shown for the interval. To leave one
    out, untick it in Output to File.
  - A comparison that stops being listed puts the choice back on `Current`. The choice isn't kept: each start begins on
    `Current`.
  - The window sets `SplitList.Choice` on a pick, and the next read (within 500 ms) uses it; `SplitList.Resolve`
    (tested) turns the choice into the folder to read. The dropdown's entries are filled in place every tick, so an open
    one stays open, and it's hidden while there are no rows.
- **The list:** the dropdown after the title picks which of Output to File's lists the column reads, each a folder
  under `<LivesplitGenerated>` with the same levels and files, turned on by its own setting there
  (`SplitList.ListChoice`):
  - `Surrounding`: `SplitList\`, each level's window around the scrolled-to or current split (*Split list*). The rest
    of this section describes it.
  - `Completed splits`: `CompletedSplits\`, every completed member of each level (*Completed splits*). Empty, so
    `Splits: none`, before the first split.
  - `Remaining splits`: `RemainingSplits\`, the current member and every one after it, or all of them before the start
    (*Remaining splits*). Empty after the finish.
  - `All splits`: `FullSplitList\`, every member (*Full split list*).
  - Each list leaves out the files that only lines it never has would fill: the completed list the current-split
    highlight and the upcoming times, the remaining list the deltas and every completed highlight. Only the surrounding
    list scrolls, so only it has `Highlight_ScrolledToSplit.txt`. A file the list doesn't write reads as a blank line per
    split (`SplitList.ParseLevel`, tested); one it does write must have a line per split, as below.
  - They merge as the windows do (below): every level of these lists starts at its first member, its first completed
    one or its current one, so each lines up with `AllSplits` from its first line (`Merge`, tested on each list). The
    completed list ends in a `…` at the top level until the finish, for the splits to come; the remaining list starts
    with one for the splits done, at their shallowest depth. Checked on 2026-10-30 against 62 outputs of the real
    component (Output to File's working tree after release 9, playing GTASA 100% Splitspam, Portal 2 Coop Splitspam and
    Hitman ICA Facility headless before the start, every few splits, and finished): each list's rows were exactly its
    splits in run order, each in every level it belongs to, with the current split right and a `…` only where
    described.
  - A chosen list with no folder reads `Splits (no <folder> folder)`. The dropdown shows from the first read on, rows or
    not, so another can always be picked. The choice isn't kept: each start begins on `Surrounding`.
  - A draw of another list shows the current split: the list scrolls to centre the first line holding it, or without
    one, to its end for the completed list and to its top for the others. Later splits don't move it.
- **How the levels merge:** no file says which split of the run a line is, so each line is placed by the
  component's layout. Every level's window holds its *anchor*: that level's first split at or after the split the
  windows are laid out around, marked by the level's scrolled-to highlight, else its current-split highlight, else
  the first line before the start and the last after the finish. Starting from `AllSplits`, each coarser level is
  laid against the splits merged so far that belong to it (depth at most its own): a line `n` lines from its anchor
  is the split `n` places from the first of those at or after the anchor split. A line with no such split goes before
  all of them when it's above, after all of them when below. `Merge` is tested on the component's own scenarios, and
  was checked on 2026-10-29 against 1056 outputs of the real component (release 8's build, playing GTASA Any%,
  Portal 2 Coop Splitspam and Hitman ICA Facility headless with six window sizes, scrolled and not, before the start,
  mid-run and finished): the order, depths and current split were right in every one.
- **Splits not shown:** a grey `…` line marks where the run has splits that no window shows: between two lines, above
  the first, below the last. No file says how far apart two shown splits are, so the column places each in the run as
  closely as the files allow, and puts a `…` wherever it can't rule hidden splits out, except above a split whose own
  name is left out and where the `…` would be all its section holds (below).
  - Where each window sits in its level: its first line is member `Number − 1 − <current line>` when it has the
    current-split highlight (`Number` from that level's `CurrentSplit\<level>\Number.txt`), member 0 before the start,
    and the level's last lines after the finish (`TotalSplits_<level>.txt`). When scrolled away from the current
    split, it's the one place in the level's completed splits where the window's completed lines match them by raw
    name, with its other lines after them (`SplitList.FirstFromCompleted`, tested); else it isn't known. A place before
    the level's first member isn't known either, and goes the same way: `Number.txt` and the window are separate files,
    so a read can pair a number with a window it doesn't belong to, which early in a level puts the window before the
    level's start. Seen once, with `Error LSL1`, before this was handled; what caused it is only read, not reproduced
    (see [gotchas.md](gotchas.md#concurrency)).
  - Each shown split's run index, as a range: exact in `AllSplits`, which is the run itself. A split that is member
    `m` of a level of `total` members has at least `m` splits before it and `total − 1 − m` after it. So its index is at
    least `m` and at most `T − total + m`, where `T` is the run's split count. A completed member's index is exact: the
    `m`-th completed split no deeper than the level. Each split comes after the one above, which tightens the ranges.
  - Between two lines: no `…` when their ranges make them neighbours in the run, or when one window shows every split
    of its level across the gap and that level is `AllSplits`. When both lines' indices are exact and every split
    between them is completed, the `…` is at the shallowest depth among those splits. Otherwise it nests one level
    deeper than the deepest window showing every split of its level across the gap: a window with lines on both sides,
    one starting at its level's first member after the gap, or one ending at its last before it. It goes just above the
    lower line, inside its section.
  - Above the first line: none when it must be the run's first split. Else one level deeper than the deepest window
    starting at its level's first member.
  - Below the last line: none when a window ends at its level's last member, which is the run's last split. Else at the
    top level.
  - So no `…` is ever missing, but some mark gaps that hold nothing, where the counts can't say. Checked on 2026-10-29
    against the same 1056 headless outputs, with each level's `CurrentSplit` and `TotalSplits` files and the completed
    splits, and two from GTASA's `100% Splitspam.lss`: no gap with hidden splits went unmarked, and none among completed
    splits was marked without any. Of the gaps marked, about one in twelve held nothing (one in eight without the
    completed splits). That happens among upcoming splits, where a count only says some hidden splits lie in one of
    several gaps, and when scrolled where no completed line places a window.
- **When the levels don't line up:** a matched line must have the same raw name, every file of a level the same
  number of lines, and the levels must agree on whether a line is scrolled to (and, if none is, on whether one is
  current). A read during LiveSplit's writes can fail that. The column then keeps its last rows. After three
  failed reads in a row (1.5 s) the title reads `Splits (levels don't line up)` in red, until a read succeeds.
- **How it updates:** the 250 ms tick calls `UpdateSplits()`, which builds the lines with
  `SplitList.StatusLines(SplitList.Current)` and redraws only when they differ from the last draw, which is on a
  split, a scroll, a change of times, of comparison or of list. `Merge` and `StatusLines` are pure (tested).
- **Scrolling:** the list is virtualised (a recycling `VirtualizingStackPanel`), so a draw builds only the rows in view:
  all of GTASA's 1317 splits (1627 lines) redraw in about 0.1 s offscreen, against 2 to 3.7 s unvirtualised. So it
  scrolls by whole rows (`CanContentScroll`), and keeps its place across draws. Its `ScrollViewer` is in the
  `ItemsControl`'s template, found on `Loaded`.
- **Width:** the name column is as wide as the longest line's tree guides and name seen this run, but at most
  `SplitList.NameWidth` characters (40), guides included, or the widest guides seen and one character more when those
  are wider. A name that doesn't fit after its guides is cut short before a `…` (`SplitList.Shorten`, tested), and
  hovering its line shows its raw name whole. The run and segment times are as wide as the longest time or heading
  seen, plus two characters. Neither column narrows again until a restart, or a smaller `NameWidth` on `!reloaddata`,
  which redraws at once. GTASA's 100% splits reach 69 characters in the full list (21 of guides before
  `SS25: To the left of first arch on parallel wall`), 48 in its windows. Run times are right-aligned. Segment times are left-aligned, two characters after the
  run times, and start two characters further right per level, right after their dots, so the deepest level's start
  twice the number of levels less one characters right of the top level's (12 with GTASA's seven). Every row is as
  wide as the deepest level's seen this run: a segment cell is wider by the dots of the levels it's short of, so the
  virtualised list keeps its width as rows scroll into view. The column is as wide as all that.
- **Headings,** a grey line atop the list while there are rows: `Run` and `Segment`, the latter over the top
  level's times. Hovering it lists the levels' folders.
- **A split's lines:** a split has a name at each level it belongs to, from its depth to `AllSplits`; a split's depth
  is its number of leading dashes, as the component counts them (the `Names_Indent_<depth>.txt` it's in, or the
  dashes for a blank name). The names are parsed from `Names_Raw.txt` the way the component's
  `SplitLevels.GenerateNames` does: a `{section}` prefix is the name at one level. Each run of levels with the same
  name *and the same segment* is one line, finest first: the split's own name (the one `AllSplits` shows when it's in
  that window), then each section it closes, innermost first. So `{Desert}{Desert Second Half}Learning to Fly` is three
  lines: `Learning to Fly` (L3 and finer), `Desert Second Half` (L2) and `Desert` (Sans). And `---Buy Wang Cars`, right
  after `------SS39: Pillar`, is two: `Buy Wang Cars` at `All`, whose segment starts at SS39, and `Buy Wang Cars` at
  L4 to L6, whose segment starts at `Yay Ka-Boom-Boom` and takes in SS35 to SS39. A split without sections is one line.
  A section line whose level's window doesn't reach the split is still listed, without a time, unless it's left out
  (below).
- **Lines left out** (`SplitList.Alone` and `StatusLines`, tested): a split's own name is dropped when no window at
  its levels shows the split, as it would have no segment time. When it would also be the only split inside the
  section line under it, because the row above ends at that section's level or further out (or there is none), the
  same goes for that section, outward, while each is shown by no window at its levels, and the `…` line above the row
  goes too, even the one above the first line. `…` lines don't count as splits. The split's outermost line always
  stays. So `---Enter Mothership`, past the finer windows, which end at `Plant 5`, is one line, its section at L4 with
  its `1:21`, and the plants and the `…` for `Plant 6` onward sit inside it.
  And any `…` line goes when it's all its section holds (`SplitList.EmptySections`, tested): the section, the next
  line below at a lower level, has no split's line inside it, only `…` lines. So a split shown at its level whose own
  splits aren't (`Tanker Commander`, `Trucking 1`) is one line, with no `…` under it. A `…` among shown splits stays,
  like the one above `SCYR Cp 13` or one between two sibling sections, and so does one with no section below it. So past the windows, `{Desert}{Desert Second Half}Learning to Fly` shows as `Desert Second Half` and `Desert`
  only, and `{RTLS}{The End}{End of the Line Part 3}Credits` as `The End` and `RTLS` when `The End` has a time.
- **Where a segment grows:** a split's segment at a level is longer than at the next finer level when the split before
  it at the finer level is deeper than the level. That split is the line before it in the finer level's window, or
  in any finer window whose line before it is shallow enough to be in the finer level too. When no window shows it (the
  split is first in those windows, or past them): the segment grows when the merged list shows a split one level
  deeper between it and the line before it at the level (`Monster` past `AllSplits`' window, with `Ø28` shown before
  it); otherwise it grows when its two segment times differ. A split outside the finer windows whose deeper splits
  are hidden too stays one line there.
- **What a line shows:**
  - Tree guides in `#555555`, three characters for each level from the top down to the line's, like a file tree
    hung upside down (`SplitList.TreeGuides`, tested). A line's level is the first level of its name: a plain split's depth, the own
    name of a split that closes sections beside the splits in its innermost section (`Learning to Fly` beside its
    sibling `N.O.E`, `Buy Wang Cars` beside SS39), and each section further out. A line's section is the next line
    below at a lower level. The section's vertical runs up from it, at its own level, to its first line: `┌─` there,
    `├─` at its other lines, `│` past the deeper lines between, and a line's branch runs `─` to just before its name
    when it's more than a level deeper. A line with no section below hangs from one a level up below the last line, so
    the top-level lines hang from a root below the column, with `│` past the deeper lines between them. A section that
    isn't shown (its windows don't reach it) makes the line hang from the next shown line at a lower level instead.
  - The root is a dot at the left edge, on a row of its own below the last line, with the top level's `│` running into
    it (`SplitList.RootGuides`, tested). Any vertical further right that runs past the last line ends on that row too,
    joined to the dot: `└──┘`, with `┴` for each between. The dot is drawn, not a character, so the line meets it.
  - A marked line has `> ` in the line's colour in the two characters right before its name, in place of the end of its
    branch (`├> Desert Second Half`, `┌> Desert`).
  - The path to the current split is orange (`SplitList.PathGuides`, tested): from each line holding the current split,
    its branch, then its section's vertical where it passes the lines between, down to the section, and from a
    top-level one down to the root, whose dot is orange too. So it runs from the current split through every marked
    line, the way the tree goes. It's drawn as orange strokes over the grey guides (`┌─` for a branch, `│` past a line), so only the strokes
    on it are orange: a sibling it passes keeps its stub grey, and a marked line's `├` keeps grey the half above its
    branch, which leads up to the lines before it.
  - The run time on every line of the split: in the line's colour on its outermost line, grey on the lines inside it.
  - A split's outermost line is white, and its lines inside that are grey: `San Fierro` white, `San Fierro Finale` and
    `Yay Ka-Boom-Boom` grey. A split that closes nothing is one line, so white.
  - At each level, the line holding the current split there is marked, from that level's current-split highlight:
    orange when it's its split's outermost line, dark orange otherwise. So on a subsplit the sections holding it are
    marked out to the top level. On `SS7` in GTASA's 100%: `SS7`, `Buy Wang Cars` (L4 to L6), `Monster` (L3),
    `Desert First Half` (L2) and `Desert` (Sans), all orange. On `Yay Ka-Boom-Boom`, which closes its sections itself:
    `Yay Ka-Boom-Boom` and `San Fierro Finale` dark orange, `San Fierro` orange. The split scrolled to has the marker on
    its outermost line, which stays white.
  - A time not yet completed is in its line's colour (white, grey, orange or dark orange); a completed one in its
    highlight's colour.
  - Under `Run`: a completed split's run delta against the comparison shown (`Delta_Run.txt`); an upcoming split's
    comparison run time (`Time_Run_Upcoming.txt`). Both as LiveSplit wrote them (`-` for none). It and its highlight
    come from the finest level that shows the split, so a section seen only in a coarse level has the section's
    highlight.
  - Under `Segment`, right after a faint `· ` per level of the line (`#444444`), so it starts two characters right per
    level, like its name: the line's segment, which is the same
    at all its levels, from the finest of their windows that holds the split. A completed split's segment delta
    (`Delta_Segment.txt`), with that level's highlight; an upcoming split's comparison segment time
    (`Time_Segment_Upcoming.txt`). So a section's line has the section's segment, from the level's split before it.
    Blank where none of the line's levels' windows reaches the split.
  - A `…` line has `…` under `Run` and `Segment` too, at its level.
  - The rows under the headings alternate between the window's black and a lighter black (`#1C1C1C`), starting with
    black. The root's row is always black.
  - Hovering any of a split's lines shows its raw name and the levels whose windows hold it.
- **Titles,** in the bar above the list, with the list dropdown after them, both at least as wide as the name column
  so the comparison dropdown after them stands over the times: `Splits` over the rows; `Splits: none` before the
  first read or when the list's `AllSplits` is empty; `Splits (LivesplitGenerated not set)` or
  `Splits (no <list folder> folder)`, with no rows.

| Line | Hex |
|---|---|
| Title | `#CCCCCC` |
| A split's outermost line, the split scrolled to, a completed time without a highlight | `#EEEEEE` |
| A line holding the current split, its split's outermost | `#EE8844` (the `SceneChanges` orange) |
| A line holding the current split, inside its split's outermost | `#A85F30` |
| A split's lines inside its outermost, headings | `#888888` |
| Tree guides, the root's dot | `#555555` |
| Tree guides on the path to the current split, the root's dot when the path reaches it | `#EE8844` |
| `…` for splits not shown | `#888888` |
| A run time repeated on a split's inner line | `#888888` |
| The dots before a segment time | `#444444` |
| Every other row's background | `#1C1C1C` |
| `levels don't line up` title | `#EE4444` (the `Error` red) |

A time not yet completed takes its line's colour. Completed times take LiveSplit's default colours (written from
memory of LiveSplit's defaults, not read from its source):

| Highlight | Hex |
|---|---|
| Gold | `#D8AF1F` |
| Ahead, gained time | `#00CC36` |
| Ahead, lost time | `#52CC73` |
| Behind, gained time | `#CC5C52` |
| Behind, lost time | `#CC1200` |

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
