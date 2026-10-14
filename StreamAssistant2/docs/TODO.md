# TODO — master index

Every tracked item across the detail docs in this folder, by code. This file is the registry: a code can be resolved
to its area, summary and detail location from here alone, without opening the detail docs.

Refer to items by code in conversation (e.g. "do DCD").

## Conventions

- A **code** is three letters, stable for the life of the item, and unique across *all* files, so one
  code always means one thing while it exists.
- When an item is **finished**, delete its row here *and* its section in the detail doc. No "done"
  annotations, no changelog — git history is the record.
- When an item is **added**, add a row here *and* a section in the appropriate detail doc.
- Items state whether a behaviour was **reproduced** (observed by running the real compiled assembly)
  or only read.
- Rows are ordered by suggested order of work, not by code.

## Open — defects and tasks

Detail and a plan for each: [robustness-fixes.md](robustness-fixes.md), except LIV, whose checklist is [live-checks.md](live-checks.md). Plans note their dependencies at the top of robustness-fixes.md.

| Code | Area | Summary | Severity |
|---|---|---|---|
| [LIV](live-checks.md) | All | Owner's checklist of built-and-unit-tested behaviour not yet seen live: per-game setup on boot, category change and stream start, chatter reset and greeting, the train's 62 s and refund, gift bombs without `💣`, StreamElements tips (`!test tip`, a real tip; the connection, probe, quiet probe log and resumed reconnect work live), the disk-space warning sound, the OBS clock text and midnight date post, Twitch's stream online/offline lines, the OBS uptime text and its go-live poll, the raid chat line, the left-panel rotation, restart on OBS reconnect, the stop/start commands, the previous-split panel and the dashboard's left-panel column (with panel names and missing groups in red), `!changepanel` / `!changecapture`, `!nextpanelitem`, interchangeable panel copies and the strict `_LP:` / `_LPC:` prefixes, SWAT4's rebuilt panels, the split colours on the highlights and previous-split differences, kept label copies, the per-game border shapes and layout and games scenes, the colour fade across both layouts, the dynamic 5:4 sidebar | Medium |
| [TRS](robustness-fixes.md#trs--train-left-on-screen-after-the-bot-closes-mid-train) | OBS | Closing the bot while a train is showing leaves `Image: Train` visible for good (seen by the owner); hide and reset it when the bot next connects to OBS | Medium |
| [SGN](robustness-fixes.md#sgn--livesplit-never-writes-gainedahead--gainedbehind-during-a-run) | LiveSplit | Your `LiveSplit.OutputToFile` component has no `GainedAhead` / `GainedBehind` case while a run is going, so a segment that beat its PB (not a gold) is written as `Lost…` and its previous-split difference is coloured negative on stream; a one-branch fix in the component (read in its source) | Medium |
| [RLD](robustness-fixes.md#rld--reloaddata-reload-the-data-files-without-a-restart) | All | `!reloaddata`: reload the colour tables and schemes, `games.json` and `leftpanel.json` without a restart. The colour registries are refilled in place and a broken file throws, so they need an all-at-once swap first; restarting the rotation and redoing the game setup are the owner's calls | Low |
| [TSS](robustness-fixes.md#tss--tests-that-share-static-state-run-in-parallel) | Tests | Test classes in different xUnit collections share `LeftPanel`'s panels, the global log event and the colour registries while running in parallel, so the games test that expects no panels and no `Left panel:` line can fail by timing (read; every run so far passed) | Low |
| [ESR](robustness-fixes.md#esr--eventsub-reconnect-path-unverified) | Twitch | The daily `session_reconnect` now succeeds live (no `4007`, no resubscribe, same session ID kept; reproduced 2026-10-08 and 10-09). Left: see an event delivered on a reconnected session, proving the subscriptions carried over (owner: change the stream title after the next reconnect) | Low |
| [DCD](robustness-fixes.md#dcd--connection-timeouts-unverified-live) | Twitch | IRC and EventSub now time out silent connections and hung connects; unit-tested. The EventSub keepalive timeout has been seen live (2026-10-09); the IRC silence timeout and both connect timeouts are still to be verified with a firewall block (owner, off-stream) | Low |
| [PGD](robustness-fixes.md#pgd--portal2s-per-map-gold-panel-reads-files-livesplit-never-writes) | OBS | Portal2's `Per map: Golds Delta` panel reads `SansSubsplits_Level1` gold files LiveSplit never writes (golds exist only for `AllSplits`), so its values column is empty; drop it or rebuild it on `AllSplits` | Low |
| [PBL](robustness-fixes.md#pbl--pblost-leaves-the-segment-difference-uncoloured) | OBS | The ported sign table leaves `PBLost`'s segment difference uncoloured where `NoPBLost` makes it negative; likely a slip in the old Streamer.Bot table, the owner's call | Low |
| [BOT](robustness-fixes.md#bot--chat-as-the-bot-account-not-lotsofs) | Twitch | The bot logs into IRC as `lotsofs` with the broadcaster's token; give chat its own bot-account token and login, keeping the broadcaster token for EventSub and Helix | Low |
| [DSB](robustness-fixes.md#dsb--unused-disabled-colour-category) | Colour | The `disabled` category in `countryschemes.json` (and that file's own `none`) is unused, a Streamer.Bot leftover with `none`'s scheme; delete it | Low |
| [PSL](robustness-fixes.md#psl--a-previous-split-sign-per-loop-panel) | OBS | Shelved: a previous-split `Sign` per loop panel, so the previous split is coloured (and maybe shown) at the level of the list it interrupted | Low |

## Open — porting from the Streamer.Bot era

Streamer.Bot-era features not yet rebuilt. A plan for each: [porting.md](porting.md), which also lists the decisions each one needs.

| Code | Area | Summary | Severity |
|---|---|---|---|
| [FAQ](porting.md#faq--public-faq-commands) | Twitch | Public FAQ commands match but never send, and matching lost its case-insensitivity | Low |
| [MNY](porting.md#mny--money-tracking) | Twitch | Money goal tracking from cheers, subs and tips; never displayed even before. Keep or delete | Low |
