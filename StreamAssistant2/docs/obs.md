# OBS

How the bot drives OBS Studio: the websocket connection, the thin wrapper, and the layout-recolouring
sequence that is its main use. Which colour gets chosen is [color.md](color.md); this is what happens
once one has been.

Read from the code. The connection and animation were not run against a live OBS.

## Connection

[ObsConnection.cs](../code/Obs/ObsConnection.cs) owns the one `OBSWebsocket` (library `obs-websocket-dotnet`
5.0.1, obs-websocket protocol v5), exposed as `ObsConnection.ObsSocket`.

- The address is hardcoded: `ws://127.0.0.1:4455`. The password is `Obs.SocketPassword` from
  `secrets.json`.
- `Connect()` is idempotent. It hooks `Connected` and `Disconnected` (which log `Connected to OBS` and
  `Lost connection to OBS: <reason>`) and `StreamStateChanged`, and starts `Loop`. `Connected` also restarts
  the left-panel rotation ([Left panel](#the-rotation)), sets the dynamic sidebar again
  ([Per-game setup](#per-game-setup)) and rewrites the money goal text ([Money goal](#money-goal)).
- `OnStreamStateChanged` logs `OBS started streaming` and `OBS stopped streaming` (the in-between
  states are ignored). On start it resets the chatter list (`ChatterList.Reset`, see
  [twitch.md](twitch.md#chatterlist)), starts the go-live game setup
  ([events.md](events.md#category-change-and-going-live)) and wakes the uptime poll (`TwitchUptime.PollNow`,
  [infrastructure.md](infrastructure.md#uptime)). It runs on obs-websocket's receive thread and catches its own
  exceptions (`Error OSS1`). Connecting to an OBS that is already streaming raises no event, so no reset.
  Unit-tested with the event args obs-websocket builds; not yet seen at a real stream start.
- `Loop` checks once a second while connected. When not connected it fires `ConnectAsync` (which
  returns at once; the outcome arrives through the events) and sleeps 3 s. So OBS can be started
  before or after the bot, and a restart of OBS is picked up within a few seconds.
- `Disconnect()` cancels the loop and closes the socket if it is open. The cancelled `Task.Delay` ends
  the loop with an exception in a discarded task, which is harmless at shutdown.
- `IsConnected()` is the guard every OBS action starts with. If not connected it logs `Trying to do an
  OBS action but not connected to OBS` on `Important` and returns false.
- **Socket open is not the same as ready.** The library's `ObsSocket.IsConnected` is true as soon as the
  WebSocket is running, which is before OBS has finished the Hello/Identify handshake and accepted the
  bot; requests sent in that window fail. `Connected` fires only once OBS has identified the bot, so
  `ObsConnection.IsReady` is a flag set in the `Connected` handler and cleared in `Disconnected`. The
  game setup (`WhenConnected`, `GameCapture.Set`) and the left-panel rotation use `IsReady`; `Loop`, `Disconnect()` and the
  `IsConnected()` guard still use the socket state.

OBS being closed is a normal state, not an error: actions are skipped, not queued, and nothing is
retried afterwards.

## The wrapper

[Obs.cs](../code/Obs/Obs.cs) holds these calls, each of which does nothing but log when OBS is unreachable:

| Method | OBS request | Used by |
|---|---|---|
| `SetSourceEnabled(scene, source, enabled)` | `GetSceneItemId(scene, source, 0)` then `SetSceneItemEnabled` | `LayoutColoring`, the train reward, `LeftPanel` |
| `GetSceneItemNames(scene)` | `GetSceneItemList(scene)`, the top-level items' `SourceName`s (empty when not connected) | `LeftPanel` |
| `SetInputSetting(input, key, value)` | `SetInputSettings(input, {key: value}, overlay: true)` | `SetImageSource`, `GameCapture`, `Clock`, `TwitchUptime`, `LeftPanel` |
| `SetImageSource(source, file)` | `SetInputSetting(source, "file", file)` | the train reward, `GameBackground`, `GameBorder` |
| `SetInputVolumeDb(input, dB)` | `SetInputVolume(input, dB, inputVolumeDb: true)` | `GameCapture` |
| `SetFilterProperty(source, filter, property, value)` | `SetSourceFilterSettings(source, filter, {property: value}, overlay: true)` | `LayoutColoring` |

None of them catch. A missing scene, source or filter name makes the library throw. That is handled
for colour changes (the worker logs `Error Obs29`) but not for the train reward, where it ends the
reward's task silently and can leave the train image showing. All names are hardcoded strings,
listed in [reference.md](reference.md#obs-names); renaming something in OBS breaks the matching call
without any compile-time hint.

## Layout recolouring

[LayoutColoring.cs](../code/Obs/LayoutColoring.cs). The channel's layout has three colourable parts, recoloured
through OBS's *Color Correction* filter (called `Color Correction` on every source):

| Part | Sources | Filter property | Colour |
|---|---|---|---|
| inner borders | `Border: Colorable Inner`, `Border: Colorable Inner Attachment` | `color_multiply` | inner |
| outer borders | `Border: Colorable Outer`, `Border: Colorable Outer Attachment` | `color_add` | outer |
| text and body | `!Scene: All Colorable` | `color_multiply` | text |

The attachments are the frame's piece on the left. `!Scene: Layout 5x4` shifts the rest of the layout right
to make room for it.

Each source has a twin whose name ends in ` (Transitionary)`, and the twins sit in a duplicate group,
`!Layout: Colorables (Transitionary)`, beside the real group `!Layout: Colorables`. Both groups are items of
every layout scene, `!Scene: Layout <aspect>` (`16x9` and `5x4`), and the animation shows and hides them in
each. It finds those scenes by name in OBS's scene list (`LayoutScenes`, tested). The old single
`!Scene: Layout` is gone. While its name was still in the code, every colour change failed with
`Error Obs29` (`No source was found by the name of !Scene: Layout`, seen 2026-10-14).

### Requests are queued

Recolouring takes about nine seconds and must not overlap itself, so requests never run directly:

- `TryChangeToSingle`, `TryChangeToTriple` and `ChangeToRandom` resolve a colour, call `ChangeColor`,
  and report the outcome in the log and, on failure, in chat.
- `ChangeColor` enqueues a `ColorRequest(Inner, Outer, Text)` hex triple on an unbounded
  `Channel<ColorRequest>` and **immediately** posts
  `Changing to color <name> [<source>]: <hex> <hex> <hex>` to chat. The announcement therefore
  precedes the visible change by the length of the queue ahead of it.
- One worker, started by `StartWorker()` at boot, reads the channel with `ReadAllAsync` and runs
  `ChangeColorAsync` for each request.
  - A source that can't be coloured logs `Couldn't colour <source>: <message>`. A group that can't be shown
    or hidden in a layout scene logs `Couldn't show|hide <group> in <scene>: <message>`. Either way the rest
    of the change still runs.
  - With no layout scenes at all (and OBS connected), it logs
    `No !Scene: Layout <aspect> scenes, layout colour changes without the fade`.
  - Anything else thrown is caught per request: `Error Obs29`, then the exception and its message.

Never call `ChangeColorAsync` directly. Starting the worker twice is refused with a log line.

### The animation

`ChangeColorAsync` converts the three hex colours with `ColorUtil.ToOBS` (the opaque ABGR value OBS
colour filters take) and then:

| t | Step |
|---|---|
| 0 s | Set the **Transitionary** filters to the new colours (`Filters`, tested). Enable `Colorables (Transitionary)` in each layout scene |
| +5.5 s | Disable the real `Colorables` group |
| +2.5 s | Set the **real** filters to the new colours (the group is hidden, so nothing shows) |
| +1.0 s | Enable the real `Colorables` group; disable the transitionary one |

The cross-fade itself is done by OBS's show and hide transitions on those scene items, configured in
OBS and invisible from this code. The visible effect is the old colour fading into the new one while
the real group is swapped underneath.

Total: 9 s per request. Ten colour redemptions in a row take 90 s to play out.

### With OBS closed

Every OBS call in a run logs its own `Trying to do an OBS action…` line and returns, but the
`Task.Delay`s still run, so a colour change with OBS closed still occupies the queue for 9 s
and still announces in chat as though it happened. Read, not reproduced.

## Train

The train reward ([events.md](events.md#channel-point-rewards)) shows a PNG over the layout: it sets the
`Image: Train` input's file, enables it in `!Scene: Basics Colored`, waits 62 s, disables it and resets
the file to `Empty.png`. It bypasses `LayoutColoring`'s queue entirely. The entry point is
`ChannelPoints.TryStartTrain`, shared by the reward and the `!train` admin command; it holds one flag
(`_trainOnTracks`, claimed with `Interlocked.CompareExchange`) for the whole 62 s, and a train started while
it is set is dropped with a chat line instead. A train that starts logs `Choo choo!` on the `SceneChanges`
channel. `RunTrainAsync` starts with `await Task.Yield()`, so its
blocking OBS calls run on the thread pool, not on the IRC or EventSub read loop that started it.

## Money goal

[MoneyGoal.cs](../code/Money/MoneyGoal.cs) keeps a running total of tips toward a goal, in EUR, and shows
it in the text source `Text: Money Goal` as `<total> / <goal> EUR` (`16.54 / 500 EUR`, amounts formatted
like the tip lines). You create that source in OBS, wherever it should show; the bot only sets its
`text`.

- **What counts:** StreamElements tips only, in full, once each ([events.md](events.md#tips)). A tip in
  another currency is counted as its number, logged on `Important` with `counted as … EUR`. Cheers and
  subs don't count: Twitch doesn't say what a sub cost (its payloads carry only the tier), and cheers
  are left out by choice. `!test tip` isn't counted, and a tip that arrives while the bot isn't
  running is never seen.
- **Where it's kept:** the `money_goal` row in the database, in whole cents
  ([infrastructure.md](infrastructure.md#database)), so it survives restarts. A first start begins at
  `0 / 999 EUR`.
- **Changing it:** `!moneygoal <amount>` sets the goal and `!resetmoney` sets the total back to 0
  ([twitch.md](twitch.md#admin-commands)).
- **When the text is written:** after each tip, goal change and reset, and on every OBS connect, read
  back from the database. With OBS closed nothing is written and nothing is logged; the next connect
  catches up. A missing source logs `Couldn't set Text: Money Goal: …`.
- **Log lines:** `Money goal: +4.20 EUR from <user>, now 16.54 / 500 EUR` on `Notification`. A failed
  credit logs `Error MON1`, a failed `!moneygoal` or `!resetmoney` `Error MON2`, and a failed write on
  OBS connect `Error MON3`.

## Per-game setup

`Games.Apply`, on bot start, a category change, or when OBS starts streaming
([events.md](events.md#category-change-and-going-live)), runs on the thread pool, not on a read loop,
and sets up the game's background, borders, layout and games scenes, sidebar, colour, left panel
([Left panel](#left-panel)) and capture sources from its `games.json` entry. Each step
runs through `Games.RunStep`, so one that throws logs `Error GMS2: game setup <step> failed: …` and the
later steps still run (tested). The background, border, scene and capture steps also catch OBS
rejections themselves, with a clearer `Couldn't set …: …` warning.

- **Background**, [GameBackground.cs](../code/Games/GameBackground.cs): `Set` points the `Image: Background`
  input at `<Directories.Backgrounds>\<game name>.png` with `Obs.SetImageSource` and logs
  `Image: Background → <file>`. A missing file falls back to `Template.png` in the same folder, logging
  `No background for <game name>, using Template.png`; with that missing too, the image stays as it is
  (`PathFor`, tested; the fallback itself isn't). If OBS
  rejects the request (the source is missing or renamed), it logs `Couldn't set Image: Background: <message>`
  and the colour and capture steps still run.
- **Borders**, [GameBorder.cs](../code/Games/GameBorder.cs): the layout's borders frame the game, so their
  shape follows the game's aspect ratio, `Aspect` in `games.json`. 16:9 is the default, for a game without
  one and for a category not in `games.json`.
  - The aspect's own pair is `<Directories.Layout>\Borders_Inner_<aspect>.png` and
    `Borders_Outer_<aspect>.png`. Only `16x9` has a pair.
  - An aspect without both of its own images gets the plain `Borders_Inner.png` and `Borders_Outer.png`.
    So does SWAT4's `5x4`, whose layout scene adds the attachments on the left
    ([Layout recolouring](#layout-recolouring)).
  - The attachments' images aren't changed per game.
  - `Set` points `Border: Colorable Inner` and `Border: Colorable Outer` at them, and their
    `(Transitionary)` twins too, so the colour animation fades between frames of the same shape. It logs
    `Borders → <inner file>, <outer file>`.
  - With no plain pair either it logs `No border images for <name>, borders left as is`, and with no
    `Layout` in `paths.json`, `Layout not set in paths.json, borders left as is`.
  - An OBS rejection logs `Couldn't set the borders: <message>`, and the later steps still run.
  - It runs before the colour step, which recolours these same four sources (`Files`, tested).
- **Scenes**, [GameScene.cs](../code/Games/GameScene.cs): the layout and the game capture each come in a scene
  per aspect. The layouts are `!Scene: Layout 16x9` and `!Scene: Layout 5x4`. The captures are
  `!Scene: Games 16x9` and `!Scene: Games 5x4`, which hold the same capture sources, framed for that shape.
  `Basic Stream Layout` holds them as top-level items.
  - `Set` lists `Basic Stream Layout`'s items. For each kind, it takes those named `!Scene: Layout <aspect>`
    or `!Scene: Games <aspect>` (`Named`).
  - It shows the one for the game's `Aspect`, `16x9` without one (any case), hides the others of that kind
    there, and logs `Basic Stream Layout → <scene>`.
  - A kind with no scene for the aspect logs `No <kind prefix><aspect> in Basic Stream Layout, left as is`.
  - With OBS not ready it logs `OBS not connected, layout and games scenes not set`.
  - `Choose` is tested.
  - Only `Basic Stream Layout` is changed. A few other scenes hold their own `!Scene: Games 16x9`, and those
    stay as they are.
- **Sidebar**, [GameSidebar.cs](../code/Games/GameSidebar.cs): `!Scene: Layout 5x4` holds `!Scene: Sidebar 5x4`,
  the strip left of the 5:4 game. In it, `Sidebar: Dynamic 1` and `Sidebar: Dynamic 2` follow LiveSplit's split
  index, each with its own series of images in `<Directories.Sidebar>\Dynamic\`:

  | Source | Images |
  |---|---|
  | `Sidebar: Dynamic 1` | `<game name>_<n>.png` |
  | `Sidebar: Dynamic 2` | `<game name>_2_<n>.png` |

  - n is the split index + 1. So `_00` shows before a run (index −1), `_01` for the first split, and so on.
    SWAT4 has `_00` to `_20` in both series, one per mission. A name whose part after the series isn't all
    digits (`SWAT4_Border.png`) belongs to neither.
  - A missing number takes the highest one below it. So after a run ends (the index is the split count),
    the last mission's images stay up. With nothing below, it takes the lowest.
  - `SetGame` makes the game the one followed and shows its images for the current index. The split watcher
    calls `OnIndexChanged` on every change of the index: splits, skips, undos, resets and starts. So does an
    OBS connect, off the connecting thread (`Error SDB1` if that throws), since OBS may have missed changes
    while closed.
  - Each change logs `<source> → <file>` (`SceneChangesUnimportant`). An unchanged image isn't set again.
  - A game without images in a series, or OBS not ready, leaves that source as it is, without a log line.
    An OBS rejection logs `Couldn't set <source>: <message>`, and the other source is still set.
  - `Choose` is tested.
  - `Sidebar: Fixed`, the static part of the strip, isn't changed per game.
- **Colour**, [GameColor.cs](../code/Games/GameColor.cs): `Set` requests `gameschemes <game name>` through
  `LayoutColoring.TryChangeToSingle`, the reward's path: the game's category in the `gameschemes` scheme
  set, at its `Default` scheme. It is queued like any colour change (the animation runs later on the
  colour worker), replaces whatever colour is showing, and posts the usual `Changing to color …` line.
  A game with no category there (RoN) logs `No colour scheme for <name>, colour left as is` instead of
  the chat failure line (`SchemeInput`, tested; the real `gameschemes KTANE` is checked in the real-data
  smoke test).
  - **By split sign.** A category with `SignSchemes` ([color.md](color.md#data-files)) follows LiveSplit's
    previous-split sign. SWAT4 does: `BlueTeam` when ahead, `RedTeam` when behind, `Element` for a gold,
    `Menu` (its `Default`) otherwise.
  - The sign is the one over all splits in real time, `PreviousSplit\AllSplits\RealTime\Sign.txt`. Its trend
    comes from `SplitColors.Trend` (tested):
    - any gold sign (`GoldAhead` too) is Gold;
    - otherwise the run's side: `…Ahead` and `PB…` are Ahead, `…Behind` and `NoPB…` are Behind;
    - `NotApplicable`, `Gained`, `Lost` and the like have no trend.
  - `Set` takes the scheme for the sign as it is then, so a SWAT4 setup mid-run goes straight to that.
  - After every change of the split index, once the sign has been rewritten (or 0.5 s have passed, for a
    sign that didn't change), the split watcher calls `GameColor.OnSign`. That covers splits, skips, undos, resets and starting a run, so a reset goes back
    to `Menu`.
  - When the scheme differs from the one it last went to, `OnSign` queues it on the colour worker without
    a chat line and logs `Split sign <sign>: layout colour <game> <scheme>`. Choosing it is
    `ChooseScheme`, tested.
  - A manual colour (a reward or `!changecolor`) stays until the trend next changes.
- **Capture sources**, [GameCapture.cs](../code/Games/GameCapture.cs): three kinds, three numbered slots
  each, numbered from 0:

  | Sources | `games.json` list |
  |---|---|
  | `Game: Game Capture 0`–`2` | `GameCaptureExecutable` |
  | `Game: Window Capture 0`–`2` | `WindowCaptureExecutable` |
  | `Audio 5: App Capture 0`–`2` | `AudioCaptureExecutable` |

  Slot N gets the list's entry N; slots without one get `none`, which matches nothing, and so does an
  explicit `"none"` entry. Entries beyond three are logged and dropped. Each source's `window` setting is
  set to `PLACEHOLDER-TITLE:PLACEHOLDER-CLASS:<exe>` (OBS's `title:class:executable` form; the title and
  class are deliberately fake, so each source must match by executable, which is set in OBS, as is a
  game capture source's capture mode). A used slot is then shown (eye on) in every games scene in
  `Basic Stream Layout` (both `!Scene: Games 16x9` and `5x4`, so a later aspect change finds them right), an
  unused one hidden there. With no games scenes there it logs
  `No games scenes in Basic Stream Layout, capture slots not shown or hidden` and still points the slots
  and sets their volumes. A shown slot also gets its volume, in dB: the slot's own, else `games.json`'s
  `default` volume, clamped to OBS's −100 to 26; with neither, the volume is left alone. Hidden slots keep
  theirs. Each shown slot logs `<source> → <exe>, shown, <dB> dB` (no dB part if none was set); the
  hidden ones share one line,
  slot numbers grouped by kind, e.g. `→ none, hidden: Game: Game Capture 1, 2; Game: Window Capture 0, 1, 2;
  Audio 5: App Capture 1, 2` (`HiddenLine`, tested). A source
  that can't be set (missing or renamed in OBS, or not in that scene) logs `Couldn't set …` and the
  rest still go. With OBS disconnected it logs `OBS not connected, capture sources not set` once.
  For a category id not in `games.json`, `Apply` skips this step and logs
  `<id> not in games.json, capture sources left as is`; the `0` fallback only supplies the name for the
  background and colour. Choosing `0` (`None`) explicitly does run it, hiding every slot.
  Audio capture is a fallback: most game audio now comes from the video capture source itself.
  (`Assignments`, `IsUsed`, `VolumeFor`, `ShownLine` and `WindowValue`, tested.)

  A capture list entry is either a plain `"exe"` string or an object with its own volume:

  ```json
  "default": { "Volume": -4 },
  "461492": {
    "Name": "KTANE",
    "GameCaptureExecutable": [ { "Exe": "ktane.exe", "Volume": -6 } ],
    "AudioCaptureExecutable": [ "ktane.exe" ]
  }
  ```

  The `default` entry isn't a game: it only holds the fallback volume, and `!changegame default` doesn't
  resolve to it. Loading logs `Loaded <n> games, default volume <dB> dB` (or that there is none).

`!changecapture <game>` runs the capture-sources step alone, for a game in `games.json`. It logs
`Capture setup: <name> (<id>)` and runs off the IRC thread (`Error GMS_cap` if it throws outside
`RunStep`). The stored category, background, borders, colour and left panel don't change, and nothing is
posted to chat. `!changepanel` is its left-panel counterpart ([The rotation](#the-rotation)).

On bot start, `Games.OnBootAsync` hands its apply to `ObsConnection.WhenConnected`, which runs it at
once if OBS is ready (`IsReady`, see [Connection](#connection)), otherwise on OBS's next `Connected` event,
however late, and returns which it did. So the capture sources and background aren't skipped because OBS
connects after the bot, and aren't sent mid-handshake. It applies once per boot: later reconnects (an OBS
restart mid-stream) don't re-apply, since OBS keeps the settings. The left panel is the exception: its
rotation restarts on every connect ([The rotation](#the-rotation)). `WhenConnected` can fire twice if OBS
connects during the call, so the boot apply guards itself with a flag.

## Left panel

`!Scene: Left Panel` is the panel beside the game: split lists, the current split, the key count, the
sellout goals. Each of these is a group: `_LP: <name>` in `!Scene: Left Panel`, or `_LPC: <name>` in
`!Scene: Left Panel Colored` for the coloured ones. A game's panels take turns there.
The bot rotates them, per game, from a file. For games that have one, a LiveSplit split interrupts the
rotation with a previous-split panel ([Splits from LiveSplit](#splits-from-livesplit)).

### The panel file

[LeftPanel.cs](../code/LeftPanel/LeftPanel.cs) loads `<BotInput>\leftpanel.json` at startup, after `games.json`.
The file is keyed by `games.json` `Name` (any case), plus a `default` entry for a game without its own:

```json
"GTA3": {
	"Always": ["Splits Basics"],
	"Panels": [
		{
			"Name": "Splits: Run Delta",
			"Show": ["Splits List Labels", "Splits List Values"],
			"Text": {
				"Livesplit: SL - Header Right": "Run Delta"
			},
			"File": {
				"Livesplit: SL - Current": "D:\\Repositories\\Stream-Resources\\Text\\Livesplit Generated\\SplitList\\AllSplits\\RealTime\\Delta_Run.txt"
			},
			"Seconds": 50
		},
		{ "Show": ["Splits Key Count"], "Seconds": 50 },
		{ "Show": [], "Seconds": 1 }
	],
	"Previous": [
		{ "Show": ["Splits Previous", "Splits Previous Values"], "Seconds": 30 }
	]
},
"GTA4": "GTA3"
```

- `Always` lists the groups shown for the whole game.
- `Panels` take turns in order. Each panel lists the groups it shows (`Show`), the text and file to set on
  text sources before showing them (`Text`, `File`), and how long it stays up (`Seconds`).
- Group names leave out the `_LP: ` / `_LPC: ` prefix. A name written with either prefix, or with spaces
  around it, is cleaned up. The file doesn't say which of the two scenes (below) a group is in.
- **Copies.** OBS groups named `<name> <number>` are interchangeable copies of `<name>`: `Splits Current 1`–`3`,
  `Splits List Labels 1`–`3`, `Splits List Values 1`–`3`. The bot picks the copy ([Copies](#copies)).
  - In `Show`, the family name (`Splits Current`) and any numbered name (`Splits List Values 2`) both mean
    the family. The file uses the family name.
  - In `Text` and `File`, in a panel that shows a family, every source is a copy source, written without a
    number (`Livesplit: SL - Current`). The bot adds the number of the copy it's about to show. A number
    written anyway is replaced.
  - So a panel with a family can't set a source shared by all copies (`Livesplit: Current Labels`), or one
    in another group it shows. Set those in OBS, or in a panel without a family.
- `Keep` is optional: more groups to show, as they are. A family listed there stays on the copy that's up,
  rather than moving to a fresh one ([Copies](#copies)). A plain group is shown just as if it were in `Show`;
  plain groups never move anyway, since a step only shows and hides what differs from the step before. So a
  group belongs in one of the two lists, not both. Names are cleaned up like `Show`'s, and missing ones are
  reported and flagged the same way.
- A panel without a positive `Seconds` is skipped. A panel with an empty `Show` is a pause: only the
  `Always` groups stay up.
- `Name` is optional and only for the dashboard: what the left-panel column calls the panel instead of listing
  its groups and texts ([dashboard.md](dashboard.md#the-left-panel)). Blank counts as none. On 2026-10-12 the
  panels showing more than one group were named after their on-stream headers, `<list>: <column>` (for example
  `Per map: Run Delta`); `Run_NoSubsplits`'s after their header or the group that changes (`Run Delta`,
  `Sellout Goals`, `List On`). Single-group panels have no name.
- `Previous` is optional: the panels to play once after each LiveSplit split, same shape as `Panels`
  ([Splits from LiveSplit](#splits-from-livesplit)). Only SWAT4 has it.
- `Sign` is optional: the path of one of LiveSplit's `PreviousSplit\<level>\RealTime\Sign.txt` files, which
  colours the previous split's differences during the `Previous` panels ([Split colours](#split-colours)).
  It is trimmed, and blank counts as none. Only SWAT4 has it, for `AllSplits`.
- An entry can be another entry's name instead of its own panels: `"GTA4": "GTA3"` gives GTA4 GTA3's
  panels (any case, and a name can lead on to another name). One that leads nowhere, or round in a loop, is
  skipped and logged as `leftpanel.json: skipped <name>, "<target>" leads to no panels`. A value that is
  neither panels nor a name logs `leftpanel.json: skipped <name>, not panels or a game name`.
- Missing lists and maps count as empty, and `null` entries and values are skipped.

`Lookup` returns a game's entry, else `default`, else nothing. A category not in `games.json` becomes the
`None` game, which has no entry, so it gets `default`.

Loading logs `Loaded left panels for <n> games and a default` (or `, no default`). A missing file logs
`leftpanel.json not found, left panel off`. A file that can't be read or parsed logs `Error LPN1` and
leaves no panels. `Parse` and `Lookup` are tested, and a real-data test checks that every key in the real
file is a `games.json` name or `default`, that every name entry leads to panels, and that no panel is
skipped.

### The rotation

`Games.Apply` calls `LeftPanel.Start(<game name>)` as one of its steps ([Per-game setup](#per-game-setup)),
so a rotation starts on boot (once OBS is ready), on every category change and when OBS starts streaming.
It also restarts whenever OBS connects (below).
It runs all the time, not only while streaming.

`Start` does the following:

- It does nothing when no panels are loaded.
- It cancels the running rotation, through a `CancellationTokenSource` swapped with `Interlocked.Exchange`,
  as `Ads` does.
- If `Lookup` finds no entry and there is no `default`, it logs `No left panels for <game>, left as is`.
- Otherwise it looks up the game's split colours ([Split colours](#split-colours)) and logs
  `Left panel: <game>, <n> panels` on `SceneChanges`. After the game it names where the panels come from
  when that isn't the game's own entry: `(default)`, or the entry the game's entry names
  (`SWAT4 (Run_NoSubsplits)`; `Which`, tested). It adds
  `, <n> on a split` when the entry has `Previous`, then `, split colours from <category>` or
  `, no split colours`. Then it starts `RunAsync` through `FireForget` (`Error LPN3` if it throws
  unexpectedly).

`RunAsync` then loops over the panels, step by step:

1. **OBS not ready** (`ObsConnection.IsReady`): it makes no OBS calls at all. It waits 5 s and tries again,
   without moving on to the next panel. So a closed OBS logs nothing.
2. **First step with OBS ready:** it reads what OBS has (`FindLayout`), once per rotation.
   - It lists the top-level items of both scenes (`Obs.GetSceneItemNames`). An `_LP: <name>` item counts
     only in `!Scene: Left Panel`, and an `_LPC: <name>` item only in `!Scene: Left Panel Colored`
     (`GroupName`, tested). Anything else is left alone, including an `_LP:` item in the coloured scene.
   - Numbered groups become copies of a family.
   - The coloured scene holds the whole `!Scene: Left Panel` as an item, for reference while editing in
     OBS; this step hides it.
   - With split colours, it colours the split list highlights.
   - Groups the game's entry names that OBS doesn't have are logged once per rotation, as
     `Left panel: <game> has groups OBS doesn't: …`, and then skipped.
3. **Each step:**
   - It plans the panel (`Plan`, tested): which copy of each family to use (see [Copies](#copies)), and the
     texts and files to set, including remembered ones.
   - It sets the panel's `Text` and `File` (`Obs.SetInputSetting` with `text` / `file`).
   - It shows the groups that are wanted but not up, then hides the ones that are up but not wanted
     (`Transition`). Wanted means `Always` plus the panel's groups, with each family as its chosen copy.
   - On the first step nothing is known to be up, so it shows all of the wanted groups and hides every
     other known group.
   - Showing before hiding means the panel never goes blank between two panels, and the values land while
     their copy is still hidden.
   - It then waits the panel's `Seconds` and moves to the next panel, wrapping round at the end.
4. **A split**, when the entry has `Previous`, cuts that wait short. The `Previous` panels play once, each
   for its `Seconds`, and a split during them starts them over. Then the rotation returns to the panel that
   was cut short and shows it again for its full time. Each `Previous` step first colours the previous
   split's differences by the entry's `Sign`, when it has one and there are split colours. Without
   `Previous`, splits are ignored. While OBS isn't ready, the 5 s retry ignores them too.
5. **One panel, or none:** with a single panel, or none (only `Always`), the rotation ends once it is up,
   unless the entry has `Previous`. Then it waits for splits instead, and shows that panel again after each
   one.

More detail on the edges:

- **Concurrency:** each step runs under one lock and checks for cancellation first. A replaced rotation can
  finish the step it is on, but it can't interleave with the next rotation.
- **A failing call:** an OBS call that throws logs `Error LPN2: left panel <what> failed: <message>`, and the
  rest of the step still runs. A cancelled rotation ends quietly.
- **OBS connecting:** the groups' state is read once per rotation, at its first ready step. An OBS restart
  reloads the scene as saved, which could leave groups from before up next to the current panel. So every
  time OBS connects, `ObsConnection`'s `Connected` handler calls `LeftPanel.OnObsConnected`, which restarts
  the last game's rotation with `Start`:
  - The rotation starts over from its first panel, lists the groups again, and hides the leftovers.
  - `Left panel: …` is logged again.
  - It also covers a rotation that had already ended (one panel, or none).
  - A game whose panel was "left as is" stays as it is.
  - A rotation stopped with `!stoppaneltimer` stays stopped.
  - At boot no game has started yet, so the first connect does nothing here.
  - `Error LPN4` if the restart itself throws; it runs on obs-websocket's thread.
- **On the dashboard:** `RunAsync` reports each panel it shows: the loop index, plus the `Previous` index
  during a split. `Start` turns those reports into `LeftPanel.Status`, a `RotationStatus` record holding
  the game, its set, the panel up, when it was shown, and whether it's stopped. It's replaced under a small lock of its own, and
  only by the rotation it belongs to. Once the groups have been listed, `RunAsync` also reports the ones OBS
  doesn't have, into the status's `Missing`. The window's right-hand column draws it
  ([dashboard.md](dashboard.md#the-left-panel)).
- **Changing by command:** `!changepanel <game>` (`Games.TryChangePanel`) starts a rotation without the rest
  of the game setup. The category, background, colour and captures stay, and nothing is posted to chat.
  - The input is resolved by `Games.ResolvePanel` (tested): a `games.json` key, game name or numeric id gives
    that game's name; otherwise the input itself, if it names a `leftpanel.json` entry, such as `default`.
  - Anything else logs `No game or left panel "…"`. With no panels loaded it logs `No left panels loaded`.
  - The panel it starts is the one an OBS reconnect restarts, until the next category change.
- **Moving on by command:** `!nextpanelitem` (`LeftPanel.Next`) cuts the wait short, as though the panel's
  time were up. In the loop it moves to the next panel; during a previous split, to the next previous-split
  panel, and after the last of those back to the interrupted loop panel. It works like the split signal: a
  `TaskCompletionSource` that every wait also watches, captured before each step so a skip during a step
  isn't lost (`SkipPanel`, tested). Splits still win over a skip. It does nothing while OBS isn't ready.
  With no rotation, a stopped one, or one panel and no `Previous`, it logs `No left panel rotation to move
  on` or `Only one left panel, nothing to move on to`.
- **Stopping by command:** `!stoppaneltimer` (`LeftPanel.Stop`) cancels the rotation, leaves the current
  panel up and logs `Left panel rotation stopped`. It stays stopped through OBS reconnects. Any `Start`
  clears the stop: `!startpaneltimer` (`LeftPanel.Resume`, the last game's rotation from its first panel),
  a category change, or going live. With no rotation to act on they log `No left panel rotation to stop` or
  `No left panel to start`.

`Transition` and `Missing` are tested. The real `RunAsync` is also tested against fake OBS calls, covering:
the order of a step (with the copies listed and the reference item hidden), the timings and the wrap, waiting for OBS, one panel and none,
cancelling, missing groups, a throwing call, a kept family staying up while the others move, splits (with
and without `Previous`, during `Previous`, and for a single panel), and split colours (the highlights on the first step, each `Previous` step's colours
by its sign, and none without a sign, without colours or with an unreadable sign). `LeftPanelStopTests`
cover how a stop, a resume, a new `Start` and an OBS connect interact, with panels loaded from a temporary
file and OBS never ready, and the start line's split-colours part. None of this has been seen in a real OBS
yet.

### Copies

Some panels come in interchangeable copies in OBS, so a panel's contents can change while it's hidden. A
copy is an `_LP:` (or `_LPC:`) group named `<family> <number>`: `Splits Current 1`–`3`,
`Splits List Labels 1`–`3`, `Splits List Values 1`–`3`. A copy's own text sources carry its number
(`Livesplit: Current Index 2`). Sources without a number (`Livesplit: Current Labels`) are shared by every
copy.

The bot doesn't ask OBS what's inside a copy; it decides from the names alone and remembers what it set.

**Choosing a copy.** `Plan` moves every family a panel shows to a fresh copy, together:

- All of them take the lowest copy number that is hidden in every one of them: 1 when nothing is up, 2 when
  1 is up, and so on. The panel's values go into those copies while they're hidden, then they're shown and
  the old copies hidden. So two panels of the same family in a row (two `Splits Current` panels, or
  `Run Delta` after `Finished at`) always alternate copies, and the viewer never sees values change.
- If no number is hidden in every family (they're out of step), each family takes its own lowest hidden
  copy, and copy sources get the first family's number.
- **A family in the panel's `Keep` stays on the copy that's up.** The others move as above, avoiding the kept
  copies' numbers when they can, and copy sources get the first moving family's number (the first kept
  one's when every family is kept). Remembered values for the kept family go onto its hidden copy with that
  number, out of sight. A kept family that isn't up moves like any other.
  - This is for a panel whose family looks the same as on the panel before. The split lists' Run, Seg. and
    Golds Delta panels show `Splits List Values` and keep `Splits List Labels`, so only the values column
    swaps.
  - It isn't checked: a kept family that the panel does change shows the change in place, on screen.
- An `Always` family keeps the copy that's up, or takes copy 1.
- Two copies per family are enough; a third is spare.

**Copy sources.** In a panel that shows families, every `Text` and `File` source is a copy source and
gets the chosen number: `Livesplit: SL - Current` → `Livesplit: SL - Current 2`. A name that already ends in
a number has it replaced instead (`Livesplit: SL - Current 1` → `Livesplit: SL - Current 2`). In a panel
without families, sources are set as written.

**Remembering.** The bot remembers each copy-source value it sets for the rest of the rotation, by the name
without a number, with the families that were shown with it. On a later panel, a remembered value the panel
doesn't set itself is set again on the new copy, but only when every family it was set with is shown again.
That's how the split list's labels (`Livesplit: SL - Names`, `Header Left`) follow the move to the next copy
when only the values change, while `Splits Current`'s values are never written into a list's copies. A new
rotation starts with nothing remembered.

### Splits from LiveSplit

[SplitWatcher.cs](../code/LiveSplit/SplitWatcher.cs) tells the rotation about splits.
`Program.StartBotAsync` starts it with the other periodic work.

- It watches `<LivesplitGenerated>\CurrentSplit\AllSplits\Index.txt` with a `FileSystemWatcher`. That
  folder is a `paths.json` directory, and LiveSplit's text output writes this file.
- The file holds the current split's 0-based index, and `-1` when no run is going. `AllSplits` is the list
  the previous-split texts read (`PreviousSplit\AllSplits\…`).
- On every change it reads the index, retrying for about 100 ms while LiveSplit holds the file. A read that
  fails or isn't a number is ignored, so a half-written file doesn't count.
- It compares the index with the last one read. Any change is passed on to the sidebar
  ([Per-game setup](#per-game-setup)), and `CurrentIndex` holds the last one read. A rise of exactly one, to 1
  or more, is a split or a skip. Starting a run (-1 → 0), a reset, an undo or a repeat are neither.
- **A skip is told apart by the previous split's time.** Your component (`LiveSplit.OutputToFile`) writes
  `PreviousSplit\AllSplits\RealTime\RunTime.txt` after `Index.txt`, as the previous split's live real-time
  time. A split always has one. A skipped split has none, and the component writes that as `-`.
  - So after any change of the index, `AfterIndexChangeAsync` waits, off the watcher's thread, until
    `PreviousSplit\AllSplits\RealTime\Sign.txt` has been written since `Index.txt`. It checks every 50 ms,
    for up to 0.5 s.
  - The component writes each level's sign after its times, real time before game time, and `AllSplits` is
    its last level. So once that sign is fresh, the run time is too, and so is every real-time sign the
    `Previous` panels colour by.
  - The component's source, since 2026-10-15, skips a file whose contents haven't changed, and writes the
    files on a background thread, in the order it queued them. So a sign that repeats (`LostBehind` twice
    in a row) is never rewritten, and the wait runs out. That's why it is only 0.5 s: a split's files are
    all written within about 40 ms, so by then an old sign is the current one. Reproduced headless on
    2026-10-15: on two repeated `LostBehind` splits, `Sign.txt` kept the earlier split's time. A changed
    sign was written within 1 ms of `Index.txt`. The build in LiveSplit still rewrites every file on every
    split, so there the sign is always fresh
    ([SGN](robustness-fixes.md#sgn--livesplit-never-writes-gainedahead--gainedbehind-during-a-run)).
  - For a rise, it then reads `RunTime.txt`. `-` means a skip, which doesn't show the previous-split panel.
  - Anything else calls `LeftPanel.OnSplit()`. That includes a sign that isn't rewritten in time or a time
    that can't be read, so the panel still shows for those.
  - Last, for every change, it passes the sign (`ReadSign`) to `GameColor.OnSign`, which recolours the
    layout by it for a game with sign schemes ([Per-game setup](#per-game-setup)).
- `OnSplit` completes a `TaskCompletionSource` that the rotation's wait is watching, then swaps in a fresh
  one. It runs continuations asynchronously, so the watcher's thread never runs a rotation step.
- Logs:
  - `Watching LiveSplit's split index` on start.
  - `LivesplitGenerated not set in paths.json, previous-split panel off`, or `No <folder>, previous-split
    panel off`, when it can't start.
  - Nothing per split or skip.
  - `Error LSW1` when handling a change throws, `Error LSW2: <message>` when the watcher itself fails,
    `Error LSW3` when starting it throws, and `Error LSW4: index change failed: …` when the handling after an
    index change throws.

`IsSplit`, `IsSkip` and `ParseIndex` are tested. The watcher itself isn't, since its file events don't arrive on a
fixed schedule.

### Split colours

[SplitColors.cs](../code/LiveSplit/SplitColors.cs) colours LiveSplit's sources in `!Scene: Left Panel Colored`
from `SplitColors` in the `gameschemes` colour scheme file ([color.md](color.md#data-files)). This ports
Streamer.Bot's "Change Split Colors" action.

**Which colours.** `Start` takes the game's category's `SplitColors` (the name `Start` was given, so
`!changepanel SWAT4` gets SWAT4's although its panels are `Run_NoSubsplits`'s). These are laid over the
`none` category's, so a key the game leaves out comes from `none`. A game with no category, or one without
`SplitColors`, gets `none`'s alone. Only SWAT4, SS2 and `none` have any. With neither, nothing
is coloured. `Resolve` is tested.

**The split list highlights**, on the first step with OBS ready. That is every rotation start: game setup,
`!changepanel`, `!startpaneltimer` and an OBS reconnect. Each source's `Color Correction 2` filter gets its
`color_multiply`:

| Source | Key |
|---|---|
| `Livesplit: Splits List Highlights Gold` | `ListGold` |
| `Livesplit: Splits List Highlights AheadGained` | `ListGainedAhead` |
| `Livesplit: Splits List Highlights AheadLost` | `ListLostAhead` |
| `Livesplit: Splits List Highlights BehindGained` | `ListGainedBehind` |
| `Livesplit: Splits List Highlights BehindLost` | `ListLostBehind` |

**The previous split's differences**, at the start of each `Previous` step, when the entry has a `Sign`. The
step reads the sign file (retrying about 100 ms while LiveSplit holds it). Then it sets the `color_multiply`
of the `Color Correction` filter on `Livesplit: Previous Run`, `Segment` and `Gold`, by `SignKeys` (tested),
Streamer.Bot's table as it was:

| Sign | Run | Segment | Gold |
|---|---|---|---|
| `Ahead`, `PB`, `PBLost` | `LabelPositive` | `None` | `None` |
| `Behind`, `NoPB` | `LabelNegative` | `None` | `None` |
| `Gained` | `None` | `LabelPositive` | `None` |
| `Lost` | `None` | `LabelNegative` | `None` |
| `GainedAhead`, `PBGained` | `LabelPositive` | `LabelPositive` | `None` |
| `LostAhead` | `LabelPositive` | `LabelNegative` | `None` |
| `GainedBehind`, `NoPBGained` | `LabelNegative` | `LabelPositive` | `None` |
| `LostBehind`, `NoPBLost` | `LabelNegative` | `LabelNegative` | `None` |
| `Gold` | `None` | `LabelGold` | `LabelGold` |
| `GoldAhead`, `PBGold` | `LabelPositive` | `LabelGold` | `LabelGold` |
| `GoldBehind`, `NoPBGold` | `LabelNegative` | `LabelGold` | `LabelGold` |
| anything else (`NotApplicable`, `Undetermined`, blank) | `None` | `None` | `None` |

The sign is matched in any case, trimmed. `PBLost` leaves the segment `None`, unlike `NoPBLost`; that is how
the old table had it ([PBL](robustness-fixes.md#pbl--pblost-leaves-the-segment-difference-uncoloured)).

**Notes.**
- A key with no colour leaves that source as it is.
- A sign file that can't be read logs `Left panel: can't read <path>, previous-split colours left as is`.
- A filter call that throws logs `Error LPN2: left panel colouring <source> failed: …`, like the rotation's
  other OBS calls.
- **LiveSplit never writes `GainedAhead` or `GainedBehind` during a run.** The build in LiveSplit's
  `Components` folder (2026-07-02) has no case, in the running branch, for a segment that beat its PB
  without being a gold. It writes `LostAhead` / `LostBehind` instead, so that segment's difference gets
  `LabelNegative`. The end-of-run branch has the case (`PBGained` / `NoPBGained`). The component's source
  has written them since 2026-10-15, but that build isn't in LiveSplit yet
  ([SGN](robustness-fixes.md#sgn--livesplit-never-writes-gainedahead--gainedbehind-during-a-run)).

### Where the data came from

The file was converted on 2026-10-11 from the Streamer.Bot-era `Bot Input\!OLD_Streamerbot\scenes.json`,
which is still there but no longer read. That file has a `GenericSceneList` of steps per game, run in
order and looped. A step does one of three things, then waits `Duration` ms:

- shows or hides a group (`Name`, `Enabled`)
- sets a text source's text (`ChangeTextOf`, `ChangeTextTo`)
- sets a text source's file (`ChangeFileOf`, `ChangeFileTo`, with doubled backslashes)

`Comment` objects are separators. The conversion played each list through once. Every step with a
`Duration` closed a panel: the groups enabled at that point, plus the changes made since the previous
panel. Groups enabled from the start and never hidden became `Always`.

What was converted, and what was left out:

- Converted: GTA3, GTA4, GTA5, GTASA, GTAVC, L4D2, Portal2, HITMAN, SWAT4 and RoN, with `Other` as `default`.
  GTA4 and GTA5 came out identical to GTA3, so they are now `"GTA3"`. RoN came out identical to `default`,
  so it was dropped and falls back to it.
- Left out: `None`, a blank panel.
- Left out: `GTASA1`, `GTASA2` and `GTAVC2`, alternate lists that match no game.
- Converted: SWAT4's `JustSplitSceneList`, its previous-split panel, into SWAT4's `Previous`, by the same
  rule: `Nothing` for 2 s, then `Nothing`, `Splits Previous` and `Splits Previous Values` for 30 s, then
  `Nothing` for 2 s. `Nothing` is an empty group, so those are pauses. Only SWAT4 had a list; any entry
  can be given one by hand.
- Left out: the `"Colored": true` marks, which said a group is in `!Scene: Left Panel Colored`.

The file has been edited by hand since: entries reworked and shared under new names (SWAT4's now points at
`Run_NoSubsplits`), and the OBS groups renamed. Then on 2026-10-12 it was brought in line with OBS:

- `Show` lists and copy sources use names without a copy number ([Copies](#copies)).
- `default`'s `Sellout Goals` became `Sellout List`, the group's name in OBS.
- GTAVC's `Splits SSSA` panel was dropped; OBS has no such group.
- `Run_NoSubsplits` was rebuilt on the split-list copies. SWAT4's list had been converted as is, with
  groups OBS no longer has (`Splits Main List Big Delta` and the like) and no texts or files. Each panel now
  sets its own, all from `AllSplits`: `Splits Current` with `Current Split`, `List On` with the names
  and highlight, then `Finished at`, `Run Delta`, `Seg. Delta` and `Golds Delta`. Its `Previous` is as it was.

On 2026-10-13, the highlight group became `Splits List Highlights`, with all five highlights in it, in OBS
and the file. `Run_NoSubsplits`'s panels were renamed by hand, and its sellout and key-count panels no longer
show the split list. It also got a `Sign`, for `AllSplits`. Every panel that shows the split list's labels
right after another that does, without setting any label source itself, now keeps them: `Splits List Labels`
moved from `Show` to `Keep`, with `Splits List Highlights` where the panel shows it. That's SWAT4's
`Finished at` through `List Off`, and the other games' Run, Seg. and Golds Delta panels.

Which groups are missing is what the rotation logs and the dashboard column shows in red. One gap is
known: **Portal2's per-map gold panel reads files LiveSplit doesn't write.** It reads
`SansSubsplits_Level1\RealTime\Delta_Gold.txt` and `Time_Gold_Upcoming.txt`, but LiveSplit writes gold
files only for its last split level, `AllSplits`
([PGD](robustness-fixes.md#pgd--portal2s-per-map-gold-panel-reads-files-livesplit-never-writes)). Checking the
file against the scene collection file and the disk finds nothing else.

The panel rotated once before. In 2022, `SceneManager.cs` (deleted;
`git show 5f105ef^:StreamAssistant2/SceneManager.cs`) cycled a list of panels per game, and after each
LiveSplit split it showed a previous-split panel for a while. It switched panels by writing the next
panel's name to a `TargetSource.txt` for OBS to pick up. Under Streamer.Bot, a rewrite in a root
`LeftPanel.cs` never got past commented-out scaffolding.

### In OBS

This was read from the scene collection file (`%APPDATA%\obs-studio\basic\scenes\2022.json`) on
2026-10-12, after the groups were renamed from `_Left Panel: <name>` to `_LP: <name>` (and `_LPC: <name>`
for coloured ones), and the coloured scene again on 2026-10-13. It wasn't checked in a running OBS.

- `!Scene: Left Panel` holds `_LP:` groups:
  - the split lists, double-buffered as `Splits List Labels` / `Values` `1`–`3`
  - `Splits Current 1`–`3`
  - `Splits Information`
  - `Splits Previous`
  - `Splits Key Count`, `Splits Sellout Goals` and `Splits Sellout List`
  - `Splits Basics`
  - `Sellout Basics`, `Sellout Key Count` and `Sellout List`
  - `Nothing`
- `!Scene: Left Panel Colored` holds `_LPC: Splits List Highlights` and `_LPC: Splits Previous Values`,
  plus the whole `!Scene: Left Panel` as an item, for reference while editing. The bot hides that item when a
  rotation starts.
  - `Splits List Highlights` holds the five `Livesplit: Splits List Highlights <kind>` sources (`Gold`,
    `AheadGained`, `AheadLost`, `BehindGained`, `BehindLost`). Each reads its
    `SplitList\AllSplits\RealTime\Highlight_<kind>.txt` and has a `Color Correction 2` filter, which the bot
    colours, and a `Crop/Pad`.
  - `Splits Previous Values` holds `Livesplit: Previous Run`, `Segment` and `Gold`, each with a
    `Color Correction` filter, which the bot colours. On 2026-10-13 their `color_multiply` still held
    Streamer.Bot's `%splitColRun%`-style placeholders. It also holds the previous split's name, index and
    labels.
- The copies' own sources: `Livesplit: Current …` / `Header Current Split` / `Total Splits` `<n>` in
  `Splits Current <n>`; `Livesplit: SL - Names` / `Highlight` / `Header Left` `<n>` in
  `Splits List Labels <n>`; and `Livesplit: SL - Current` / `Upcoming` / `Header Right` `<n>` in
  `Splits List Values <n>`.
- The `Livesplit: …` text sources read files that LiveSplit writes under
  `Stream-Resources\Text\Livesplit Generated\` (last written 2026-07-30).
