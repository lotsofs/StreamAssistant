# History and unused code

What the bot was before it talked to Twitch directly, what of that was rebuilt, and the code that is
compiled but has no caller. **Don't remove any of it unprompted.**

Read from the code and git history. Nothing here runs.

## History in one paragraph

The program began as a helper for **Streamer.Bot**: Streamer.Bot received the Twitch events and called
into this program with a `Dictionary<string, object> variables` (`tipAmount`, `gifts`, `userName`,
`gameId`…), and this program answered by pushing messages onto a `MsgQueue`, which Streamer.Bot drained
to send chat, speak and drive OBS. The bot now owns the Twitch connections itself, so `MsgQueue` is
gone, along with all the code that used it. The UI went WinForms (`Form_StreamAssistant.*`), then a
console status bar plus a separate log-viewer process (`StreamAssistantLog`), then the WPF window;
`ConsoleLogger` keeps its name from the console era. All of that is deleted from the tree and recoverable
from git history. The Streamer.Bot era ran from July 2024 to June 2026; the rewrite is `bb25c27` ("Work
with Twitch API directly"), so `git show bb25c27^:StreamAssistant2/<File>.cs` shows a file as it was.

The files used to sit flat in the project folder. Of those, `Program.cs` (2017), `Sound.cs` and
`LanguageFilter.cs` (2024) were adapted in place, `DiskSpace` and `TwitchUptime` are the two halves of the
old `Clock.cs`, and the rest were written for the rewrite or later. Everything but `Program.cs` now lives
under `code/<Area>/` ([architecture.md](architecture.md#where-everything-lives)).

## What was rebuilt

Every Streamer.Bot-era feature has been rebuilt or dropped: subs, resubs, gift subs and gift bombs
(`Subscriptions`), cheers (`Cheers`), raids (`Raids`, the same chat line as before), the flush, retrieve
and colour rewards (`ChannelPoints`, now a SQLite table instead of `Flushes.json`), the ad warnings
(`Ads`, now scheduled after each ad break instead of from Streamer.Bot's upcoming-ad event), the
disk-space alerts (`DiskSpace`), the uptime fetch and OBS uptime text (`TwitchUptime`), the OBS clock
text (`Clock`), the colour admin commands, the public FAQ commands (`ChatHandler`, now with a cooldown
per user), sounds, TTS and the language filter. Tips (`Tips`) come straight from StreamElements instead
of through Streamer.Bot ([streamelements.md](streamelements.md)). The rotating left panel, with its
previous-split panel and `!stoppaneltimer`, is rebuilt from a new `leftpanel.json`
([obs.md](obs.md#left-panel)). The old `!stoppaneltimer` sent a `MsgTypes.Termint` whose meaning isn't
recoverable from the code; the command now stops the left-panel rotation.

The old money goal (`Money.Goal` and `Money.Current`, never shown or saved, and reset on every start)
credited cheers at `bits × 0.005` and tips in full; a sub credit was written but never called. Its
rebuild, `MoneyGoal`, counts tips only, since Twitch doesn't say what a sub cost, keeps the total in
SQLite and shows it in OBS ([obs.md](obs.md#money-goal)).

## Unused but live

Compiled and reachable by name, with no caller:

| Item | Notes |
|---|---|
| `TextToSpeech.StopSpeech`, `PurgeQueue`, `Dispose` | would back a "shut up" command |
| `ColorUtil.Darken(Color)`, `Lighten(Color)` | only the string overloads are used by the bot; tests cover both |
| `SplitColors` outside `gameschemes` | loaded, never read; only `gameschemes`' are used ([color.md](color.md)) |
| `TwitchIds.ModeratorId` branch | used only if a subscription sets `RequiresModeratorId` without `WantsBroadcasterAsModerator` |
| `secrets.json` `twitchTestNames` | in the example, absent from the model |
| `TwitchEventSub.IS_TEST` | a `static readonly bool`; flipping it needs a rebuild |
| `channel.follow` subscription | subscribed, never handled |

## Stubs with a `TODO` in the code

`TwitchIRCManager` (`TODO: handle`,
`notify no connection`), `ChatHandler.ProcessMessage` (`TODO: Handle`). They are all marked in place.
