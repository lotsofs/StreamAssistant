# Live checks

Things built and unit-tested but not yet seen working in the real bot, OBS and Twitch. Indexed in
[TODO.md](TODO.md) as LIV. Checks are grouped by when they can be done. Each keeps its number for good, so
`LIV 41` names one; a new check takes the next number after the highest and goes in the group for when it
can be done. Delete a check when it checks out; when none are left, delete this file and the LIV row. If
something fails, note it here and raise it.

## Any time, with the bot running and OBS open

- **41.** `!test tip 4.20 msg hello there`: the anthem plays, chat shows `💸 testtipper tipped 4.20 EUR 💸`, and about 6 s later TTS says "testtipper donated 4.20 EUR: hello there" (`!test tip 4.20 wassaaa`, without `msg`, gave only the usage line on 2026-10-14: TPM; the form here, with `msg`, should work)
- **42.** Emulate a tip from the StreamElements activity feed: note whether `SE notification: channel.tips` appears at all (the docs don't say whether emulated tips reach this topic)
- **82.** `!changecapture KTANE`: `Capture setup: KTANE (461492)` and the capture lines; no chat line, and the background, colour and left panel stay
- **89.** `!changepanel GTASA`: the two `Splits Current` panels in a row (Current Mission, then Current Split) use different copies in OBS: the second appears with its own values, with no flash of the first's
- **114.** From an account other than `lotsofs` or `botsofs`: `!skeys` logs `FAQ !skeys for <user>` and chat gets `@<user> The program used to show…` once; `!skeys` again within a minute logs `FAQ !skeys for <user>: on cooldown` and chat gets nothing; `Why are you KILLING hostages` gets the `!civilians` answer
- **115.** Create a text source `Text: Money Goal` in OBS and F5: right after `Connected to OBS` it reads `0 / 999 EUR`, with no `Couldn't set Text: Money Goal` or `Error MON3`
- **116.** `!moneygoal 500`: the log shows `Money goal set to 500 EUR, now 0 / 500 EUR` and OBS follows; `!moneygoal abc` logs `Not a money goal: "abc" (try !moneygoal 500)` and changes nothing
- **117.** `!test tip 4.20 msg hello`: the tip alert runs, the log shows `Test tip: not counted toward the money goal`, and the OBS text stays as it was
- **118.** `!resetmoney` logs `Money total reset (was … EUR), now 0 / <goal> EUR` and OBS follows; then F5: the goal and total are as you left them
- **120.** F5 with OBS open: once the game setup has run, the window opens at 1600 × 900 and the OBS sources column (between the log and the left panel) shows bold blue headings on a dark band that stand out from the lighter blue source names before each arrow, `Captures` with each used slot's executable and volume and the unused ones on one grey `Audio capture …; Game capture …; Window capture … → none` line, and `Images` with the background, borders and sidebar file names, newest first. A name too long for one line, such as `Border: Colorable Outer (Transitionary)`, is shortened to fit (`… (T)`, then `… O (T)`), and stays on one line. Hovering an image line shows its full name and path, and the tooltip stays open while the ages tick. The groups read `Captures`, `Filters`, `Text`, `Images`. Within 2 s, the images and texts not showing in `Basic Stream Layout` are grey (left-panel texts of panels not up among them), whichever scene is live (the fade borders `(Transitionary)`, `Image: Train` between trains, left-panel files of panels not up), and the rest white
- **121.** The `Text` group has `Text: Time Of Day` and `Text: Stream Uptime` with their texts in quotes, their ages climbing and going back to `0 s ago` about each minute, and `Text: Money Goal`; with a game's left panel running, its LiveSplit `.txt` sources are under `Text` by file name (not under `Images`), with the full path on hover
- **122.** `!changecolorrandom`: `Filters` shows the `Layout` and `Fade` rows (`In` `Out` `Txt`), each swatch matching what OBS shows, with no attachment lines under them, each row ending `· <n> s ago`; hovering a swatch shows its source and hex, and stays open. `Fade`'s label and codes are white only during the fade and grey after it, and `Layout`'s go grey for the few seconds the real group is hidden. With SWAT4's panels running, the `Highlights` row (`AG` `AL` `BG` `BL` `Au`) appears, grey while no panel shows `Splits List Highlights`, and after a split the `Previous` row (`Run` `Seg` `Au`), white during the previous-split panel and grey after
- **123.** Before creating `Text: Money Goal` in OBS (or with it renamed), its line is red with OBS's error; once it exists, the next write turns it white. Close OBS: the title reads `OBS sources (OBS not connected)` and the values stay listed

## With LiveSplit running SWAT4 splits

- **86.** `!nextpanelitem` during `!changepanel GTA3`: the next panel comes up at once in OBS and in the column (timer from 0); during a SWAT4 previous split it moves to the next previous-split panel, and after the last one back to the interrupted panel
- **110.** A SWAT4 split while ahead: `Split sign <sign>: layout colour SWAT4 BlueTeam`, the layout fades to blue, and nothing is posted to chat; while behind it goes `RedTeam`, on a gold `Element` (seen 2026-10-14 15:43: `LostBehind` gave `RedTeam`; ahead and gold, and that nothing went to chat, are still to see)
- **111.** A second split with the same trend doesn't fade again; a reset (or starting a new run) fades back to `Menu` (seen 2026-10-14 15:43: sign `NotApplicable` gave `Menu`; the same-trend split is still to see)
- **112.** `!changegame SWAT4` mid-run while ahead: the setup's chat line is `Changing to color SWAT4:BlueTeam …`, not `Menu`
- **113.** Once the component's 2026-10-15 build is in LiveSplit (SGN): two SWAT4 splits in a row with the same sign (say `LostBehind` twice). The second's previous-split panel comes in as promptly as the first's, within about half a second of the split and not 2 s later

## Closing and reopening OBS while the bot runs

- **53.** Within a minute of reopening, `Text: Stream Uptime` is right again, with no `Trying to do an OBS action…` line every minute while OBS was closed (on F5 OBS's saved scene showed `U: Offline`, and no `Trying to do…` line ever; OBS hasn't been restarted under a build with it)
- **66.** Close OBS mid-rotation: no `Trying to do an OBS action…` lines while it's closed. Reopen it: right after `Connected to OBS` the log shows `Left panel: <game>, …` again, the rotation starts over from its first panel, and only `Always` and that panel are up, with nothing left over from before the restart
- **67.** `!stoppaneltimer` mid-rotation: the log shows `Left panel rotation stopped` and the current panel stays up past its time. Close and reopen OBS: no `Left panel: …` line after `Connected to OBS` (`Left panel rotation stopped` seen 2026-10-12; the panel staying up and the OBS restart are still to see)
- **98.** While SWAT4's panels run: after `Connected to OBS` the split list highlights are coloured again
- **109.** Mid-run in LiveSplit, having split while OBS was closed: right after `Connected to OBS` both sidebar images show the current mission's

## F5 with OBS closed

- **23.** Open OBS minutes later: the game setup runs as soon as OBS connects
- **75.** Before OBS connects (so before the first game setup), the dashboard's right-hand column reads `Left panel: none`

## After a temporary change (undo it afterwards)

- **22.** Rename a source the game setup sets, then `!changegame <a game>`: it logs `Couldn't set <source>: …` and the rest still apply (also try it with `Image: Background` renamed: colour and captures must still change)
- **37.** Move the `streamElements` block out of `Bot Input\secrets.json` and F5: the log shows `StreamElements: no channelId or jwt in secrets.json, tips are off`, the status bar's third segment shows `off`, and everything else starts as normal (the log line seen 2026-10-10, everything else starting; the status bar's `off` is still to see)
- **48.** Off-stream, raise `SPAM_GB_THRESHOLD` in `DiskSpace.cs` above A:'s free space and F5: at the next whole minute the warning beep plays (a real alert came 2026-10-11 22:40 at 1.4 GB free: chat line and TTS, no `Error DSK1`; only the beep isn't in the log, so the next real alert also settles it)
- **85.** Give a game's `leftpanel.json` entry a group OBS lacks and F5: once OBS is connected, exactly the column's lines with such a group are red (the panel up keeps its `>` and timer), matching the `has groups OBS doesn't` log line; the `_LP:` and `_LPC:` groups that do exist show and hide in OBS
- **91.** Put an `_LP:` group in the coloured scene, or an `_LPC:` group in the plain one: the bot never shows or hides it

## Closing the bot by its window (not the debugger)

- **45.** No `💥 SE Disconnected` in chat

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
