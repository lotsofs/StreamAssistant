# TODO — master index

Every tracked item across [docs/](docs/), by code. This file is the registry: a code can be resolved
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

Detail: [docs/robustness-fixes.md](docs/robustness-fixes.md)

| Code | Area | Summary | Severity |
|---|---|---|---|
| [SEC](docs/robustness-fixes.md#sec--credentials-in-a-past-transcript) | Security | Live Twitch tokens read into a past transcript; rotate the pair | Act soon |
| [CMD](docs/robustness-fixes.md#cmd--port-the-colour-chat-commands) | Colour | Port `!changecolor` / `!changecolorrandom` from stubs — unlocks testing RAN and VFY from chat | Enabler |
| [RAN](docs/robustness-fixes.md#ran--random-is-case-sensitive) | Colour | `random` keyword matched case-sensitively; `Random` from a viewer fails | Medium, reproduced |
| [VFY](docs/robustness-fixes.md#vfy--finish-verifying-the-colour-paths-in-the-app) | Colour | Colour paths verified out-of-process only; never exercised via a real redemption | Task |
| [DRV](docs/robustness-fixes.md#drv--log-drive-failure-kills-all-console-output) | Logging | A full or missing `D:` silently kills *all* console output — no error, bot just goes quiet | High, silent |
| [RND](docs/robustness-fixes.md#rnd--getrandomcolor-hard-codes-one-table-name) | Colour | `GetRandomColor` hard-codes `Tables["encycolorpedia"]`; throws if that file is renamed | Latent, reproduced |
| [CAN](docs/robustness-fixes.md#can--system-colours-report-the-raw-input-as-their-name) | Colour | System-colour branch reports raw input instead of canonical `c.Name` | Low, cosmetic |
| [CHN](docs/robustness-fixes.md#chn--one-exception-logged-on-the-wrong-channel) | Logging | `Subscriptions.cs` logs an exception on `ColorType.None` instead of `Error` | Trivial |
| [WRN](docs/robustness-fixes.md#wrn--make-the-build-warning-clean) | Build | Compiler warnings: unused `ChatMessage` type, unused `MONEY_PER_BIT`, `CA1416` from the platform-neutral TFM | Housekeeping |
| [NRE](docs/robustness-fixes.md#nre--nullreferenceexception-inside-the-error-handler) | Logging | Three `CS8602` null-derefs in `ConsoleLogger`'s failure path; the handler itself throws | Medium — *skip if SCL lands* |
| [ROT](docs/robustness-fixes.md#rot--log-directories-grow-without-bound) | Logging | `AssistantLogs/` 133 files/33 MB, `Custom/` 585 files; nothing prunes | Housekeeping |

## Banked — designed, not started

| Code | Area | Summary | State |
|---|---|---|---|
| [SCL](docs/single-console-logging.md) | Logging | Drop the external log-viewer process; show the log in the app's own console with a sticky status footer, scrollback intact | Designed, not started |

### Deferred within SCL

Detail: [docs/single-console-logging.md § Deferred](docs/single-console-logging.md#deferred)

| Code | Summary | Depends on SCL |
|---|---|---|
| [ALR](docs/single-console-logging.md#deferred) | Coloured log line when IRC/EventSub degrades or recovers, debounced | No |
| [RED](docs/single-console-logging.md#deferred) | Guard console setup with `IsOutputRedirected` — redirecting stdout crashes startup today | No |
| [UTF](docs/single-console-logging.md#deferred) | `Console.OutputEncoding = UTF8` so the bot's emoji render | No |
| [BUF](docs/single-console-logging.md#deferred) | Hold one `StreamWriter` instead of open/append/close per log line | No |
| [TTL](docs/single-console-logging.md#deferred) | Put the status metrics in the window title | No |
| [CFG](docs/single-console-logging.md#deferred) | Move `LOG_DIRECTORY` into `secrets.json` alongside the other paths; see ROT | No |
| [FTR](docs/single-console-logging.md#deferred) | Grow the status footer to 2–3 stat lines | **Yes** |
| [DEL](docs/single-console-logging.md#deferred) | Delete the `StreamAssistantLog` project once nothing launches it | **Yes** |
