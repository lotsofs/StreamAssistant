# TODO — master index

Every tracked item across the detail docs in this folder, by code. This file is the registry: a code can be resolved
to its area, summary and detail location from here alone, without opening the detail docs.

Refer to items by code in conversation (e.g. "do SBM").

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

Detail and a plan for each: [robustness-fixes.md](robustness-fixes.md). Plans note their dependencies at the top of that file.

| Code | Area | Summary | Severity |
|---|---|---|---|
| [ESR](robustness-fixes.md#esr--eventsub-reconnect-path-unverified) | Twitch | The daily `session_reconnect` used to fail with `4007` and fall back to a fresh session (~7 s of lost events). Now follows Twitch's flow (new socket before old is dropped, no resubscribe, 409 is success); tested against a loopback fake, to be checked after the next daily reconnect | Medium |
| [SBM](robustness-fixes.md#sbm--subscription-message-defects) | Alerts | Missing separator in the `sub` sentence, inverted tier test in the bomb sentence, bomb entries never removed | Low |
| [TRX](robustness-fixes.md#trx--overlapping-trains-hide-each-other) | OBS | Two trains within 62 s share one source: the first one's cleanup hides the second early. `!train` makes this easy to trigger | Low |
| [OSY](robustness-fixes.md#osy--train-obs-calls-run-on-the-read-loops) | OBS | The train's OBS calls run synchronously on the EventSub or IRC thread; a slow OBS stalls reading | Low |
| [DCD](robustness-fixes.md#dcd--connection-timeouts-unverified-live) | Twitch | IRC and EventSub now time out silent connections and hung connects; unit-tested, still to be verified live with a firewall block (owner, off-stream) | Low |
