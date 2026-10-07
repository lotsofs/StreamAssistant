# Robustness fixes

Known defects and loose ends in the tree as it stands, each with a plan. Mostly independent of each
other: each can be done, tested and committed on its own, except where an item says otherwise.

Items are indexed in [TODO.md](TODO.md): summaries, severities and the suggested order of work live
there, along with the conventions these codes follow. This file holds the detail and the plans.

**Dependencies between plans**

- [ALR](#alr--log-line-when-a-connection-degrades-or-recovers) follows
  [DCD](#dcd--dead-connections-are-never-recycled), whose timeouts it shares thresholds with.
- [ESR](#esr--eventsub-reconnect-path-unverified) and DCD both edit `TwitchEventSub.ReceiveFullMessage`;
  do one, then the other, not in parallel.
- Everything else is independent.

Each plan ends with the docs to touch. When an item is finished, delete its section here and its row in
TODO.md, per the conventions there.

---

## DCD — Dead connections are never recycled

Neither transport can detect a socket that dies without closing:

- **IRC**: `ReadLineAsync` has no timeout. The dashboard's age turns red at 420 s, but nothing
  reconnects. Twitch pings about every five minutes, so a timeout of well over that would be needed.
- **EventSub**: `ListenLoop` checks the 20 s keepalive limit only at the top of an iteration, but the
  `ReceiveAsync` inside `ReceiveFullMessage` has no timeout, so total silence blocks before the check
  is reached. The dashboard shows red; the session stays.

Related to [ALR](#alr--log-line-when-a-connection-degrades-or-recovers), which would at least record it.
Read, not reproduced: it needs a connection that goes quiet without a TCP close.

### Plan

**IRC** ([TwitchIRCManager.cs](../code/Twitch/TwitchIRCManager.cs)):

1. Add `internal static readonly TimeSpan SilenceTimeout = TimeSpan.FromMinutes(7)`, matching the
   dashboard's red threshold (Twitch pings roughly every five minutes, so seven means two missed pings).
2. Extract `internal static async Task<string?> ReadLineOrTimeoutAsync(StreamReader reader, TimeSpan timeout, CancellationToken token)`.
   It creates a linked `CancellationTokenSource`, calls `CancelAfter(timeout)`, and awaits
   `reader.ReadLineAsync(linked.Token)`. If it throws `OperationCanceledException` while the *outer*
   token is not cancelled, rethrow as `TimeoutException`; if the outer token is cancelled, let it
   propagate (shutdown).
3. `ListenLoop` calls it instead of `ReadLineAsync`. A `TimeoutException` flows through the existing
   path: `Error TIRC2`, `Error 1`, 3 s, reconnect.
4. Give `ConnectOnce`'s `TcpClient.ConnectAsync` a timeout too (a linked token with `CancelAfter(15 s)`);
   it can also hang.

**EventSub** ([TwitchEventSub.cs](../code/Twitch/TwitchEventSub.cs)):

5. Add `internal static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(20)`, replacing the
   literal in `ListenLoop`.
6. Change `ReceiveFullMessage` to take the socket and a timeout: before each `ReceiveAsync`, compute
   `remaining = KeepAliveTimeout - KeepAliveTimer.Elapsed`; if it is not positive, set
   `KeepAliveTimeout` as the exit reason and return `""`. Otherwise receive with a linked token and
   `CancelAfter(remaining)`; if that fires while the outer token is not cancelled, set
   `SessionExitReason.KeepAliveTimeout` and return `""`. (A cancelled `ReceiveAsync` aborts the socket,
   which `CleanupSession` is about to do anyway.)
7. Optionally read `keepalive_timeout_seconds` from the welcome message and use it plus a margin
   instead of the fixed 20 s. Twitch's default is 10 s; leave it fixed unless the setting is changed.

Coordinate with [ESR](#esr--eventsub-reconnect-path-unverified), which edits the same method.

**Tests:**

- IRC: run `ReadLineOrTimeoutAsync` over a stream that never produces data (an open pipe or a
  `TcpListener` connection that sends nothing) with a 200 ms timeout: expect `TimeoutException`; with the
  outer token cancelled: expect `OperationCanceledException`; with data available: expect the line.
- EventSub: a loopback `HttpListener` that accepts a WebSocket and never sends: the receive helper
  returns with `KeepAliveTimeout` after the (shortened) timeout. Same with a server that sends a message
  slowly in two frames.

**Verify live (owner, off-stream):** temporarily drop the IRC and EventSub timeouts to a few seconds,
start the bot, then block outbound traffic to Twitch without closing the sockets (a Windows Firewall
outbound block rule on the process is enough) and watch the dashboard go red and then the connection
recycle. Restore the constants.

**Docs when done:** the "no timeout" sentences in [twitch.md](twitch.md#irc), the 20 s paragraph in
[twitch.md](twitch.md#session-lifecycle), [dashboard.md](dashboard.md#the-status-bar), and the DCD bullet
in [gotchas.md](gotchas.md#twitch-and-helix).

## ESR — EventSub reconnect path unverified

When Twitch sends `session_reconnect`, `ListenLoop` stores `reconnect_url` and exits. The loop then runs
`CleanupSession` (which **aborts** the old socket), waits 3 s, connects to the new URL and, on
`session_welcome`, runs `SubscribeToEvents()` again.

**Observed in the real logs** (`<BotOutput>\AssistantLogs\`, the recent ones read, not just one): Twitch
sends `session_reconnect` about once a day, around 19:00 to 19:35 local time. Every occurrence that was
read has the same shape:

```
EventSub sent session_reconnect
> 💥 ES Disconnected
Connecting to wss://cell-a.eventsub.wss.twitch.tv/ws?challenge=…&id=…      (3 s later)
Connected to EventSub
> 🟣 ES Connected
> 💥 ES Disconnected                                                       (1 to 15 ms later)
Connecting to wss://eventsub.wss.twitch.tv/ws                              (3 s later)
Connected to EventSub  →  session welcome, resubscribe
```

So the **reconnect URL never works**: the new connection ends within milliseconds, with no
`session_welcome` and no `Error TES…` line, which means the loop left through a `SessionExitReason` that
sets no log of its own (`SocketClosed` or `SocketDied`: the server, or the socket, closed it). The bot
recovers only by falling back to a full new session on the default URL, roughly seven seconds later, and
resubscribing from scratch. Events in that gap are lost, and the chat sees a
`💥 ES Disconnected` / `🟣 ES Connected` pair twice.

What is *not* known is why the new connection is closed. Candidates, none confirmed:

1. The old socket was aborted *before* the new one connected. Twitch's reconnect flow says to open the new
   connection while keeping the old one until the new one's welcome arrives; closing first may
   invalidate the reconnect token embedded in the URL.
2. The new connection did get a welcome-less close for some other documented reason (Twitch closes with a
   4xxx code; the bot discards the code).
3. A subscription conflict. Ruled out as the *cause* of this symptom, since no subscribe was attempted,
   but it is a separate risk once the connection survives: if subscriptions carry over, re-POSTing them
   on the new session may answer 409, which `Subscribe` throws as `Subscription failed (…)` for any
   status other than 400.

### Plan

1. **Instrument first** (small, safe, worth keeping). In `ReceiveFullMessage`, when a Close frame arrives,
   log `result.CloseStatus` and `result.CloseStatusDescription` on `EventSubConfusion`. In
   `StartConnectionLoop`, log one line for **every** session exit saying the reason (today only
   exceptions and `None` are logged). Ship that; the next daily reconnect will say why.
2. **Fix per Twitch's documented flow**, informed by what step 1 logs:
   - On `session_reconnect`, **don't clean up first.** Connect a second socket to `reconnect_url` while
     the first stays open.
   - On the new socket's `session_welcome`, **skip `SubscribeToEvents()`**: the subscriptions move with
     the session. Then swap `_socket` to the new socket, restart the keepalive stopwatch, and close the old
     socket gracefully.
   - Keep the old socket unread during the overlap; events in that brief window are Twitch's to
     redeliver or drop, which is acceptable. If the new connection doesn't welcome within about 10 s,
     fall back to today's behaviour (cleanup and a fresh default-URL session).
   - Carry "this is a reconnect" as state (`_isReconnect`), set when connecting to a pending URL and
     cleared once welcomed.
   - Don't post `💥 ES Disconnected` / `🟣 ES Connected` to chat for a planned reconnect.
3. **Make a 409 on subscribe harmless** (an already-existing subscription is success). This protects the
   fallback and any future overlap.
4. Make this testable: `ListenLoop` and the connect step should take the socket and the URLs as
   parameters, with the two endpoint URLs as `internal static` overrides so a mock server can stand in.

**Tests:** the Twitch CLI ships a WebSocket mock EventSub server (`twitch event websocket`; check
`--help` for its reconnect and close options and its local Helix endpoint). Point the overrides at it and
exercise: welcome then reconnect then welcome-on-new-socket (no resubscribe, old socket closed, events
still delivered), a reconnect whose new socket never welcomes (fallback), and a 409 on subscribe.
Without the CLI, a loopback `HttpListener` WebSocket server scripted to do the same is enough for the
state machine.

**Verify live:** after step 1, read the next day's reconnect lines for the close code. After step 2, the
next daily `session_reconnect` should show no `ES Disconnected`, no second `Connecting to`, no
subscribe attempts, and events still arriving.

**Docs when done:** the session lifecycle and exit-reason tables in [twitch.md](twitch.md#eventsub), and the
two ES chat lines in [gotchas.md](gotchas.md#code-that-goes-live).

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

**Verify (owner, off-stream):** `!test` replays of saved `sub_*.log` payloads from
`AssistantLogs\Custom\` (wrap each in the `{"subscription":…,"event":…}` envelope, see
[twitch.md](twitch.md#the-test-harness)). The spoken text is logged as `TTS Enqueue: …`, so the result
can be read without listening. These replays do play sounds and speak.

**Docs when done:** the table and the "Known message defects" subsection in
[events.md](events.md#subscriptions), and the "Gift bombs" bullet about entries never being removed.

## ALR — Log line when a connection degrades or recovers

The status bar shows IRC ping age and EventSub keepalive age live, but nothing records them: once
the window has moved on, the dated log has no trace that a connection went stale. Add a coloured log
line when either crosses into a worse band (the status bar's thresholds) and when it recovers,
debounced so a value hovering on a threshold doesn't spam, and with the startup false positive
suppressed (both ages start counting before the first ping or keepalive arrives).

`DiskSpace.AlertState` is a working model for the escalate-immediately, repeat-on-an-interval part.

### Plan

Do after DCD (the dead band then ends in a reconnect, and the thresholds are shared).

1. **One source of thresholds.** New `code/Twitch/ConnectionHealth.cs` with `enum Band { Healthy, Warn,
   Bad, Dead }`, a `Thresholds(warn, bad, dead)` record, and static `Irc` (300, 360, 420 s) and `EventSub`
   (10, 12, 15 s) instances, plus `Band Classify(TimeSpan age)`. `MainWindow.UpdateStatus` switches to
   these instead of its literals, so the window and the log can't disagree.
2. **A pure tracker.** A small class (one instance per connection) with
   `Update(TimeSpan age, bool hasData, DateTime nowUtc)` returning an optional `(ColorType, string)`.
   Rules:
   - `hasData == false` (nothing received yet, or between sessions) resets the tracker and returns nothing.
     That is the startup suppression.
   - Only transitions into **Bad** or **Dead** are logged (`Important`), and a return to **Healthy** after a
     logged degradation (`ConnectionNotification`, saying how long it was degraded). Warn alone is silent.
   - **Escalation is immediate** (the age is already a sustained measure); every other transition must hold
     for a debounce period (say 10 s) before it counts. Recovery from Bad to Warn is not logged; only the
     return to Healthy is.
   - Messages carry the connection name and the age, for example `IRC quiet for 06:02, connection may be
     dead` and `IRC recovered after 07:12`.
3. **Inputs.** Add `TwitchIRCManager.HasReceivedLine` (the last-line time is not `MinValue`) and
   `TwitchEventSub.IsConnected` (a session id is set), so `hasData` is real and a planned 3 s reconnect gap
   isn't flagged.
4. **Run it as its own loop** (`async` with `Task.Delay(5 s)` and a try/catch, started from `Program`
   like `DiskSpace.Start()`) that updates both trackers and logs the results. Don't run it from the window's
   timer; the log must not depend on the window, and don't post to chat.
5. State is per connection, and nothing depends on the wall-clock second.

**Tests:** the tracker is pure, so feed it synthetic age sequences and a fake clock: silent while
`hasData` is false; a climb to Bad logs once and then to Dead logs once; an age hovering across the Bad
threshold logs once, not repeatedly; recovery logs once and only after a degradation was logged; a Warn
excursion that returns to Healthy logs nothing; restarting (hasData false then true) starts clean.

**Verify live:** with the bot idle, the first minutes log nothing. With DCD's shortened timeouts (see its
verification) and a blocked connection, the dashboard going red coincides with one `Important` line, and
the reconnect that follows with one recovery line.

**Docs when done:** the thresholds in [dashboard.md](dashboard.md#the-status-bar) and
[reference.md](reference.md#thresholds), and a line in [infrastructure.md](infrastructure.md#periodic-work).

## BUF — Buffered log file writes

`ConsoleLogger.LogToFileAsync` opens, appends and closes the log file for every line, on the
per-chat-message hot path. Holding one `StreamWriter` and flushing when the queue drains would be
cheaper. Deferred because it trades away durability of the log tail on a hard kill (stopping the
debugger kills the process without running shutdown). Read, not measured.

### Plan

1. **Measure first.** A scratch program that calls `ConsoleLogger.ColoredLine` 10,000 times and times it,
   before and after. If the saving doesn't justify the durability trade, close BUF with the
   measurement written into this section instead of changing code. (Real chat rates are low; the case for
   the change is mostly bursts such as a gift bomb or a log-heavy test replay.)
2. **Design if it goes ahead.** Replace the per-line `File.AppendAllTextAsync` for the *main* log with a
   single-reader `Channel<string>` and one writer task holding a `StreamWriter` opened for append with
   `FileShare.Read`, so the log can still be tailed in an editor.
   - The writer awaits one line, writes it, then keeps writing while `TryRead` succeeds, and **flushes when
     the channel is empty**. That keeps the exposure to a hard kill to the lines still queued, a
     few milliseconds' worth, instead of a whole buffer.
   - Entries keep today's format: the text followed by a blank line.
   - The channel also gives true FIFO ordering, which the semaphore only roughly does.
   - Leave `LogToCustomFile` as open/append/close: it is rare and one file per event.
3. **Failures.** On an `IOException` the writer drops its stream, raises `Error LOG1` once through the
   existing `ReportFileWriteFailure` (still file-free), and reopens on the next line, so a drive that
   comes back resumes without a restart. Keep the "once until a write succeeds" behaviour.
4. **Shutdown.** Add `ConsoleLogger.FlushAsync(TimeSpan)` that completes the channel and waits for the writer;
   call it at the end of `Program.Main` after `DisableBot()`, so the `SHUTDOWN!` line survives. A hard kill
   still loses the lines in flight; the docs should say so.
5. The log file's name is still fixed at first use; `LogPath` is unchanged.

**Tests:** the logger writes to `Config.Data.Directories.BotOutput`, so a test can point that at a
temp directory: many concurrent `LogToFile` calls arrive complete and in per-thread order; a
`FlushAsync` leaves everything on disk; a read-only target raises `Error LOG1` once, then recovers when
made writable.

**Verify:** run the 10,000-line scratch program against both builds and compare; open the log in an
editor mid-run to confirm it can be read while open.

**Docs when done:** the "Writes" bullet in [infrastructure.md](infrastructure.md#logging), the log-hot-path
bullet in [gotchas.md](gotchas.md#logging) (the durability note changes), and
[development.md](development.md) if it mentions the scratch-program recipe.
