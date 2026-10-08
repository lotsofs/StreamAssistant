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

## SBM — Subscription message defects

All in [Subscriptions.cs](../Subscriptions.cs), all read, not reproduced:

- `HandleSubNotif` appends `It is a N month sub. ` with no leading separator, so the spoken sentence
  runs together: `foo subscribedIt is a 3 month sub.` (the `resub` handler uses `, `).
- `HandleCommunitySubGiftNotif` builds the tier text as `sub_tier > 1 ? "" : $"tier {sub_tier} "`, which
  is inverted: tier 1 is announced, tiers 2 and 3 are not. (When it is empty the sentence also has a
  double space, `… N  subs`.)
- `_giftBombs` entries are never removed, so each bomb leaves a `CommunityGiftSub` (and its recipient
  set) behind for the rest of the run. Small per bomb.

### Plan

Make the sentences pure functions so they can be tested, then fix them.

1. **Extract the builders.** `internal static` functions in `Subscriptions`:
   `BuildSubMessage(user, tier, isPrime, months)`, `BuildResubMessage(…)`, `BuildGiftMessage(…)`,
   `BuildBombMessage(gifter, total, tier, cumulative, recipients)`. Each handler reads its JSON, calls
   its builder, then does the sound, delay and `EnqueueSpeech` exactly as before. No change to timing.
2. **Fix the sub sentence.** Join the month count with a separator, in the resub handler's style:
   `foo subscribed, It is a 3 month sub` or, closer to the original intent, `foo subscribed at tier 2.
   It is a 3 month sub.` Pick one and use the same style in the resub builder; the owner can veto wording.
3. **Fix the tier test** to match the sub and resub sentences, where tier 1 is the unmarked default:
   `tierText = sub_tier > 1 ? $"tier {sub_tier} " : ""`, and format as `{total} {tierText}subs` so there is
   no double space. (Alternative: always say the tier, as the targeted-gift sentence does. Owner's call.)
4. **Clean up bombs.**
   - Take `_giftBombsLock` for every access to `_giftBombs`. The continuation after
     `WaitForRecipientsAsync` runs on a thread-pool thread, so once removal happens there the "single
     thread in practice" argument no longer holds.
   - Remove the entry after the announcement is built.
   - A late `sub_gift` arriving after removal would recreate an entry nobody announces. Give
     `CommunityGiftSub` a `CreatedUtc` and sweep entries older than an hour whenever a new one is created.
   - Delete the unused `_giftees` and `_giftBombWaiters` fields only if asked; they are dead but harmless.
5. Make the 10 s wait in `CommunityGiftSub.WaitForRecipientsAsync` take an optional `TimeSpan` so tests
   don't wait ten seconds.

**Tests** (new `SubscriptionMessageTests`): sub with and without prime, tier 1, 2 and 3, and months 1
and 3; resub with and without a streak and a message; targeted gift with first-gift and multi-gift
totals; bomb sentence for each tier with no double spaces; `CommunityGiftSub` completes when recipients
arrive before `SetExpected`, after it, and never (times out with the partial list); sweep drops old
entries and keeps new ones. These touch only pure functions and `CommunityGiftSub`, so nothing posts to
chat or plays sound.

**Verify (owner, off-stream):** `!test replay <file>` of saved `sub_…`, `resub_…` and
`community_sub_gift_…` files from `AssistantLogs\EventSubs\`, plus `!test sub` / `resub` / `bomb` for the
variants the logs lack (see [twitch.md](twitch.md#the-test-harness)). The spoken text is logged as `TTS Enqueue: …`, so the result
can be read without listening. These replays do play sounds and speak.

**Docs when done:** the table and the "Known message defects" subsection in
[events.md](events.md#subscriptions), and the "Gift bombs" bullet about entries never being removed.

## TRX — Overlapping trains hide each other

`ChannelPoints.RunTrainAsync` sets `Image: Train` to a random image, shows it, waits 62 s, then hides it
and resets it to `Empty.png`. A second train started inside that window swaps the image, and the first
run's cleanup then hides it about 62 s after the *first* start, cutting the second one short. Both the
reward and the `!train` admin command call it. Read, not reproduced.

### Plan

Pick one, owner's call:

- **Queue:** serialise trains through a single worker, like `LayoutColoring`, so each gets its full 62 s.
- **Extend:** keep a generation counter or `CancellationTokenSource`; a new train cancels the previous
  run's pending cleanup and restarts the 62 s, so only the last one hides the source.
- **Ignore:** drop the second train while one is showing, and log it.

**Verify:** `!train` twice a few seconds apart, then watch whether the second image stays up its full time.

**Docs when done:** the Train row and the overlap bullet in [events.md](events.md), and the Train section
in [obs.md](obs.md#train).

## OSY — Train OBS calls run on the read loops

Handlers are invoked synchronously up to their first `await`, by design, so they can read the
`JsonElement` before it's disposed. `RunTrainAsync` calls `Obs.SetImageSource` and `Obs.SetSourceEnabled`
before its first `await`, so those run on the EventSub listen loop (the reward) or the IRC read thread
(`!train`). The `Obs` wrappers are synchronous, so a slow or hung obs-websocket call stalls reading for
that long. The colour rewards and commands are not affected: they only enqueue onto `LayoutColoring`'s
channel, whose worker runs on the thread pool. Read, not reproduced; how long a call can block hasn't been
measured.

### Plan

1. **Measure first:** time the `Obs` wrapper calls with OBS running, idle and busy. If they're consistently
   a few ms, close this item and record the measurement in [obs.md](obs.md).
2. **If not:** start `RunTrainAsync` with `await Task.Yield()` (it reads no JSON, so the lifetime rule
   doesn't apply), moving its OBS calls off the read loop.

**Docs when done:** [obs.md](obs.md#train) and the handler-threading note in
[twitch.md](twitch.md#adding-an-eventsub-event).

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
