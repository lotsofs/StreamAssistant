# TODO — master index

Every tracked item across the detail docs in this folder, by code. This file is the registry: a code can be resolved
to its area, summary and detail location from here alone, without opening the detail docs.

Refer to items by code in conversation (e.g. "do RAN", "what's left on WRN").

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

Detail: [robustness-fixes.md](robustness-fixes.md)

| Code | Area | Summary | Severity |
|---|---|---|---|
| [SEC](robustness-fixes.md#sec--credentials-in-a-past-transcript) | Security | Live Twitch tokens read into a past transcript; rotate the pair | Act soon |
| [DRV](robustness-fixes.md#drv--log-drive-failure-kills-all-console-output) | Logging | A full or missing `D:` silently kills *all* console output — no error, bot just goes quiet | High, silent |
| [CHN](robustness-fixes.md#chn--one-exception-logged-on-the-wrong-channel) | Logging | `Subscriptions.cs` logs an exception on `ColorType.None` instead of `Error` | Trivial |
| [WRN](robustness-fixes.md#wrn--make-the-build-warning-clean) | Build | Compiler warnings: unused `ChatMessage` type, unused `MONEY_PER_BIT`, `CA1416` from the platform-neutral TFM | Housekeeping |
| [NRE](robustness-fixes.md#nre--nullreferenceexception-inside-the-error-handler) | Logging | Three `CS8602` null-derefs in `ConsoleLogger`'s failure path; the handler itself throws | Medium — *skip if SCL lands* |
| [ROT](robustness-fixes.md#rot--log-directories-grow-without-bound) | Logging | `AssistantLogs/` 133 files/33 MB, `Custom/` 585 files; nothing prunes | Housekeeping |

## Banked — designed, not started

| Code | Area | Summary | State |
|---|---|---|---|
| [SCL](single-console-logging.md) | Logging | Drop the external log-viewer process; show the log in the app's own console with a sticky status footer, scrollback intact | Designed, not started |

### Deferred within SCL

Detail: [single-console-logging.md § Deferred](single-console-logging.md#deferred)

| Code | Summary | Depends on SCL |
|---|---|---|
| [ALR](single-console-logging.md#deferred) | Coloured log line when IRC/EventSub degrades or recovers, debounced | No |
| [RED](single-console-logging.md#deferred) | Guard console setup with `IsOutputRedirected` — redirecting stdout crashes startup today | No |
| [UTF](single-console-logging.md#deferred) | `Console.OutputEncoding = UTF8` so the bot's emoji render | No |
| [BUF](single-console-logging.md#deferred) | Hold one `StreamWriter` instead of open/append/close per log line | No |
| [TTL](single-console-logging.md#deferred) | Put the status metrics in the window title | No |
| [CFG](single-console-logging.md#deferred) | Move `LOG_DIRECTORY` into `secrets.json` alongside the other paths; see ROT | No |
| [FTR](single-console-logging.md#deferred) | Grow the status footer to 2–3 stat lines | **Yes** |
| [DEL](single-console-logging.md#deferred) | Delete the `StreamAssistantLog` project once nothing launches it | **Yes** |
