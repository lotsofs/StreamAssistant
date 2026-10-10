# Events and alerts

What the bot does when something happens on the channel: each EventSub event, each StreamElements tip,
each channel-point reward, and the sounds and speech behind them. The transport and dispatch are in
[twitch.md](twitch.md) and [streamelements.md](streamelements.md); the colour rewards are in [color.md](color.md).

Read from the code. Nothing here was run against live Twitch events.

## Dispatch

`TwitchEventHandler.Handle(type, event)` logs `notification: <type>` and switches on the type:

| Type | Goes to |
|---|---|
| `channel.ad_break.begin` | `Ads.Process` |
| `channel.chat.notification` | `HandleChannelChatNotification`, then on `notice_type` |
| `channel.channel_points_custom_reward_redemption.add` | `ChannelPoints.ProcessAdd` |
| `channel.cheer` | `Cheers.Process` |
| `channel.update` | `Games.HandleUpdate` ([Category change](#category-change-and-going-live)) |
| anything else (including `channel.follow`) | logged on `EventSubConfusion`, nothing else |

After the switch it appends the raw event JSON to the day's log file.

Chat notifications fan out on `notice_type`: `sub`, `resub`, `sub_gift`, `community_sub_gift`.
Anything else (raids, announcements, `bits_badge_tier`, `charity_donation`, …) logs `Unhandled chat
notice event` with the type, system message and text. Like every real EventSub event, each chat
notification, handled or not, is first written pretty-printed to
`AssistantLogs\EventSubs\<notice_type>_<timestamp>.log` by `TwitchEventHandler.DumpEvent` (not `!test`
events; `!test replay` reads these back, see [twitch.md](twitch.md#the-test-harness)). Two in the same
millisecond share the file, the second appended.

## The alert pattern

Most alerts do the same three things: **play a sound, wait, speak a sentence.** The wait lets the sound
finish before TTS starts.

- `Sound.PlaySound(Sounds.X)` returns immediately; the clip plays on its own device.
- `await Task.Delay(n)`.
- `TextToSpeech.EnqueueSpeech(text)` joins the single speech queue, so alerts never talk over each
  other, though their *sounds* can overlap.

Each handler is an `async Task` started with `FireForget.Run("<code>", type, () => Handler(evt))`, which
logs anything it throws as `Error <code>`, one `TEH_…` code per handler (see [twitch.md](twitch.md#adding-an-eventsub-event)). A handler
that throws stops there, so the rest of its alert doesn't happen.

## Subscriptions

The handlers are in [Subscriptions.cs](../code/Twitch/Subscriptions.cs), the sentences in
[SubscriptionMessages.cs](../code/Twitch/SubscriptionMessages.cs). The tier is `int.Parse(sub_tier) / 1000`, so
`"1000"` → 1, `"2000"` → 2, `"3000"` → 3. Prime is a flag on tier 1.

Each handler reads its JSON, builds the sentence with a pure `SubscriptionMessages.Build…Message` function (tested in
`SubscriptionMessageTests`), then plays the sound, waits, and speaks:

| Handler | Builder | Sound, delay | Spoken |
|---|---|---|---|
| `HandleSubNotif` | `BuildSubMessage` | Tribal hymn, 4.2 s | `<user> subscribed`, `with prime` or `at tier N` (tier above 1), then `. It is a N month sub.` if above 1: `foo subscribed at tier 2. It is a 3 month sub.` |
| `HandleResubNotif` | `BuildResubMessage` | Tribal hymn, 4.2 s | `<user> subscribed`, prime/tier, `, They've subscribed for N months`, `, Currently on a N month streak`, then `: <message>` with the viewer's text run through `LanguageFilter`. A gifted resub also posts `🎁` in chat |
| `HandleSubGiftNotif` (targeted) | `BuildGiftMessage` | Clap, 1 s, hymn, 4.2 s | `<gifter> gifted a tier N sub to <recipient>`, their gift total (or "first gift sub"), and `It is a N month gift` if above 1 |
| `HandleSubGiftNotif` (part of a bomb) | `BuildGiftlessMessage` if the bomb never comes | none | nothing; adds the recipient to the bomb (see [Gift bombs](#gift-bombs) for a bomb that never arrives) |
| `HandleCommunitySubGiftNotif` | `BuildBombMessage` | N hymns 66 ms apart, 3.8 s, clap, 3 s | `<gifter> is gifting N subs to Lots Of Ess's community! … Congratulations to: a, b, c,`, plus `and N other people` for recipients that hadn't arrived in time (`N people` if none had). The tier is said only above 1 (`5 tier 3 subs`), as in the sub sentences |

The resub deliberately omits `duration_months` from the sentence; a code comment says it shows the
streak by mistake.

**Anonymous gifters.** Twitch sends an anonymous gift (bomb or targeted) with `chatter_is_anonymous: true`,
`chatter_user_login: null` and `cumulative_total: null`. Both gift handlers say "Anonymous" for the name
and read `cumulative_total` (like `streak_months`) with `ReadInt`, which treats null or missing as 0
([JsonElementExtensions](../code/Util/JsonElementExtensions.cs)), so the total sentence is simply left out.

### Gift bombs

A bomb is one `community_sub_gift` notification (id, total, gifter) plus one `sub_gift` notification per
recipient, each carrying `community_gift_id`. They can arrive **in either order**, so both handlers
`get-or-create` a [CommunityGiftSub](../code/Twitch/CommunityGiftSub.cs) in the `_giftBombs` dictionary,
keyed by that id.

- `AddRecipient(login)` adds to a set; `SetExpected(total)` records the expected count. Either call
  checks whether the set has reached the count and, if so, completes a `TaskCompletionSource`.
- The `community_sub_gift` handler (after cloning its JSON, see the lifetime rule) awaits
  `WaitForRecipientsAsync`: **completion or 10 seconds, whichever is first**, then reads whatever
  recipients have arrived. A slow bomb is announced with a partial list and the rest counted
  (`a, b, and 3 other people`), and logs `Gift bomb <id>: 2 of 5 recipients arrived in time` on `Important`.
- Once announced, the bomb is **marked** (`MarkAnnounced`) and stays in the dictionary. A recipient
  arriving after that is logged on `Important` (`<login> arrived after gift bomb <id> was announced`) and
  otherwise ignored; it was already counted among the "other people".
- **Recipients whose bomb never arrives.** The `sub_gift` that creates an entry starts
  `ReportIfGiftlessAsync` (through `FireForget`, code `SUB2`). After `GiftlessWait` (30 s), if the bomb event
  still hasn't come, it removes the entry, logs `Gift bomb <id> never arrived; recipients: …` on `Important`,
  and posts to chat: `🛢️ 2 people received a gift sub, but no gift bomb arrived: a, b`. The check and the bomb
  handler's get-or-create-and-`SetExpected` both run under `_giftBombsLock`, so a bomb can't arrive between
  the check and the removal. A bomb that arrives later still is announced normally, with all its
  recipients as "other people".
- Every access to `_giftBombs` takes `_giftBombsLock`. Each `CommunityGiftSub` carries `CreatedUtc`, and
  creating a new entry first sweeps out any older than an hour (`SweepOldBombs`), which is how announced
  bombs eventually leave.
- `_giftees` and `_giftBombWaiters` are declared and unused.
- A single gift "to the community" is a bomb of one: a `community_sub_gift` with `total: 1` plus one
  `sub_gift` carrying its id.
- `!test bomb <n>` simulates a whole bomb, including recipients first (`late`) and a short bomb that waits
  out the 10 s (`missing <k>`). See [twitch.md](twitch.md#the-test-harness).

## Cheers

[Cheers.cs](../code/Twitch/Cheers.cs): reads `is_anonymous`, `user_login`, `bits` and `message` (the
message goes through `LanguageFilter`), plays *Team17 Applauds*, waits 5 s, then speaks
`<user> cheered <bits>: <message>`. The user name isn't filtered. `MONEY_PER_BIT` and the `Money` update
are leftovers (commented out).

## Tips

Tips are StreamElements events, not Twitch ones. They arrive on the StreamElements connection
([streamelements.md](streamelements.md)) as `channel.tips` and go through
`StreamElementsEventHandler.Handle`. It dumps them to `EventSubs\se.channel.tips_<stamp>.log` with the
tipper's email removed, and hands them to `Tips.Process` ([Tips.cs](../code/StreamElements/Tips.cs),
`Error SEH_t` if it throws):

1. **Skip a repeat.** An `_id` already alerted this run logs `Tip <id> already alerted, skipped` and stops
   there. An empty id always counts as new.
2. **Read and filter.** It reads `donation.user.username`, `donation.amount`, `donation.currency` and
   `donation.message`, all before the first `await`. Tippers type their own name, so it is free text and
   goes through `LanguageFilter` like the message. A blank name becomes `Someone`.
3. **Log** `Tip: <user> <amount> <currency> (status <status>, approved <approved>)` on
   `StreamElementsNotification`.
4. **Play** *IndianAnthem* (0.4).
5. **Post** `💸 <user> tipped <amount> <currency> 💸` to chat.
6. **Wait 6 s**, then speak `<user> donated <amount> <currency>: <message>`. Without a message, the
   sentence ends after the currency.

The amount is `5` when whole and two decimals otherwise (`4.20`), always with a dot, whatever the
machine's culture (`Tips.FormatAmount`). Nothing is credited to `Money` (see MNY in [TODO.md](TODO.md)).
`status` and `approved` are logged but not acted on; how moderated tips arrive isn't known yet.

`!test tip <amount> [msg <text…>]` runs the whole alert, chat line included
([twitch.md](twitch.md#the-test-harness)).

## Ads

[Ads.cs](../code/Twitch/Ads.cs) turns Twitch's ad schedule into warnings in chat.

On `channel.ad_break.begin`:

1. read `is_automatic` and `duration_seconds` with `ReadBool` and `ReadInt`, which accept Twitch's strings
   (`"true"`, `"60"`) as well as real booleans and numbers, log
   `Running automatic|manual ad (N seconds)` on `AdNotification`
2. wait the ad's duration, log `Ad break over`
3. cancel the previous round's `CancellationTokenSource` (the old pending warnings stop) and swap in a fresh one
4. start `RunScheduleAsync` with four warnings, timed from *now* ([infrastructure.md](infrastructure.md#periodic-work)),
   assuming an ad every `MINUTES_BETWEEN_ADS` = 60 minutes:

| Minutes after the break | Chat message |
|---|---|
| 55 | `Obligatory ad in 5 minutes :(` |
| 57 | one of three random "please follow / subscribe / use your Prime sub" messages (`sssDino …`), with a 1 in 100 extra `@LotsOfS Stop making me beg …` |
| 58 | `Obligatory ad in 2 minutes :( :( :(` |
| 59 | `Obligatory ad coming right up. Maybe snooze it if something interesting is about to happen.` |

`RunScheduleAsync` waits for each entry's deadline in order, so cancelling the source ends it and nothing else.
A warning that throws logs `Error ADS1` and the later ones still go out.

The 60-minute figure is a guess at Twitch's automatic cadence. If an ad arrives earlier (manual, or the
streamer ran one) the pending warnings are cancelled and rescheduled from that break's end.

## Category change and going live

[Games.cs](../code/Obs/Games.cs). `channel.update` fires on any channel-info edit (title, language,
labels), not just the category. `HandleUpdate` reads `category_id` and compares it with the last one
seen (`CategoryChangeMessage`, tested): the same id does nothing. A different one is stored, posts
`Stream category change from <old id> to <new id>` to chat (`Stream category change to <id>` when there is
no previous id, which only happens if the boot query below failed, and then a title edit re-applies the
game once), and runs `Games.Apply` on the thread pool after `await Task.Yield()`, off the listen loop (`Error GMS_86`
if it throws). An empty `category_id` (no category set) counts as an id like any other.
The admin commands `!changegame` / `!changecategory <id or game name>` call the same `ChangeCategory`, so they post
the real chat line, change OBS and update the stored category, without changing Twitch's category
([twitch.md](twitch.md#admin-commands)).

**On bot start**, `Games.OnBootAsync` (`Error GMS_boot`) asks Helix for the channel's category
(`GetChannelCategoryIdAsync`), stores it, logs `Boot: category <id>`, and applies it once: straight away if
OBS is ready, else the first time OBS connects, in which case it also logs `Boot: OBS not ready, game setup
waits for it` ([obs.md](obs.md#per-game-setup)). No chat line of its own, but the colour change posts its
usual one. If Helix has no channel it logs `Boot: category unknown, no game setup`. If a category change
arrived while the fetch was out, that change has already stored and applied its category (Helix's answer
may even be the stale one), so the boot keeps it, logs `Boot: category <id> already set up by a category
change, boot setup skipped`, and doesn't apply again (tested).

**Going live** means OBS starting its stream ([obs.md](obs.md#connection)), not Twitch's `stream.online`.
`OnStreamStartedAsync` runs on the thread pool (`Error GMS_live`) and applies the last category seen.
If none is known (the boot query failed and no `channel.update` has come since), it asks Helix once (`GetChannelCategoryIdAsync`) and stores
the answer, so a `channel.update` with the same category afterwards does nothing. If Helix has no channel
it logs `Going live: category unknown, no game setup`. No chat line. The last category is guarded by a
lock, since `channel.update` (EventSub thread) and going live (obs-websocket, then the thread pool) both
touch it. Tested in `GamesGoLiveTests`, with the Helix fetch replaced.

`Apply` resolves the id to a game through `games.json` (unknown ids fall back to id `0`), logs
`Game setup: <name> (<id>)`, and has `GameBackground.Set` point the OBS input `Image: Background` at
`<Directories.Backgrounds>\<name>.png`. If that file doesn't exist (the `None` fallback has none) it logs
`No background for <name>, using Template.png` and shows `Template.png` from the same folder; with
that missing too, it logs `No background for <name> and no Template.png, left as is` and leaves the
background alone. Then `GameColor.Set` queues the
game's `gameschemes` colour scheme, which replaces any colour showing and posts the usual
`Changing to color …` chat line; a game without a scheme (RoN) only logs. Last, `GameCapture.Set` points
the numbered game, window and audio capture sources at the game's executables, logging each one.
Details in [obs.md](obs.md#per-game-setup).

## Channel-point rewards

[ChannelPoints.cs](../code/Twitch/ChannelPoints.cs). `ProcessAdd` reads `reward.id`, `id` (the
redemption id), `user_id`, `user_login` and `user_input`, then switches on the reward GUID (the
constants are listed in [reference.md](reference.md#channel-point-rewards)). An unknown id logs
`Unhandled channel point reward redemption id: <id>` and the payload.

| Reward | Does |
|---|---|
| Toilet flush | play `Flush`, insert a row in `flushes` (redemption id, user id, login, time), log it. From `!test`: sound only, no row |
| Toilet retrieve | look up that user's flushes; none → chat `🪠 … didn't find any of <user>'s stuff`; else `CANCELED` each flushed redemption through Helix (refunding it), delete the rows, chat `🪠 Found and returned N … 🪠`. From `!test`: only logs how many it would return |
| Train | if a train is already showing: chat `🚂 A train is already on the tracks 🚂` and `CANCELED` the redemption (refunding it). Otherwise `FULFILLED` it, pick a random `Train0.png`–`Train99.png` from `Directories.Trains`, show it in OBS, wait 62 s, hide it, point the source back at `Empty.png`. Lives in `ChannelPoints.TryStartTrain`; the admin command `!train` runs it without a redemption (and is ignored the same way). From `!test`: runs the train, but only logs the status instead of calling Helix |
| Colour random / single / triple | call `LayoutColoring`, then close the redemption (`FULFILLED`, or `CANCELED` if the colour didn't resolve). From `!test`: recolours, but only logs the status instead of calling Helix |

### How the flush pair works

The flush reward's redemptions are **left unresolved** in Twitch's queue on purpose, so their points
stay spent. That is why only a bot-created reward can be used for it (see
[color.md](color.md)): later, the retrieve reward walks the user's recorded redemption ids and cancels
them one at a time, which is the only way to refund.

Things the code doesn't cover, read not reproduced:

- The retrieve redemption itself is never fulfilled or cancelled by the bot.
- If one `UpdateRedemption` call fails (say a flushed redemption was already cancelled by hand), the
  exception ends the loop *before* `DeleteFlushesAsync`, so the rows stay and every later retrieve hits
  the same failing id first.

### Reward-side requirements

Rewards that the bot closes through Helix (`flush`, train, and the three colour ones) must have been created
through the API by the bot's own client id, and have the "skip reward requests queue" option **off**, or
there is no `UNFULFILLED` redemption to update. (Twitch's documented behaviour; the setting itself
wasn't inspected.)
