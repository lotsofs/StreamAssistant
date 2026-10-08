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
  `Lost connection to OBS: <reason>`) and `StreamStateChanged`, and starts `Loop`.
- `OnStreamStateChanged` logs `OBS started streaming` and `OBS stopped streaming` (the in-between
  states are ignored). On start it resets the chatter list (`ChatterList.Reset`, see
  [twitch.md](twitch.md#chatterlist)) and starts the go-live game setup
  ([events.md](events.md#category-change-and-going-live)). It runs on obs-websocket's receive thread and catches its own
  exceptions (`Error OSS1`). Connecting to an OBS that is already streaming raises no event, so no reset.
  Unit-tested with the event args obs-websocket builds; not yet seen at a real stream start.
- `Loop` checks once a second while connected. When not connected it fires `ConnectAsync` (which
  returns at once; the outcome arrives through the events) and sleeps 3 s. So OBS can be started
  before or after the bot, and a restart of OBS is picked up within a few seconds.
- `Disconnect()` cancels the loop and closes the socket if it is open. The cancelled `Task.Delay` ends
  the loop with an exception in a discarded task, which is harmless at shutdown.
- `IsConnected()` is the guard every OBS action starts with. If not connected it logs `Trying to do an
  OBS action but not connected to OBS` on `Important` and returns false.

OBS being closed is a normal state, not an error: actions are skipped, not queued, and nothing is
retried afterwards.

## The wrapper

[Obs.cs](../code/Obs/Obs.cs) holds four calls, each of which does nothing but log when OBS is unreachable:

| Method | OBS request | Used by |
|---|---|---|
| `SetSourceEnabled(scene, source, enabled)` | `GetSceneItemId(scene, source, 0)` then `SetSceneItemEnabled` | `LayoutColoring`, the train reward |
| `SetInputSetting(input, key, value)` | `SetInputSettings(input, {key: value}, overlay: true)` | `SetImageSource`, `GameCapture` |
| `SetImageSource(source, file)` | `SetInputSetting(source, "file", file)` | the train reward, `GameBackground` |
| `SetFilterProperty(source, filter, property, value)` | `SetSourceFilterSettings(source, filter, {property: value}, overlay: true)` | `LayoutColoring` |

None of them catch. A missing scene, source or filter name makes the library throw. That is handled
for colour changes (the worker logs `Error Obs29`) but not for the train reward, where it ends the
reward's task silently and can leave the train image showing. All names are hardcoded strings,
listed in [reference.md](reference.md#obs-names); renaming something in OBS breaks the matching call
without any compile-time hint.

The rest of `Obs.cs` is a large commented-out block from the Streamer.Bot era. See
[legacy.md](legacy.md#obs-raw-request-builder).

## Layout recolouring

[LayoutColoring.cs](../LayoutColoring.cs). The channel's layout has three colourable parts, recoloured
through OBS's *Color Correction* filter (called `Color Correction` on every source):

| Part | Source | Filter property |
|---|---|---|
| inner borders | `Border: Colorable Inner` | `color_multiply` |
| outer borders | `Border: Colorable Outer` | `color_add` |
| text and body | `!Scene: All Colorable` | `color_multiply` |

Each has a twin whose name ends in ` (Transitionary)`, and the twins sit in a duplicate group, `!Layout:
Colorables (Transitionary)`, beside the real group `!Layout: Colorables`, both inside the scene
`!Scene: Layout`.

### Requests are queued

Recolouring takes about nine seconds and must not overlap itself, so requests never run directly:

- `TryChangeToSingle`, `TryChangeToTriple` and `ChangeToRandom` resolve a colour, call `ChangeColor`,
  and report the outcome in the log and, on failure, in chat.
- `ChangeColor` enqueues a `ColorRequest(Inner, Outer, Text)` hex triple on an unbounded
  `Channel<ColorRequest>` and **immediately** posts
  `Changing to color <name> [<source>]: <hex> <hex> <hex>` to chat. The announcement therefore
  precedes the visible change by the length of the queue ahead of it.
- One worker, started by `StartWorker()` at boot, reads the channel with `ReadAllAsync` and runs
  `ChangeColorAsync` for each request. Exceptions are caught per request: `Error Obs29`, then the
  exception and its message.

Never call `ChangeColorAsync` directly. Starting the worker twice is refused with a log line.

### The animation

`ChangeColorAsync` converts the three hex colours with `ColorUtil.ToOBS` (the opaque ABGR value OBS
colour filters take) and then:

| t | Step |
|---|---|
| 0 s | Set the three **Transitionary** filters to the new colours. Enable `Colorables (Transitionary)` |
| +5.5 s | Disable the real `Colorables` group |
| +2.5 s | Set the three **real** filters to the new colours (the group is hidden, so nothing shows) |
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

## Per-game setup

`Games.Apply`, on bot start, a category change, or when OBS starts streaming
([events.md](events.md#category-change-and-going-live)), runs on the thread pool, not on a read loop,
and sets up the game's background, colour and capture sources from its `games.json` entry. Each step
runs through `Games.RunStep`, so one that throws logs `Error GMS2: game setup <step> failed: …` and the
later steps still run (tested). The background and capture steps also catch OBS rejections themselves,
with a clearer `Couldn't set <source>: …` warning.

- **Background**, [GameBackground.cs](../code/Obs/GameBackground.cs): `Set` points the `Image: Background`
  input at `<Directories.Backgrounds>\<game name>.png` with `Obs.SetImageSource` and logs
  `Image: Background → <file>`. A missing file leaves the image as it is (`PathFor`, tested). If OBS
  rejects the request (the source is missing or renamed), it logs `Couldn't set Image: Background: <message>`
  and the colour and capture steps still run.
- **Colour**, [GameColor.cs](../code/Obs/GameColor.cs): `Set` requests `gameschemes <game name>` through
  `LayoutColoring.TryChangeToSingle`, the reward's path: the game's category in the `gameschemes` scheme
  set, at its `Default` scheme. It is queued like any colour change (the animation runs later on the
  colour worker), replaces whatever colour is showing, and posts the usual `Changing to color …` line.
  A game with no category there (RoN) logs `No colour scheme for <name>, colour left as is` instead of
  the chat failure line (`SchemeInput`, tested; the real `gameschemes KTANE` is checked in the real-data
  smoke test).
- **Capture sources**, [GameCapture.cs](../code/Obs/GameCapture.cs): three kinds, three numbered slots
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
  game capture source's capture mode). A used slot is then shown (eye on) in `!Scene: Games 1920x1080`, an
  unused one hidden there. Each shown slot logs `<source> → <exe>, shown`; the hidden ones share one line,
  slot numbers grouped by kind, e.g. `→ none, hidden: Game: Game Capture 1, 2; Game: Window Capture 0, 1, 2;
  Audio 5: App Capture 1, 2` (`HiddenLine`, tested). A source
  that can't be set (missing or renamed in OBS, or not in that scene) logs `Couldn't set …` and the
  rest still go. With OBS disconnected it logs `OBS not connected, capture sources not set` once.
  Audio capture is a fallback: most game audio now comes from the video capture source itself.
  (`Assignments`, `IsUsed` and `WindowValue`, tested.)

On bot start, `Games.OnBootAsync` hands its apply to `ObsConnection.WhenConnected`, which runs it at
once if OBS is connected, otherwise on OBS's next `Connected` event, however late. So the capture
sources and background aren't skipped because OBS connects after the bot. It applies once per boot:
later reconnects (an OBS restart mid-stream) don't re-apply, since OBS keeps the settings.
`WhenConnected` can fire twice if OBS connects during the call, so the boot apply guards itself with a
flag.
