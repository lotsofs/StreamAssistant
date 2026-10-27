# Reference

Lookup tables: the error codes the bot logs, every setting in `settings.json` with its default, and
every value that stays a constant on purpose. Grep for the code, the key or the constant to find the
line. This file goes stale fastest, so when you change a value in code, update its row here.

Read from the code.

## Error codes

Every `catch` logs `Error <code>` on the `Error` channel and writes the exception to the log file with
`LogToFile(ex)`. The codes are grep handles, not a scheme: some are numbers, some are mnemonics.

| Code | Where | Meaning | Then |
|---|---|---|---|
| `PRG1` | `Program.StartBotAsync` | A startup step threw (message appended) | the bot stays half-started |
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
| `SET1` | `Settings.Load` (from `DataFiles.Reload`) | `settings.json: <reason>`: on `!reloaddata`, the file couldn't be read or parsed, has a key no setting has, a `null`, or a number out of range; the reason names the key. At startup the same stops the bot with a message box instead | the settings loaded before stay |
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
| `5` | `TextToSpeech.ProcessQueueAsync` | Speaking an item failed, including when `Speech.Voice` isn't an installed voice | next item |
| `UpT4` | `TwitchUptime.UptimeCheck` | The decapi.me request failed | the OBS text shows `U: ?` |
| `NCL1` | `NameColor.ChangeAsync` | `<login>: <message>`: validating an account's token or changing its chat name colour failed (a refused token, a 400 for a bad colour, Twitch unreachable); logged once until a change works again, which logs `Name colour: <login> changes again` | that account's colour stays; tried again next pass |
| `NCL2` | `NameColor.RunAsync` | A pass of the name colour loop threw outside the per-account handling | next pass |
| `LOG1` | `ConsoleLogger` | Log files can't be written (once, until a write succeeds) | window only |

Not every failure has a code. A bare `_ = SomeAsync()` swallows exceptions from its body; use
`FireForget.Run` or give the body its own catch.

### Other lines worth recognising

| Line | Meaning |
|---|---|
| `Unexpected response code` | a Helix or EventSub call returned something other than the expected 202 / 200 |
| `Subscription <type> failed` | a `400` from the subscribe POST: wrong version or missing condition |
| `Unhandled channel point reward redemption id: <id>` | a reward whose id isn't in `Rewards` |
| `Event Sub Event happened, but is not handled in code: <type>` | a subscription with no `case`, e.g. `channel.follow` |
| `Loaded settings.json, all defaults` / `…, changed <section>: <keys>; …` | `settings.json` loaded, at startup or on `!reloaddata`, and which keys differ from the defaults (`changed Directories: BotOutput, Trains; Train: Seconds`) |
| `Created <path> from settings.json.example, every setting at its default` | a start found no `settings.json` in `BotInput` and copied the example there |
| `settings.json lacks <keys>; their defaults apply (settings.json.example has every key)` | the file leaves out those sections or keys (one added by a later version, say); copy them from the example |
| `settings.json not found, keeping the settings loaded` / `…, using the defaults` | there's no `settings.json` in `BotInput` on `!reloaddata`, or at startup with no example to copy |
| `BotOutput changes after a restart` | `!reloaddata` found a new `Directories.BotOutput`; the log and the database stay where they are until the bot restarts |
| `<directory> not set in settings.json, …` | that directory is empty, so its feature is off (`Sounds not set in settings.json, <sound> not played`, `Layout not set …, borders left as is`, `LivesplitGenerated not set …, previous-split panel off`) |
| `Money goal: +<amount> <currency> from <user>, now <total> / <goal> <currency>` | a tip was counted; on `Important`, with `counted as …`, when it came in a currency other than `MoneyGoal.Currency` |
| `Test tip: not counted toward the money goal` | a `!test tip` ran the alert without counting |
| `FAQ <command> for <user>` / `…: on cooldown` | a chat line matched an FAQ command: answered, or not because that user had it within `Chat.FaqCooldownSeconds` |
| `Chat: no twitchBot login or accessToken in secrets.json, chatting as <broadcaster>` | no bot-account credentials; IRC logs in with the broadcaster's |
| `Name colour: <login>'s token lacks user:manage:chat_color, its colour stays as is` | that account's token wasn't issued with the scope; its colour isn't changed until a restart with a new token, and the dashboard shows `NO TOKEN` ([twitch.md](twitch.md#chat-name-colours)) |
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

## Settings

The owner's choices, read from `settings.json` in `BotInput` by `Settings`
([infrastructure.md § Settings](infrastructure.md#settings)). The file lists every key: a first start
copies it from [settings.json.example](../settings.json.example) in the project folder, which has them
all at the defaults listed here. A key left out keeps its default, and the log names it. Keys are read
in any case. Prefixes and suffixes are stored without the space
that joins them to the rest of a name: the code adds it.

### Directories

Every directory but `BotInput`, which is in `paths.json`. An empty one turns its feature off with a log
line (`… not set in settings.json, …`). Environment variables such as `%APPDATA%` are expanded. The
owner's all sit under `D:\Repositories\Stream-Resources\`.

| Key | Default | Holds |
|---|---|---|
| `BotOutput` | `%APPDATA%\StreamAssistant2\Bot Output` | `AssistantLogs\` (the log files and `EventSubs\`) and `streamAssistant.db`; a change on `!reloaddata` waits for a restart |
| `Trains` | empty | `Train<n>.png` and `Empty.png` (`ChannelPoints`) |
| `Colors` | empty | the colour tables (`ColorTableRegistry`) |
| `ColorSchemes` | empty | the scheme files (`ColorSchemeRegistry`) |
| `Backgrounds` | empty | per-game backgrounds and `Template.png` (`GameBackground`) |
| `LivesplitGenerated` | empty | LiveSplit's text output (`SplitWatcher`, `LeftPanel`) |
| `Layout` | empty | the border images (`GameBorder`) |
| `Sidebar` | empty | the sidebar images, the split-index ones in `Dynamic\` (`GameSidebar`) |
| `Sounds` | empty | the alert sounds (`Sound`) |

### Rewards

Channel-point reward ids, matched by `ChannelPoints.RewardOf`. Recreating a reward gives it a new id; put
the new one here.

| Key | Default | Reward |
|---|---|---|
| `Train` | `0260c648-c3ba-4ff1-920d-dffa5431da74` | train image over the layout |
| `ToiletFlush` | `cd46e822-f288-47e6-8e8c-c56155603a0e` | flush channel points (bot-created) |
| `ToiletRetrieve` | `785967e7-9b58-41eb-aa11-15fed82a72ec` | get flushed points back |
| `ColorRandom` | `bc78e213-6818-464d-8d5f-3408e9bd2413` | random layout colour (bot-created) |
| `ColorSingle` | `ad70a9d9-12fa-44dd-a3fb-dde21ebc4ec1` | one layout colour (bot-created) |
| `ColorTriple` | `30168c08-09f6-4557-ab49-b7eaed212978` | up to three layout colours (bot-created) |

### Train

| Key | Default | Role |
|---|---|---|
| `Scene` | `!Scene: Basics Colored` | the scene holding the train image |
| `Source` | `Image: Train` | the train image input |
| `Seconds` | 62 | how long the image stays up |
| `Images` | 100 | a train is a random `Train0.png` to `Train<Images − 1>.png` in `Trains` |

### Sounds

Each is `{ "File": …, "Volume": … }`: a file in `Directories.Sounds` and a volume from 0 to 1. A sound
given only one of the two keeps the other's default.

| Key | File | Volume | Played for |
|---|---|---|---|
| `TribalHymn` | `Tribal hymn.mp3` | 0.5 | subs, resubs and gifts; one per sub in a gift bomb |
| `TheClap` | `theclap.mp3` | 0.4 | gifts and gift bombs |
| `Team17Applauds` | `Team17-Applauds.mp3` | 0.75 | cheers |
| `IndianAnthem` | `IndianAnthem.mp3` | 0.4 | tips |
| `Flush` | `Flush.wav` | 0.4 | the flush reward |
| `Warning` | `Warning.wav` | 0.6 | the disk-space spam alert |

### Speech

| Key | Default | Role |
|---|---|---|
| `Voice` | `Microsoft Catherine` | the SAPI voice; selected before each utterance, so `!reloaddata` changes it, and one that isn't installed logs `Error 5` per utterance |

### DiskSpace

| Key | Default | Role |
|---|---|---|
| `Drive` | `A:\` | the drive watched (with or without the backslash); the first drive when it's missing |
| `SpamGiB`, `WarnGiB`, `NotifyGiB` | 2, 15, 100 | free space under each starts that level |
| `SpamEveryMinutes`, `WarnEveryMinutes`, `NotifyEveryMinutes` | 1, 12, 60 | how often a level repeats, on clock marks; each must divide 60 |

### Ads, Chat, MoneyGoal, Clock, Uptime, NameColor

| Key | Default | Role |
|---|---|---|
| `Ads.IntervalMinutes` | 60 | assumed time from an ad break's end to the next; the warnings go 5, 3, 2 and 1 minutes before it, so at least 5 |
| `Chat.FaqCooldownSeconds` | 60 | a user gets the same FAQ command answered at most this often |
| `MoneyGoal.Source` | `Text: Money Goal` | the OBS text, `<total> / <goal> <currency>`; you create it ([obs.md](obs.md#money-goal)) |
| `MoneyGoal.Currency` | `EUR` | the goal and total's currency; a tip in another is counted as if in this one |
| `MoneyGoal.DefaultGoal` | 999 | the goal on a first start (the database's first row), until `!moneygoal`; at least 0.01 |
| `Clock.Source` | `Text: Time Of Day` | the clock text, `yyyy-MM-dd HH:mm`, setting `text` |
| `Uptime.Source` | `Text: Stream Uptime` | the uptime text, `U: <h>h <mm>m`, `U: Offline` or `U: ?`, setting `text` |
| `NameColor.Enabled` | `true` | `false` leaves both chat name colours alone (`OFF` on the dashboard); read each pass, so `!reloaddata` switches it after the current wait |
| `NameColor.IntervalSeconds` | 60 | how often the broadcaster's and the bot's chat name colours change; at least 5. A change on `!reloaddata` applies after the current wait |

### Layout

OBS names are plain strings, here and in the sections below. Renaming one in OBS silently breaks its call
until the setting is changed too.

| Key | Default | Used in | Role |
|---|---|---|---|
| `StreamScene` | `Basic Stream Layout` | `GameScene`, `GameCapture`, `ObsInputs` | the stream scene, whose layout and games scene items are found by name; of each kind, the game's aspect's one is shown and the others hidden. The dashboard greys the images and texts not showing in it |
| `LayoutScenePrefix` | `!Scene: Layout` | `LayoutColoring`, `GameScene` | `<prefix> <aspect>` (`!Scene: Layout 16x9`, `… 5x4`): the layout per aspect, each holding both colour groups; the colour fade shows and hides the groups in all of them |
| `GamesScenePrefix` | `!Scene: Games` | `GameCapture`, `GameScene` | `<prefix> <aspect>`: the scenes holding the nine capture sources, framed per aspect; unused ones are hidden in each |
| `Colorables` | `!Layout: Colorables` | `LayoutColoring` | the real colour group; its twin is the fade group |
| `TwinSuffix` | `(Transitionary)` | `LayoutColoring`, `GameBorder`, `ObsInputs` | `<name> <suffix>` is a source's or group's fade twin |
| `InnerBorder`, `OuterBorder` | `Border: Colorable Inner`, `Border: Colorable Outer` | `LayoutColoring`, `GameBorder` | recoloured; their image (`file`), and their twins', is also set per game |
| `InnerAttachment`, `OuterAttachment` | `Border: Colorable Inner Attachment`, `Border: Colorable Outer Attachment` | `LayoutColoring` | the frame's piece on the left, recoloured like the inner and outer borders |
| `TextScene` | `!Scene: All Colorable` | `LayoutColoring` | recoloured with the text colour |
| `Filter` | `Color Correction` | `LayoutColoring` | the filter on every one of them and their twins; properties `color_multiply`, `color_add` |
| `ShowTwinsSeconds`, `HideRealSeconds`, `RecolorSeconds` | 5.5, 2.5, 1 | `LayoutColoring` | the fade's waits: after showing the twins, after hiding the real group, after recolouring it |

### Game

| Key | Default | Used in | Role |
|---|---|---|---|
| `BackgroundSource` | `Image: Background` | `GameBackground` | the per-game background image input |
| `SidebarSources` | `Sidebar: Dynamic 1`, `Sidebar: Dynamic 2` | `GameSidebar` | the images in `!Scene: Sidebar 5x4` (inside `!Scene: Layout 5x4`) that follow the split index, setting `file`; by position they show `<game>_<n>.png`, `<game>_2_<n>.png`, `<game>_3_<n>.png`, … |
| `GameCapturePrefix`, `WindowCapturePrefix`, `AudioCapturePrefix` | `Game: Game Capture`, `Game: Window Capture`, `Audio 5: App Capture` | `GameCapture` | `<prefix> <slot>`: the game, window and application audio capture sources, retargeted per game |
| `CaptureSlots` | 3 | `GameCapture` | slots of each kind, numbered from 0 |

### LeftPanel

| Key | Default | Role |
|---|---|---|
| `Scene`, `ColoredScene` | `!Scene: Left Panel`, `!Scene: Left Panel Colored` | the two scenes whose top-level groups the rotation shows and hides. `Scene` as an item in `ColoredScene` is a reference copy for editing, hidden when a rotation starts |
| `Prefix`, `ColoredPrefix` | `_LP:`, `_LPC:` | `<prefix> <name>`: a group in `Scene` and in `ColoredScene`; `<name> <n>` groups are copies of `<name>` |

The text sources the rotation sets (`Livesplit: …`, settings `text` and `file`) are named in `leftpanel.json`.

### SplitColors

| Key | Default | Role |
|---|---|---|
| `HighlightGroup` | `Splits List Highlights` | the left-panel group holding the split list highlights |
| `HighlightFilter` | `Color Correction 2` | their filter, property `color_multiply`, set when a rotation starts |
| `HighlightGold`, `HighlightAheadGained`, `HighlightAheadLost`, `HighlightBehindGained`, `HighlightBehindLost` | `Livesplit: SL Highlights Gold`, `… AheadGained`, `… AheadLost`, `… BehindGained`, `… BehindLost` | the highlights, coloured `ListGold`, `ListGainedAhead`, `ListLostAhead`, `ListGainedBehind`, `ListLostBehind` |
| `PreviousGroup` | `Splits Previous Values` | the left-panel group holding the previous split's differences |
| `PreviousFilter` | `Color Correction` | their filter, property `color_multiply`, set on each previous-split panel |
| `PreviousRun`, `PreviousSegment`, `PreviousGold` | `Livesplit: Previous Run`, `… Segment`, `… Gold` | the previous split's differences |

## Constants

Values that stay in code: protocol addresses and timings, internal polls and limits, data-file formats,
and the texts and the delays matched to sounds (tracked as
[TXT](robustness-fixes.md#txt--texts-and-sound-delays-as-settings)).

### Identity and addresses

| Value | Where |
|---|---|
| IRC host `irc.chat.twitch.tv:6667` | `TwitchIRCManager` |
| EventSub `wss://eventsub.wss.twitch.tv/ws`; subscribe URL `https://api.twitch.tv/helix/eventsub/subscriptions` | `TwitchEventSub` |
| Helix redemption URL `https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions` | `TwitchHelixApi` |
| Token validation `https://id.twitch.tv/oauth2/validate`; chat colour `https://api.twitch.tv/helix/chat/color`, scope `user:manage:chat_color` | `TwitchHelixApi`, `NameColor` |
| Twitch's named chat colours, the only ones an account without Turbo or Prime may set, with the hex Twitch shows for each: `blue`, `blue_violet`, `cadet_blue`, `chocolate`, `coral`, `dodger_blue`, `firebrick`, `golden_rod`, `green`, `hot_pink`, `orange_red`, `red`, `sea_green`, `spring_green`, `yellow_green`; the bot account picks from all of them. The `twitch` colour table holds the same | `NameColor.Named` |
| StreamElements Astro `wss://astro.streamelements.com`, topic `channel.tips` | `StreamElementsSocket` |
| OBS `ws://127.0.0.1:4455` | `ObsConnection` |
| Uptime `https://decapi.me/twitch/uptime/`, followed by the broadcaster's login | `TwitchUptime` |

The broadcaster's and the bot account's logins (the channel, the admins) aren't constants: they come from
`secrets.json` ([twitch.md](twitch.md#credentials-and-identity)).

### Paths

| Value | Where |
|---|---|
| `paths.json` in the working directory, holding only `BotInput`; `secrets.json`, `settings.json`, `games.json` and `leftpanel.json` in `BotInput` | `Config`, `Settings`, `Games`, `LeftPanel` |
| `Template.png` in `Backgrounds`, the background for a game without its own | `GameBackground` |
| `Dynamic\<game>_<n>.png` and `Dynamic\<game>_2_<n>.png` in `Sidebar`, n the split index + 1 (`_00` before a run) | `GameSidebar` |
| `Borders_Inner_<aspect>.png` and `Borders_Outer_<aspect>.png` in `Layout` (only `16x9` has them), else `Borders_Inner.png` and `Borders_Outer.png` there | `GameBorder` |
| `16x9`, the aspect a game has without an `Aspect` in `games.json` | `GameBorder`, `GameScene` |
| `Train<n>.png` and `Empty.png` in `Trains` | `ChannelPoints` |
| `Number.txt` and `Completed.txt` in `CurrentSplit\AllSplits\`, `PreviousSplit\AllSplits\RealTime\RunTime.txt` and `PreviousSplit\AllSplits\RealTime\CurrentComparison\Sign.txt`, in `LivesplitGenerated` | `SplitWatcher` |
| a `PreviousSplit\<level>\RealTime\CurrentComparison\Sign.txt`, named by an entry's `Sign` in `leftpanel.json` | `LeftPanel` |
| `gameschemes.json` in `ColorSchemes`, its categories' `SplitColors`, with `none` as the fallback | `SplitColors` |
| `AssistantLogs` and `AssistantLogs\EventSubs` under `BotOutput`, `streamAssistant.db` under `BotOutput` | `ConsoleLogger`, `TestReplay` (reads `EventSubs`, including StreamElements' `se.…` files), `Database` |

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
| 6 s | `Tips` | let the anthem play before speaking (TXT) |
| 4.2 s | `Subscriptions` | let the tribal hymn finish before speaking (TXT) |
| 1 s | `HandleSubGiftNotif` | clap before the hymn (TXT) |
| 66 ms ×N, 3.8 s, 3 s | `HandleCommunitySubGiftNotif` | hymn per gift, then clap, then speech (TXT) |
| 10 s | `CommunityGiftSub.WaitForRecipientsAsync` | longest wait for bomb recipients |
| 30 s | `Subscriptions.GiftlessWait` | bomb recipients wait this long for their bomb before chat is told it never came |
| 1 h | `Subscriptions.SweepOldBombs` | a gift-bomb entry older than this is dropped when a new one is created |
| 5 s | `Cheers` | let the applause finish before speaking (TXT) |
| 5 s | `LeftPanel.OBS_WAIT` | while OBS isn't ready, the rotation checks again this often (each panel's own time is its `Seconds` in `leftpanel.json`) |
| 5, 3, 2, 1 min before the next ad | `Ads` | the ad warnings, each with its text (TXT); the interval is `Ads.IntervalMinutes` |
| 5 s | `TwitchUptime.MIN_GAP` | least time between uptime requests; a poll inside it waits out the rest |
| 2 s | `ObsInputs.SHOWING_POLL` | how often the dashboard lists the stream scene, its nested scenes and groups, to see which image and text sources are showing |
| 60 s | `TwitchHelixApi` | HTTP timeout, for the broadcaster's calls and the name colours' (EventSub's separate client keeps the default) |

### Thresholds

| Value | Where |
|---|---|
| 300 / 360 / 420 s, 12 / 15 / 20 s and 40 / 50 / 70 s | `ConnectionHealth.Irc` / `.EventSub` / `.StreamElements`: status colours and the health log (420 s, 20 s and 70 s are the reconnect timeouts) |
| 5000 lines | `MainWindow.MAX_LINES` |
| 32 tokens | `TripleColorParser.MAX_TOKENS` |

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
