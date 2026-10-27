# Live checks

Things built and unit-tested but not yet seen working in the real bot, OBS and Twitch. Indexed in
[TODO.md](TODO.md) as LIV. Checks are grouped by when they can be done. Each keeps its number for good, so
`LIV 41` names one; a new check takes the next number after the highest and goes in the group for when it
can be done. Delete a check when it checks out, and log it in [DONE.md](DONE.md) as `LIV <n>`. When none
are left, delete this file and the LIV row, and log LIV as finished. If something fails, note it here and
raise it.

## Any time, with the bot running and OBS open

- **41.** `!test tip 4.20 hello there` (no `msg` keyword): the anthem plays, chat shows `💸 testtipper tipped 4.20 EUR 💸`, and about 6 s later TTS says "testtipper donated 4.20 EUR: hello there"
- **42.** Emulate a tip from the StreamElements activity feed: note whether `SE notification: channel.tips` appears at all (the docs don't say whether emulated tips reach this topic)
- **82.** `!changecapture KTANE`: `Capture setup: KTANE (461492)` and the capture lines; no chat line, and the background, colour and left panel stay
- **89.** `!changepanel GTASA`: the two `Splits Current` panels in a row (Current Mission, then Current Split) use different copies in OBS: the second appears with its own values, with no flash of the first's
- **114.** From any account, `lotsofs` included: `!skeys` logs `FAQ !skeys for <user>` and chat gets `@<user> The program used to show…` once; `!skeys` again within a minute logs `FAQ !skeys for <user>: on cooldown` and chat gets nothing; `Why are you KILLING hostages` gets the `!civilians` answer
- **115.** Create a text source `Text: Money Goal` in OBS and F5: right after `Connected to OBS` it reads `0 / 999 EUR`, with no `Couldn't set Text: Money Goal` or `Error MON3`
- **116.** `!moneygoal 500`: the log shows `Money goal set to 500 EUR, now 0 / 500 EUR` and OBS follows; `!moneygoal abc` logs `Not a money goal: "abc" (try !moneygoal 500)` and changes nothing
- **117.** `!test tip 4.20 msg hello`: the tip alert runs, the log shows `Test tip: not counted toward the money goal`, and the OBS text stays as it was
- **118.** `!resetmoney` logs `Money total reset (was … EUR), now 0 / <goal> EUR` and OBS follows; then F5: the goal and total are as you left them
- **120.** F5 with OBS open: once the game setup has run, the window opens at 1600 × 900 and the OBS sources column (between the log and the left panel) shows bold blue headings on a dark band that stand out from the lighter blue source names before each arrow, `Captures` with each used slot's executable and volume and the unused ones on one grey `Audio capture …; Game capture …; Window capture … → none` line, and `Images` with the background, borders and sidebar file names, sorted by source name. A name too long for one line, such as `Border: Colorable Outer (Transitionary)`, is shortened to fit (`… (T)`, then `… O (T)`), and stays on one line. Hovering an image line shows its full name and path, and the tooltip stays open while the ages tick. The groups read `Captures`, `Filters`, `Images`, `Text`, and a newly set source slots in by name rather than at the top. Within 2 s, the images and texts not showing in `Basic Stream Layout` are grey (left-panel texts of panels not up among them), whichever scene is live (the fade borders `(Transitionary)`, `Image: Train` between trains, left-panel files of panels not up), and the rest white
- **121.** The `Text` group has `Text: Time Of Day` and `Text: Stream Uptime` with their texts in quotes, their ages climbing and going back to `0 s ago` about each minute, and `Text: Money Goal`; with a game's left panel running, its LiveSplit `.txt` sources are under `Text` by file name (not under `Images`), with the full path on hover
- **122.** `!changecolorrandom`: `Filters` shows the `Layout` and `Fade` rows (`In` `Out` `Txt`), each swatch matching what OBS shows, with no attachment lines under them, each row ending `· <n> s ago`; hovering a swatch shows its source and hex, and stays open. `Fade`'s label and codes are white only during the fade and grey after it, and `Layout`'s go grey for the few seconds the real group is hidden. With SWAT4's panels running, the `Highlights` row (`AG` `AL` `BG` `BL` `Au`) appears, grey while no panel shows `Splits List Highlights`, and after a split the `Previous` row (`Run` `Seg` `Au`), white during the previous-split panel and grey after
- **123.** Before creating `Text: Money Goal` in OBS (or with it renamed), its line is red with OBS's error; once it exists, the next write turns it white. Close OBS: the title reads `OBS sources (OBS not connected)` and the values stay listed
- **127.** `!changepanel SWAT4`, through List On to List Off: the OBS sources column lists `SL - Names 1`, `SL - Highlight 1` and `SL - Header Left 1`, and no `2` or `3` of them, while the values move between copies 2 and 3, each panel with its own `SL - Upcoming`
- **128.** `!changepanel GTA3`, after one full loop: `Splits: Run Delta`'s right-hand column shows the run's upcoming times, as `Finished at` does, not `Golds Delta`'s
- **134.** `!train`, then stop the debugger within 62 s: the train stays up. F5: right after `Connected to OBS` the log shows `Leftover train cleared` and the train is gone. A later F5 with no train up logs no such line
- **137.** Change a panel's `Seconds` in `leftpanel.json`, then `!reloaddata`: the log shows `Loaded <n> colour tables and <n> scheme sets`, `Loaded <n> games, …`, `Loaded left panels for …`, then `Game setup: <game> (<id>)` with its steps, and the rotation starts over from its first panel, which now stays up for the new length
- **138.** Change a scheme's colours in a scheme file, `!reloaddata`, then `!changecolor <that scheme>`: the layout takes the new colours
- **141.** F5 with `twitchBot` filled in and `botsofs` a moderator: no `Chat: no twitchBot…` line, and `🟣 Connected` comes from `botsofs`; `!changecolorrandom` typed as `lotsofs` still runs, its `Changing to color …` coming from `botsofs`; a real colour redemption still works and is marked fulfilled; your own first message still gets `YOOO BRO`, and `Text: Stream Uptime` still updates
- **142.** `!test online`: the log shows `Twitch: stream online (…)` as before
- **151.** Before the tokens have the new scope, F5: the log shows `Name colour: lotsofs's token lacks user:manage:chat_color, its colour stays as is` and the same for `botsofs`, once each, and the row under the timers reads `Lotsofs NO TOKEN  ·  Botsofs NO TOKEN` in red, each with the reason on hover (briefly `waiting` before that)
- **152.** Reissue both tokens (`twitchAuth`'s and `twitchBot`'s in `secrets.json`) with `user:manage:chat_color` added to the scopes they have, then F5: the row under the timers shows `Lotsofs ■ <name> #rrggbb (<table>) 00:00  ·  Botsofs ■ <colour> #rrggbb (twitch) 00:00`, the ages climbing to `00:59` and both colours changing together each minute, with no `Error NCL1` and no per-change log line. Your messages and the bot's (`!changecolorrandom`) a minute or more apart come in different colours, each matching its swatch, and `botsofs`'s never the same twice in a row
- **144.** F5 with the migrated files (`paths.json` holding only `BotInput`, `Bot Input\settings.json` every setting, with your directories): no `settings.json lacks …` line, and the first line is `Loaded settings.json, changed Directories: BotOutput, Trains, Colors, ColorSchemes, Backgrounds, LivesplitGenerated, Layout, Sidebar, Sounds`; the log file is still in `Stream-Resources\Bot Output\AssistantLogs`; `Text: Money Goal` keeps its total; the game setup sets the background, borders and sidebar; and `!test cheer` plays the applause and speaks in Catherine's voice

## With LiveSplit running SWAT4 splits

- **86.** `!nextpanelitem` during `!changepanel GTA3`: the next panel comes up at once in OBS and in the column (timer from 0); during a SWAT4 previous split it moves to the next previous-split panel, and after the last one back to the interrupted panel
- **110.** A SWAT4 split while ahead: `Split sign <sign>: layout colour SWAT4 BlueTeam`, the layout fades to blue, and nothing is posted to chat; while behind it goes `RedTeam`, on a gold `Element` (seen 2026-10-14 15:43: `LostBehind` gave `RedTeam`; ahead and gold, and that nothing went to chat, are still to see)
- **111.** A second split with the same trend doesn't fade again; a reset (or starting a new run) fades back to `Menu` (seen 2026-10-14 15:43: sign `NotApplicable` gave `Menu`; the same-trend split is still to see)
- **112.** `!changegame SWAT4` mid-run while ahead: the setup's chat line is `Changing to color SWAT4:BlueTeam …`, not `Menu`
- **113.** Two SWAT4 splits in a row with the same sign (say `LostBehind` twice). The second's previous-split panel comes in as promptly as the first's, within about half a second of the split and not 2 s later
- **124.** A SWAT4 split that beats the comparison's segment without being a gold: `PreviousSplit\AllSplits\RealTime\CurrentComparison\Sign.txt` reads `GainedAhead` or `GainedBehind` (not `LostAhead` / `LostBehind`, as LiveSplit's older build wrote on 2026-10-14), and the previous-split panel colours the segment difference `LabelPositive`
- **129.** A SWAT4 split during one of the Delta panels: after the previous-split panel, the split list comes back with its names and highlight on the copy it had before (`SL - Names 1`), nothing blank, and the interrupted panel's values
- **130.** SWAT4's sidebar under Output to File release 8: both dynamic sidebar images show `_00` before the run, `_01` once it starts, move on one per split, and go back to `_00` on a reset, both mid-run and after finishing a run
- **131.** Finishing a SWAT4 run: the last split's previous-split panel comes up, its differences coloured by the `Finished…` sign as by the same sign mid-run (`FinishedLostAhead`: run positive, segment negative), and the layout follows its trend (`Split sign FinishedLostBehind: layout colour SWAT4 RedTeam`)
- **132.** SWAT4's `Current Split` panel: `Livesplit: Current Index` shows `1` on the first split (`Number.txt`), and the run and segment times are the current comparison's
- **133.** SWAT4's `List On`: the split list's gold and ahead/behind highlights fill and are coloured, with no `Error LPN2` for `Livesplit: SL Highlights …` (they're set by name, not as `… 1`); after a split, `Previous split` shows the previous split's name, the splits remaining, and its run, segment and gold differences

## With LiveSplit running GTASA or Portal 2 splits

- **125.** GTASA's `100% Splitspam.lss` and `!changepanel GTASA`: `Current Mission` shows a mission (such as `In the Beginning`) out of 181, and `Per mission: Run Delta` lists missions, the same level as under LiveSplit's older build; neither is blank
- **126.** Portal 2's `Coop Splitspam.lss` and `!changepanel Portal2`: the `Per map` panels list the chapters and maps (45 lines), not blank, and `Per map: Golds Delta` shows values (Output to File release 8 writes golds at every level). With `Coop.lss`, which has no two-dash splits, the `Per map` panels read a level LiveSplit doesn't write for it and are blank, as they were under the older build

## Closing and reopening OBS while the bot runs

- **53.** Within a minute of reopening, `Text: Stream Uptime` is right again, with no `Trying to do an OBS action…` line every minute while OBS was closed (on F5 OBS's saved scene showed `U: Offline`, and no `Trying to do…` line ever; OBS hasn't been restarted under a build with it)
- **66.** Close OBS mid-rotation: no `Trying to do an OBS action…` lines while it's closed. Reopen it: right after `Connected to OBS` the log shows `Left panel: <game>, …` again, the rotation starts over from its first panel, and only `Always` and that panel are up, with nothing left over from before the restart
- **67.** `!stoppaneltimer` mid-rotation: the log shows `Left panel rotation stopped` and the current panel stays up past its time. Close and reopen OBS: no `Left panel: …` line after `Connected to OBS` (`Left panel rotation stopped` seen 2026-10-12; the panel staying up and the OBS restart are still to see)
- **98.** While SWAT4's panels run: after `Connected to OBS` the split list highlights are coloured again
- **109.** Mid-run in LiveSplit, having split while OBS was closed: right after `Connected to OBS` both sidebar images show the current mission's
- **136.** `!train`, close OBS, wait out the 62 s, reopen it: after `Connected to OBS` the train is gone, with `Leftover train cleared` if OBS had kept it showing. Reopening OBS within the 62 s instead logs no such line, and the train goes when its time is up

## F5 with OBS closed

- **23.** Open OBS minutes later: the game setup runs as soon as OBS connects
- **75.** Before OBS connects (so before the first game setup), the dashboard's right-hand column reads `Left panel: none`

## After a temporary change (undo it afterwards)

- **22.** Rename a source the game setup sets, then `!changegame <a game>`: it logs `Couldn't set <source>: …` and the rest still apply (also try it with `Image: Background` renamed: colour and captures must still change)
- **37.** Move the `streamElements` block out of `Bot Input\secrets.json` and F5: the log shows `StreamElements: no channelId or jwt in secrets.json, tips are off`, the status bar's third segment shows `off`, and everything else starts as normal (the log line seen 2026-10-10, everything else starting; the status bar's `off` is still to see)
- **48.** Off-stream, raise `DiskSpace.SpamGiB` in `settings.json` above A:'s free space and `!reloaddata`: at the next whole minute the warning beep plays (a real alert came 2026-10-11 22:40 at 1.4 GB free: chat line and TTS, no `Error DSK1`; only the beep isn't in the log, so the next real alert also settles it)
- **85.** Give a game's `leftpanel.json` entry a group OBS lacks and F5: once OBS is connected, exactly the column's lines with such a group are red (the panel up keeps its `>` and timer), matching the `has groups OBS doesn't` log line; the `_LP:` and `_LPC:` groups that do exist show and hide in OBS
- **91.** Put an `_LP:` group in the coloured scene, or an `_LPC:` group in the plain one: the bot never shows or hides it
- **140.** Move the `twitchBot` block out of `Bot Input\secrets.json` and F5: the log shows `Chat: no twitchBot login or accessToken in secrets.json, chatting as lotsofs`, and `🟣 Connected` comes from `lotsofs`
- **143.** Empty `twitchAuth`'s `login` and F5: a message box says `secrets.json` has no `twitchAuth` login, and the bot doesn't start
- **139.** Add a stray comma to a scheme file, then `!reloaddata`: the log shows `Error CLR1: can't load <file>: …`, the other files load, and `!changecolor` still finds that file's schemes as they were. Remove the comma and `!reloaddata` again: no `Error CLR1`
- **145.** In `settings.json` change `Train.Seconds` to 10 and `Game.BackgroundSource` to `Image: Nothing`, then `!reloaddata`: the log lists `changed … Train: Seconds; Game: BackgroundSource`, the game setup logs `Couldn't set Image: Nothing: …`, and `!train` goes after about 10 s. With `Rewards.Train` set to `x` too, `!test replay` of a saved train redemption logs `Unhandled channel point reward redemption id: <id>`
- **146.** Misspell a key in `settings.json` (`Train.Seconds` as `Secnds`), then `!reloaddata`: `Error SET1: settings.json: The JSON property 'Secnds' could not be mapped …`, and `!train` lasts as long as before. F5 with it still misspelt: a message box names `settings.json` and `Secnds`, and the bot doesn't start
- **147.** Set `Speech.Voice` to another installed voice, `!reloaddata`, then `!test cheer`: it's spoken in that voice. A voice that isn't installed logs `Error 5` and says nothing
- **148.** Change `Directories.BotOutput`, then `!reloaddata`: the log shows `BotOutput changes after a restart`, and lines keep going into the same log file
- **149.** Delete the `Clock` section from `settings.json`, then `!reloaddata`: the log shows `settings.json lacks Clock; their defaults apply …`, and the OBS clock still updates at the next minute
- **153.** Set `NameColor.IntervalSeconds` to 10 in `settings.json`, then `!reloaddata`: after the current minute's wait, the ages go back to `00:00` every 10 s. Set `NameColor.Enabled` to `false` and `!reloaddata`: after the current wait both show `OFF` (hover: `NameColor.Enabled is false`) with their last colours and climbing ages, and chat colours stop changing; `true` again resumes them at the next pass
- **150.** Off-stream, move `settings.json` out of `Bot Input` and F5: the window shows `Created …\settings.json from settings.json.example, every setting at its default`, and the new file lists every setting (the log and a fresh database are under `%APPDATA%\StreamAssistant2\Bot Output` for this run). Close the bot, put your file back and delete `%APPDATA%\StreamAssistant2`

## Closing the bot by its window (not the debugger)

- **45.** No `💥 SE Disconnected` in chat
- **135.** `!train`, then close the window within 62 s: the train goes at once, with no `Error TRN3`

## At local midnight

- **50.** Chat gets the date (`yyyy-MM-dd`) once and the OBS clock shows `00:00`, with no `Error CLK2` (the date posted once each midnight from 2026-10-05 to 2026-10-14, no `Error CLK2`; the OBS `00:00` is still to see)

## During a real stream

- **25.** Going live: the log shows `OBS started streaming`, then `Chatter list reset @ n`, then `Game setup: …`
- **26.** Next chatter gets `Test 1`
- **27.** Your own first message also gets `YOOO BRO`
- **28.** Stopping the stream logs `OBS stopped streaming`
- **52.** The connect's single `EventSub subscribed (202): …` line includes `stream.online` and `stream.offline` (no `Subscription … failed`), and going live and ending log the two lines. Note here how long after `OBS started streaming` the online line came
- **55.** The uptime text shows the uptime once decapi has it, then changes just after each uptime minute (within a second or so of the stream's own minute). Note whether the go-live polls (OBS's start, Twitch's online) already found it live

## When it happens on Twitch or StreamElements

- **31.** Someone redeems the train reward while a train runs: the points are refunded (or `Error CP1` if the reward wasn't created by the bot)
- **32.** An accepted train reward is marked fulfilled
- **43.** The first real tip: alerted once, dumped as `AssistantLogs\EventSubs\se.channel.tips_<stamp>.log` with no `email` in it, and the `Tip: …` line shows its `status` and `approved`. Note those values here, and whether a moderated tip arrives twice (the second would log `already alerted, skipped`)
- **44.** Then `!test replay se.channel.tips_<that stamp>` replays it
- **59.** The first real raid: chat shows its `Raid:` line, and the log has no `Unhandled chat notice event` for it
- **119.** The first real tip: the log shows `Money goal: +<amount> EUR from <user>, now …` and the OBS text goes up by the amount. Note its currency here if it isn't EUR (the line would then say `counted as … EUR`)
