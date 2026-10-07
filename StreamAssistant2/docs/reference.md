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
| `3` | `FireForget.Run` (from `TwitchIRCManager.SendMessage`) | Writing a chat message failed | message dropped |
| `7` | `ChatHandler.ProcessMessage` | A raw IRC line couldn't be parsed, **or** a command handler threw synchronously (including the colour commands) | line dropped |
| `TES1` | `TwitchEventSub.StartConnectionLoop` | The EventSub session ended with an exception; suffix is the `SessionExitReason` | cleanup, 3 s, reconnect |
| `TES2` | `TwitchEventSub.CleanupSession` | Closing the old socket threw | continues |
| `TES3` | `TwitchEventSub.StartConnectionLoop` | The listen loop exited without setting a reason | cleanup, reconnect |
| `TEH1` | `TwitchEventHandler.Handle` | The dispatch itself threw synchronously (not the handlers, which `Run` wraps) | event dropped |
| `TEH2` | `FireForget.Run` (from `TwitchEventHandler`) | An async handler threw, before or after its first `await`; the message names the event or notice type | rest of that alert skipped |
| `SUB1` | `Subscriptions.HandleCommunitySubGiftNotif` | The gift-bomb announcement threw (the only handler with its own catch) | no announcement |
| `CP1` | `ChannelPoints.CloseColorRedemption` | Helix refused to fulfil or cancel a colour redemption (often a 403; see [color.md](color.md)) | redemption left open |
| `Obs29` | `LayoutColoring.ProcessColorChangeQueueAsync` | An OBS call threw during a colour change; the message is logged next | worker moves to the next request |
| `OBSException58` | `ObsConnection.Loop` | `ConnectAsync` threw synchronously | retries in 3 s |
| `DSK1` | `DiskSpace.WatchAsync` | A disk-space check threw | next minute |
| `UpT5` | `TwitchUptime.UptimeLoopAsync` | An uptime check threw (outside its own HTTP handling) | next poll |
| `UpT6` | `TwitchUptime.DatePostLoopAsync` | The midnight loop threw | retries after 1 s |
| `ADS1` | `Ads.RunScheduleAsync` | One ad warning threw | later warnings still go out |
| `5` | `TextToSpeech.ProcessQueueAsync` | Speaking an item failed | next item |
| `UpT4` | `TwitchUptime.UptimeCheck` | The decapi.me request failed | value set to `Error` |
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
| `Unhandled chat notice event` | a `channel.chat.notification` `notice_type` with no case |
| `Trying to do an OBS action but not connected to OBS` | OBS is closed; the action was skipped |
| `Color change request FAILED: <text>` | no colour matched; the points were refunded |
| `Attempted to read uptime twice in 5 seconds` | the uptime rate limit |

## Hardcoded values

Single-user software, so these are constants in code rather than settings.

### Identity and addresses

| Value | Where |
|---|---|
| Channel `#lotsofs`, IRC nick `lotsofs` | `TwitchIRCManager` |
| IRC host `irc.chat.twitch.tv:6667` | `TwitchIRCManager` |
| EventSub `wss://eventsub.wss.twitch.tv/ws`; subscribe URL `https://api.twitch.tv/helix/eventsub/subscriptions` | `TwitchEventSub` |
| Helix redemption URL `https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions` | `TwitchHelixApi` |
| OBS `ws://127.0.0.1:4455` | `ObsConnection` |
| Uptime `https://decapi.me/twitch/uptime/lotsofs` | `TwitchUptime` |
| Admin logins `lotsofs`, `botsofs` | `ChatHandler.HandlePrivMsg` |
| TTS voice `Microsoft Catherine` | `TextToSpeech` |

### Paths

| Value | Where |
|---|---|
| `D:\Repositories\Stream-Resources\Alert Sounds\`, the only path not in `paths.json` | `Sound` |
| Drive `A:\` (falls back to the first drive) | `DiskSpace` |
| `Tests` under `Directories.BotInput`, `AssistantLogs` and `AssistantLogs\Custom` under `BotOutput`, `streamAssistant.db` under `BotOutput` | `TwitchEventSub`, `ConsoleLogger`, `Database` |

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
| `!Scene: Layout` | `LayoutColoring` | scene holding both colour groups |
| `!Layout: Colorables`, `!Layout: Colorables (Transitionary)` | `LayoutColoring` | the real group and its fade twin |
| `Border: Colorable Inner`, `Border: Colorable Outer`, `!Scene: All Colorable` | `LayoutColoring` | the three sources recoloured |
| the same three with ` (Transitionary)` appended | `LayoutColoring` | their twins |
| `Color Correction` | `LayoutColoring` | filter on every one of them; properties `color_multiply`, `color_add` |
| `!Scene: Basics Colored` | `ChannelPoints` | scene of the train image |
| `Image: Train` | `ChannelPoints` | the train image input |

### Timings

| Value | Where | Why |
|---|---|---|
| 3 s | IRC, EventSub, OBS loops | pause before reconnecting or retrying |
| 20 s | `TwitchEventSub.ListenLoop` | keepalive timeout (Twitch's own is 10 s) |
| 4.2 s | `Subscriptions` | let the tribal hymn finish before speaking |
| 1 s | `HandleSubGiftNotif` | clap before the hymn |
| 66 ms ×N, 3.8 s, 3 s | `HandleCommunitySubGiftNotif` | hymn per gift, then clap, then speech |
| 10 s | `CommunityGiftSub.WaitForRecipientsAsync` | longest wait for bomb recipients |
| 5 s | `Cheers` | let the applause finish before speaking |
| 62 s | train reward | how long the image stays up |
| 5.5 s, 2.5 s, 1 s | `LayoutColoring.ChangeColorAsync` | the recolour animation, nine seconds in all |
| 60 min; warnings at 5, 3, 2, 1 min before | `Ads` | assumed ad cadence |
| 5 s | `TwitchUptime` | uptime request rate limit |
| 60 s | `TwitchHelixApi` | HTTP timeout (EventSub's separate client keeps the default) |

### Thresholds

| Value | Where |
|---|---|
| 2, 15, 100 GiB | `DiskSpace` spam, warn, notify |
| 300 / 360 / 420 s and 10 / 12 / 15 s | `MainWindow` status colours |
| 5000 lines | `MainWindow.MAX_LINES` |
| 32 tokens | `TripleColorParser.MAX_TOKENS` |

## Chat conventions

The bot's chat messages are prefixed with an emoji that says what kind they are.

| Prefix | Meaning |
|---|---|
| 🟣 | a connection came up (`Connected`, `ES Connected`) |
| 💥 | EventSub went down |
| 🍂 | shutdown |
| 🎁 / 💣 | a gifted resub / one recipient of a gift bomb |
| 🪠 | the toilet rewards |
| 🎨 | a colour request that failed |
| ⚠️ 🚨 | disk-space alerts |
| ❌ | uptime regex failure (unreachable in practice) |
| `sssDino` | the stream's own emote, at the start of the ad-break begging messages |
