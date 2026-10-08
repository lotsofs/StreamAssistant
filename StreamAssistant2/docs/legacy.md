# Legacy code and half-finished features

The tree carries a lot of code from before the bot talked to Twitch directly. This file says what each
piece was for, what is commented out versus merely unused, and how to port something back to life.
**Don't clean any of it up unprompted**: two migrations are in flight (see [CLAUDE.md](../CLAUDE.md)),
and the dead code records intent that hasn't been rebuilt yet.

Read from the code and git history. Nothing here runs.

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
element; the field names differ (`tipAmount` → there is no EventSub equivalent for third-party tips).

## Entirely commented out

### [Games.cs](../Games.cs): per-game setup on a category change

On a stream-category change (or going live) it looked the game id up in a `games.json` kept under the
old Streamer.Bot input folder, then:

- switched the OBS background image to `<Backgrounds>\<game name>.png`
- recoloured the layout with a colour scheme `gameschemes[<game name>]`, using a `Coloring.ChangeColor`
  API that no longer exists
- retargeted up to five OBS audio-capture sources to the game's executables, through a window string of
  the form `GAMESOUND:Set by StreamerBot:<exe>`, with `none` for unused slots

The old OBS source names it used survive in the commented `Sources` enum in `Obs.cs`. There is no
EventSub subscription for category changes yet (`channel.update` would be the one). The colour *scheme
set* named `gameschemes` is real and still loaded by `ColorSchemeRegistry`, so the colour half already
has data.

### [Donations.cs](../Donations.cs): tip alerts

A tip added `amount` to `Money.Current`, played the Indian anthem, and queued TTS six seconds later:
`<user> donated <amount> <currency>: <message>`. Tips arrived through Streamer.Bot's tip variables. Nothing feeds tips to the bot now.

### [LeftPanel.cs](../LeftPanel.cs): rotating left panel

Unfinished scaffolding for cycling OBS sources in the `!Scene: Left Panel` scene (split times lists and
labels, double-buffered with `…1`/`…2` source pairs). Mostly stubs; no behaviour to preserve.

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
line and the money tracking were not ported. The old one
read `variables["fromGiftBomb"]` to skip bomb recipients, as the new one does with `community_gift_id`.

### OBS raw request builder

Everything in [Obs.cs](../Obs.cs) below the three live wrappers is commented out. It built obs-websocket
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
| `ChatterList.Reset()` | has a `TODO: Send chat msg`; nothing calls it |
| `TextToSpeech.StopSpeech`, `PurgeQueue`, `Dispose` | would back a "shut up" command |
| `Sound.Sounds.Warning`, `IndianAnthem` | `IndianAnthem` only by the dead donation code |
| `ColorType.Helix` | has a colour in the window; Helix lines use `EventSubConfusion` |
| `ColorUtil.Darken(Color)`, `Lighten(Color)` | only the string overloads are used by the bot; tests cover both |
| `SplitColors` in colour scheme files | loaded, never read ([color.md](color.md)) |
| `TwitchIds.ModeratorId` branch | used only if a subscription sets `RequiresModeratorId` without `WantsBroadcasterAsModerator` |
| `secrets.json` `twitchTestNames` | in the example, absent from the model |
| `TwitchEventSub.IS_TEST` | a `static readonly bool`; flipping it needs a rebuild |
| `TwitchUptime` results | computed every minute, written nowhere (`TODO: Write in OBS`) |
| `channel.follow` subscription | subscribed, never handled |

## Stubs with a `TODO` in the code

`TwitchIRCManager` (`TODO: handle`,
`notify no connection`), `ChatHandler.ProcessMessage` (`TODO: Handle`), `ChatterList.Reset`,
`TwitchUptime` (two OBS-text TODOs), `DiskSpace` (`TODO: Sound`). They are all marked in place.
