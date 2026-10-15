# Legacy code and half-finished features

The tree carries a lot of code from before the bot talked to Twitch directly. This file says what each
piece was for, what is commented out versus merely unused, which root files are from which era, and how
to port something back to life. **Don't clean any of it up unprompted**: two migrations are in flight
(see [CLAUDE.md](../CLAUDE.md)), and the dead code records how things used to work.

Read from the code and git history. Nothing here runs.

## History in one paragraph

The program began as a helper for **Streamer.Bot**: Streamer.Bot received the Twitch events and called
into this program with a `Dictionary<string, object> variables` (`tipAmount`, `gifts`, `userName`,
`gameId`…), and this program answered by pushing messages onto a `MsgQueue`, which Streamer.Bot drained
to send chat, speak and drive OBS. The bot now owns the Twitch connections itself, so `MsgQueue` is
gone, but many references to it survive in comments. The UI went WinForms
(`Form_StreamAssistant.*`), then a console status bar plus a separate log-viewer process
(`StreamAssistantLog`), then the WPF window. All of that is deleted from the tree and recoverable from
git history. The Streamer.Bot era ran from July 2024 to June 2026; the rewrite is `bb25c27` ("Work with
Twitch API directly"), so `git show bb25c27^:StreamAssistant2/<File>.cs` shows a file as it was.

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
([obs.md](obs.md#left-panel)).

The old money goal (`Money.Goal` and `Money.Current`, never shown or saved, and reset on every start)
credited cheers at `bits × 0.005` and tips in full; a sub credit was written but never called. Its
rebuild, `MoneyGoal`, counts tips only, since Twitch doesn't say what a sub cost, keeps the total in
SQLite and shows it in OBS ([obs.md](obs.md#money-goal)).

## Root folder: old and new files

The root still holds files from both eras; new files go under `code/<Area>/` (see [CLAUDE.md](../CLAUDE.md)).
`Obs.cs` (2024, its wrappers rewritten) and `ObsConnection.cs` (new) have moved to `code/Obs/`.

**New, written for the June 2026 rewrite or later:**

| File | Notes |
|---|---|
| `Config.cs` | `paths.json` + `secrets.json` |
| `ConsoleLogger.cs` | named from the console era; feeds the WPF window |
| `Database.cs` | SQLite; replaced `Flushes.json`, and holds the money goal |
| `DiskSpace.cs` | the disk half of the old `Clock.cs` |
| `TwitchUptime.cs` | the uptime half of the old `Clock.cs` |
| `TextToSpeech.cs` | SAPI; Streamer.Bot used to speak |
| `LayoutColoring.cs` | the colour animation queue |
| `Util.cs` | `TrueModulo`, used by `ColorUtil` |

**Old but live, adapted in place:**

| File | Since | Notes |
|---|---|---|
| `Program.cs` | 2017 | rewritten as the WPF composition root |
| `Sound.cs` | 2024 | plays files itself now instead of queueing `PlaySfx` |
| `LanguageFilter.cs` | 2024 | unchanged in purpose |

**Old and dead:**

| File | Since | Notes |
|---|---|---|
| `ISaveable.cs` | 2018 | compiled, nothing implements it; the WinForms settings save that used it is gone. Nothing to port |

## Porting a `MsgQueue` call

| Old call | Replacement |
|---|---|
| `MsgQueue.Enqueue(MsgTypes.ChatMsg, text)` | `TwitchIRCManager.SendMessage(text)` |
| `MsgQueue.Enqueue(MsgTypes.TextToS, text)` | `TextToSpeech.EnqueueSpeech(text)` |
| `MsgQueue.TimedEnqueue(ms, type, payload)` | `await Task.Delay(ms)` in an async handler |
| `MsgTypes.ShowSrc` / `HideSrc` with `"scene\|source"` | `Obs.SetSourceEnabled(scene, source, true/false)` |
| `MsgTypes.ObsRawI` with `SetInputSettings` / `SetSourceFilterSettings` JSON | `Obs.SetImageSource` / `Obs.SetFilterProperty` |
| `MsgTypes.HideAll` | `Obs.GetSceneItemNames(scene)`, then `Obs.SetSourceEnabled` on each item to hide (as `LeftPanel` does) |
| `MsgTypes.Termint` | meaning not recoverable from the code (only `!stoppaneltimer` used it); that command now stops the left-panel rotation (`LeftPanel.Stop`) |
| `Sound.PlaySoundDelayed(sound, ms)` | removed; `await Task.Delay(ms)` then `Sound.PlaySound(sound)` |

The old handlers took the Streamer.Bot variables dictionary. The new ones take the EventSub `event`
element; the field names differ (`tipAmount` → tips now come from StreamElements as `donation.amount`,
see [streamelements.md](streamelements.md)).

## Partly commented out

### ChatHandler

`_commands` keeps a commented-out `!wherefrom` FAQ command and empty `ChatCommand` templates. The live
ones are in [twitch.md](twitch.md#public-commands).

### OBS raw request builder

Everything in [Obs.cs](../code/Obs/Obs.cs) below the three live wrappers is commented out. It built obs-websocket
**raw batch request JSON** by string formatting (`SetInputSettings`, `SetSourceFilterSettings`) for
Streamer.Bot to send, kept a `Sources` enum mapping logical names to OBS source names (text sources for
uptime and clock, the background image, five game-audio sources, the colourable layout sources and
their `(Transitionary)` twins, the two layout groups), a `Scenes` enum, and the original
`ChangeLayoutColor` timeline, which is the same nine-second animation `LayoutColoring` now runs
([obs.md](obs.md#the-animation)).

The enum of source names is a useful inventory of the OBS scene collection: uptime text
`Text: Stream Uptime`, clock text `Text: Time Of Day`, `Image: Background`, `Audio: Z5 Game0…4`.

## Unused but live

Compiled and reachable by name, with no caller:

| Item | Notes |
|---|---|
| `ISaveable` | `LoadSettings`/`SaveSettings`; nothing implements it |
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
