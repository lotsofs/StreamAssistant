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
twitch.tv/membership`, then `JOIN #lotsofs`, then post `🟣 Connected` to chat.

**Loop** (`StartConnectionLoop`): connect, then await `ListenLoop`. Any exception logs `Error 1`, waits
3 s and goes round again. `ListenLoop` has its own catch that logs `Error TIRC2: Connection Lost` and
rethrows, so a dropped connection logs both. A failure inside `ConnectOnce` itself logs only `Error 1`.

**Reading**: `ReadLineAsync`. A `null` line (remote closed) is turned into an exception. Every line
received, of any kind, sets `_lastPingTime`; `PING` is answered with `PONG :tmi.twitch.tv` and not
forwarded; everything else goes to `OnMessage(string raw)`.

`TimeSinceLastPing` therefore measures *time since any line arrived*, not time since the last `PING`.
Twitch pings roughly every five minutes, and busy chat keeps it near zero. The dashboard's colour
thresholds (5, 6 and 7 minutes) assume that pace. There is **no timeout** on the read: a connection
that dies without closing is shown red on the dashboard but never recycled (see the dead-connection
item in [TODO.md](TODO.md)).

**Writing**: `SendMessage(string)` is fire-and-forget. `SendMessageAsync` returns silently if the writer
is null (not connected yet), writes `PRIVMSG #lotsofs :<text>`, and logs `> <text>` on `ChatOutgoing`.
A write failure logs `Error 3` (via `FireForget.Run`). There is no rate limiting, no length limiting (Twitch drops messages over
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
| `!test <name>` | `TwitchEventSub.SendTest(name)`; ignored without an argument |
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
time each one speaks**. That is intended: `Test <n>` is the real wording. `Reset()` exists but nothing
calls it.

## EventSub

[TwitchEventSub.cs](../code/Twitch/TwitchEventSub.cs) is a `ClientWebSocket` to
`wss://eventsub.wss.twitch.tv/ws` (or the `reconnect_url` Twitch hands out).

### Session lifecycle

`StartConnectionLoop` repeats forever until cancelled:

1. reset `_exitReason` to `None`
2. `ConnectOnce` (new socket, connect, post `🟣 ES Connected`), restart the keepalive stopwatch
3. `ListenLoop` until something sets an exit reason
4. exceptions log `Error TES1: <reason>` plus the exception to the file; a loop that ends without a
   reason logs `Error TES3`
5. `CleanupSession`: a graceful close handshake only when the reason is `CancelRequested`, otherwise
   `Abort()`; then `_sessionId` cleared and the stopwatch restarted (`Error TES2` if cleanup throws)
6. post `💥 ES Disconnected` to chat, wait 3 s, go again

`ListenLoop` reads one full message at a time (`ReceiveFullMessage` reassembles 8 KiB frames), parses
it with `JsonDocument`, and switches on `metadata.message_type`:

| Message | Does |
|---|---|
| `session_welcome` | restart keepalive stopwatch, store the session id, `SubscribeToEvents()` |
| `notification` | restart stopwatch, pull `subscription.type` and `event`, call `TwitchEventHandler.Handle` |
| `session_keepalive` | restart stopwatch |
| `session_reconnect` | store `reconnect_url`, exit reason `ReconnectRequested`, leave the loop |
| anything else | log on `EventSubConfusion`, payload to the file |

The `session_reconnect` path does not currently work: in the real logs the new connection closes
within milliseconds, and the bot recovers through a fresh session on the default URL (item ESR in
[TODO.md](TODO.md)).

At the top of each iteration it checks that the socket is `Open` and that the stopwatch is under 20 s
(Twitch's default keepalive is 10 s). That check only runs *between* messages: `ReceiveAsync` has no
timeout, so total silence on a live-looking socket blocks forever.

`SessionExitReason` says why a session ended, so the reconnect log line is useful:

| Reason | Set when |
|---|---|
| `None` | initial value; seeing it at exit logs `Error TES3` |
| `Error` | the socket wasn't open at the top of the loop |
| `KeepAliveTimeout` | stopwatch over 20 s at the top of the loop |
| `ReconnectRequested` | Twitch sent `session_reconnect` |
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
| `channel.follow` | 2 | broadcaster + moderator (= broadcaster) | **no**: each follow logs "not handled in code" on `EventSubConfusion` |

Responses: `202` is expected. A non-success status sets `SubscriptionFailed`; a `400` just logs
`Subscription <type> failed` and carries on with the next one, any other failing status throws and
recycles the session. The response body goes to the log file either way.

`IS_TEST` is a `static readonly bool` in `TwitchEventSub`, so using it means editing and rebuilding.

EventSub keeps its **own** `HttpClient` for subscribing, separate from `TwitchHelixApi`'s. Both are
configured with the same headers.

### Adding an EventSub event

Two edits:

1. Add a descriptor to `TwitchEventSubSubscription.Subscriptions` (type, version, which condition ids).
   Check Twitch's subscription-types page for the version and the required condition fields and scope.
2. Add a `case` to `TwitchEventHandler.Handle`. Unhandled types log on `EventSubConfusion`, never fail.

For `channel.chat.notification` sub-types, add a `case` to `HandleChannelChatNotification` instead.

**The JSON lifetime rule.** `ListenLoop` disposes its `JsonDocument` at the end of each iteration, and
handlers receive a `JsonElement` into it. A handler that is `async` must read every property it needs
*before its first `await`*, or call `evt.Clone()` first (as `HandleCommunitySubGiftNotif` does), or it
will hit `ObjectDisposedException` after the delay. All the current handlers follow one of these.

**Exceptions in handlers.** Handlers are started with `FireForget.Run("TEH2", type, () => Handler(evt))`,
which awaits the handler inside a try/catch and logs a throw as `Error TEH2`. The handler is invoked
synchronously, so its part before the first `await` still runs on the listen loop and can read the
`JsonElement`. `Handle`'s own try/catch (`Error TEH1`) covers only the dispatch. `HandleCommunitySubGiftNotif`
keeps its own `Error SUB1` catch. `SendMessage` uses the same helper (`Error 3`). Other fire-and-forget sites (`ConsoleLogger`, the
`LayoutColoring` and `TextToSpeech` workers, the `DiskSpace` and `TwitchUptime` loops, the connection
loops) catch their own exceptions.

### The test harness

`TwitchEventSub.SendTest("<name>")` reads `<BotInput>\Tests\<name>.txt` and feeds it straight into
`TwitchEventHandler.Handle`, bypassing the socket. In chat an admin runs `!test <name>`.

The file must be a notification's `payload` object, with at least:

```json
{ "subscription": { "type": "channel.cheer" }, "event": { …the event object… } }
```

The bot's own logs hold only the `event` half. `Handle` appends `evtJson` to the day's log file, and
`HandleChannelChatNotification` writes each chat notification, pretty-printed, to
`AssistantLogs\Custom\<notice_type>_<timestamp>.log`. To turn one of those into a replayable test, wrap
it in the `{ "subscription": …, "event": … }` envelope with the right `type`.
Replays run the real handlers: sounds play, TTS speaks, chat messages are posted, and a redemption
payload would call Helix with a made-up id.

## Helix

[TwitchHelixApi.cs](../code/Twitch/TwitchHelixApi.cs) has one call:

`UpdateRedemption(rewardId, redemptionId, status)`: `PATCH
/helix/channel_points/custom_rewards/redemptions?broadcaster_id=…&reward_id=…&id=…` with
`{"status":"FULFILLED"|"CANCELED"}`. Any other status string throws. `CANCELED` refunds the points.
Anything but 200 logs a warning; non-success throws `Redemption update failed: <body>`. The attempt is
logged on `EventSubConfusion` (the `Helix` colour channel exists in the enum and window but nothing
uses it). `Init()` sets headers once and a 60 s timeout.

Callers: `ChannelPoints.CloseColorRedemption` (catches and logs `Error CP1`) and the toilet-retrieve
branch of `ProcessAdd` (does not catch).
