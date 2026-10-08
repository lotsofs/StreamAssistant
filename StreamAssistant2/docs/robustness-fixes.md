# Robustness fixes

Known defects and loose ends in the tree as it stands, each with a plan. Mostly independent of each
other: each can be done, tested and committed on its own, except where an item says otherwise.

Items are indexed in [TODO.md](TODO.md): summaries, severities and the suggested order of work live
there, along with the conventions these codes follow. This file holds the detail and the plans.

**Dependencies between plans**

None at present: each item is independent.

Each plan ends with the docs to touch. When an item is finished, delete its section here and its row in
TODO.md, per the conventions there.

---

## TRS — Train left on screen after the bot closes mid-train

`ChannelPoints.RunTrainAsync` shows `Image: Train` in `!Scene: Basics Colored`, waits 62 s, then hides it
and points it back at `Empty.png`. The hide only happens if the bot is still running: closing the bot
during those 62 s leaves the train visible in OBS until someone hides it by hand. Seen by the owner;
not reproduced by a test.

### Plan

1. On bot start, once OBS is connected, hide `Image: Train` and point it at `Empty.png`, the same two calls
   as the end of `RunTrainAsync`. Use `ObsConnection.WhenConnected`, as the boot game setup does, so it
   runs however late OBS connects, once per boot. Pull the two calls into one method in `ChannelPoints`
   (`ClearTrain`) shared by `RunTrainAsync` and the boot step, and log it on `SceneChanges`
   (`Train cleared`) only when the source was actually showing, if that is cheap to check
   (`GetSceneItemEnabled`); otherwise always clear quietly.
2. Starting it from `Program.StartBotAsync` beside `Games.OnBootAsync` keeps the boot steps in one place.
3. A train started before OBS connects (`!train` in the first seconds) must not be cleared by the boot
   step: skip the clear if `_trainOnTracks` is set.
4. Optional: also clear on shutdown in `Program.Main` after `app.Run`, best effort. Not enough on its own,
   since a crash or a killed process skips it, which is why the boot clear is the fix.

**Verify:** `!train`, close the bot within 62 s, the train stays up; F5: it disappears as soon as the bot
connects to OBS. A unit test can cover the "skip while a train runs" decision if it's pulled into a pure
function; the OBS calls themselves are a live check (add to [live-checks.md](live-checks.md)).

**Docs when done:** delete this section and the TODO row; [obs.md § Train](obs.md#train) (the boot
clear), [architecture.md](architecture.md) startup order.

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

**What's left:** no event arrived on the reconnected session, so it isn't proven yet that the subscriptions
carried over. That session ended at 20:17 when Twitch reset the connection (`Error TES1: None` + `TES3`,
the documented exception path). The bot then started a fresh session with a full resubscribe. The next check: in
the log after a reconnect, find an `EventSub reconnected` line followed by a delivered event (a saved file in
`AssistantLogs\EventSubs\`) before the next `EventSub session ended`.

**Docs when done:** delete this section and the TODO row; drop the "not yet seen working" sentence in
[twitch.md § Planned reconnects](twitch.md#planned-reconnects).

## RST — EventSub connection reset logged as an unknown error

**Reproduced 2026-10-08, 20:17:** Twitch dropped the EventSub connection without a Close frame ("An existing
connection was forcibly closed by the remote host", `SocketException 10054`). `socket.ReceiveAsync` in
`ReceiveFullMessage` threw a `WebSocketException`. Nothing catches it there, so it reached the generic catch
in `StartConnectionLoop` with no reason set, and the log showed `Error TES1: None` (stack trace to the file),
`Error TES3` and `EventSub session ended: None (socket Aborted, close none)`. Recovery was correct: a
fresh session and full resubscribe 4 s later. Only the logging is wrong: a normal drop is logged as two
unexplained errors.

**Proposed fix (shelved):** catch `WebSocketException` around `ReceiveAsync` in `ReceiveFullMessage`, log
its message on `EventSubConfusion` (`EventSub connection lost: …`), and return `SocketDied`. Other
exceptions keep the `TES1`/`TES3` path. Alternative: a separate `ConnectionReset` reason. Add a loopback test
in `EventSubReceiveTimeoutTests` where the fake server resets the connection.

**Shelved by the owner to gather more data:** how often resets happen and whether they show other
exception types or messages, so the fix covers what actually arrives. To find them, search the logs for
`Error TES1: None` and check each exception in the file log.

**Docs when done:** the `SocketDied` row and the lifecycle steps in [twitch.md](twitch.md), `TES1`/`TES3`
in [reference.md](reference.md), the throw path in [eventsub-flow.md](eventsub-flow.md); delete this
section and the TODO row.

## DCD — Connection timeouts unverified live

Both transports now time out a connection that goes silent without closing, and a connect that hangs:

| Constant | Value | Effect |
|---|---|---|
| `TwitchIRCManager.SilenceTimeout` | 7 min | no IRC line → `Error TIRC2`, `Error 1`, reconnect |
| `TwitchIRCManager.ConnectTimeout` | 15 s | hung IRC connect → `Error 1`, retry |
| `TwitchEventSub.KeepAliveTimeout` | 20 s | no EventSub message → session ends with `KeepAliveTimeout`, reconnect |
| `TwitchEventSub.ConnectTimeout` | 15 s | hung EventSub connect → `Error TES1`, retry |

The helpers are unit-tested against loopback servers (`IrcReadTimeoutTests`, `IrcConnectTimeoutTests`,
`EventSubReceiveTimeoutTests`, `EventSubConnectTimeoutTests`). Not yet reproduced against Twitch: it
needs a connection that goes quiet without a TCP close.

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

**Docs when done:** delete this section and the TODO row; drop "not yet verified live" from the bullet
in [gotchas.md](gotchas.md#twitch-and-helix).

## SOE — Log Twitch stream online and offline

Nothing tells the log when Twitch itself marks the channel live or offline. OBS's stream start and
stop are logged (`OBS started streaming` / `OBS stopped streaming`, [obs.md](obs.md#connection)), but
those say what OBS did, not what Twitch saw. Not built, so nothing to reproduce.

### Plan

1. Subscribe `stream.online` and `stream.offline` (both v1, `RequiresBroadcasterId`, no scope) in
   `TwitchEventSubSubscription.Subscriptions`.
2. New `code/Twitch/StreamEvents.cs`: `Online(evt)` logs `Twitch: stream online (<started_at>)` and
   `Offline(evt)` logs `Twitch: stream offline`, both on `Important`. Synchronous; they read the JSON
   before returning.
3. Two `case`s in `TwitchEventHandler.Handle`, inside its try/catch (`Error TEH1` covers a throw).
4. `!test online` and `!test offline` scripts in `TestEvents/`, modelled on `TestCheer`, listed in
   `TestEventRunner.Scripts`, with tests of the built events in `TestScriptTests`.

**Verify:** `!test online` and `!test offline` log the lines; at the next real stream start and end,
the subscription lines at connect show both subscribed and the lines appear.

**Docs when done:** delete this section and the TODO row; add both to the subscription and script
tables in [twitch.md](twitch.md) and the dispatch table in [events.md](events.md).

## BOT — Chat as the bot account, not lotsofs

The bot connects to IRC with the broadcaster's own token (`NICK lotsofs`, hardcoded in
`TwitchIRCManager`), so everything it says in chat appears as `lotsofs`. It should speak as a separate
bot account (presumably `botsofs`, already an admin login in `ChatHandler.HandlePrivMsg`). Read only;
not built.

Only chat moves. Channel-point redemptions, bits, ad schedule and the redemption PATCH need the
broadcaster's token, so EventSub and Helix keep it.

### Plan

1. `secrets.json`: a second token block for the bot account (access token, and its login), alongside
   `twitchAuth`. Add it to `secrets.json.example` and the `Config` model; get a token with
   `chat:read` and `chat:edit` for the bot account.
2. `TwitchIRCManager`: `PASS` the bot token and `NICK` the bot login; keep `JOIN #lotsofs`.
3. Decide whether `channel.chat.notification`'s `user_id` (`TwitchIds.UserId`) stays the broadcaster
   or becomes the bot (which would then need `user:read:chat` on a token EventSub uses; simplest to
   leave it).
4. Messages typed from `lotsofs` are now read by the bot like anyone else's: check `ChatterList`
   (`"YOOO BRO"` for `lotsofs`) and whether the bot account should be skipped there.
5. Make the bot account a moderator (or VIP) in the channel so its messages aren't rate-limited or
   held.

**Verify:** after an F5 start, the connect line and a chat message from the bot show the bot login;
an EventSub event and a colour redemption still work.

**Docs when done:** delete this section and the TODO row; update [twitch.md](twitch.md#credentials-and-identity)
(credentials table and "speaks in chat as `lotsofs`"), the admin-login line in
[gotchas.md](gotchas.md#twitch-and-helix), the secrets list in CLAUDE.md, and the hardcoded values in
[reference.md](reference.md).
