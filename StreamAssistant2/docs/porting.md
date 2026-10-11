# Porting what's left from the Streamer.Bot era

From July 2024 to June 2026 the bot was a helper for Streamer.Bot: Streamer.Bot received Twitch events and
passed them in, and the bot answered through `MsgQueue` (see [legacy.md](legacy.md) for that history and
the `MsgQueue` → current-API table). The June 2026 rewrite (`bb25c27`, "Work with Twitch API directly")
rebuilt most of it on IRC and EventSub, and tips later moved to a direct StreamElements connection. This
file lists what wasn't rebuilt, each with a checklist.

Each item has a code and a row in [TODO.md](TODO.md), per the conventions there. Its steps are numbered
through the whole section, alternative plans included, so `FAQ 4` names one step. Tick a box when it's
done; when all of an item's boxes are ticked (or it's dropped), delete its section here and its row there.
Delete this file once it's empty.

Sources: the current tree, and the Streamer.Bot-era files at `bb25c27^` (`git show bb25c27^:StreamAssistant2/<File>.cs`).
Everything here was read, not run.

## What is already ported

For orientation, so nothing below is mistaken for missing: subs, resubs, gift subs and gift bombs
(`Subscriptions`), cheers (`Cheers`, minus money), raids (`Raids`, the same chat line as before), the flush, retrieve and colour rewards
(`ChannelPoints`, now a SQLite table instead of `Flushes.json`), the ad warnings (`Ads`, now scheduled
after each ad break instead of from Streamer.Bot's upcoming-ad event), the disk-space alerts
(`DiskSpace`), the uptime fetch and OBS uptime text (`TwitchUptime`), the OBS clock text (`Clock`), the colour admin
commands, sounds, TTS and the language filter. Tips (`Tips`, minus money) now come straight from
StreamElements instead of through Streamer.Bot ([streamelements.md](streamelements.md)).

## Root folder: old and new files

The root still holds files from both eras; new files go under `code/<Area>/` (see [CLAUDE.md](../CLAUDE.md)).
`Obs.cs` (2024, its wrappers rewritten) and `ObsConnection.cs` (new) have moved to `code/Obs/`.

**New, written for the June 2026 rewrite or later:**

| File | Notes |
|---|---|
| `Config.cs` | `paths.json` + `secrets.json` |
| `ConsoleLogger.cs` | named from the console era; feeds the WPF window |
| `Database.cs` | SQLite; replaced `Flushes.json` |
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
| `LeftPanel.cs` | 2024 | entirely commented scaffolding; see [LFP](#lfp--left-panel-and-stoppaneltimer) |
| `Money.cs` | 2024 | compiled, no live reader or writer; see [MNY](#mny--money-tracking) |
| `ISaveable.cs` | 2018 | compiled, nothing implements it; the WinForms settings save that used it is gone. Nothing to port |

## Left to port

Each item: what it did, what's there now, the plan, the decisions that are yours (with a recommended
default), how to verify, and which docs to touch. Ordered roughly easiest first.

### FAQ — Public FAQ commands

**Was:** the six commands (`!civilians`, `!ktane`, `!language`, `!skeys`, `!song`, `!sssa`) and their
question-shaped regexes replied `<user> used <command>: <text>`, matching on the lower-cased message.
`!wherefrom` was already commented out.
**Now:** `ChatHandler.CheckForCommands` matches case-sensitively (so `Why are you killing hostages`
misses) and the send is commented out.

**Plan:**
1. [ ] Prune the list to what's still wanted
2. [ ] Add `RegexOptions.IgnoreCase` to the match
3. [ ] Add a cooldown per command: a `Dictionary<string, DateTime>` of last sends, with the "is it due?" decision a pure function so it can be tested
4. [ ] Send with `TwitchIRCManager.SendMessage`; stop after the first match so one message can't trigger several replies
5. [ ] Test each regex against a few lines that should and shouldn't match, and the cooldown decision; making `_commands` `internal` is enough for that

**Decide:**
- Which commands to keep. Recommend all six: they only fire when asked.
- Reply format. Recommend `@<user> <text>`.
- Cooldown. Recommend 60 s per command.
- Whether the question regexes (no `!` needed) stay. They're the part that can misfire; recommend keeping them, with the cooldown.

**Verify:** from a non-admin account, `!skeys` replies once; again within the cooldown, nothing.

**Docs:** [twitch.md § Public commands](twitch.md#public-commands); legacy.md's ChatHandler section.

### MNY — Money tracking

**Was:** `Money.Goal` (999) and `Money.Current`, credited by cheers (`bits × 0.005`), subs (`0.09` ×1
for tier 1 and Prime, ×2 tier 2, ×5 tier 3) and tips (the full amount). Nothing displayed or saved it,
even then; it reset on every start.
**Now:** `Money.cs` compiled and unused; `Cheers.MONEY_PER_BIT` and a commented credit; the sub payouts
only in `Subscriptions.cs`'s commented old code.

**Decide:** keep or drop. **Recommend drop**: it never worked end to end, and Twitch's own goals cover
the use.

**Plan if dropped:**
1. [ ] Delete `Money.cs`, `MONEY_PER_BIT` and its commented credit in `Cheers`
2. [ ] Remove `Money` from legacy.md (the unused table, the Cheers section, the money sentence in the Subscriptions section)

**Plan if kept:** a `money` table in `Database` holding the total, `Money.Credit(amount, reason)` called
from `Cheers`, `Subscriptions` and `Tips`, and a display (a new OBS text source
through `Obs.SetInputSetting`, or a dashboard panel). Plan the details then.

**Verify (drop):** the build stays clean, 0 warnings.

### LFP — Left panel and `!stoppaneltimer`

**Was:** `LeftPanel.cs` and the deleted `SceneManager.cs` were scaffolding for cycling splits panels in
`!Scene: Left Panel`; `!stoppaneltimer` sent `Termint`, whose meaning is lost. Never finished.
**Now:** all commented out; `!stoppaneltimer` is an empty `case`. Its per-game panel data,
`Bot Input\!OLD_Streamerbot\scenes.json`, is kept for later ([legacy.md](legacy.md#leftpanelcs-rotating-left-panel)).

**Decide:** drop or redesign. **Recommend drop**: there's no working behaviour to keep, and git has the
sketch.

**Plan if dropped:**
1. [ ] Delete `LeftPanel.cs` and the `!stoppaneltimer` case in `ChatHandler.CheckForAdminCommands`
2. [ ] Remove LeftPanel from legacy.md, and `Termint`/`HideAll` from its `MsgQueue` table; update [twitch.md](twitch.md)'s admin command table and CLAUDE.md's mention of the stub

**If redesigned:** a new item with its own plan.
