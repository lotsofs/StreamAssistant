# Legacy code and half-finished features

The tree carries a lot of code from before the bot talked to Twitch directly. This file says what each
piece was for, what is commented out versus merely unused, and how to port something back to life.
**Don't clean any of it up unprompted**: two migrations are in flight (see [CLAUDE.md](../CLAUDE.md)),
and the dead code records intent that hasn't been rebuilt yet.

Read from the code and git history. Nothing here runs. What is still to be rebuilt, with a checklist
each, is in [porting.md](porting.md).

## History in one paragraph

The program began as a helper for **Streamer.Bot**: Streamer.Bot received the Twitch events and called
into this program with a `Dictionary<string, object> variables` (`tipAmount`, `gifts`, `userName`,
`gameId`…), and this program answered by pushing messages onto a `MsgQueue`, which Streamer.Bot drained
to send chat, speak and drive OBS. The bot now owns the Twitch connections itself, so `MsgQueue` is
gone, but many references to it survive in comments. The UI went WinForms
(`Form_StreamAssistant.*`), then a console status bar plus a separate log-viewer process
(`StreamAssistantLog`), then the WPF window. All of that is deleted from the tree and recoverable from
git history.

## Porting a `MsgQueue` call

| Old call | Replacement |
|---|---|
| `MsgQueue.Enqueue(MsgTypes.ChatMsg, text)` | `TwitchIRCManager.SendMessage(text)` |
| `MsgQueue.Enqueue(MsgTypes.TextToS, text)` | `TextToSpeech.EnqueueSpeech(text)` |
| `MsgQueue.TimedEnqueue(ms, type, payload)` | `await Task.Delay(ms)` in an async handler |
| `MsgTypes.ShowSrc` / `HideSrc` with `"scene\|source"` | `Obs.SetSourceEnabled(scene, source, true/false)` |
| `MsgTypes.ObsRawI` with `SetInputSettings` / `SetSourceFilterSettings` JSON | `Obs.SetImageSource` / `Obs.SetFilterProperty` |
| `MsgTypes.HideAll` | no wrapper yet; needs a scene-items listing call |
| `MsgTypes.Termint` | meaning not recoverable from the code (only `!stoppaneltimer` used it) |
| `Sound.PlaySoundDelayed(sound, ms)` | removed; `await Task.Delay(ms)` then `Sound.PlaySound(sound)` |

The old handlers took the Streamer.Bot variables dictionary. The new ones take the EventSub `event`
element; the field names differ (`tipAmount` → tips now come from StreamElements as `donation.amount`,
see [streamelements.md](streamelements.md)).

## Entirely commented out

### [LeftPanel.cs](../LeftPanel.cs): rotating left panel

Unfinished scaffolding for cycling OBS sources in the `!Scene: Left Panel` scene (split times lists and
labels, double-buffered with `…1`/`…2` source pairs). Mostly stubs; no behaviour to preserve.

Its data survives, kept on purpose for later: `Bot Input\!OLD_Streamerbot\scenes.json` maps a game key
(`GTASA1`, `GTA3`, …) to a `GenericSceneList` of panels, each with a `Name`, a `Duration`, `Enabled`, and
optionally a LiveSplit text source to change (`ChangeTextOf`, `ChangeTextTo`). See LFP in
[TODO.md](TODO.md).

## Partly commented out

### ChatHandler

`CheckForCommands` matches the FAQ commands but the send is commented out;
`CheckForAdminCommands`'s `!stoppaneltimer` is a stub. See [twitch.md](twitch.md#public-commands).

### [Subscriptions.cs](../code/Twitch/Subscriptions.cs): the old generators

Below the live handlers is the previous implementation, as comments: message constants
(`MSG_SUB_FIRST_PRIME`, `MSG_BOMB_LONG`, …), `GenerateSubMessage` and friends, a `GiftBomb` class with
queues of gifters and bomb recipients, and `AddMoneyBasedOnTier`, which credited `Money.Current` with
`SUB_PAYOUT` (0.09, the cheapest sub's payout, "anything extra I pocket for food") ×1 for tier 1 and
prime, ×2 for tier 2, ×5 for tier 3. The live code replaced all of this. Its two bomb error messages live on in other forms: `ERROR_NO_BOMBEES`
(fewer recipients than expected) as the spoken `and N other people`, and `ERROR_NO_BOMBERS` (recipients with
no bomb) as the giftless chat line ([events.md](events.md#gift-bombs)). The immediate `MSG_BOMB_SHORT` chat
line was deliberately not ported, and the money tracking hasn't been (MNY in [TODO.md](TODO.md)). The old one
read `variables["fromGiftBomb"]` to skip bomb recipients, as the new one does with `community_gift_id`.

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

### Cheers

`Cheers.cs` keeps `MONEY_PER_BIT` (0.005, "half of earnings") and a commented `Money.Current` update.

## Unused but live

Compiled and reachable by name, with no caller:

| Item | Notes |
|---|---|
| `Money` | `Goal` and `Current`; only commented code touches them |
| `ISaveable` | `LoadSettings`/`SaveSettings`; nothing implements it |
| `TextToSpeech.StopSpeech`, `PurgeQueue`, `Dispose` | would back a "shut up" command |
| `Sound.Sounds.Warning` | nothing plays it yet (DSA in [TODO.md](TODO.md)) |
| `ColorUtil.Darken(Color)`, `Lighten(Color)` | only the string overloads are used by the bot; tests cover both |
| `SplitColors` in colour scheme files | loaded, never read ([color.md](color.md)) |
| `TwitchIds.ModeratorId` branch | used only if a subscription sets `RequiresModeratorId` without `WantsBroadcasterAsModerator` |
| `secrets.json` `twitchTestNames` | in the example, absent from the model |
| `TwitchEventSub.IS_TEST` | a `static readonly bool`; flipping it needs a rebuild |
| `TwitchUptime` results | computed every minute, written nowhere (`TODO: Write in OBS`) |
| `channel.follow` subscription | subscribed, never handled |

## Stubs with a `TODO` in the code

`TwitchIRCManager` (`TODO: handle`,
`notify no connection`), `ChatHandler.ProcessMessage` (`TODO: Handle`),
`TwitchUptime` (two OBS-text TODOs), `DiskSpace` (`TODO: Sound`). They are all marked in place.
