# StreamElements

Tips come through StreamElements, and Twitch has no tip event, so the bot keeps a third connection
next to IRC and EventSub ([twitch.md](twitch.md)): a WebSocket to StreamElements' **Astro** gateway,
subscribed to one topic, `channel.tips`. What a tip does is in [events.md § Tips](events.md#tips).

The protocol below was checked against StreamElements' documentation (docs.streamelements.com/websockets)
on 2026-10-10. The loop is tested against a loopback fake of Astro (`StreamElementsLoopTests`). It first
ran against StreamElements on 2026-10-10, for 45 minutes: it connected, subscribed and kept the probe
answered, and survived one unannounced drop. No tip has arrived yet. What's left to see is under "Tips"
in [live-checks.md](live-checks.md).

## Files

| File | Holds |
|---|---|
| [StreamElementsSocket.cs](../code/StreamElements/StreamElementsSocket.cs) | the connection: session loop, subscribe and probes, exit reasons, retry backoff |
| [StreamElementsEventHandler.cs](../code/StreamElements/StreamElementsEventHandler.cs) | dispatch by topic, the `EventSubs\` dump, `Redact` |
| [Tips.cs](../code/StreamElements/Tips.cs) | the tip alert and its sentences |

## Credentials

`secrets.json` holds a `streamElements` block ([infrastructure.md § Config](infrastructure.md#config)):

| Setting | Used for |
|---|---|
| `StreamElements.ChannelId` | the subscribe's `room`: the StreamElements channel id (24 hex characters), not the Twitch id |
| `StreamElements.Jwt` | the subscribe's `token`, sent with `token_type: "jwt"` |

Both are on the StreamElements dashboard: avatar (top right) → the channel → *Show secrets*. With
several platforms linked, copy them while switched to the Twitch account. Never print or log either.
The code writes neither to the window or the log file, and outgoing frames are never logged.
`TokensAreNeverLogged` checks the window lines.

If either value is empty, or the block is missing, `Connect()` logs `StreamElements: no channelId or jwt
in secrets.json, tips are off` (`Important`) and never connects. The dashboard shows `off`. The rest of
the bot runs as normal.

## The protocol

All messages are JSON text frames.

| Direction | `type` | Shape and meaning |
|---|---|---|
| server → bot | `welcome` | sent on connect; `data.client_id` |
| bot → server | `subscribe` | `nonce`, `data: {topic, room, token, token_type}` |
| server → bot | `response` | echoes `nonce`; success has `data.message`, failure adds `error` (`err_unauthorized`, `err_bad_request`, `rate_limit_exceeded`, `err_internal_error`, `err_deadline_exceeded`, `invalid_message_type`) |
| server → bot | `message` | an event: `topic`, `room`, `data` |
| server → bot | `reconnect` | the server is going down; `data.reconnect_token` resumes the session on a new connection |

A `channel.tips` event's `data` holds `_id`, `donation.user.username`, `donation.user.email`,
`donation.message`, `donation.amount` (a number), `donation.currency`, `donation.paymentMethod`,
`provider`, `approved`, `status`, `createdAt`, `updatedAt` and `transactionId`.

Rate limits: one command per 100 ms (burst 100 per 10 s), and HTTP 429 on too many connections.

## Keepalive: the probe

Astro keeps the connection alive with WebSocket PING control frames every 30 s, and drops a client that
doesn't answer within 70 s. .NET 8's `ClientWebSocket` answers pings itself but never shows them to the
caller. Tips are rare, so hours without a message are normal, and a dead connection would look exactly
like a quiet one.

So the bot **probes**: it sends the subscribe again every `Times.Probe` (30 s), and any `response` counts
as a sign of life. Re-subscribing also restores a subscription the server might have dropped. A session
with no message of any kind for `SilenceTimeout` (70 s) ends with `SilenceTimeout` and reconnects.

**What Astro answers** (seen live 2026-10-10; the docs don't say): a subscribe for a topic already
subscribed gets a `response` with `error: "err_bad_request"` and `data.message: "already subscribed to
topic"`. Every probe gets exactly that, so it's the normal reply. The bot treats that pair as a success
for any nonce and logs nothing for it. In the first live run, every probe was answered and no session
hit the silence timeout in 45 minutes.

The same answer matters after a resumed `reconnect`. Astro has restored the subscription by then, so the
session's *first* subscribe gets "already subscribed" too. That has to count as acknowledged, or the
resumed session would end as `SubscribeFailed`.

## Session lifecycle

`StartConnectionLoop` reads the credentials, then repeats until cancelled or refused:

1. `ConnectOnce`: a new socket to `AstroUrl` (`wss://astro.streamelements.com`). If a reconnect token is
   saved, it goes in the query (`/?reconnect_token=…`) and is cleared, since each token is used once.
   The log says `Connecting to wss://astro.streamelements.com (resuming)` and never shows the token. The
   connect gives up after `ConnectTimeout` (15 s).
2. `ListenLoop` until something sets an exit reason. Every message restarts `SinceLastMessage`.
3. An exception logs `Error SES1: <type>: <message>`, with the exception in the file, and sets `Error`.
   A cancel during shutdown is not an error: it sets `CancelRequested`. Every exit logs
   `StreamElements session ended: <reason> (socket <state>, close …)` on `StreamElementsConfusion`.
4. `CleanupSession`:
   - On `CancelRequested`, it sends a close frame without waiting for the reply (2 s limit).
   - Otherwise it calls `Abort()`.
   - Either way it then clears `IsConnected` and restarts the stopwatch. `Error SES2` if this throws.
5. On `CancelRequested`: stop, posting nothing to chat ("🍂 Shutting Down" has already gone out).
6. If chat was told `🟣 SE Connected` and the reason isn't `ReconnectRequested`, post
   `💥 SE Disconnected`.
7. `Unauthorized`: log `StreamElements stopped: fix streamElements in secrets.json and restart the bot`
   (`Important`) and stop for good. Credentials are read once, so retrying can't help.
8. `ReconnectRequested`: go again at once, with the token.
9. Anything else: wait `RetryDelay(failures)`, then go again. The wait is 3 s, doubling each failure
   (3, 6, 12, 24 s), capped at 30 s. The failure count resets when a subscribe is acknowledged.

`ListenLoop` switches on `type`:

| Message | Does |
|---|---|
| `welcome` | logs `StreamElements welcome`, starts the session's sender task (once per session) |
| `response`, success, or `err_bad_request` / `already subscribed to topic` | the first one in a session: sets `IsConnected`, logs `StreamElements subscribed to channel.tips`, resets the failure count, and posts `🟣 SE Connected` if chat isn't already told. Later ones (every probe reply) log nothing |
| `response`, `err_unauthorized` | `Error SES3: StreamElements refused the token: <message>`, exit `Unauthorized` |
| `response`, any other error on the first subscribe | `Error SES3: StreamElements subscribe failed: <error> <message>`, exit `SubscribeFailed` |
| `response`, any other error on a probe | file log only; the session carries on |
| `message` | `NotificationHandler(topic, data)`, which is `StreamElementsEventHandler.Handle` |
| `reconnect` | saves `data.reconnect_token`, logs `StreamElements sent reconnect: <message>`, exit `ReconnectRequested` |
| anything else | log on `StreamElementsConfusion`, payload to the file |

**The sender task** (`SubscribeAndProbeAsync`) does all of a session's sending, because a
`ClientWebSocket` allows only one send at a time:
- It sends the subscribe with nonce `subscribe`, then repeats it every probe interval with nonces
  `probe-1`, `probe-2`, ….
- It stops when the session's cancellation source fires.
- A send that throws while the session is still open logs `Error SES4`; the receive side then ends the
  session.

**Chat stays quiet across a planned reconnect.** `🟣 SE Connected` goes out on the first acknowledged
subscribe after chat was last told `💥 SE Disconnected` (or after startup). A `reconnect` that resumes
cleanly posts neither line. A resume that fails ends in some other reason, and that posts
`💥 SE Disconnected`. A long outage posts one `💥 SE Disconnected`, not one per retry.

| Reason | Set when |
|---|---|
| `None` | initial value |
| `Error` | an exception (`Error SES1`) |
| `SilenceTimeout` | no message for `SilenceTimeout` (70 s) |
| `ReconnectRequested` | the server sent `reconnect` |
| `SubscribeFailed` | the first subscribe got an error other than `err_unauthorized` |
| `Unauthorized` | any `response` with `err_unauthorized`; the loop stops |
| `CancelRequested` | the token was cancelled (shutdown) |
| `SocketClosed` | a Close frame arrived (`StreamElements closed the socket: …`) |
| `SocketDied` | the socket stopped being `Open` mid-receive |
| `ConnectionLost` | the connection dropped without a Close frame (`StreamElements connection lost (<WebSocketError>): …`) |

`ConnectOrTimeoutAsync` and `ReceiveFullMessage` are copies of EventSub's, with their own log text and
reasons, so a StreamElements drop never reads as an EventSub one.

**Unannounced drops happen.** In the first live run (2026-10-10, 20:19:13), Astro ended a session after
21.5 minutes with no `reconnect` message and no Close frame. The log read `StreamElements connection lost
(ConnectionClosedPrematurely): The remote party closed the WebSocket connection without completing the
close handshake.` That was a clean end of the connection, unlike Twitch's resets ("forcibly closed by the
remote host"). IRC and EventSub stayed up, so the home connection was fine. A probe reply had arrived 5 s
earlier, so the bot's own timeout wasn't the cause either. `astro.streamelements.com` resolves to
Cloudflare addresses, and Cloudflare can end long-lived WebSockets at its edge, so that is the likely
cause. The next session ran past 22.5 minutes, which argues against a fixed lifetime. The bot handled it
as designed: `ConnectionLost`, `💥 SE Disconnected`, a new session 3 s later, `🟣 SE Connected`. A tip
sent during such a gap probably never reaches the bot. StreamElements' own overlays are unaffected.

## Dashboard and health

The status bar's third segment, `StreamElements time since last reply`, shows `SinceLastMessage`. A
healthy connection counts up to about 30 s and resets with each probe reply.
`ConnectionHealth.StreamElements` sets the colours: yellow past 40 s, orange past 50 s, red past
`SilenceTimeout` (70 s), the reconnect point. Its tracker logs `StreamElements quiet for …` once on
reaching 50 s, and `StreamElements recovered after …` once on recovery
([infrastructure.md § Connection health](infrastructure.md#connection-health)). It skips while
`IsConnected` is false. With no credentials, the segment shows `off`.

## Events

`StreamElementsEventHandler.Handle(topic, data, isTest)` mirrors `TwitchEventHandler.Handle`:
- It logs `SE notification: <topic>` on `StreamElementsNotification`.
- Unless `isTest`, it dumps the event to `AssistantLogs\EventSubs\se.<topic>_<stamp>.log`, so
  `!test replay` can rerun it.
- It switches on topic. `channel.tips` goes to `Tips.Process` through `FireForget.Run("SEH_t", …)`, on a
  clone of the data. Any other topic logs on `StreamElementsConfusion`.
- It writes the event to the log file.
- `Error SEH1` if it throws.

Both the dump and the log file get `Redact(data)`, which removes `donation.user.email`. The tipper's
email address never reaches disk.

`!test tip <amount> [msg <text…>]` builds a tip and `!test replay se.channel.tips_…` replays a real one.
Both go through this handler with `isTest: true`, because their type starts with `se.`
([twitch.md § The test harness](twitch.md#the-test-harness)).

## Overrides

Three `internal static` fields exist only so tests can run the real loop against a loopback fake. The
bot never changes them:
- `AstroUrl`
- `Times`: probe interval, silence limit, retry base and cap
- `NotificationHandler`

`SilenceTimeout` itself is `readonly`, since `ConnectionHealth` takes its dead threshold from it.

## Not verified

- A resumed `reconnect`: none has been seen yet. The resume relies on the "already subscribed" answer
  counting as acknowledged (above), which is unit-tested but not seen live.
- Whether the dashboard's emulated tips (activity feed → *Emulate*) arrive on `channel.tips`, or only reach
  overlays.
- Whether a tip held for moderation arrives once, or again on approval. `Tips` skips an `_id` it has
  already alerted this run, and logs each tip's `status` and `approved`, so the first real tips will show.
