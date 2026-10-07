# TODO — master index

Every tracked item across the detail docs in this folder, by code. This file is the registry: a code can be resolved
to its area, summary and detail location from here alone, without opening the detail docs.

Refer to items by code in conversation (e.g. "do ALR").

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
| [DCD](robustness-fixes.md#dcd--dead-connections-are-never-recycled) | Twitch | IRC and EventSub reads have no timeout; a silently dead socket is shown red but never replaced | Medium |
| [ESR](robustness-fixes.md#esr--eventsub-reconnect-path-unverified) | Twitch | The daily `session_reconnect` never succeeds: the new connection closes within ms and the bot falls back to a fresh session, losing events for ~7 s. Seen in the logs; cause unknown, logging added, read it after the next reconnect | Medium |
| [SBM](robustness-fixes.md#sbm--subscription-message-defects) | Alerts | Missing separator in the `sub` sentence, inverted tier test in the bomb sentence, bomb entries never removed | Low |
| [ALR](robustness-fixes.md#alr--log-line-when-a-connection-degrades-or-recovers) | Logging | Log line when IRC/EventSub degrades or recovers, debounced | Low |
| [TRX](robustness-fixes.md#trx--overlapping-trains-hide-each-other) | OBS | Two trains within 62 s share one source: the first one's cleanup hides the second early. `!train` makes this easy to trigger | Low |
| [OSY](robustness-fixes.md#osy--train-obs-calls-run-on-the-read-loops) | OBS | The train's OBS calls run synchronously on the EventSub or IRC thread; a slow OBS stalls reading | Low |
