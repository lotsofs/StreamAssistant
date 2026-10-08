# Twitch

How the bot talks to Twitch, with no SDK: two independent transports (IRC and EventSub), one small REST
client (Helix), and the hand-rolled parsing on top. What the bot *does* with each event is in
[events.md](events.md).

Read from the code; nothing here was run against Twitch.

## Credentials and identity

[Config.cs](../Config.cs) puts these in `Config.Data` (from `secrets.json`; never print their values):

| Setting | Used for |
|---|---|
| `TwitchAuth.AccessToken` | IRC `PASS oauth:<token>`, and the `Authorization: Bearer` header for both EventSub and Helix |
| `TwitchAuth.ClientId` | The `Client-Id` header on every REST call |
| `TwitchAuth.RefreshToken` | Present in the file and the model, **never read**. Tokens are not refreshed at runtime |
| `TwitchIds.BroadcasterId` | Channel the subscriptions and redemption updates apply to |
| `TwitchIds.ModeratorId` | `moderator_user_id` for subscriptions that don't use the broadcaster as moderator (none currently) |
| `TwitchIds.UserId` | `user_id` condition of `channel.chat.notification` |
| `TwitchIds.TestBroadcasterId` | The channel watched instead when `IS_TEST` is on |

One user token does everything, so the bot speaks in chat **as `lotsofs`** (`NICK lotsofs`,
`JOIN #lotsofs`; both hardcoded in `TwitchIRCManager`). Twitch's IRC doesn't echo a connection's own
messages back, so the bot never sees what it says.

When the token expires, nothing renews it. Expected symptoms, inferred rather than observed: IRC gets a
login-failure notice and the socket closes, so the connect loop retries every 3 s forever
(`Error 1` / `Error TIRC2`); EventSub's subscribe POST returns 401, which throws
`Subscription failed (401)` and restarts the session every 3 s (`Error TES1`).

The token needs scopes for chat read/write, channel-point redemption read **and** manage (the PATCH),
bits, ad schedule, followers and user chat read. That list comes from Twitch's documentation for the
subscription types used, not from inspecting the stored token.

**Helix rewards ownership.** Updating a redemption's status only works for rewards created by the same
client id. See [color.md](color.md) for the colour rewards and why they were recreated through the API.

## IRC

[TwitchIRCManager.cs](../code/Twitch/TwitchIRCManager.cs) is a `TcpClient` to `irc.chat.twitch.tv:6667`
(plain text, no TLS).

**Connect** (`ConnectOnce`): send `PASS`, `NICK`, then `CAP REQ :twitch.tv/tags twitch.tv/commands
twitch.tv/membership`, then `JOIN #lotsofs`, then post `🟣 Connected` to chat. The TCP connect gives up
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
is null (not connected yet), writes `PRIVMSG #lotsofs :<text>`, and logs `> <text>` on `ChatOutgoing`.
A write failure logs `Error TIRC3` (via `FireForget.Run`). There is no rate limiting, no length limiting (Twitch drops messages over
500 characters) and no queue, so bursts of `SendMessage` calls (a gift bomb sends one per recipient) go
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

- **admins** (`username == "lotsofs" || "botsofs"`): `CheckForAdminCommands`
- **everyone else**: `CheckForCommands`

Admins never reach the public commands, and nobody else reaches the admin ones. The check is on the
lowercased login only, not on badges.

### Admin commands

Command is the first word (case-sensitive); the rest, trimmed, is the argument.

| Command | Does |
|---|---|
| `!changecolor <text>` | `LayoutColoring.TryChangeToSingle` |
| `!changecolors <text>` | `LayoutColoring.TryChangeToTriple` |
| `!changecolorrandom` | `LayoutColoring.ChangeToRandom` |
| `!test <script> …` | `TestEventRunner.Run`: simulated events, see [the test harness](#the-test-harness) |
| `!train` | `ChannelPoints.TryStartTrain`, the train reward without a redemption (ignored while one is showing; `Error TRN1` if it throws) |
| `!stoppaneltimer` | stub (its `MsgQueue` call is commented out) |

### Public commands

`ChatHandler._commands` is a list of `ChatCommand(name, regex, output)` for the stream's FAQ
(`!civilians`, `!ktane`, `!language`, `!skeys`, `!song`, `!sssa`). `CheckForCommands` runs every regex
against every message and builds `@user: <output>`, but the `MsgQueue.Enqueue` that would send it is
commented out, so **nothing is ever sent**. Re-enabling means calling
`TwitchIRCManager.SendMessage(outputMsg)`. The regexes use lookaheads so a command fires on its `!name`
or on a natural-language question (`why/how … killing … civilians`). Note that `Regex.Match` runs with no
timeout and no per-user cooldown, and a message matching several commands would trigger them all.

### ChatterList

`ChatterList.AddChatter` keeps every login it has seen this run and **posts `Test <n>` to chat the first
time each one speaks**. That is intended: `Test <n>` is the real wording. The streamer's (`lotsofs`)
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
   reason logs `Error TES3`. Every exit then logs `EventSub session ended: <reason> (socket <state>,
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
   subscriptions carry over. Logs `EventSub reconnected. Session ID: …`.
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
close code and description (Twitch uses 4xxx codes to say why):

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
| `channel.follow` | 2 | broadcaster + moderator (= broadcaster) | **no**: each follow logs "not handled in code" on `EventSubConfusion` |

Responses: `202` is expected. `409` (the subscription already exists) counts as success: it logs
`Subscription <type> already exists` and carries on. Any other non-success status sets
`SubscriptionFailed`; a `400` just logs `Subscription <type> failed` and carries on with the next one, any
other failing status throws and recycles the session. The response body goes to the log file except
after a 409.

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
`ChannelPoints.RunTrainAsync`. `Handle`'s own try/catch (`Error TEH1`) covers only the dispatch. `HandleCommunitySubGiftNotif`
keeps its own `Error SUB1` catch. `SendMessage` uses the same helper (`Error TIRC3`). Other fire-and-forget sites (`ConsoleLogger`, the
`LayoutColoring` and `TextToSpeech` workers, the `DiskSpace` and `TwitchUptime` loops, the connection
loops) catch their own exceptions.

### The test harness

`!test <script> [arguments]` (admin only) builds simulated EventSub events in code and feeds them to
`TwitchEventHandler.Handle`, bypassing the socket. The scripts live in
[code/Twitch/TestEvents/](../code/Twitch/TestEvents/), and `TestEventRunner.Scripts` lists them:

| Script | Arguments | Simulates |
|---|---|---|
| `sub` | `[tier <1-3>] [prime] [months <1-12>]` | a chat `sub` notification |
| `resub` | `[months <n>] [streak <n>] [tier <1-3>] [prime] [gift] [msg <text…>]` (defaults 12 and 3) | a chat `resub` |
| `gift` | `[tier <1-3>] [total <n>] [months <1-12>] [anon]` | a targeted `sub_gift` (`community_gift_id` null); `total` is the gifter's channel total |
| `bomb` | `<gifts> [late] [missing <k>] [anon] [tier <1-3>]` | a gift bomb, below. `bomb 1` is a single random community gift, which Twitch sends as a bomb of one |
| `cheer` | `<bits> [anon] [msg <text…>]` | a `channel.cheer` |
| `replay` | `<file>` | a real EventSub event saved in `AssistantLogs\EventSubs\`, below |

**Arguments** ([TestArgs.cs](../code/Twitch/TestEvents/TestArgs.cs)): required ones (`<…>`) come first.
The optional ones follow either as keywords, in any order (`!test resub tier 2 gift msg hi`), or
positionally in the order listed, with `true`/`false` for flags (`!test resub 20 5 2 false true hi`).
Positional is chosen when the first optional word is a boolean or a number; trailing values can be left
off. A text argument takes the rest of the line. A bad argument logs the script's usage (both forms) on
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
  never contain a dot, so `TestReplay.TypeOf` tells them apart. The stamp is `yyyy-MM-dd HH-mm-ss.fff`.
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

**Test events don't write `EventSubs\`.** `Handle(type, evt, isTest: true)` skips the dump, so the
folder holds only real events and replays don't duplicate themselves.

Each run logs `Test <script>: <values>` on `EventSubNotification`, then sends its events 50 ms apart
through `FireForget.Run`; a failure logs `Error TEV1`. **They run the real handlers**: sounds play, TTS
speaks, and anything a handler posts to chat (`🎁` for a gifted resub) goes to
the live channel.

To add a script: a `TestScript` (name, required and optional `TestArgs` parameters, and a pure `Build`
returning the events) in a file under `TestEvents/`, listed in `TestEventRunner.Scripts`. Build each event
from an anonymous object carrying the fields the handler reads, and add a test to `TestEventTests.cs`.

## Helix

[TwitchHelixApi.cs](../code/Twitch/TwitchHelixApi.cs) has two calls. `Init()` sets headers once and a
60 s timeout. Attempts are logged on `EventSubConfusion` (the `Helix` colour channel exists in the enum
and window but nothing uses it).

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
