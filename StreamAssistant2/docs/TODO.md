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

Detail and a plan for each: [robustness-fixes.md](robustness-fixes.md). Plans note their dependencies at the top of that file.

| Code | Area | Summary | Severity |
|---|---|---|---|
| [ESR](robustness-fixes.md#esr--eventsub-reconnect-path-unverified) | Twitch | The daily `session_reconnect` used to fail with `4007` and fall back to a fresh session (~7 s of lost events). Now follows Twitch's flow (new socket before old is dropped, no resubscribe, 409 is success); tested against a loopback fake, to be checked after the next daily reconnect | Medium |
| [DCD](robustness-fixes.md#dcd--connection-timeouts-unverified-live) | Twitch | IRC and EventSub now time out silent connections and hung connects; unit-tested, still to be verified live with a firewall block (owner, off-stream) | Low |
| [SOE](robustness-fixes.md#soe--log-twitch-stream-online-and-offline) | Twitch | Nothing logs Twitch's own stream online/offline events; subscribe `stream.online` and `stream.offline` and log them | Low |

## Open — porting from the Streamer.Bot era

Streamer.Bot-era features not yet rebuilt. A plan for each: [porting.md](porting.md), which also lists the decisions each one needs. UTX, CLK and GAM share a new OBS wrapper (porting.md § Shared pieces).

| Code | Area | Summary | Severity |
|---|---|---|---|
| [DSA](porting.md#dsa--disk-space-alarm-sound) | Infra | The below-1 GB disk alert no longer plays the `Warning` sound (`TODO: Sound`) | Low |
| [UTX](porting.md#utx--obs-uptime-text) | OBS | Uptime is computed every minute but no longer written to `Text: Stream Uptime` | Low |
| [CLK](porting.md#clk--obs-clock-text) | OBS | Nothing writes `Text: Time Of Day` any more | Low |
| [RAD](porting.md#rad--raid-announcement) | Twitch | No raid announcement; raids already arrive as a `raid` chat notification and are logged as unhandled | Low |
| [FAQ](porting.md#faq--public-faq-commands) | Twitch | Public FAQ commands match but never send, and matching lost its case-insensitivity | Low |
| [GAM](porting.md#gam--per-game-setup-on-category-change) | OBS | Per-game background, colour scheme and audio sources on a category change (built: background, colour and capture sources on category change and going live; left: a `!test category` script, cleanup, and per-source volume) | Low |
| [MNY](porting.md#mny--money-tracking) | Twitch | Money goal tracking from cheers, subs and tips; never displayed even before. Keep or delete | Low |
| [TIP](porting.md#tip--tips) | Twitch | Tip alerts (`Donations.cs`) need a StreamElements connection; keep or delete | Low |
| [LFP](porting.md#lfp--left-panel-and-stoppaneltimer) | OBS | `LeftPanel.cs` scaffolding and `!stoppaneltimer`: nothing working to port; keep or delete | Low |
