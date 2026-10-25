# Reference

Lookup tables: the error codes the bot logs, and every value that is hardcoded on purpose. Grep for the
code or the constant to find the line. This file goes stale fastest, so when you change a value in
code, update its row here.

Read from the code.

## Error codes

Every `catch` logs `Error <code>` on the `Error` channel and writes the exception to the log file with
`LogToFile(ex)`. The codes are grep handles, not a scheme: some are numbers, some are mnemonics.

| Code | Where | Meaning | Then |
|---|---|---|---|
| `PRG1` | `Program.StartBotAsync` | A startup step threw (message appended). Common cause: the TTS voice is missing | the bot stays half-started |
| `1` | `TwitchIRCManager.StartConnectionLoop` | IRC connect or listen failed | waits 3 s, reconnects |
| `TIRC2` | `TwitchIRCManager.ListenLoop` | IRC connection lost (always followed by `1`) | rethrows to the loop |
| `TIRC3` | `FireForget.Run` (from `TwitchIRCManager.SendMessage`) | Writing a chat message failed | message dropped |
| `7` | `ChatHandler.ProcessMessage` | A raw IRC line couldn't be parsed, **or** a command handler threw synchronously (including the colour commands) | line dropped |
| `TES1` | `TwitchEventSub.StartConnectionLoop` | The EventSub session ended with an exception; suffix is the `SessionExitReason`. A dropped connection doesn't land here: it ends as `ConnectionLost` | cleanup, 3 s, reconnect |
| `TES2` | `TwitchEventSub.CleanupSession` | Closing the old socket threw | continues |
| `TES3` | `TwitchEventSub.StartConnectionLoop` | The session ended without setting a reason, including every failed or timed-out connect (after `TES1: None`) | cleanup, reconnect |
| `TES4` | `TwitchEventSub.ConnectToReconnectUrlAsync` | Connecting to `reconnect_url` or reading its welcome threw (timeout, refused, bad JSON) | fresh session on the default URL |
| `TES5` | `TwitchEventSub.SwitchToReconnectUrl` | Draining the old socket after a successful reconnect threw | old socket closed, new session carries on |
| `SES1` | `StreamElementsSocket.StartConnectionLoop` | The StreamElements session threw (connect failure or timeout included); the line ends with the exception type and message | cleanup, backoff 3–30 s, reconnect |
| `SES2` | `StreamElementsSocket.CleanupSession` | Closing the socket threw | continues |
| `SES3` | `StreamElementsSocket.HandleResponse` | StreamElements refused the subscribe: `refused the token` (`err_unauthorized`, the loop stops for good) or `subscribe failed: <error>` | restart the bot after fixing `secrets.json`, or backoff and reconnect |
| `SES4` | `StreamElementsSocket.SubscribeAndProbeAsync` | Sending the subscribe or a probe threw while the socket was open | no more probes; the silence timeout ends the session |
| `SEH1` | `StreamElementsEventHandler.Handle` | The dispatch itself threw synchronously | event dropped |
| `SEH_t` | `FireForget.Run` (from `StreamElementsEventHandler.Handle`) | The tip handler threw (a missing sound file, say) | rest of that alert skipped |
| `CHL1` | `ConnectionHealth.WatchAsync` | A connection-health check threw | next check a second later |
| `TEH1` | `TwitchEventHandler.Handle` | The dispatch itself threw synchronously (not the handlers, which `Run` wraps) | event dropped |
| `TEH_adb`, `TEH_cpcrra`, `TEH_c` | `FireForget.Run` (from `TwitchEventHandler.Handle`) | The ad-break, redemption or cheer handler threw, before or after its first `await`; the message names the event type | rest of that alert skipped |
| `TEH_CN_s`, `TEH_CN_rs`, `TEH_CN_sg`, `TEH_CN_csg` | `FireForget.Run` (from `TwitchEventHandler.HandleChannelChatNotification`) | The sub, resub, sub-gift or gift-bomb handler threw; the message names the notice type | rest of that alert skipped |
| `GMS_86` | `FireForget.Run` (from `Games.HandleUpdate`) | Applying the new category's game threw | that game's setup skipped |
| `GMS_boot` | `FireForget.Run` (from `Program.StartBotAsync`) | The boot game setup threw, including a failed Helix channel-info call | no game setup until the next category change or stream start |
| `GMS_live` | `FireForget.Run` (from `ObsConnection.OnStreamStateChanged`) | The go-live game setup threw, including a failed Helix channel-info call | no game setup this stream start |
| `TEV1` | `FireForget.Run` (from `TestEventRunner.Run`) | Feeding a `!test` script's events into the handler threw | rest of that test not sent |
| `TRN1` | `FireForget.Run` (from `ChannelPoints.TryStartTrain`) | The train threw (OBS calls no-op when disconnected, so rarely) | train skipped |
| `TRN2` | `FireForget.Run` (from `ChannelPoints.OnObsConnected`) | Hiding a train left showing, after an OBS connect, threw | the train stays up until hidden by hand or the next connect |
| `TRN3` | `ChannelPoints.OnShutdown` (from `Program.Main`) | Hiding a running train at shutdown threw | the train stays up until the next start |
| `SUB1` | `Subscriptions.HandleCommunitySubGiftNotif` | The gift-bomb announcement threw (the only handler with its own catch) | no announcement |
| `SUB2` | `FireForget.Run` (from `Subscriptions.HandleSubGiftNotif`) | The check for bomb recipients whose bomb never arrived threw | those recipients go unreported |
| `CP1` | `ChannelPoints.CloseRedemption` | Helix refused to fulfil or cancel a train or colour redemption (often a 403; see [color.md](color.md)) | redemption left open |
| `Obs29` | `LayoutColoring.ProcessColorChangeQueueAsync` | Something threw during a colour change outside the per-source and per-scene handling (each failed filter or group logs `Couldn't colour …` / `Couldn't show|hide …` and the change goes on); the message is logged next | worker moves to the next request |
| `OBSException58` | `ObsConnection.Loop` | `ConnectAsync` threw synchronously | retries in 3 s |
| `GMS_cap` | `FireForget.Run` (from `Games.TryChangeCapture`) | `!changecapture` threw outside `RunStep`'s own catch | the capture sources aren't changed |
| `GMS2` | `Games.RunStep` (from `Games.Apply`, and `!changecapture`) | One game setup step (background, border, scenes, sidebar, colour, left panel or capture sources) threw; the message names the step | that step skipped, the later ones still run |
| `GMS1` | `Games.Load` | `games.json` couldn't be read or parsed | at startup no games are loaded, so every category resolves to `None` and leaves the capture sources alone; on `!reloaddata` the games loaded before stay |
| `LPN1` | `LeftPanel.Load` | `leftpanel.json` couldn't be read or parsed | at startup no left panels are loaded; on `!reloaddata` the panels loaded before stay |
| `CLR1` | `Coloring.ListFiles`, `LoadFailed` (from `ColorTableRegistry.LoadTables`, `ColorSchemeRegistry.LoadSets`) | `can't load <path>: <message>`: a colour table or scheme file couldn't be read or parsed, or its folder couldn't be listed | that file keeps the table or set it held before (none at startup) and the other files load; a folder keeps everything its registry held |
| `DAT1` | `FireForget.Run` (from `DataFiles.Reload`) | `!reloaddata` threw outside the loaders' and the game setup's own catches | the rest of that reload is skipped |
| `LPN2` | `LeftPanel.RunAsync` | One OBS call of the left-panel rotation threw (listing a scene, hiding the reference item, setting a text or file, colouring a split filter, showing or hiding a group); the message says which | the rest of that step still runs |
| `LPN3` | `FireForget.Run` (from `LeftPanel.Start`) | The rotation threw outside its own handling | that rotation stops until the next `Start` |
| `LPN4` | `LeftPanel.OnObsConnected` (from `ObsConnection`'s `Connected` handler) | Restarting the rotation on an OBS connect threw | the rotation isn't restarted; leftovers from before the connect may stay up |
| `SDB1` | `FireForget.Run` (from `GameSidebar.OnObsConnected`) | Setting the dynamic sidebar again after an OBS connect threw | the sidebar keeps what OBS had until the next index change or game setup |
| `MON1` | `FireForget.Run` (from `MoneyGoal.Credit`) | Adding a tip to the money goal threw, usually the database | that tip isn't counted |
| `MON2` | `FireForget.Run` (from `MoneyGoal.TrySetGoal`, `Reset`) | `!moneygoal` or `!resetmoney` threw, usually the database | goal or total unchanged |
| `OBI1` | `ObsInputs.WatchShowingAsync` | Working out which image and text sources are showing in `Basic Stream Layout` threw (a scene or group OBS won't list is skipped quietly) | next check 2 s later |
| `MON3` | `FireForget.Run` (from `MoneyGoal.OnObsConnected`) | Reading the money goal to rewrite its OBS text after a connect threw | the text keeps what OBS had until the next tip or command |
| `LSW1` | `SplitWatcher.OnChanged` | Handling a change to LiveSplit's split index threw | that change is missed; the next one is read as usual |
| `LSW2` | `SplitWatcher` (the watcher's `Error` event) | The `FileSystemWatcher` failed, e.g. its buffer overflowed; the message is logged | events may be missed |
| `LSW3` | `SplitWatcher.Start` | Starting the watcher threw | no previous-split panel this run |
| `LSW4` | `FireForget.Run` (from `SplitWatcher.OnChanged`) | The handling after a change of the split index threw: the skip check, the previous-split panel or the sign's layout colour (`index change failed`) | that change doesn't show the previous-split panel or recolour the layout |
| `OSS1` | `ObsConnection.OnStreamStateChanged` | Handling OBS's stream start or stop threw | chatter list not reset |
| `DSK1` | `DiskSpace.WatchAsync` | A disk-space check threw | next minute |
| `UpT5` | `TwitchUptime.UptimeLoopAsync` | An uptime check threw (outside its own HTTP handling), including the OBS write | next poll |
| `CLK1` | `Clock.WatchAsync` | Writing the OBS clock text threw (a missing source, or OBS dropping mid-request) | next minute |
| `CLK2` | `Clock.WatchAsync` | The midnight date post threw | that day's date isn't posted; the next midnight's is |
| `ADS1` | `Ads.RunScheduleAsync` | One ad warning threw | later warnings still go out |
| `5` | `TextToSpeech.ProcessQueueAsync` | Speaking an item failed | next item |
| `UpT4` | `TwitchUptime.UptimeCheck` | The decapi.me request failed | the OBS text shows `U: ?` |
| `LOG1` | `ConsoleLogger` | Log files can't be written (once, until a write succeeds) | window only |

Not every failure has a code. A bare `_ = SomeAsync()` swallows exceptions from its body; use
`FireForget.Run` or give the body its own catch.

### Other lines worth recognising

| Line | Meaning |
|---|---|
| `Unexpected response code` | a Helix or EventSub call returned something other than the expected 202 / 200 |
| `Subscription <type> failed` | a `400` from the subscribe POST: wrong version or missing condition |
| `Unhandled channel point reward redemption id: <id>` | a reward the code has no case for |
| `Event Sub Event happened, but is not handled in code: <type>` | a subscription with no `case`, e.g. `channel.follow` |
| `Money goal: +<amount> EUR from <user>, now <total> / <goal> EUR` | a tip was counted; on `Important`, with `counted as … EUR`, when it came in another currency |
| `Test tip: not counted toward the money goal` | a `!test tip` ran the alert without counting |
| `FAQ <command> for <user>` / `…: on cooldown` | a chat line matched an FAQ command: answered, or not because that user had it within 60 s |
| `Chat: no twitchBot login or accessToken in secrets.json, chatting as <broadcaster>` | no bot-account credentials; IRC logs in with the broadcaster's |
| `StreamElements: no channelId or jwt in secrets.json, tips are off` | no `streamElements` credentials; the bot runs without tips |
| `StreamElements stopped: fix streamElements in secrets.json and restart the bot` | StreamElements refused the JWT (after `Error SES3`) |
| `Tip <id> already alerted, skipped` | the same tip arrived twice |
| `Left panel: <game>, <n> panels` | a left-panel rotation started, from a game setup or OBS connecting; `(default)` after the name when the game has no entry of its own, or `(<entry>)` when its entry names another one (`SWAT4 (Run_NoSubsplits)`), `, <n> on a split` when it has previous-split panels, then `, split colours from <category>` or `, no split colours` |
| `Watching LiveSplit's split index` | the split watcher started; `… previous-split panel off` instead when `LivesplitGenerated` isn't set or its folder is missing |
| `Left panel: <game> has groups OBS doesn't: …` | the game's entry names groups missing from both left-panel scenes; they're skipped |
| `Left panel: can't read <path>, previous-split colours left as is` | a previous-split panel came up but the entry's `Sign` file couldn't be read |
| `Split sign <sign>: layout colour <game> <scheme>` | after a split index change, a game with sign schemes (SWAT4) fades to the scheme for the previous split's sign trend; no chat line |
| `No left panels for <game>, left as is` | the game has no entry and there's no `default`; the panel isn't touched |
| `Capture setup: <name> (<id>)` | `!changecapture`: the capture sources alone are being set for that game |
| `No game or left panel "…"`, `No left panels loaded` | `!changepanel` with an input that is neither a game nor a `leftpanel.json` entry, or with no panels loaded |
| `No left panel rotation to move on`, `Only one left panel, nothing to move on to` | `!nextpanelitem` with no rotation (or a stopped one), or with one panel and no previous split |
| `Left panel rotation stopped` | `!stoppaneltimer`; the current panel stays up, and OBS reconnects don't restart it |
| `No left panel rotation to stop`, `No left panel to start` | `!stoppaneltimer` or `!startpaneltimer` with no game's rotation to act on |
| `Loaded <n> colour tables and <n> scheme sets` | the colour data loaded, at startup or on `!reloaddata`; beside `Loaded <n> games, …` and `Loaded left panels for …` |
| `games.json not found, per-game setup off`, `leftpanel.json not found, left panel off` | the file is missing at startup |
| `games.json not found, keeping the <n> games loaded`, `leftpanel.json not found, keeping the left panels loaded` | the file is missing on `!reloaddata`; what was loaded stays |
| `leftpanel.json: skipped <name>, …` | an entry naming another that leads to no panels (missing, or a loop), or a value that is neither panels nor a name |
| `Unhandled chat notice event` | a `channel.chat.notification` `notice_type` with no case |
| `Twitch: stream online (<started_at>)` | Twitch marked the channel live (`stream.online`); `started_at` is Twitch's start time, UTC |
| `Twitch: stream offline` | Twitch marked the channel offline (`stream.offline`) |
| `Trying to do an OBS action but not connected to OBS` | OBS is closed; the action was skipped |
| `Color change request FAILED: <text>` | no colour matched; the points were refunded |

## Hardcoded values

Single-user software, so these are constants in code rather than settings.

### Identity and addresses

| Value | Where |
|---|---|
| IRC host `irc.chat.twitch.tv:6667` | `TwitchIRCManager` |
| EventSub `wss://eventsub.wss.twitch.tv/ws`; subscribe URL `https://api.twitch.tv/helix/eventsub/subscriptions` | `TwitchEventSub` |
| Helix redemption URL `https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions` | `TwitchHelixApi` |
| StreamElements Astro `wss://astro.streamelements.com`, topic `channel.tips` | `StreamElementsSocket` |
| OBS `ws://127.0.0.1:4455` | `ObsConnection` |
| Uptime `https://decapi.me/twitch/uptime/`, followed by the broadcaster's login | `TwitchUptime` |
| TTS voice `Microsoft Catherine` | `TextToSpeech` |

The broadcaster's and the bot account's logins (the channel, the admins) aren't constants: they come from
`secrets.json` ([twitch.md](twitch.md#credentials-and-identity)).

### Paths

| Value | Where |
|---|---|
| `D:\Repositories\Stream-Resources\Alert Sounds\`, the only path not in `paths.json` | `Sound` |
| Drive `A:\` (falls back to the first drive) | `DiskSpace` |
| `Template.png` in `Backgrounds`, the background for a game without its own | `GameBackground` |
| `Dynamic\<game>_<n>.png` and `Dynamic\<game>_2_<n>.png` in `Sidebar`, n the split index + 1 (`_00` before a run) | `GameSidebar` |
| `Borders_Inner_<aspect>.png` and `Borders_Outer_<aspect>.png` in `Layout` (only `16x9` has them), else `Borders_Inner.png` and `Borders_Outer.png` there | `GameBorder` |
| `16x9`, the aspect a game has without an `Aspect` in `games.json` | `GameBorder`, `GameScene` |
| `games.json` and `leftpanel.json` in `BotInput` | `Games`, `LeftPanel` |
| `Number.txt` and `Completed.txt` in `CurrentSplit\AllSplits\`, `PreviousSplit\AllSplits\RealTime\RunTime.txt` and `PreviousSplit\AllSplits\RealTime\CurrentComparison\Sign.txt`, in `LivesplitGenerated` | `SplitWatcher` |
| a `PreviousSplit\<level>\RealTime\CurrentComparison\Sign.txt`, named by an entry's `Sign` in `leftpanel.json` | `LeftPanel` |
| `gameschemes.json` in `ColorSchemes`, its categories' `SplitColors`, with `none` as the fallback | `SplitColors` |
| `AssistantLogs` and `AssistantLogs\EventSubs` under `BotOutput`, `streamAssistant.db` under `BotOutput` | `ConsoleLogger`, `TestReplay` (reads `EventSubs`, including StreamElements' `se.…` files), `Database` |

### Channel-point rewards

GUID constants in [ChannelPoints.cs](../code/Twitch/ChannelPoints.cs):

| Constant | Reward |
|---|---|
| `REWARD_ID_TRAIN` | train image over the layout |
| `REWARD_ID_TOILET_FLUSH` | flush channel points (bot-created) |
| `REWARD_ID_TOILET_RETRIEVE` | get flushed points back |
| `REWARD_ID_COLOR_RANDOM` / `_SINGLE` / `_TRIPLE` | the three colour rewards (bot-created) |

Recreating a reward gives it a new GUID; update the constant and the `ProcessAdd` case together.

### OBS names

Scene, source and filter names in OBS are plain strings. Renaming one in OBS silently breaks its call.

| Name | Used in | Role |
|---|---|---|
| `!Scene: Layout 16x9`, `!Scene: Layout 5x4` (any scene named `!Scene: Layout <aspect>`) | `LayoutColoring`, `GameScene` | the layout per aspect, each holding both colour groups; the colour fade shows and hides the groups in all of them, and the game's aspect's one is shown in `Basic Stream Layout` |
| `!Layout: Colorables`, `!Layout: Colorables (Transitionary)` | `LayoutColoring` | the real group and its fade twin |
| `Border: Colorable Inner`, `Border: Colorable Outer`, `!Scene: All Colorable` | `LayoutColoring` | the three sources recoloured; the two borders' image (`file`) is also set per game by `GameBorder` |
| the same three with ` (Transitionary)` appended | `LayoutColoring`, `GameBorder` | their twins; `GameBorder` sets the two borders' image the same as the real ones |
| `Border: Colorable Inner Attachment`, `Border: Colorable Outer Attachment`, and their ` (Transitionary)` twins | `LayoutColoring` | the frame's piece on the left, recoloured like the inner and outer borders |
| `Color Correction` | `LayoutColoring` | filter on every one of them; properties `color_multiply`, `color_add` |
| `!Scene: Basics Colored` | `ChannelPoints` | scene of the train image |
| `Image: Train` | `ChannelPoints` | the train image input |
| `Image: Background` | `GameBackground` | the per-game background image input |
| `Sidebar: Dynamic 1`, `Sidebar: Dynamic 2` | `GameSidebar` | the images in `!Scene: Sidebar 5x4` (inside `!Scene: Layout 5x4`) that follow the split index, each from its own series, setting `file` |
| `Text: Stream Uptime` | `TwitchUptime` | the uptime text, `U: <h>h <mm>m`, `U: Offline` or `U: ?`, setting `text` |
| `Text: Time Of Day` | `Clock` | the clock text, `yyyy-MM-dd HH:mm`, setting `text` |
| `Text: Money Goal` | `MoneyGoal` | the money goal, `<total> / <goal> EUR`, setting `text`; you create it ([obs.md](obs.md#money-goal)) |
| `Game: Game Capture 0`–`2` | `GameCapture` | game capture sources, retargeted per game |
| `Game: Window Capture 0`–`2` | `GameCapture` | window capture sources, retargeted per game |
| `Audio 5: App Capture 0`–`2` | `GameCapture` | application audio capture, fallback, retargeted per game |
| `!Scene: Games 16x9`, `!Scene: Games 5x4` (any `!Scene: Games <aspect>` item of `Basic Stream Layout`) | `GameCapture`, `GameScene` | scenes holding those nine capture sources, framed per aspect; unused ones are hidden in each |
| `Basic Stream Layout` | `GameScene`, `GameCapture`, `ObsInputs` | the stream scene whose `!Scene: Layout <aspect>` and `!Scene: Games <aspect>` items are found by name; of each kind, the game's aspect's one is shown and the others hidden. The dashboard greys the images and texts not showing in it |
| `!Scene: Left Panel`, `!Scene: Left Panel Colored` | `LeftPanel` | the two scenes whose top-level groups the rotation shows and hides: `_LP: <name>` in `!Scene: Left Panel`, `_LPC: <name>` in the coloured one; `<name> <n>` groups are copies of `<name>` |
| `!Scene: Left Panel` as an item in `!Scene: Left Panel Colored` | `LeftPanel` | a reference copy for editing; hidden when a rotation starts |
| the text sources named in `leftpanel.json` (`Livesplit: …`) | `LeftPanel` | settings `text` and `file` |
| `Livesplit: SL Highlights Gold`, `AheadGained`, `AheadLost`, `BehindGained`, `BehindLost` | `SplitColors` | in `_LPC: Splits List Highlights`; filter `Color Correction 2`, property `color_multiply`, set when a rotation starts |
| `Livesplit: Previous Run`, `Livesplit: Previous Segment`, `Livesplit: Previous Gold` | `SplitColors` | in `_LPC: Splits Previous Values`; filter `Color Correction`, property `color_multiply`, set on each previous-split panel |

### OBS volumes

A baseline from before the bot set any volume; the capture sources now take theirs from `games.json`
([obs.md](obs.md#per-game-setup)). Read from OBS on 2026-10-08 with a read-only
query. obs-websocket keeps each volume as dB (≤ 0, below −100 is −∞) and as a linear multiplier (0–1);
`SetInputVolume(name, dB, inputVolumeDb: true)` takes dB directly.

| Source | Kind | dB | Linear |
|---|---|---|---|
| `Audio: Z2 Microphone` | microphone | −4.12 | 0.622 |
| `Audio: Z3 TwitchSpeaker` | app audio | −4.02 | 0.629 |
| `Audio: Z3 StreamerBot` | app audio | −4.05 | 0.628 |
| `Audio: Z3 Discord` | app audio | −6.99 | 0.447 |
| `Audio: Z4 Winamp` | app audio | −4.05 | 0.628 |
| `Audio: Z5 Game0`, `Game1`, `Game3` | app audio | −4.00 | 0.631 |
| `Audio: Z5 Game2` | app audio | −4.02 | 0.629 |
| `Audio: Z5 Game4` | app audio | −13.17 | 0.220 |
| `Audio: Z5 ALL (DANGER)` | desktop audio | −3.76 | 0.649 |
| `GameA: AoE2DE` | app audio | −4.02 | 0.630 |
| `Game: SS2` | game capture | −4.05 | 0.628 |
| `Game: FC2`, `Game: ME2LE` | game capture | −3.98 | 0.633 |
| `Game: HMWOA`, `Game: GTA_SA`, `Game: DetroitBH` | game capture | −3.98 | 0.632 |
| `Game: StanleyParableUD` | game capture | −3.96 | 0.634 |
| `Game: DXHR` | game capture | −4.00 | 0.631 |
| `Game: GTA-SA` | window capture | −3.96 | 0.634 |
| `Game: AS` | game capture | 0.00 | 1.000 |
| `Game: MELE_Launcher` | game capture | −5.04 | 0.560 |
| `Game: DG2` | game capture | −4.01 | 0.630 |
| `Game: DG1`, `Game: DXMD` | game capture | −4.07 | 0.626 |
| `Game: SWAT4` | game capture | −3.83 | 0.643 |
| `Game: TR123` | game capture | −3.82 | 0.644 |

Every other input with audio (window captures for chat, S Keys, Winamp, LiveSplit, the camera, …) sat at
0 dB. The new `Game: … Capture` and `Audio 5: App Capture` sources didn't exist yet.

### Timings

| Value | Where | Why |
|---|---|---|
| 3 s | IRC, EventSub (`TwitchEventSub.RetryDelay`), OBS loops | pause before reconnecting or retrying |
| 7 min | `TwitchIRCManager.SilenceTimeout` | no IRC line for this long recycles the connection (two missed pings) |
| 15 s | `TwitchIRCManager.ConnectTimeout` | longest wait for the IRC TCP connect |
| 20 s | `TwitchEventSub.KeepAliveTimeout` | keepalive timeout (Twitch's own is 10 s) |
| 15 s | `TwitchEventSub.ConnectTimeout` | longest wait for the EventSub WebSocket connect, including to `reconnect_url` |
| 10 s | `TwitchEventSub.ReconnectWelcomeTimeout` | longest wait for `session_welcome` on the `reconnect_url` socket |
| 1 s | `TwitchEventSub.OldSocketDrainLimit` | how long the old socket is read for leftover events after a reconnect |
| 2 s | `TwitchEventSub.CloseQuietlyAsync` | close handshake on the old socket |
| 30 s | `StreamElementsSocket.Times.Probe` | the subscribe is re-sent this often; each reply proves the connection is alive |
| 70 s | `StreamElementsSocket.SilenceTimeout` | no StreamElements message for this long ends the session (Astro's own pong deadline is also 70 s) |
| 15 s | `StreamElementsSocket.ConnectTimeout` | longest wait for the StreamElements WebSocket connect |
| 3 s doubling to 30 s | `StreamElementsSocket.Times.RetryBase` / `RetryCap` | pause before reconnecting after a failed session; none after a `reconnect` message |
| 2 s | `StreamElementsSocket.CleanupSession` | sending the close frame on shutdown |
| 6 s | `Tips` | let the anthem play before speaking |
| 4.2 s | `Subscriptions` | let the tribal hymn finish before speaking |
| 1 s | `HandleSubGiftNotif` | clap before the hymn |
| 66 ms ×N, 3.8 s, 3 s | `HandleCommunitySubGiftNotif` | hymn per gift, then clap, then speech |
| 10 s | `CommunityGiftSub.WaitForRecipientsAsync` | longest wait for bomb recipients |
| 30 s | `Subscriptions.GiftlessWait` | bomb recipients wait this long for their bomb before chat is told it never came |
| 1 h | `Subscriptions.SweepOldBombs` | a gift-bomb entry older than this is dropped when a new one is created |
| 5 s | `Cheers` | let the applause finish before speaking |
| 62 s | train reward | how long the image stays up |
| 5 s | `LeftPanel.OBS_WAIT` | while OBS isn't ready, the rotation checks again this often (each panel's own time is its `Seconds` in `leftpanel.json`) |
| 5.5 s, 2.5 s, 1 s | `LayoutColoring.ChangeColorAsync` | the recolour animation, nine seconds in all |
| 60 min; warnings at 5, 3, 2, 1 min before | `Ads` | assumed ad cadence |
| 5 s | `TwitchUptime.MIN_GAP` | least time between uptime requests; a poll inside it waits out the rest |
| 60 s | `ChatHandler.COOLDOWN` | a user gets the same FAQ command answered at most this often |
| 2 s | `ObsInputs.SHOWING_POLL` | how often the dashboard lists `Basic Stream Layout`, its nested scenes and groups, to see which image and text sources are showing |
| 60 s | `TwitchHelixApi` | HTTP timeout (EventSub's separate client keeps the default) |

### Thresholds

| Value | Where |
|---|---|
| 2, 15, 100 GiB | `DiskSpace` spam, warn, notify |
| 300 / 360 / 420 s, 12 / 15 / 20 s and 40 / 50 / 70 s | `ConnectionHealth.Irc` / `.EventSub` / `.StreamElements`: status colours and the health log (420 s, 20 s and 70 s are the reconnect timeouts) |
| 5000 lines | `MainWindow.MAX_LINES` |
| 32 tokens | `TripleColorParser.MAX_TOKENS` |
| 999 EUR | `MoneyGoal.DEFAULT_GOAL_CENTS`: the money goal on a first start, until `!moneygoal`; the goal and total are in `MoneyGoal.CURRENCY`, EUR |

## Chat conventions

The bot's chat messages are prefixed with an emoji that says what kind they are.

| Prefix | Meaning |
|---|---|
| 🟣 | a connection came up (`Connected`, `ES Connected`, `SE Connected`) |
| 💥 | EventSub or StreamElements went down |
| 💸 | a tip |
| 🍂 | shutdown |
| 🎁 | a gifted resub |
| 🪠 | the toilet rewards |
| 🎨 | a colour request that failed |
| ⚠️ 🚨 | disk-space alerts |
| ❌ | uptime regex failure (unreachable in practice) |
| `sssDino` | the stream's own emote, at the start of the ad-break begging messages |

Some lines carry no prefix: `Test <n>` (a first-time chatter), `YOOO BRO`, the ad warnings,
`Raid: <user> (<viewers>)`, `Stream category change [from <id>] to <id>`, and the FAQ answers,
`@<user> <answer>`.
