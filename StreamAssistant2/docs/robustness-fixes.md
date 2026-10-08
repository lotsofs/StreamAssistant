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
falls back to a fresh session; 409 keeps the session; 500 ends it). **Not yet seen against Twitch**: that
the old socket being gone caused the 4007 fits the evidence, but is only proven once a live reconnect
succeeds. What's left is that check.

**Verify live (after a restart with this build):** the next daily `session_reconnect` should log
`EventSub sent session_reconnect` → `Connecting to …cell-…` → `EventSub reconnected. Session ID: …` →
`EventSub old connection closed (…)`, with no `ES Disconnected` in chat, no `EventSub attempt to …`
subscribe lines, and events still arriving. A failure logs its reason, then `EventSub reconnect failed,
starting a fresh session`.

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

GAM ([porting.md](porting.md#gam--per-game-setup-on-category-change)) later adds its own call to
`StreamEvents.Online`.

**Verify:** `!test online` and `!test offline` log the lines; at the next real stream start and end,
the subscription lines at connect show both subscribed and the lines appear.

**Docs when done:** delete this section and the TODO row; add both to the subscription and script
tables in [twitch.md](twitch.md) and the dispatch table in [events.md](events.md); point porting.md's
shared-pieces bullet at the code instead of here.
