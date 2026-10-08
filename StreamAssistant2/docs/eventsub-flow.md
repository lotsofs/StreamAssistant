# EventSub flow charts

How [TwitchEventSub.cs](../code/Twitch/TwitchEventSub.cs) runs, method by method. The prose, the exit-reason
table and the log lines are in [twitch.md § EventSub](twitch.md#eventsub); these charts are the map.

The charts are [Mermaid](https://mermaid.js.org/). GitHub renders them; in VS Code the built-in Markdown preview
needs a Mermaid extension (for example *Markdown Preview Mermaid Support*).

Shapes: rounded boxes are steps, diamonds are decisions, `[[ ]]` boxes are calls into another chart, and red
boxes are where a session ends.

## Connection loop — `StartConnectionLoop`

Started once by `Connect()` on the thread pool; runs until `Disconnect()` cancels the token. Each pass is one
session on the default URL.

```mermaid
flowchart TD
    start(["Connect(): read credentials, set Helix headers,<br/>start StartConnectionLoop on the thread pool"])
    start --> cancelled{"token cancelled?"}
    cancelled -- yes --> stop(["loop ends"])
    cancelled -- no --> reset["_exitReason = None"]
    reset --> connect["ConnectOnce: new ClientWebSocket,<br/>ConnectOrTimeoutAsync to EventSubUrl (≤ 15 s),<br/>chat: 🟣 ES Connected"]
    connect --> sw["restart KeepAliveTimer"]
    sw --> listen[["ListenLoop"]]
    connect -.->|"throws, e.g. connect timeout"| tes1
    listen -- returns with a reason --> noreason
    listen -.->|throws| tes1["Error TES1: reason<br/>exception to the file log"]
    tes1 --> noreason{"_exitReason == None?"}
    noreason -- yes --> tes3["Error TES3"]
    noreason -- no --> ended
    tes3 --> ended["log: EventSub session ended: reason (socket state, close code)"]
    ended --> cleanup["CleanupSession: graceful close if CancelRequested, else Abort;<br/>clear _sessionId, restart KeepAliveTimer (Error TES2 if it throws)"]
    cleanup --> disc["chat: 💥 ES Disconnected"]
    disc --> wait["wait RetryDelay (3 s)"]
    wait --> cancelled
```

## Message loop — `ListenLoop`

Reads one message at a time from `_socket` and acts on it. It returns only to end the session, after setting
`_exitReason`; a successful planned reconnect stays inside it.

```mermaid
flowchart TD
    top{"token cancelled?"}
    top -- yes --> cancel(["exit: CancelRequested"]):::exit
    top -- no --> alive{"_socket open?"}
    alive -- no --> err(["exit: Error"]):::exit
    alive -- yes --> ka{"KeepAliveTimer > KeepAliveTimeout (20 s)?"}
    ka -- yes --> kat(["exit: KeepAliveTimeout"]):::exit
    ka -- no --> recv[["ReceiveFullMessage(_socket, KeepAliveTimer, 20 s)"]]
    recv -- "reason ≠ None:<br/>KeepAliveTimeout, SocketClosed, SocketDied" --> rexit(["exit with that reason"]):::exit
    recv -- whole message --> parse["parse JSON, read metadata.message_type"]
    parse --> mtype{"message type"}

    mtype -- session_welcome --> welcome["restart KeepAliveTimer, store _sessionId,<br/>log: EventSub Session Welcome"]
    welcome --> subs[["SubscribeToEvents"]]
    subs --> top

    mtype -- notification --> note["restart KeepAliveTimer"]
    note --> handle["HandleNotification: subscription.type + event<br/>→ NotificationHandler → TwitchEventHandler.Handle"]
    handle --> top

    mtype -- session_keepalive --> keep["restart KeepAliveTimer"]
    keep --> top

    mtype -- session_reconnect --> rc["log: EventSub sent session_reconnect"]
    rc --> sw2r[["SwitchToReconnectUrl(reconnect_url)"]]
    sw2r -- "true: now on the new socket" --> top
    sw2r -- false --> failed["log: EventSub reconnect failed, starting a fresh session"]
    failed --> rcexit(["exit: ReconnectRequested"]):::exit

    mtype -- anything else --> other["log on EventSubConfusion, payload to the file log"]
    other --> top

    classDef exit fill:#f8d7da,stroke:#c0392b,color:#000
```

## Subscribing — `SubscribeToEvents` / `Subscribe`

Runs after every `session_welcome` on a **fresh** session, never after a planned reconnect (subscriptions
carry over). One POST per entry in `TwitchEventSubSubscription.Subscriptions`, in order.

```mermaid
flowchart TD
    each["for each subscription descriptor"] --> cond["build condition: broadcaster / moderator / user ids<br/>(IS_TEST redirects to the test broadcaster)"]
    cond --> post["POST to SubscriptionsUrl with type, version, condition,<br/>transport = websocket + _sessionId"]
    post --> logcode["log: EventSub attempt to type: status"]
    logcode --> s409{"409?"}
    s409 -- yes --> exists["log: Subscription type already exists"] --> next
    s409 -- no --> s202{"202?"}
    s202 -- yes --> ok["response body to the file log"] --> next
    s202 -- no --> unexpected["log: Unexpected response code"]
    unexpected --> success{"other 2xx?"}
    success -- yes --> ok
    success -- no --> mark["_exitReason = SubscriptionFailed"]
    mark --> s400{"400?"}
    s400 -- yes --> f400["log: Subscription type failed, carry on"] --> ok
    s400 -- no --> thrown(["throw: Subscription failed (status)<br/>→ session ends via Error TES1"]):::exit
    next["next descriptor"] --> each

    classDef exit fill:#f8d7da,stroke:#c0392b,color:#000
```

A 400 sets `_exitReason` but doesn't end the session; the reason only shows if the session later ends through
an exception.

## Planned reconnect — `SwitchToReconnectUrl`

Twitch's flow: open the new socket while the old one stays open, take the welcome, switch, then retire the old
one.

```mermaid
flowchart TD
    url{"reconnect_url a valid absolute URI?"}
    url -- no --> badurl["log: EventSub reconnect: unusable URL"] --> f(["return false"]):::exit
    url -- yes --> c2r[["ConnectToReconnectUrlAsync(url, 15 s, 10 s)"]]
    c2r -- "null" --> f
    c2r -- "socket + session id" --> swap["_socket = new socket, _sessionId = new id,<br/>restart KeepAliveTimer<br/>log: EventSub reconnected. Session ID: id"]
    swap --> drain[["DrainOldSocketAsync(old, 1 s, HandleNotification)"]]
    drain -.->|throws| tes5["Error TES5, carry on"]
    drain --> close
    tes5 --> close["CloseQuietlyAsync(old): close handshake ≤ 2 s, dispose"]
    close --> logold["log: EventSub old connection closed (n events delivered during the switch, close code)"]
    logold --> t(["return true"])

    classDef exit fill:#f8d7da,stroke:#c0392b,color:#000
```

### `ConnectToReconnectUrlAsync`

```mermaid
flowchart TD
    mk["new ClientWebSocket<br/>log: Connecting to url"] --> conn["ConnectOrTimeoutAsync (≤ connectTimeout)"]
    conn --> first[["ReceiveFullMessage(socket, fresh stopwatch, welcomeTimeout)"]]
    first -- "reason ≠ None, e.g. 4007 close" --> nowel["log: EventSub reconnect: no welcome (reason)"] --> fail
    first -- message --> isw{"session_welcome with a session id?"}
    isw -- yes --> ok(["return socket + id"])
    isw -- no --> wrong["log: expected a session_welcome with an id, got type<br/>message to the file log"] --> fail
    conn -.->|throws| tes4["Error TES4, exception to the file log"]
    first -.->|throws| tes4
    tes4 --> fail(["dispose socket, return null"]):::exit
    conn -.->|"outer token cancelled"| rethrow(["dispose, rethrow"])

    classDef exit fill:#f8d7da,stroke:#c0392b,color:#000
```

### `DrainOldSocketAsync`

```mermaid
flowchart TD
    sw["start a stopwatch"] --> r[["ReceiveFullMessage(old, stopwatch, limit)"]]
    r -- "reason ≠ None: closed, died or limit reached" --> done(["return count"])
    r -- message --> isn{"notification?"}
    isn -- yes --> h["onNotification(root), count++"] --> r
    isn -- no --> r
```

## Reading a message — `ReceiveFullMessage`

Shared by the message loop, the reconnect welcome and the drain. Reassembles 8 KiB frames into one message and
enforces the timeout against the stopwatch it's given, so the limit counts from the last message, not from this
call.

```mermaid
flowchart TD
    open{"socket Open?"}
    open -- no --> died(["return SocketDied"])
    open -- yes --> rem{"remaining = timeout − stopwatch > 0?"}
    rem -- no --> to(["return KeepAliveTimeout"])
    rem -- yes --> rx["ReceiveAsync, cancelled after remaining"]
    rx -.->|"cancelled by the timer"| to2(["return KeepAliveTimeout<br/>(socket is aborted)"])
    rx -.->|"outer token cancelled"| thr(["throw OperationCanceledException"])
    rx --> isclose{"Close frame?"}
    isclose -- yes --> logc["log: EventSub closed the socket: code description"] --> closed(["return SocketClosed"])
    isclose -- no --> append["append frame text"]
    append --> eom{"end of message?"}
    eom -- no --> open
    eom -- yes --> msg(["return message, None"])
```

## Test seams

`EventSubLoopTests` runs `StartConnectionLoop` itself against a loopback fake of Twitch, through four
`internal static` overrides: `EventSubUrl`, `SubscriptionsUrl`, `RetryDelay`, and `NotificationHandler` (so
events reach a capture instead of the real alert handlers). The bot never changes them.
