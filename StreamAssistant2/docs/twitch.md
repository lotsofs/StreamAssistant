# Twitch

How the bot talks to Twitch, with no SDK: two independent transports (IRC and EventSub), one small REST
client (Helix), and the hand-rolled parsing on top. What the bot *does* with each event is in
[events.md](events.md). Tips come over a third connection, to StreamElements: [streamelements.md](streamelements.md).

Read from the code; nothing here was run against Twitch.

## Credentials and identity

[Config.cs](../code/Core/Config.cs) puts these in `Config.Data` (from `secrets.json`; never print their values):

| Setting | Used for |
|---|---|
| `TwitchAuth.Login` | The broadcaster's login (`lotsofs`): the IRC channel, an admin, the chatter who gets `YOOO BRO`, the decapi uptime URL, the `!test online` / `offline` events, the ad nag's `@` mention. Required: `Config.Load` stops without it |
| `TwitchAuth.AccessToken` | The broadcaster's token: the `Authorization: Bearer` header for both EventSub and Helix, and the broadcaster's [name colour](#chat-name-colours) |
| `TwitchAuth.ClientId` | The `Client-Id` header on the broadcaster's REST calls (the name colours send the client id the token validates to instead) |
| `TwitchBot.Login` | The bot account's login (`botsofs`): IRC `NICK`, and an admin |
| `TwitchBot.AccessToken` | The bot account's token: IRC `PASS oauth:<token>`, and the bot's [name colour](#chat-name-colours) |
| `TwitchAuth.RefreshToken`, `TwitchBot.RefreshToken`, `TwitchBot.ClientId` | Present in the file and the model, **never read**. Tokens are not refreshed at runtime ([TOK](robustness-fixes.md#tok--refresh-the-twitch-tokens-at-runtime)) |
| `TwitchIds.BroadcasterId` | Channel the subscriptions and redemption updates apply to |
| `TwitchIds.ModeratorId` | `moderator_user_id` for subscriptions that don't use the broadcaster as moderator (none currently) |
| `TwitchIds.UserId` | `user_id` condition of `channel.chat.notification` |
| `TwitchIds.TestBroadcasterId` | The channel watched instead when `IS_TEST` is on |

`Config.Load` lowercases both logins. Chat goes through a separate bot account: the bot speaks in chat
**as `botsofs`**, the `twitchBot` login, and joins the broadcaster's channel, `#lotsofs`.
`TwitchIRCManager.ChatAccount` picks the account. When `twitchBot` lacks a login or token, IRC logs in as
the broadcaster with the broadcaster's token instead, and `Connect` logs `Chat: no twitchBot login or
accessToken in secrets.json, chatting as <broadcaster>` on `Important`. Twitch's IRC doesn't echo a
connection's own messages back, so the bot never sees what it says. It does see what the broadcaster
types, from any other client.

The bot account should be a moderator in the channel (`/mod botsofs`), so chat settings meant for
viewers don't hold its messages: slow mode, followers- or subscriber-only chat, and blocked links (the
FAQ answers carry URLs). A moderator may also send 100 messages per 30 s rather than 20.

When a token expires, nothing renews it ([TOK](robustness-fixes.md#tok--refresh-the-twitch-tokens-at-runtime)); the two expire independently. Expected symptoms, inferred
rather than observed: with the bot's token, IRC gets a login-failure notice and the socket closes, so the
connect loop retries every 3 s forever (`Error 1` / `Error TIRC2`); with the broadcaster's, EventSub's
subscribe POST returns 401, which throws `Subscription failed (401)` and restarts the session every 3 s
(`Error TES1`). Either way the name colour change for that account logs `Error NCL1` once.

The bot's token needs `chat:read`, `chat:edit` and `user:manage:chat_color` (its name colour). The
broadcaster's needs channel-point redemption read **and** manage (the PATCH), bits, ad schedule, followers,
user chat read (`user:read:chat`, for `channel.chat.notification`) and `user:manage:chat_color` (its name
colour). That list comes from Twitch's documentation for the subscription types
used, not from inspecting the stored token.

**Helix rewards ownership.** Updating a redemption's status only works for rewards created by the same
client id. See [color.md](color.md) for the colour rewards and why they were recreated through the API.

## IRC

[TwitchIRCManager.cs](../code/Twitch/TwitchIRCManager.cs) is a `TcpClient` to `irc.chat.twitch.tv:6667`
(plain text, no TLS).

**Connect** (`ConnectOnce`): send `PASS` and `NICK` (the bot account, see
[Credentials and identity](#credentials-and-identity)), then `CAP REQ :twitch.tv/tags twitch.tv/commands
twitch.tv/membership`, then `JOIN #<broadcaster>` (`#lotsofs`), then post `🟣 Connected` to chat. The TCP connect gives up
after `ConnectTimeout` (15 s) with a `TimeoutException`, so a hung connect is retried like a failed one.

**Loop** (`StartConnectionLoop`): connect, then await `ListenLoop`. Any exception logs `Error 1`, waits
3 s and goes round again. `ListenLoop` has its own catch that logs `Error TIRC2: Connection Lost` and
rethrows, so a dropped connection logs both. A failure inside `ConnectOnce` itself logs only `Error 1`.

**Reading**: `ReadLineOrTimeoutAsync`, which wraps `ReadLineAsync` with `SilenceTimeout` (7 minutes).
Silence that long throws `TimeoutException`, which takes the normal lost-connection path (`Error TIRC2`,
`Error 1`, 3 s, reconnect). A `null` line (remote closed) is turned into an exception. Every line
received, of any kind, sets `_lastPingTime`; `PING` is answered with `PONG :tmi.twitch.tv` and not
forwarded; everything else goes to `OnMessage(string raw)`.

`TimeSinceLastPing` therefore measures *time since any line arrived*, not time since the last `PING`.
Twitch pings roughly every five minutes, and busy chat keeps it near zero. The dashboard's colour
thresholds (5, 6 and 7 minutes) assume that pace, and the red threshold is where `SilenceTimeout`
recycles the connection. A connection that dies without closing therefore turns red and is replaced at
about the same moment.

**Writing**: `SendMessage(string)` is fire-and-forget. `SendMessageAsync` returns silently if the writer
is null (not connected yet), writes `PRIVMSG #<broadcaster> :<text>`, and logs `> <text>` on `ChatOutgoing`.
A write failure logs `Error TIRC3` (via `FireForget.Run`). There is no rate limiting, no length limiting (Twitch drops messages over
500 characters) and no queue, so bursts of `SendMessage` calls go
out as fast as they are called.

**Disconnect**: cancels the token and closes the client. The connect loop's `Task.Delay(…, token)` then
throws inside a fire-and-forget task, which is harmless at shutdown.

## Chat handling

[ChatHandler.cs](../code/Twitch/ChatHandler.cs) turns a raw line into an action. The line looks like:

```
@badge-info=…;color=#FF0000;display-name=Foo;… :foo!foo@foo.tmi.twitch.tv PRIVMSG #lotsofs :hello there
```

`ProcessMessage` splits on the first space repeatedly:

1. leading `@…` is the **tags** string. It is cut off and **never parsed or used**; the bot has no
   notion of badges, display names, mod status or message ids.
2. the next word is the **target**, i.e. the `:nick!user@host` prefix. `GetUserName` takes the text
   between `:` and `!` and lowercases it; server prefixes (`:tmi.twitch.tv`) have no `!` and give `""`.
3. the next word is the **message type**; the rest is the body.

| Type | Handling |
|---|---|
| `PRIVMSG` | body is `#channel :text`; the text after `" :"` goes to `HandlePrivMsg` |
| `USERSTATE`, `JOIN`, `PART` | ignored (the log line is commented out) |
| `001 002 003 004 375 372 376` | file-only (welcome and MOTD) |
| anything else | logged in full on `ChatIncoming` (`CAP`, `NOTICE`, `353`, `ROOMSTATE`, `CLEARCHAT`, `USERNOTICE`…) |

A line with no further space after a type (for example a bare `:tmi.twitch.tv RECONNECT`) makes a
`Substring` throw, which logs `Error 7`. Nothing handles Twitch's `RECONNECT` request. `/me` actions
arrive as `PRIVMSG` text wrapped in `\u0001ACTION …\u0001` and aren't special-cased.

`HandlePrivMsg` logs `<user>: <text>` on `ChatIncoming`, calls `ChatterList.AddChatter(user)`, then:

- **admins** (`IsAdmin`: the `twitchAuth` or `twitchBot` login, `lotsofs` or `botsofs`): `CheckForAdminCommands`,
  which returns whether the message was an admin command. If it wasn't, it goes on to `CheckForCommands`
  like anyone's.
- **everyone else**: `CheckForCommands`

So admins get the public commands too, including the natural-language questions, and nobody else
reaches the admin ones. The check is on the lowercased login only, not on badges.

### Admin commands

Command is the first word (case-sensitive); the rest, trimmed, is the argument.

| Command | Does |
|---|---|
| `!changecolor <text>` | `LayoutColoring.TryChangeToSingle` |
| `!changecolors <text>` | `LayoutColoring.TryChangeToTriple` |
| `!changecolorrandom` | `LayoutColoring.ChangeToRandom` |
| `!test <script> …` | `TestEventRunner.Run`: simulated events, see [the test harness](#the-test-harness) |
| `!train` | `ChannelPoints.TryStartTrain`, the train reward without a redemption (ignored while one is showing; `Error TRN1` if it throws) |
| `!changegame <id or name>`, `!changecategory <id or name>` | `Games.TryChangeCategory`: switches the bot's category locally (Twitch's stays as is), posting `Stream category change …` and running the game setup. Takes a `games.json` key, a game `Name` (any case) or any numeric id; anything else logs `No game or category id "…"`, and the current category logs `Category is already <id>` |
| `!changepanel <game>` | `Games.TryChangePanel`: starts the left-panel rotation for a game (a `games.json` key, game name in any case, or numeric id) or for a `leftpanel.json` entry such as `default`, and changes nothing else: no chat line, and the category, background, colour and captures stay. Anything else logs `No game or left panel "…"`; with no panels loaded it logs `No left panels loaded` ([obs.md](obs.md#the-rotation)) |
| `!changecapture <game>` | `Games.TryChangeCapture`: runs only the capture-source step for a game (a `games.json` key, game name or numeric id), logging `Capture setup: <name> (<id>)`, with no chat line and nothing else changed. An unknown input logs `No game or category id "…"`, and an id not in `games.json` logs `<id> not in games.json, capture sources left as is` ([obs.md](obs.md#per-game-setup)) |
| `!reloaddata` | `DataFiles.Reload`: off the IRC thread, and one reload at a time, reloads `settings.json`, the colour tables and schemes, `games.json` and `leftpanel.json` as at startup, then runs the game setup for the current category, as a category change does but with no chat line for the category (the colour's `Changing to color …` still goes out). That restarts the left-panel rotation, even a stopped one. With no category known yet, it restarts the rotation instead, unless it was stopped. A file that can't be loaded keeps what it held: `Error SET1` for `settings.json`, `Error CLR1` for a colour file, `Error GMS1` / `Error LPN1`, or `… not found, keeping …` for a missing one ([color.md](color.md#loading)). A changed `BotOutput` waits for a restart ([infrastructure.md](infrastructure.md#settings)). `paths.json` and `secrets.json` aren't reloaded |
| `!stoppaneltimer` | `LeftPanel.Stop`: stops the left-panel rotation and leaves the current panel up, logging `Left panel rotation stopped`. An OBS reconnect doesn't restart it; `!startpaneltimer`, a category change or going live does. With no rotation it logs `No left panel rotation to stop` ([obs.md](obs.md#the-rotation)) |
| `!nextpanelitem` | `LeftPanel.Next`: cuts the panel up short, so the rotation moves on to the next panel now, as though its time were up (during a previous split, to the next previous-split panel). With no rotation, a stopped one, or only one panel and no previous split, it logs `No left panel rotation to move on` or `Only one left panel, nothing to move on to` ([obs.md](obs.md#the-rotation)) |
| `!startpaneltimer` | `LeftPanel.Resume`: starts the last game's rotation again from its first panel, logging `Left panel: <game>, …`; with none it logs `No left panel to start` |
| `!moneygoal <amount>` | `MoneyGoal.TrySetGoal`: sets the money goal in EUR (`500`, `12.50` or `12,50`), logging `Money goal set to <goal> EUR, now <total> / <goal> EUR`, and updates its OBS text. Anything but a positive amount logs `Not a money goal: "…" (try !moneygoal 500)` ([obs.md](obs.md#money-goal)) |
| `!resetmoney` | `MoneyGoal.Reset`: sets the money total back to 0, logging `Money total reset (was <total> EUR), now 0 / <goal> EUR`, and updates its OBS text; the goal stays |

### Public commands

`ChatHandler._commands` is a list of `ChatCommand(name, regex, output)` for the stream's FAQ
(`!civilians`, `!ktane`, `!language`, `!skeys`, `!song`, `!sssa`). Each regex uses lookaheads so a
command fires on its `!name` anywhere in the message (`!ktane` also on `!manual`, `!language` also on
`!spanish`, `!turkish` and the other language names), or on a natural-language question: one word from
each of its groups, in any order, such as `why/how` + `kill/shoot` + `hostage/civilian/innocent`. Each
word must start a word in the message (`\b`), so `how` doesn't match inside `show` or `are` inside
`care`, but they are stems, so `keys`, `whats` and `shooting` count. Matching ignores case.

`CheckForCommands` answers only the **first** command in the list that matches (list order is
priority), with `@<user> <output>` through `TwitchIRCManager.SendMessage`, and logs
`FAQ <command> for <user>` on `Notification`. A user gets the same command at most once every 60 s
(`Chat.FaqCooldownSeconds`, read at each answer); inside that it logs `FAQ <command> for <user>: on cooldown` and sends nothing. Other users
and other commands aren't held up. The cooldowns last until the bot restarts. Tested in `FaqCommandTests`.

### ChatterList

`ChatterList.AddChatter` keeps every login it has seen this run and **posts `Test <n>` to chat the first
time each one speaks**. That is intended: `Test <n>` is the real wording. The broadcaster's (`twitchAuth` login, `lotsofs`)
first message also gets `YOOO BRO`. It returns whether the login
was new. `Reset()` clears it and logs `Chatter list reset @ <n>`; it runs when OBS starts
streaming ([obs.md](obs.md#connection)), on obs-websocket's thread, so the logins are a `HashSet` under a
lock. Tested in `ChatterListTests`.

## EventSub

[TwitchEventSub.cs](../code/Twitch/TwitchEventSub.cs) is a `ClientWebSocket` to
`wss://eventsub.wss.twitch.tv/ws`, swapped mid-session for the `reconnect_url` Twitch hands out (see
[Planned reconnects](#planned-reconnects)). Flow charts of every method are in
[eventsub-flow.md](eventsub-flow.md).

Four `internal static` fields exist only so tests can run the real loop against a loopback fake of Twitch:
`EventSubUrl`, `SubscriptionsUrl`, `RetryDelay` (3 s) and `NotificationHandler` (defaults to
`TwitchEventHandler.Handle`). The bot never changes them.

### Session lifecycle

`StartConnectionLoop` repeats forever until cancelled:

1. reset `_exitReason` to `None`
2. `ConnectOnce` (new socket, connect, post `🟣 ES Connected`), restart the keepalive stopwatch. The
   connect gives up after `ConnectTimeout` (15 s) with a `TimeoutException`, which lands in step 4 as
   `Error TES1: None` followed by `Error TES3`
3. `ListenLoop` until something sets an exit reason
4. exceptions log `Error TES1: <reason>` plus the exception to the file; a loop that ends without a
   reason logs `Error TES3`. A dropped connection is not an exception here: it ends the loop with
   `ConnectionLost`. Every exit then logs `EventSub session ended: <reason> (socket <state>,
   close <code> <name> "<description>")` on `EventSubConfusion`
5. `CleanupSession`: a graceful close handshake only when the reason is `CancelRequested`, otherwise
   `Abort()`; then `_sessionId` cleared and the stopwatch restarted (`Error TES2` if cleanup throws)
6. post `💥 ES Disconnected` to chat, wait 3 s, go again

`ListenLoop` reads one full message at a time (`ReceiveFullMessage` reassembles 8 KiB frames), parses
it with `JsonDocument`, and switches on `metadata.message_type`:

| Message | Does |
|---|---|
| `session_welcome` | restart keepalive stopwatch, store the session id, `SubscribeToEvents()` |
| `notification` | restart stopwatch, `HandleNotification`: pull `subscription.type` and `event`, call `TwitchEventHandler.Handle` |
| `session_keepalive` | restart stopwatch |
| `session_reconnect` | `SwitchToReconnectUrl` (below); stay in the loop on success, otherwise exit reason `ReconnectRequested` |
| anything else | log on `EventSubConfusion`, payload to the file |

#### Planned reconnects

Twitch sends `session_reconnect` about once a day. The bot follows Twitch's documented flow, without
leaving `ListenLoop`:

1. `ConnectToReconnectUrlAsync` opens a **second** socket to `reconnect_url` while the old one stays open
   (`ConnectTimeout`, 15 s) and waits for its first message (`ReconnectWelcomeTimeout`, 10 s). It must be
   a `session_welcome` with a session id.
2. On success, `_socket`, `_sessionId` and the stopwatch switch to the new session. **No resubscribe**:
   subscriptions carry over. Logs `EventSub reconnected. Session ID: …`. Live, the session ID has been
   the same as the old session's.
3. `DrainOldSocketAsync` reads the old socket for up to `OldSocketDrainLimit` (1 s) or until it closes,
   handling any notifications that arrived there during the switch. Then the old socket is closed (2 s
   limit) and logged as `EventSub old connection closed (<n> events delivered during the switch, close …)`.
   A throw here logs `Error TES5` and the new session carries on.
4. Nothing goes to chat: no `💥 ES Disconnected` / `🟣 ES Connected`.

Any failure logs why on `EventSubConfusion` (`no welcome (<reason>)`, `expected a session_welcome…`,
`unusable URL`) or as `Error TES4` (an exception), disposes the new socket, then logs `EventSub reconnect
failed, starting a fresh session` (`Important`) and exits with `ReconnectRequested`: the normal cleanup,
chat lines, 3 s wait, a fresh session on the default URL and a full resubscribe.

Opening the new socket before dropping the old one matters: a reconnect made after the old socket is gone
is refused with `4007 "invalid reconnect attempt"`. The flow is tested against a loopback fake of Twitch
(`EventSubReconnectTests`, `EventSubLoopTests`) and has worked live against Twitch. An event delivered on a
reconnected session hasn't been seen yet (item ESR in [TODO.md](TODO.md)).

At the top of each iteration it checks that the socket is `Open` and that the stopwatch is under
`KeepAliveTimeout`, 20 s (Twitch's default keepalive is 10 s). `ReceiveFullMessage` enforces the same
limit while waiting: each `ReceiveAsync` is cancelled when the stopwatch would pass it, so total silence on
a live-looking socket ends the session about 20 s after the last message, and the cancelled receive aborts
the socket. It returns the message and an exit reason rather than setting `_exitReason` itself, so it can be
tested on its own (`EventSubReceiveTimeoutTests`).

`SessionExitReason` says why a session ended, and the `EventSub session ended` line reports it for every
exit. When a Close frame arrives, `ReceiveFullMessage` also logs `EventSub closed the socket:` with the
close code and description (Twitch uses 4xxx codes to say why). When the connection drops without one,
`ReceiveAsync` throws a `WebSocketException`; `ReceiveFullMessage` catches it and logs `EventSub connection
lost (<WebSocketError>): <innermost message>`. Twitch drops it with a TCP reset most days, sometimes several
times a day, which reads `EventSub connection lost (ConnectionClosedPrematurely): An existing connection was
forcibly closed by the remote host.` On 2026-10-11 there were five, between 04:30 and 08:32, while IRC and
StreamElements stayed up, which points at Twitch's side rather than the home connection. The first came
16 s after the last keepalive, before the 20 s timeout; the rest came with no silence before them.

| Reason | Set when |
|---|---|
| `None` | initial value; seeing it at exit logs `Error TES3` |
| `Error` | the socket wasn't open at the top of the loop |
| `KeepAliveTimeout` | stopwatch over `KeepAliveTimeout` (20 s), at the top of the loop or while waiting for a frame |
| `ReconnectRequested` | Twitch sent `session_reconnect` and switching to `reconnect_url` failed |
| `SubscriptionFailed` | a subscribe POST returned a non-success status |
| `CancelRequested` | the token was cancelled (shutdown) |
| `SocketClosed` | a Close frame arrived |
| `SocketDied` | the socket stopped being `Open` mid-receive |
| `ConnectionLost` | the connection dropped without a Close frame (`ReceiveAsync` threw a `WebSocketException`), usually a TCP reset from Twitch |

### Subscribing

On `session_welcome`, `SubscribeToEvents` POSTs every entry of
`TwitchEventSubSubscription.Subscriptions` to `https://api.twitch.tv/helix/eventsub/subscriptions`, one
after another, awaited **inside the listen loop** (nothing else is read meanwhile). Each body carries
`type`, `version`, a `condition` and `transport: {method: websocket, session_id}`.

The condition is built from the descriptor's flags:

- `RequiresBroadcasterId` adds `broadcaster_user_id`
- `RequiresModeratorId` adds `moderator_user_id` (the broadcaster if `WantsBroadcasterAsModerator`,
  otherwise `TwitchIds.ModeratorId`)
- `RequiresUserId` adds `user_id`; with `IS_TEST` it also overwrites `broadcaster_user_id` with the test
  channel and announces test mode in the log and in chat

The current table:

| Type | Version | Condition | Handled? |
|---|---|---|---|
| `channel.ad_break.begin` | 1 | broadcaster | yes, `Ads.Process` |
| `channel.channel_points_custom_reward_redemption.add` | 1 | broadcaster | yes, `ChannelPoints.ProcessAdd` |
| `channel.chat.notification` | 1 | broadcaster + user | yes, fans out on `notice_type` |
| `channel.cheer` | 1 | broadcaster | yes, `Cheers.Process` |
| `channel.update` | 2 | broadcaster | yes, `Games.HandleUpdate` (acts only on a category change) |
| `stream.online` | 1 | broadcaster | yes, `StreamEvents.Online` (logs, and wakes the uptime poll) |
| `stream.offline` | 1 | broadcaster | yes, `StreamEvents.Offline` (logs only) |
| `channel.follow` | 2 | broadcaster + moderator (= broadcaster) | **no**: each follow logs "not handled in code" on `EventSubConfusion` |

Responses: `202` is expected, and the 202s are logged together once all are done, as one line:
`EventSub subscribed (202): channel.ad_break.begin, …`. Anything else gets its own `EventSub attempt to
<type>: <status>` line first. `409` (the subscription already exists) counts as success: it logs
`Subscription <type> already exists` and carries on. Any other non-success status sets
`SubscriptionFailed`; a `400` just logs `Subscription <type> failed` and carries on with the next one, any
other failing status throws and recycles the session (before the 202 line, which is then not logged). The
response body goes to the log file except after a 409.

`IS_TEST` is a `static readonly bool` in `TwitchEventSub`, so using it means editing and rebuilding.

EventSub keeps its **own** `HttpClient` for subscribing, separate from `TwitchHelixApi`'s. Both are
configured with the same headers.

### Adding an EventSub event

Two edits:

1. Add a descriptor to `TwitchEventSubSubscription.Subscriptions` (type, version, which condition ids).
   Check Twitch's subscription-types page for the version and the required condition fields and scope.
2. Add a `case` to `TwitchEventHandler.Handle`. Unhandled types log on `EventSubConfusion`, never fail.

For `channel.chat.notification` sub-types, add a `case` to `HandleChannelChatNotification` instead.

**Reading fields.** Use the [JsonElementExtensions](../code/Util/JsonElementExtensions.cs):
`evt.ReadString("sub_gift.recipient_user_login", "Unknown User")`, `evt.ReadInt("sub.sub_tier") / 1000`,
`evt.ReadBool("is_anonymous")`, `evt.ReadElement("payload.event")`. A missing step, a `null` or the wrong kind gives
the fallback rather than throwing, since Twitch sends `null` for absent values. The envelope in
`ListenLoop` is read the same way. Then add a `!test` script for the event (see [the test harness](#the-test-harness)).

**The JSON lifetime rule.** `ListenLoop` disposes its `JsonDocument` at the end of each iteration, and
handlers receive a `JsonElement` into it. A handler that is `async` must read every property it needs
*before its first `await`*, or call `evt.Clone()` first (as `HandleCommunitySubGiftNotif` does), or it
will hit `ObjectDisposedException` after the delay. All the current handlers follow one of these.

**Exceptions in handlers.** Handlers are started with `FireForget.Run("<code>", type, () => Handler(evt))`,
which awaits the handler inside a try/catch and logs a throw as `Error <code>: <type> failed`. Each call
site has its own code: `TEH_adb`, `TEH_cpcrra` and `TEH_c` in `Handle`, `TEH_CN_s`, `TEH_CN_rs`,
`TEH_CN_sg` and `TEH_CN_csg` in `HandleChannelChatNotification` ([reference.md](reference.md#error-codes)). The handler is invoked
synchronously, so its part before the first `await` still runs on the listen loop and can read the
`JsonElement`. Blocking work that needs no JSON goes after an `await Task.Yield()`, as in
`ChannelPoints.RunTrainAsync`. `Handle`'s own try/catch (`Error TEH1`) covers only the dispatch, which
includes the handlers it calls directly rather than through `Run`: `Games.HandleUpdate`, `StreamEvents` and `Raids.Handle`. `HandleCommunitySubGiftNotif`
keeps its own `Error SUB1` catch. `SendMessage` uses the same helper (`Error TIRC3`). Other fire-and-forget sites (`ConsoleLogger`, the
`LayoutColoring` and `TextToSpeech` workers, the `DiskSpace`, `TwitchUptime` and `Clock` loops, the connection
loops) catch their own exceptions.

### The test harness

`!test <script> [arguments]` (admin only) builds simulated EventSub events in code and feeds them to
`TwitchEventHandler.Handle`, bypassing the socket. StreamElements events go the same way into
`StreamElementsEventHandler.Handle` ([streamelements.md](streamelements.md#events)). A type starting `se.`
(`se.channel.tips`) is routed there with the prefix stripped. The scripts live in
[code/Twitch/TestEvents/](../code/Twitch/TestEvents/), and `TestEventRunner.Scripts` lists them:

| Script | Arguments | Simulates |
|---|---|---|
| `sub` | `[tier <1-3>] [prime] [months <1-12>]` | a chat `sub` notification |
| `resub` | `[months <n>] [streak <n>] [tier <1-3>] [prime] [gift] [msg <text…>]` (defaults 12 and 3) | a chat `resub` |
| `gift` | `[tier <1-3>] [total <n>] [months <1-12>] [anon]` | a targeted `sub_gift` (`community_gift_id` null); `total` is the gifter's channel total |
| `bomb` | `<gifts> [late] [missing <k>] [anon] [tier <1-3>]` | a gift bomb, below. `bomb 1` is a single random community gift, which Twitch sends as a bomb of one |
| `cheer` | `<bits> [anon] [msg <text…>]` | a `channel.cheer` |
| `raid` | `[viewers <n>]` (default 10) | a chat `raid` notification from `TestRaider` (login `testraider`) |
| `online` | none | a `stream.online` from the broadcaster (`twitchAuth` login), `started_at` now |
| `offline` | none | a `stream.offline` from the broadcaster |
| `tip` | `<amount> [msg <text…>]` | a StreamElements `channel.tips` from `testtipper` in EUR, under a fresh `_id`; `amount` takes a dot (`4.20`), 0.01 to 100000 |
| `replay` | `<file>` | a real EventSub or StreamElements event saved in `AssistantLogs\EventSubs\`, below |

**Arguments** ([TestArgs.cs](../code/Twitch/TestEvents/TestArgs.cs)): required ones (`<…>`) come first.
The optional ones follow either as keywords, in any order (`!test resub tier 2 gift msg hi`), or
positionally in the order listed, with `true`/`false` for flags (`!test resub 20 5 2 false true hi`).
Decimal arguments are written with a dot whatever the machine's culture.
Positional is chosen unless the first optional word names an optional parameter; trailing values can be
left off. A text argument takes the rest of the line, so a `tip` message can follow the amount directly
(`!test tip 4.20 hello`), but one starting with the word `msg` needs the keyword (`!test tip 4.20 msg msg …`). A bad argument logs the script's usage (both forms) on
`EventSubConfusion` and sends nothing. A first word that isn't a script is treated as a replay file name; if no file matches either, the list of scripts is logged.

**Gift bomb** ([TestGiftBomb.cs](../code/Twitch/TestEvents/TestGiftBomb.cs)): one `community_sub_gift`
plus a `sub_gift` per recipient (`testgiftee1…`), under a fresh id each run so runs never collide in
`_giftBombs`. `late` sends the recipients first; `missing <k>` withholds `k` of them (clamped to the
total), so the announcement waits out its 10 s; `anon` sends what Twitch sends for an anonymous gifter: null login and null `cumulative_total`
(on `gift` too).

**Replay** ([TestReplay.cs](../code/Twitch/TestEvents/TestReplay.cs)): `!test replay <file>`, or plain `!test <file>` when its first word isn't a script name, takes a file
in `<BotOutput>\AssistantLogs\EventSubs\` by its name, with or without `.log`, or by a prefix only one
file has (`!test replay resub_2026-07-04 13-21`). A prefix several files share lists the first few and
sends nothing. `TwitchEventHandler.Handle` dumps every real event there before dispatching it, so this is
the way to rerun old, real data.

- **File names carry the type.** A chat notification is `<notice_type>_<stamp>.log` (`resub_…`,
  `community_sub_gift_…`); anything else is `<EventSub type>_<stamp>.log` (`channel.cheer_…`,
  `channel.channel_points_custom_reward_redemption.add_…`, `channel.ad_break.begin_…`). Notice types
  never contain a dot, so `TestReplay.TypeOf` tells them apart. A StreamElements event is
  `se.<topic>_<stamp>.log` (`se.channel.tips_…`), whose type then routes it to the StreamElements
  handler. The stamp is `yyyy-MM-dd HH-mm-ss.fff`.
- **Replays are guarded where they would touch real accounts.** From `!test` (`ProcessAdd(evt, isTest)`),
  a toilet flush plays its sound but writes no row; a toilet retrieve only reads and logs how many flushes
  it would return, with no Helix call, no delete and no chat line; a colour reward recolours the layout
  but only logs the status it would have set, without calling Helix. A replayed ad break is not guarded:
  it restarts the ad-warning schedule, posting to chat 55 to 59 minutes later.

- A file can hold several events: two notifications in the same millisecond get the same name and the
  second is appended (common for bomb recipients). All of them are replayed, in order.
- Replaying a `community_sub_gift` also replays every `sub_gift` file carrying its id, sorted by the
  timestamp in the file names. That is the real arrival order, and in the logs the recipients mostly
  arrive *before* the bomb event.
- Any bomb id is swapped for a fresh `replay-…` one, the same within a replay, so a replay never lands
  in a bomb `_giftBombs` already completed.

**Test events don't write `EventSubs\`.** `Handle(type, evt, isTest: true)` skips the dump (on both
handlers), so the folder holds only real events and replays don't duplicate themselves.

Each run logs `Test <script>: <values>` on `EventSubNotification`, then sends its events 50 ms apart
through `FireForget.Run`; a failure logs `Error TEV1`. **They run the real handlers**: sounds play, TTS
speaks, and anything a handler posts to chat (`🎁` for a gifted resub, `💸` for a tip, `Raid: …` for a
raid) goes to the live channel.

To add a script: a `TestScript` (name, required and optional `TestArgs` parameters, and a pure `Build`
returning the events) in a file under `TestEvents/`, listed in `TestEventRunner.Scripts`. Build each event
from an anonymous object carrying the fields the handler reads, and add a test to `TestEventTests.cs`.

## Helix

[TwitchHelixApi.cs](../code/Twitch/TwitchHelixApi.cs) has two calls with the broadcaster's token, and two
that take an account's token, for the [chat name colours](#chat-name-colours). `Init()` sets the broadcaster's
headers once and a 60 s timeout. Each of the broadcaster's calls logs a `HELIX> …` line with the response code
on the `Helix` channel.

`GetChannelCategoryIdAsync()`: `GET /helix/channels?broadcaster_id=…` (no scope needed), logs
`HELIX> channel info: <code>`, throws `Channel info failed: <body>` on non-success, and returns
`data[0].game_id` (`""` for no category, `null` for no channel) through `ParseCategoryId`, which is
tested. Caller: `Games.OnStreamStartedAsync`, when going live with no category seen yet
([events.md](events.md#category-change-and-going-live)).

`UpdateRedemption(rewardId, redemptionId, status)`: `PATCH
/helix/channel_points/custom_rewards/redemptions?broadcaster_id=…&reward_id=…&id=…` with
`{"status":"FULFILLED"|"CANCELED"}`. Any other status string throws. `CANCELED` refunds the points.
Anything but 200 logs a warning; non-success throws `Redemption update failed: <body>`.
Callers: `ChannelPoints.CloseRedemption`, for the train and colour rewards (catches and logs `Error CP1`), and the toilet-retrieve
branch of `ProcessAdd` (does not catch).

The other two go through their own `HttpClient`, with no default headers, so each request carries its own
account's token. Neither logs; the caller does.

`ValidateTokenAsync(accessToken)`: `GET https://id.twitch.tv/oauth2/validate` with `Authorization: OAuth
<token>`. Returns a `TwitchToken`: the client id the token was issued to, the login, the user id and the
scopes, through `ParseToken`, which is tested. A refusal throws `Token validation failed (<code>): <body>`.

`SetChatColorAsync(token, accessToken, color)`: `PUT /helix/chat/color?user_id=<id>&color=<color>`, with the
validated client id as `Client-Id` and the token as `Bearer`. `ChatColorUrl` escapes the colour, so a hex
colour's `#` goes as `%23`. Twitch answers 204; anything else throws `Chat colour <color> failed (<code>):
<body>`. It needs `user:manage:chat_color`, and the user id must be the token's own. A hex colour needs
Turbo or Prime on the account, or Twitch answers 400.

## Chat name colours

[NameColor.cs](../code/Twitch/NameColor.cs) changes the chat name colour of the broadcaster (`twitchAuth`) and
of the bot account (`twitchBot`) for as long as the bot runs, live or not. `NameColor.Start()`, called from
`EnableBot` after `TwitchHelixApi.Init()`, starts one loop: it changes both colours at once, records each
account's outcome for the dashboard, waits `NameColor.IntervalSeconds` (60 s, read each pass) and repeats. While `NameColor.Enabled` is `false`
(also read each pass, so `!reloaddata` switches it after the current wait), a pass sends nothing and marks both
accounts off, keeping their last colours; an account without the scope stays as it was.

- **The broadcaster** has Prime, so it takes any hex colour: a random colour from the colour tables, as
  `!changecolorrandom` picks (`ColorTableRegistry.GetRandomColor`; a random `#rrggbb` with no tables loaded).
  `NameColor.TableColor` sends it in upper case, as Twitch's documentation writes hex (`#9146FF`); the dashboard
  keeps the colour code's lower case, and the table it came from.
- **The bot account** has neither Turbo nor Prime, so it takes one of Twitch's fifteen named colours, at
  random and never the one it had. They're fixed in code (`NameColor.Named`), since Twitch hasn't changed
  them, each with the hex Twitch shows for it (the CSS colour of that name: `coral` is `#ff7f50`) for the
  dashboard. The `twitch` colour table (`Colors\Tables\twitch.json`) holds the same fifteen, so the layout
  commands take them too (`!changecolor twitch golden_rod`); a real-data test keeps it equal to the code.
  `NameColor.PickNamed` picks one. When chat goes as the broadcaster (no `twitchBot` login or
  token), only the broadcaster's colour changes.

Nothing is logged per change. Instead each account keeps a `NameColor.Status` (its last colour, when it was
set, and a note with its reason), replaced whole on every pass, and the dashboard shows them in a row under
the connection timers ([dashboard.md](dashboard.md#chat-name-colours)):
`Lotsofs ■ Raw umber #826644 (encycolorpedia) 00:42  ·  Botsofs NO TOKEN`. `NameColor.StatusParts` and the
pure `Part` build that row. A new colour shows only on messages sent after it, so viewers see the bot's change
when it next speaks.

Before an account's first change, the loop validates its token (`ValidateTokenAsync`) for the user and client
ids. A token without `user:manage:chat_color` logs `Name colour: <login>'s token lacks user:manage:chat_color,
its colour stays as is` once, on `Important`, and that account is left alone until a restart (`NO TOKEN` on the
dashboard). A failed validate or change (`FAILED`, the last colour kept) logs `Error NCL1: <login>: <message>` once and is tried again each pass; when it works again,
`Name colour: <login> changes again` follows, on `Important`. A validate that failed is retried, so Twitch being
unreachable at startup doesn't turn the colours off. An exception outside that logs `Error NCL2`. Not yet seen
live (LIV 151–153).
