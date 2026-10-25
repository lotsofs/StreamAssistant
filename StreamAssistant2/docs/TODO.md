# TODO — master index

Every tracked item across the detail docs in this folder, by code. This file is the registry: a code can be resolved
to its area, summary and detail location from here alone, without opening the detail docs.

Refer to items by code in conversation (e.g. "do DCD").

## Conventions

- A **code** is three letters, stable for the life of the item, and unique across *all* files, so one
  code always means one thing while it exists.
- When an item is **finished**, delete its row here *and* its section in the detail doc, and add its row
  to the log, [DONE.md](DONE.md), in the same change. No "done" annotations here.
- When an item is **added**, add a row here *and* a section in the appropriate detail doc.
- Items state whether a behaviour was **reproduced** (observed by running the real compiled assembly)
  or only read.
- Rows are ordered by suggested order of work, not by code.

## Open — defects and tasks

Detail and a plan for each: [robustness-fixes.md](robustness-fixes.md), except LIV, whose checklist is [live-checks.md](live-checks.md). Plans note their dependencies at the top of robustness-fixes.md.

| Code | Area | Summary | Severity |
|---|---|---|---|
| [LIV](live-checks.md) | All | Owner's checklist of built-and-unit-tested behaviour not yet seen live: a misnamed source in the game setup, stream start, chatter reset and greeting, the train reward's refund, StreamElements tips (`!test tip`, a real tip; the connection, probe, quiet probe log and resumed reconnect work live), the disk-space warning sound, the OBS clock at midnight, Twitch's stream online/offline lines at a real stream, the OBS uptime text after an OBS restart and at a real stream, a real raid's chat line, the left-panel rotation across an OBS restart, the dashboard's left-panel column before OBS connects and with missing groups in red, `!changecapture KTANE`, `!nextpanelitem` during a previous split, GTASA's panel copies, the split list's labels staying on one copy (also after a SWAT4 split) and GTA3's Run Delta upcoming times, and the strict `_LP:` / `_LPC:` prefixes, the split colours and the dynamic sidebar after an OBS restart, SWAT4's layout colour by split sign, the previous-split panel on a repeated sign, a gained segment's sign and colour, GTASA's and Portal2's subsplit-level panels (and Portal2's per-map golds) under LiveSplit's new build, SWAT4's sidebar, finish signs, current split number, split list highlights and previous-split values under Output to File release 8, the public FAQ answers and their cooldown, the money goal's OBS text, commands and first real tip, the dashboard's OBS sources list (captures, images, texts, filter swatches, failures in red). The checklist is grouped by when each check can be done (now, with LiveSplit, around an OBS restart, at a real stream, …) | Medium |
| [TRS](robustness-fixes.md#trs--train-left-on-screen-after-the-bot-closes-mid-train) | OBS | Closing the bot while a train is showing leaves `Image: Train` visible for good (seen by the owner); hide and reset it when the bot next connects to OBS | Medium |
| [RLD](robustness-fixes.md#rld--reloaddata-reload-the-data-files-without-a-restart) | All | `!reloaddata`: reload the colour tables and schemes, `games.json` and `leftpanel.json` without a restart. The colour registries are refilled in place and a broken file throws, so they need an all-at-once swap first; restarting the rotation and redoing the game setup are the owner's calls | Low |
| [TSS](robustness-fixes.md#tss--tests-that-share-static-state-run-in-parallel) | Tests | Test classes in different xUnit collections share `LeftPanel`'s panels, the global log event and the colour registries while running in parallel, so the games test that expects no panels and no `Left panel:` line can fail by timing (read; every run so far passed) | Low |
| [ESR](robustness-fixes.md#esr--eventsub-reconnect-path-unverified) | Twitch | The daily `session_reconnect` now succeeds live (no `4007`, no resubscribe, same session ID kept; reproduced 2026-10-08 and 10-09). Left: see an event delivered on a reconnected session, proving the subscriptions carried over (owner: change the stream title after the next reconnect) | Low |
| [DCD](robustness-fixes.md#dcd--connection-timeouts-unverified-live) | Twitch | IRC and EventSub now time out silent connections and hung connects; unit-tested. The EventSub keepalive timeout has been seen live (2026-10-09); the IRC silence timeout and both connect timeouts are still to be verified with a firewall block (owner, off-stream) | Low |
| [BOT](robustness-fixes.md#bot--chat-as-the-bot-account-not-lotsofs) | Twitch | The bot logs into IRC as `lotsofs` with the broadcaster's token; give chat its own bot-account token and login, keeping the broadcaster token for EventSub and Helix | Low |
| [DSB](robustness-fixes.md#dsb--unused-disabled-colour-category) | Colour | The `disabled` category in `countryschemes.json` (and that file's own `none`) is unused, a Streamer.Bot leftover with `none`'s scheme; delete it | Low |
| [TPM](robustness-fixes.md#tpm--a-test-scripts-message-cant-be-given-positionally) | Twitch | `!test tip 4.20 wassaaa` logs only the usage line, which offers exactly that form: optional arguments are read positionally only when the first is a number or boolean, so `tip`'s message needs the `msg` keyword unless it starts with one (seen live 2026-10-14); go positional whenever the first word isn't a keyword | Low |
| [PSL](robustness-fixes.md#psl--a-previous-split-sign-per-loop-panel) | OBS | Shelved: a previous-split `Sign` per loop panel, so the previous split is coloured (and maybe shown) at the level of the list it interrupted | Low |
