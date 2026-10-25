# Robustness fixes

Known defects and loose ends in the tree as it stands, each with a plan. Mostly independent of each
other: each can be done, tested and committed on its own, except where an item says otherwise.

Items are indexed in [TODO.md](TODO.md): summaries, severities and the suggested order of work live
there, along with the conventions these codes follow. This file holds the detail and the plans.

**Dependencies between plans**

None at present: each item is independent.

Each plan ends with the docs to touch. When an item is finished, delete its section here and its row in
TODO.md, and log it in [DONE.md](DONE.md), per the conventions there.

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


## DSB — Unused `disabled` colour category

`countryschemes.json` (in `Stream-Resources\Colors\Schemes\`, the Stream-Resources repo) has a `disabled`
category, with `none`'s black-and-white scheme and all-white split colours; `gameschemes.json`'s is gone
already. Under Streamer.Bot, `disabled` was the default of `currentGame` while the bot's game features were
off ("Disabling bot stuff for %currentGame%").

The bot uses only `gameschemes`' `none`: as the `None` game's colour and as the split-colour fallback, which
is always read from `gameschemes`. `disabled` can only be reached by typing it (`!changecolor disabled`).
`countryschemes`' own `none` is unused too; its split colours are all white where `gameschemes`' are not,
but a lookup without a set name, which may find either `none`, only takes the scheme, and that is the same.
Read only.

### Plan

Delete `disabled` and `none` from `countryschemes.json`, keeping `gameschemes`' `none`.

**Verify:** F5 logs no new colour-scheme lines, `!changecolor none` still works, and the real-data smoke
tests pass.

**Docs when done:** delete this section and the TODO row.

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

---

## RLD — `!reloaddata`: reload the data files without a restart

The data files are read once, at startup: the colour tables and schemes (`Coloring.Load`, which also holds
the split colours), `games.json` (`Games.Load`) and `leftpanel.json` (`LeftPanel.Load`). An edit to any of
them takes effect only after a restart, which also drops and remakes every connection and posts the
disconnect and connect lines in chat. Asked for by the owner (2026-10-14) as `/reloadData`; not built.

It is named `!reloaddata` here because the admin commands are `!` words matched case-sensitively in
`ChatHandler.CheckForAdminCommands`, all lowercase. A line starting with `/` is taken by Twitch's chat as
one of its own commands, and an unknown one is refused rather than sent to chat. That is from memory of
Twitch's chat, not tried here.

Left out on purpose: `paths.json` and `secrets.json` (`Config.Load`), since the connections and directories
are taken from them at start. Also files already read at each use: backgrounds, border images, train
images, alert sounds and LiveSplit's text files.

What stands in the way (read, not reproduced):

- **The colour registries are refilled in place.** `ColorSchemeRegistry.LoadSets` and
  `ColorTableRegistry.LoadTables` `Clear()` their shared dictionaries and fill them again. A colour
  redemption, `GameColor.Set` or `SplitColors.For` running at the time reads a half-filled `Dictionary`,
  which can give a wrong answer or throw. Neither catches, either: one broken file throws partway and leaves
  the registries part-empty. At startup that is `Error PRG1` and a half-started bot; on a reload it would
  wipe colours that were fine a moment before.
- **`Games.Load` and `LeftPanel.Load` assign a new dictionary**, so readers are safe and a file that fails to
  parse keeps the old data. But their missing-file lines (`games.json not found, per-game setup off`,
  `leftpanel.json not found, left panel off`) would be wrong on a reload, where the old data stays.
- **A running left-panel rotation keeps what it started with.** `LeftPanel.Start` hands `RunAsync` its
  `PanelSet` and split colours, so new panels and colours show only from the next `Start`.
- **The current game's layout isn't redone.** The background, border, captures, volumes and colour stay as
  they were set until the next game setup. `!changegame` on the current category does nothing
  (`Category is already …`), and `!changepanel` and `!changecapture` redo only their own parts.

### Decisions

- **Rotation:** restart the current game's rotation from its first panel straight after the reload (unless
  `!stoppaneltimer` stopped it), or leave it until the next `Start`. Recommended: restart, so a
  `leftpanel.json` edit can be seen at once.
- **Game setup:** also run the full game setup for the current category (`Games.Apply(CategoryId)`), or
  not. It recolours the layout to the game's colour, undoing a viewer's colour redemption. Recommended:
  not; `!changepanel` and `!changecapture` cover the parts that change most.

### Plan

1. **Colour registries:** build the new dictionaries aside and publish each in one assignment
   (`_randomPool` with the tables), so a reader sees all the old data or all the new. `Sets` and `Tables`
   stop being `readonly` fields. Catch per file: a file that fails to read or parse logs
   `Error CLR1: <file>` and keeps what it held before (nothing, at startup), and the other files still
   load. The startup load then no longer stops the bot on one broken file.
2. **`Games.Load` / `LeftPanel.Load`:** on a reload, a missing file keeps the loaded data and says so, in
   place of the "off" line.
3. **`!reloaddata`** in `CheckForAdminCommands`: off the IRC read loop through `FireForget.Run`, as
   `TryChangeCapture` does. It runs the three loaders under one lock, so two quick reloads don't
   interleave their lines, and then, per the decision, restarts the rotation if one is running and not
   stopped. The games and panels loaders already log a `Loaded …` line; colours log nothing when all is
   well, so add one `Loaded <n> colour tables and <n> scheme sets` line.

**Verify:** unit tests in `ColorDataCollection`: a reload over a broken copy of a fixture file keeps that
file's earlier contents and loads the rest; reload the fixtures in a `finally`. Live: change a panel's
`Seconds` in `leftpanel.json`, `!reloaddata`, and the rotation restarts with the new length; change a
scheme and `!changecolor` it; add a stray comma to a scheme file, `!reloaddata` logs `Error CLR1` and
colours still work. Add these to [live-checks.md](live-checks.md) when built.

**Docs when done:** delete this section and the TODO row; the admin commands in
[twitch.md](twitch.md#admin-commands) and CLAUDE.md, [color.md § Loading](color.md#loading),
[obs.md](obs.md) where it says the panel and games files are read at start, `CLR1` and the new log lines in
[reference.md](reference.md), and [architecture.md](architecture.md) startup order if the colour load stops
throwing.

---

## TPM — A test script's message can't be given positionally

`!test tip 4.20 wassaaa` logged only `Usage: !test tip <amount> [msg <text…>] | !test tip <amount> <msg>`
and sent nothing; the second form in that line is what was typed. Seen live 2026-10-14 15:17; the cause
below is read from the code.

`TestArgs.Parse` reads the optional arguments positionally only when the first of them is a number or a
boolean, as [twitch.md § The test harness](twitch.md#the-test-harness) says. Otherwise it reads each word
as a keyword, and `wassaaa` names no parameter, so the parse fails. `tip`'s only optional argument is its
message, so `!test tip <amount> <msg>` works only for a message starting with a number, `true` or `false`;
`!test tip 4.20 msg wassaaa` works. `cheer` fails the same way on `!test cheer 100 hello`, but there the
positional order puts the `anon` flag first, so `!test cheer 100 false hello` is the documented form.

### Plan

In `Parse`, choose keyword mode when the first optional word names one of the script's optional
parameters (any case), and positional mode otherwise. Only `tip` parses differently: every other
script's first optional parameter is a number or a flag, which fails on a word that is neither, as it
does now. A tip message whose first word is `msg` still needs the keyword (`!test tip 4.20 msg msg …`).
Update `TestArgs`' summary comment to the new rule, and add `TestEventTests` cases for a tip-shaped
script: `4.20 wassaaa`, `4.20 hello there` and `4.20 msg hi`.

**Verify:** the tests, then `!test tip 4.20 wassaaa` in chat.

**Docs when done:** delete this section and the TODO row; the rule for choosing positional in
[twitch.md § The test harness](twitch.md#the-test-harness).
