# Robustness fixes

Known defects and loose ends in the tree as it stands, each with a plan. Mostly independent of each
other: each can be done, tested and committed on its own, except where an item says otherwise.

Items are indexed in [TODO.md](TODO.md): summaries, severities and the suggested order of work live
there, along with the conventions these codes follow. This file holds the detail and the plans.

**Dependencies between plans**

- **NCK** and **TOK** both need the bot account's user id and send `twitchBot.clientId`; TOK's validate
  call returns the user id, so whichever comes second can reuse it.

Each plan ends with the docs to touch. When an item is finished, delete its section here and its row in
TODO.md, and log it in [DONE.md](DONE.md), per the conventions there.

---

## TOK — Refresh the Twitch tokens at runtime

`secrets.json` holds two Twitch accounts in the same shape (`login`, `accessToken`, `refreshToken`,
`clientId`): `twitchAuth`, the broadcaster, whose token EventSub and Helix use, and `twitchBot`, whose
token IRC uses. Nothing reads either `refreshToken`, or `twitchBot`'s `clientId`. Each access token is
used until it stops working, and the expected symptoms are in
[twitch.md § Credentials and identity](twitch.md#credentials-and-identity): IRC retrying every 3 s
(`Error 1` / `Error TIRC2`) for the bot's token, and EventSub restarting every 3 s on a 401
(`Error TES1`) for the broadcaster's, with the redemption PATCH failing too. The fix today is to replace
the token in `secrets.json` by hand and restart the bot. Read only; no expiry has been seen.

### Plan

1. Find out where the tokens come from. The `clientId` names the app: a confidential client needs its
   client secret to refresh (it would join `secrets.json`); a public client refreshes with the client id
   alone, and each refresh token works once; a token-generator site has its own refresh endpoint. That
   decides the refresh call.
2. Validate each token at startup and then hourly, as Twitch asks: `GET https://id.twitch.tv/oauth2/validate`
   with `Authorization: OAuth <token>` returns `login`, `scopes` and `expires_in`. Log the time left, and
   warn when the login doesn't match the block's or a scope is missing (`chat:read`, `chat:edit` for the bot).
3. Refresh a token before it expires, and on a 401 from EventSub or Helix or an IRC `Login authentication
   failed` notice: `POST https://id.twitch.tv/oauth2/token` with `grant_type=refresh_token` and the refresh
   token (plus what step 1 found). Use the new token without a restart: IRC reconnects with it, and
   EventSub and Helix send it from their next request.
4. Write the new access and refresh tokens back to `secrets.json`, changing only those two values, so a
   restart uses them. Never log them.
5. Keep the decisions pure and tested: whether a refresh is due, parsing the validate and token responses,
   and the rewrite of the two values.

**Verify:** F5 logs the time left on both tokens. Lower the refresh threshold to force a refresh: the
new token is used (IRC reconnects as `botsofs`, EventSub resubscribes or carries on) and `secrets.json`
holds it afterwards.

**Docs when done:** delete this section and the TODO row; update the credentials table, the expiry
paragraph and the TOK links in [twitch.md](twitch.md#credentials-and-identity),
[infrastructure.md § Config](infrastructure.md#config) (the bot now writes `secrets.json`), the
"never refreshed at runtime" sentence in CLAUDE.md, and [reference.md](reference.md) for new log lines,
error codes and URLs.

## ESR — EventSub reconnect path unverified

Twitch sends `session_reconnect` about once a day, around 19:00 to 19:40 local time. The bot used to abort
the old socket, wait 3 s and connect to `reconnect_url`; Twitch refused that every time with
`4007 "invalid reconnect attempt"` (seen in the logs on 2026-10-07, 19:40), and the bot fell back to a fresh
session with about 7 s of lost events and two disconnect/connect pairs in chat.

The bot now follows Twitch's documented flow: the new socket is opened while the old one stays open, there
is no resubscribe, and the old socket is drained, then closed. See
[twitch.md § Planned reconnects](twitch.md#planned-reconnects) and the charts in
[eventsub-flow.md](eventsub-flow.md). A `409` on subscribe now counts as success.

Tested against a loopback fake of Twitch: the helpers in `EventSubReconnectTests`, and the real
`StartConnectionLoop` in `EventSubLoopTests` (fresh session subscribes and delivers; a reconnect switches
sockets without resubscribing, delivers an event left on the old socket and posts nothing; a 4007 refusal
falls back to a fresh session; 409 keeps the session; 500 ends it).

**Reproduced live on 2026-10-08, 19:12:** `EventSub sent session_reconnect` → `Connecting to
wss://cell-a…` → `EventSub reconnected` 0.4 s later → `EventSub old connection closed (0 events delivered
during the switch, close none)` 1 s after that. There was no 4007, nothing in chat and no resubscribe, and the new
session then stayed up on keepalives for 65 minutes. So the 4007 was caused by the old socket being gone, and
that part is fixed.

**Reproduced again on 2026-10-09, 19:17**, with the same timings: reconnected 0.4 s after
`session_reconnect`, old connection closed 1 s later, 0 events during the switch, close none, nothing in chat.

**Both reconnects kept the session ID.** The new socket's `session_welcome` carried the same id as the old
session (10-08: `AgoQQmqx…`, 10-09: `AgoQJP7b…`, each logged at its `EventSub reconnected` line). Each
subscription is bound to a session id (`transport.session_id` in the subscribe responses), so this fits the
subscriptions carrying over. It is not proof: no event has arrived on a reconnected session yet.

**Why waiting hasn't worked:** the reconnect comes at about 19:10 to 19:40, off-stream, when nothing
happens on the channel. Neither reconnected session saw a single notification. Both ended in a Twitch
connection reset (now logged as `ConnectionLost`, see [twitch.md § Session lifecycle](twitch.md#session-lifecycle)): 10-08 after 65 min,
10-09 after 2 h 07 min. Each was followed by a fresh session and a full resubscribe.

**Next check (owner):** after the next `EventSub reconnected` line, and before that session ends, change the
stream **title** (not the category) in the Twitch dashboard. That fires `channel.update`. A title-only change
does nothing in the bot (`Games.HandleUpdate` acts only on a category change), but the event is saved as
`channel.update_<time>.log` in `AssistantLogs\EventSubs\`. If a file appears with a timestamp after the
reconnect, the subscriptions carried over and this item is done. If no file appears, check that a title change
on a fresh session does save one; if it does, the reconnect lost the subscriptions.

**Docs when done:** delete this section and the TODO row; drop the "not yet seen working" sentence in
[twitch.md § Planned reconnects](twitch.md#planned-reconnects).

## DCD — Connection timeouts unverified live

Both transports now time out a connection that goes silent without closing, and a connect that hangs:

| Constant | Value | Effect |
|---|---|---|
| `TwitchIRCManager.SilenceTimeout` | 7 min | no IRC line → `Error TIRC2`, `Error 1`, reconnect |
| `TwitchIRCManager.ConnectTimeout` | 15 s | hung IRC connect → `Error 1`, retry |
| `TwitchEventSub.KeepAliveTimeout` | 20 s | no EventSub message → session ends with `KeepAliveTimeout`, reconnect |
| `TwitchEventSub.ConnectTimeout` | 15 s | hung EventSub connect → `Error TES1`, retry |

The helpers are unit-tested against loopback servers (`IrcReadTimeoutTests`, `IrcConnectTimeoutTests`,
`EventSubReceiveTimeoutTests`, `EventSubConnectTimeoutTests`). The EventSub keepalive timeout has now been
seen live: on 2026-10-09 at 03:28, `EventSub quiet for 00:15, reconnecting at 00:20` was followed about 4 s
later by `EventSub session ended: KeepAliveTimeout (socket Aborted, close none)`. A fresh session, a full
resubscribe and `EventSub recovered after 00:19 of silence` followed within 4 s. The cause was not recorded.
The IRC silence timeout and both connect timeouts have not been seen against Twitch: they need a
connection that goes quiet without a TCP close.

Reading `keepalive_timeout_seconds` from the welcome message was considered and dropped: the bot never
asks for a non-default keepalive, and there is no reason to (raising it only slows dead-socket detection;
10 s is Twitch's minimum).

### Verify live (owner, off-stream)

1. Temporarily lower the timeouts: `SilenceTimeout` to 30 s, `KeepAliveTimeout` to 12 s (keep it above
   10 s or normal keepalive gaps will trip it).
2. Start the bot, then block outbound traffic to Twitch without closing the sockets: a Windows Firewall
   outbound block rule on the bot's process.
3. Expect: the EventSub age turns orange with one `EventSub quiet for …` line (connection health), then
   red and, at the timeout, `EventSub session ended: KeepAliveTimeout`, then
   reconnect attempts that fail (`Error TES1: None` and `Error TES3` after 15 s each while blocked, since a
   failed connect sets no exit reason). Likewise IRC: `Error TIRC2` with a `TimeoutException` in the file
   log, then `Error 1` retries.
4. Remove the rule: both reconnect, the chat gets `🟣 Connected` / `🟣 ES Connected`, and each
   connection logs one `… recovered after …` line. Lifting the block before the timeout instead should
   give the recovery line with no reconnect.
5. Restore the constants.

**Docs when done:** delete this section and the TODO row; drop "the IRC one hasn't yet" from the bullet
in [gotchas.md](gotchas.md#twitch-and-helix).

---

## TSS — Tests that share static state run in parallel

xUnit runs test collections in parallel, and some classes in different collections share static state:

- **Left-panel state and log lines.** `GamesGoLiveTests` (`GamesStateCollection`) runs `!changepanel GTA3`
  with no panels loaded and asserts `No left panels loaded` and no `Left panel:` line.
  - `LeftPanelStopTests` (`ColorDataCollection`) loads panels into `LeftPanel`'s static set for each test
    and starts rotations that log `Left panel: GTA3, …`.
  - `LeftPanelRotationTests` (no collection) log `Left panel: … has groups OBS doesn't` and
    `Left panel: can't read …`.
  - The games test captures lines through the global `ConsoleLogger.LineLogged`. So any of those lines
    logged in its window fails it, and so do panels that happen to be loaded at that moment.
- **The colour registries.** `GamesGoLiveTests` reach `Games.Apply`, whose colour step (`GameColor.Set`)
  reads `ColorSchemeRegistry.Sets`. Meanwhile `ColorDataCollection` tests may be reloading it
  (`Sets.Clear()`, then refill). A read during the refill can throw (caught and logged as `Error GMS2`) or
  find nothing.

Read only: every run so far has passed.

### Plan

1. Put every class that touches `LeftPanel`, `Games` or the colour registries in one collection. Move
   `GamesGoLiveTests`, `GamesTests` and the `LeftPanel*Tests` classes into `ColorDataCollection` (or rename
   it to something broader), and drop `GamesStateCollection`. They then run one at a time. Check the
   suite's total time stays about the same; the slow tests are the connection loops, which stay outside.
2. The alternative, filtering the games test's captured lines to the ones it causes, fixes the log lines
   but not the shared panel set, so it isn't enough on its own.

**Verify:** run the suite 20 times in a loop with no failures.

**Docs when done:** delete this section and the TODO row; update the collections note in
[development.md](development.md) and [color.md § Tests and verification](color.md#tests-and-verification)
if they name the collection's members.

---


## DSB — Unused `disabled` colour category

`countryschemes.json` (in `Stream-Resources\Colors\Schemes\`, the Stream-Resources repo) has a `disabled`
category, with `none`'s black-and-white scheme and all-white split colours; `gameschemes.json`'s is gone
already. Under Streamer.Bot, `disabled` was the default of `currentGame` while the bot's game features were
off ("Disabling bot stuff for %currentGame%").

The bot uses only `gameschemes`' `none`: as the `None` game's colour and as the split-colour fallback, which
is always read from `gameschemes`. `disabled` can only be reached by typing it (`!changecolor disabled`).
`countryschemes`' own `none` is unused too; its split colours are all white where `gameschemes`' are not,
but a lookup without a set name, which may find either `none`, only takes the scheme, and that is the same.
Read only.

### Plan

Delete `disabled` and `none` from `countryschemes.json`, keeping `gameschemes`' `none`.

**Verify:** F5 logs no new colour-scheme lines, `!changecolor none` still works, and the real-data smoke
tests pass.

**Docs when done:** delete this section and the TODO row.

## NCK — Change the chat name colour every 60 seconds

Wanted: the bot's name in chat changes colour every 60 seconds. Not built.

Twitch no longer takes `/color` over IRC, so it goes through Helix's Update User Chat Color:
`PUT https://api.twitch.tv/helix/chat/color?user_id=<id>&color=<color>`, with that account's own token
and the scope `user:manage:chat_color` (the URL and scope are from memory of Twitch's API reference;
confirm them before building). Any account can use the named colours: `blue`, `blue_violet`,
`cadet_blue`, `chocolate`, `coral`, `dodger_blue`, `firebrick`, `golden_rod`, `green`, `hot_pink`,
`orange_red`, `red`, `sea_green`, `spring_green` and `yellow_green`. A hex colour needs Turbo or Prime
on the account. A new colour shows on messages sent after the change, so viewers only see it when the
bot speaks.

### Decisions

- **Whose name:** the bot account's (`botsofs`), presumably.
- **Which colours:** a random named colour, a cycle through them, or the layout's current colour (hex,
  so only with Turbo or Prime on that account).
- **When:** always, or only while live.

### Plan

1. Get the bot a new token with `user:manage:chat_color` added to `chat:read` and `chat:edit`. The
   call sends `twitchBot.clientId` as `Client-Id`, which must be the app the token was issued to. It
   also needs the bot's user id, which `secrets.json` lacks. Look it up once at startup, through
   `GET https://id.twitch.tv/oauth2/validate` (its `user_id`) or `GET /helix/users?login=<login>`.
2. Add a `NickColor` loop, started from `Program.StartBotAsync` and written like `Clock`. Every 60 s it
   picks the next colour with a pure, tested function and sends it. A failure logs a new `Error` code,
   and the loop carries on. The call goes in `TwitchHelixApi` and uses the bot's token, not the
   broadcaster's.
3. Make the interval and the colour list settings, in a section of [SettingsModel.cs](../code/Core/SettingsModel.cs)
   with today's choice as the default ([infrastructure.md § Settings](infrastructure.md#settings)).

**Verify:** F5. The log shows each change, and bot lines a minute or more apart (`!changecolorrandom`'s,
say) come in different colours.

**Docs when done:** delete this section and the TODO row. Then update:
- [twitch.md](twitch.md#credentials-and-identity): the scope, the bot's user id and the Helix call
- the loop lists in [infrastructure.md](infrastructure.md), [architecture.md](architecture.md) and
  CLAUDE.md's Scheduling paragraph
- [reference.md](reference.md): the URL, the interval and the error code
- the test row in [development.md](development.md#unit-tests)

## DIR — Ask for the BotInput folder at first boot

Wanted by the owner on 2026-10-25: at first boot, prompt for the `BotInput` folder rather than needing
`paths.json` edited by hand. Not built.

`paths.json` holds only `BotInput`; every other directory is in `settings.json` there
([infrastructure.md § Config](infrastructure.md#config)). When `paths.json` is missing, the csproj's
`BeforeBuild` target copies `paths.json.example`, whose `BotInput` is `%APPDATA%\StreamAssistant2\Bot Input`.
`Config.Load` then creates that folder, copies the secrets template into it and stops with a message box.
So a fresh install runs from `%APPDATA%` unless the user edits `paths.json`.

### Decisions

- **What counts as a first boot:** only a missing `paths.json`, or also a `BotInput` that doesn't exist
  or holds no `secrets.json`.
- **The build-time copy:** drop the `BeforeBuild` target, so a missing `paths.json` means a first boot;
  or keep it and ask while `BotInput` is still the example's value.

### Plan

1. In `Main`, before `Config.Load()`, on a first boot: show WPF's `Microsoft.Win32.OpenFolderDialog` (new
   in .NET 8), starting at `%APPDATA%\StreamAssistant2\Bot Input`. Cancelling exits.
2. Write `paths.json` with the chosen folder as its only key, then carry on into `Config.Load()`, which
   creates `secrets.json` there from the template as it does today.
3. Keep the decision (is this a first boot?) in a pure function, and test it.

**Verify:** with `paths.json` moved away, F5 shows the folder picker. Picking a folder writes `paths.json`
and stops at the `secrets.json` message; cancelling exits. Put the file back.

**Docs when done:** delete this section and the TODO row; [infrastructure.md § Config](infrastructure.md#config),
CLAUDE.md's config files, the prerequisites in [development.md](development.md), and
[gotchas.md](gotchas.md#startup-and-environment).

## SED — Edit the settings in the app

Wanted by the owner on 2026-10-25: change the settings from the bot's window rather than by editing
`settings.json`. Not built.

Every setting is in `settings.json` in `BotInput`, created from `settings.json.example` at the first
start, edited by hand and applied with `!reloaddata` ([infrastructure.md § Settings](infrastructure.md#settings)).
`Settings.Parse` already rejects a misspelt key, a `null` or a number out of range, naming the key.

### Decisions

- **Where:** a settings window opened from the dashboard, or a panel in it
  ([dashboard.md](dashboard.md#adding-a-panel)).
- **Saving:** writing the file back with `System.Text.Json` drops its comments and layout. Either keep the
  descriptions in the editor (attributes on `SettingsModel`'s properties) and write the file plain, or
  change only the edited values in the file's text.
- **Applying:** a save reloads at once, as `!reloaddata` does for the settings, or waits for a button.

### Plan

1. Build the form from `SettingsModel` by reflection: a group per section, a field per key. Text for
   names and ids, a folder picker for directories, numbers checked as `Settings.CheckRanges` does.
2. On save, check the result through `Settings.Parse` before writing, so the file is never left broken;
   then write it and reload. Keep a changed `BotOutput`'s "after a restart" rule.
3. Keep the mapping between the model and the form pure, and test it.

**Verify:** change an OBS name and a timing in the editor and save: the log shows `Loaded settings.json,
changed …`, both take effect, and the file holds them. An out-of-range value is refused, with the reason,
before anything is written.

**Docs when done:** delete this section and the TODO row; [dashboard.md](dashboard.md),
[infrastructure.md § Settings](infrastructure.md#settings), CLAUDE.md's dashboard and config-files
paragraphs, and the "Change a setting" recipe in [development.md](development.md).

## TXT — Texts and sound delays as settings

Wanted by the owner on 2026-10-25: look later at making the chat and spoken texts, and the delays
matched to sounds, settings too. The owner's other values are settings in `settings.json`
([infrastructure.md § Settings](infrastructure.md#settings)); these stayed in code. Not built.

What is still in code:
- **Texts:**
  - the FAQ commands (`ChatHandler._commands`: name, regex, answer)
  - the ad warnings and nags (`Ads`), with their 5, 3, 2 and 1-minute schedule
  - the train, flush and retrieve chat lines (`ChannelPoints`)
  - the disk-space alerts (`DiskSpace`'s `*_CHAT` and `SPAM_SPEAK`)
  - the sub, resub, gift and bomb sentences (`SubscriptionMessages`, which also names the community
    "Lots Of Ess's")
  - the tip lines (`Tips`), the raid line, and the startup and shutdown lines
  - the clock, uptime and money-goal OBS text formats
- **Delays matched to sounds:**
  - speech 4.2 s after the tribal hymn (subs, resubs, gifts)
  - the hymn 1 s after the clap (gifts)
  - a bomb's 66 ms between hymns, 3.8 s to the clap and 3 s to the speech
  - speech 5 s after the applause (cheers) and 6 s after the anthem (tips)

### Decisions

- **Where texts live:** `settings.json`, or a file of their own (`texts.json`, `faq.json`), since they
  are content more than settings.
- **Which texts:** all of them, or the FAQ answers first, as the most likely to change.
- **Delays:** settings beside each sound, or taken from the sound file's length (NAudio's
  `AudioFileReader.TotalTime`), so a new file needs no new delay.

### Plan

1. Settle the decisions and list the texts that move.
2. Move them the way the settings work: a model with today's values as defaults, read at each use,
   reloaded by `!reloaddata`, an example file, and a test that the example equals the defaults.
3. Keep the sentence builders pure and tested, with the texts passed in.

**Docs when done:** delete this section and the TODO row; [reference.md](reference.md) (the TXT rows in
§ Constants, and § Settings), [infrastructure.md § Settings](infrastructure.md#settings),
[events.md](events.md), and the FAQ in [twitch.md](twitch.md).

---

## PSL — A previous-split sign per loop panel

Shelved by the owner on 2026-10-13. A game's entry has one `Sign`. The previous split's differences
(`Livesplit: Previous Run`, `Segment` and `Gold` in `_LPC: Splits Previous Values`) read
`PreviousSplit\AllSplits\…` files, fixed in OBS. A game whose loop shows lists at different levels (GTASA's
per-city and per-mission lists) might want the previous split shown, and coloured, at the level of the list
the split interrupted. Only SWAT4 has `Previous` panels today.

### Decisions

- **Scope:** is a `Sign` on a loop panel sticky (in force until another panel names one) or per panel
  (the panel's own, else the entry's)?
- **Files:** do the three differences' files switch level with it? The sign's folder holds
  `RunDifference.txt`, `SegmentDifference.txt` and, for `AllSplits` only, `GoldDifference.txt`. The previous
  split's name is one folder up.

### Plan

1. Add `Sign` to `Panel`. `RunAsync` keeps track of the sign in force for the loop panel up, and
   `PlayPrevious` passes it to `ColorPrevious` instead of the entry's.
2. If the files follow, set them on the three sources through `SetSetting` before the `Previous` panels
   show, and reset them when the rotation starts.

**Docs when done:** delete this section and the TODO row; [obs.md](obs.md#the-panel-file) and
[obs.md § Split colours](obs.md#split-colours).
