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

---

## SGN — LiveSplit never writes `GainedAhead` / `GainedBehind` during a run

Your component, `LiveSplit.OutputToFile` (`D:\Repositories\LiveSplit.OutputToFile`), writes the previous
split's sign to `PreviousSplit\<level>\<method>\Sign.txt`. In `OutputToFileComponent.WriteSplitTimes`, the
branch for a run in progress goes:

1. no run delta → `NotApplicable`
2. a gold (or no gold before) → `GoldAhead` / `GoldBehind`
3. no segment delta → `Ahead` / `Behind`
4. anything else → `LostAhead` / `LostBehind`

It has no case for a segment that beat its PB without being a gold, so such a split is written as `Lost…`.
The end-of-run branch does have it (`PBGained` / `NoPBGained`). The bot's split colours
([obs.md](obs.md#split-colours)) then colour that segment difference `LabelNegative` instead of
`LabelPositive`, on stream. Read in the component's source; not reproduced.

### Plan

1. In the running branch, before the last `else`, add
   `else if (segmentDelta < TimeSpan.Zero) { MakeFile(FILE_PREVIOUS_SIGN, (runDelta < TimeSpan.Zero ? Signs.GainedAhead : Signs.GainedBehind).ToString(), method, lvl); }`,
   the same test the end-of-run branch makes.
2. Rebuild the component and copy its DLL into LiveSplit's `Components` folder, with LiveSplit closed
   (owner).
3. Nothing changes in the bot: `SplitColors.SignKeys` already maps `GainedAhead` and `GainedBehind`.

**Verify:** in a practice run, beat a segment's PB without a gold. `Sign.txt` reads `GainedAhead` or
`GainedBehind`, and on SWAT4 the bot colours the previous split's segment difference `LabelPositive`.

**Docs when done:** delete this section and the TODO row, and the LiveSplit note at the end of
[obs.md § Split colours](obs.md#split-colours).

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

## PGD — Portal2's per-map gold panel reads files LiveSplit never writes

Portal2's `Per map: Golds Delta` panel in `leftpanel.json` points `Livesplit: SL - Current` and `Upcoming`
at `SplitList\SansSubsplits_Level1\RealTime\Delta_Gold.txt` and `Time_Gold_Upcoming.txt`. The component
writes gold files only for its last level, `AllSplits` ("Golds are not supported for subsplits" in
`OutputToFileComponent.cs`), so neither file exists and the values column is empty for the panel's 50 s.
The panel came over from Streamer.Bot's `scenes.json` as it was. Found by checking the file against the disk
(2026-10-12) and the component's source; not seen live.

### Plan

The owner picks one:

- Drop the panel.
- Make it an all-splits gold panel: `AllSplits` names, highlight and gold files, placed after Portal2's
  `All splits: Finished at` (which sets the `AllSplits` labels) with `"Keep": ["Splits List Labels"]`.
- Have the component write per-level golds. A level's "gold" would have to be defined (the sum of its
  splits' golds isn't one), so this is the most work.

**Verify:** the scratch cross-check of `leftpanel.json` against the disk finds no missing files; live, the
panel shows values.

**Docs when done:** delete this section and the TODO row, and the known-gap paragraph in
[obs.md § Where the data came from](obs.md#where-the-data-came-from).

---

## PBL — `PBLost` leaves the segment difference uncoloured

`SplitColors.SignKeys` ports Streamer.Bot's sign table as it was. For `PBLost` (the run's last split, a PB,
but that segment lost time) the old table gives the segment `None`. `NoPBLost` gives `LabelNegative`, and
`PBGained` gives `LabelPositive`, so `PBLost` looks like a slip. Read only.

### Plan

If the owner agrees: `"pblost" => (POSITIVE, NEGATIVE, NONE)` in `SignKeys`, the `PBLost` row in
`SplitColorsTests`, and the table in [obs.md § Split colours](obs.md#split-colours) (which also drops its
note about it).

**Verify:** the tests.

**Docs when done:** delete this section and the TODO row.

---

## DSB — Unused `disabled` colour category

`gameschemes.json` and `countryschemes.json` (in `Stream-Resources\Colors\Schemes\`, the Stream-Resources
repo) each have a `disabled` category. Its black-and-white scheme and all-white split colours are the same
as `none`'s. Under Streamer.Bot, `disabled` was the default of `currentGame` while the bot's game features
were off ("Disabling bot stuff for %currentGame%").

The bot uses only `gameschemes`' `none`: as the `None` game's colour and as the split-colour fallback.
`disabled` can only be reached by typing it (`!changecolor disabled`). `countryschemes`' own `none` is
unused too. A lookup without a set name may find either file's `none`, but the colours are the same. Read
only.

### Plan

Delete `disabled` from both files, and `none` from `countryschemes.json`, keeping `gameschemes`' `none`.

**Verify:** F5 logs no new colour-scheme lines, `!changecolor none` still works, and the real-data smoke
tests pass.

**Docs when done:** delete this section and the TODO row; drop `disabled` from the lists of categories with
split colours in [color.md](color.md#data-files) and [obs.md § Split colours](obs.md#split-colours).

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
