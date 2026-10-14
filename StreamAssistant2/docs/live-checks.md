# Live checks

Things built and unit-tested but not yet seen working in the real bot, OBS and Twitch. Indexed in
[TODO.md](TODO.md) as LIV. Delete a check when it checks out, leaving the others' numbers as they are; when none are left, delete this
file and the LIV row. Checks are numbered through the whole file, so `LIV 12` names one. If something fails,
note it here and raise it.

## Category changes (on Twitch)

20 and 22 can also be driven with `!changegame <id or game name>` (e.g. `!changegame KTANE`,
`!changegame 999` for one not in `games.json`) instead of changing the category on Twitch.

20. [ ] A category not in `games.json`: `No background for None, using Template.png`, then `Image: Background → Template.png`, the `none` colour scheme applies, then `999 not in games.json, capture sources left as is`, and the capture sources in OBS stay as they were
22. [ ] A missing or misnamed source logs `Couldn't set <source>: …` and the rest still apply (also try it with `Image: Background` renamed: colour and captures must still change)

## OBS connecting late (optional)

23. [ ] Start the bot with OBS closed, open OBS minutes later: the game setup runs as soon as OBS connects

## Stream start

25. [ ] Log shows `OBS started streaming`, then `Chatter list reset @ n`, then `Game setup: …`
26. [ ] Next chatter gets `Test 1`
27. [ ] Your own first message also gets `YOOO BRO`
28. [ ] Stopping the stream logs `OBS stopped streaming`

## Train

30. [ ] The first train image stays up its full 62 s
31. [ ] Redeeming the train reward while a train runs refunds the points (or logs `Error CP1` if the reward wasn't created by the bot)
32. [ ] An accepted train reward is marked fulfilled

## Tips (StreamElements)

First add a `streamElements` block to `Bot Input\secrets.json`, with `channelId` and `jwt` from the
StreamElements dashboard: avatar → the channel → *Show secrets*, copied while switched to the Twitch
account. The shape is in `secrets.json.example`.

37. [ ] Before adding it, F5: the log shows `StreamElements: no channelId or jwt in secrets.json, tips are off`, the status bar's third segment shows `off`, and everything else starts as normal (the log line seen 2026-10-10, everything else starting; the status bar's `off` is still to see)
41. [ ] `!test tip 4.20 msg hello there`: the anthem plays, chat shows `💸 testtipper tipped 4.20 EUR 💸`, and about 6 s later TTS says "testtipper donated 4.20 EUR: hello there" (`!test tip 4.20 wassaaa`, without `msg`, gave only the usage line on 2026-10-14: TPM; the form here, with `msg`, should work)
42. [ ] Emulate a tip from the StreamElements activity feed: note whether `SE notification: channel.tips` appears at all (the docs don't say whether emulated tips reach this topic)
43. [ ] The first real tip: alerted once, dumped as `AssistantLogs\EventSubs\se.channel.tips_<stamp>.log` with no `email` in it, and the `Tip: …` line shows its `status` and `approved`. Note those values here, and whether a moderated tip arrives twice (the second would log `already alerted, skipped`)
44. [ ] `!test replay se.channel.tips_<that stamp>` replays it
45. [ ] Closing the bot (the window, not the debugger) posts no `💥 SE Disconnected`

## Disk space

48. [ ] Off-stream, temporarily raise `SPAM_GB_THRESHOLD` in `DiskSpace.cs` above A:'s free space and F5: at the next whole minute the warning beep plays, chat shows the `Alert: Less than …` line, and TTS speaks the alert, with no `Error DSK1`. Restore the constant afterwards (a real alert came 2026-10-11 22:40 at 1.4 GB free: chat line and TTS, no `Error DSK1`, so the constant needn't change; only the beep isn't in the log)

## Clock

50. [ ] At local midnight, chat gets the date (`yyyy-MM-dd`) once and the OBS clock shows `00:00`, with no `Error CLK2` (the date posted once each midnight from 2026-10-05 to 2026-10-14, no `Error CLK2`; the OBS `00:00` is still to see)

## Twitch stream events

52. [ ] At the next real stream, the connect's single `EventSub subscribed (202): …` line includes `stream.online` and `stream.offline` (no `Subscription … failed`), and going live and ending log the two lines. Note here how long after `OBS started streaming` the online line came

## Uptime text

53. [ ] With OBS open, F5: within a minute `Text: Stream Uptime` shows `U: Offline`. Close and reopen OBS: it's right again within a minute, with no `Trying to do an OBS action…` line every minute while OBS was closed (OBS's saved scene shows `U: Offline`, no `Trying to do…` line ever; OBS hasn't been restarted under a build with it)
54. [ ] Type something else into `Text: Stream Uptime` in OBS, then `!test online`: within a second or so it's back to `U: Offline` (the wake polled at once)
55. [ ] At a real stream, the text shows the uptime once decapi has it, then changes just after each uptime minute (within a second or so of the stream's own minute). Note whether the go-live polls (OBS's start, Twitch's online) already found it live

## Raids

59. [ ] The first real raid: chat shows its `Raid:` line, and the log has no `Unhandled chat notice event` for it

## Left panel

63. [ ] On those swaps the new values appear at once, with no flash of the old column (the double-buffered `Values 1` / `Values 2`)
66. [ ] Close OBS mid-rotation: no `Trying to do an OBS action…` lines while it's closed. Reopen it: right after `Connected to OBS` the log shows `Left panel: <game>, …` again, the rotation starts over from its first panel, and only `Always` and that panel are up, with nothing left over from before the restart
67. [ ] `!stoppaneltimer` mid-rotation: the log shows `Left panel rotation stopped` and the current panel stays up past its time. Close and reopen OBS: no `Left panel: …` line after `Connected to OBS` (`Left panel rotation stopped` seen 2026-10-12; the panel staying up and the OBS restart are still to see)
68. [ ] Then `!startpaneltimer`: `Left panel: <game>, …`, and the rotation starts over from its first panel (`Left panel: SWAT4, …` seen 2026-10-12; the start-over is still to see)

## Previous-split panel

72. [ ] With LiveSplit running SWAT4 splits during the rotation, a split: the panel goes blank (Splits Basics only) for 2 s, shows the previous split (`Splits Previous` with its run, segment and gold differences) for 30 s, goes blank for 2 s, then shows the panel that was cut short again, for its full time (a split at 01:36 on 2026-10-14, during SWAT4's rotation, did run it, colouring the differences by its sign in OBS; the sequence is still to watch)
73. [ ] Starting a run, undoing a split, skipping a split and resetting don't show it; a split during the previous-split panel starts it over
74. [ ] In another game (say `!changegame GTA3` while LiveSplit runs), a split changes nothing in the rotation

## Dashboard left-panel column

75. [ ] On F5, before the first game setup, the right-hand column reads `Left panel: none`
76. [ ] With a rotation running (`!changegame GTA3`): the title, then `Always` in orange, `Loop` with every panel in white, the one in OBS in orange with `> ` and a `<n> / 50 s` timer counting up each second, and the marker moving on as the panel changes in OBS
77. [ ] `!changegame SWAT4`: a `Previous split` section at the bottom. On a split, the playing previous-split panel turns orange with `> ` and its own timer, the loop panel it interrupted keeps a white `> `, and afterwards that loop panel is orange again with its timer from 0
78. [ ] `!stoppaneltimer` adds `, stopped` to the title, the marker stays put and the timer goes; `!changegame KTANE` shows `(default)`, and `!changegame SWAT4` shows `Left panel: SWAT4 (Run_NoSubsplits)` in the title and the log line
79. [ ] The column doesn't flicker (it redraws once a second for the timer), and long panel lines wrap without a horizontal scrollbar

## Partial game setup commands

80. [ ] `!changepanel GTA3`: only the left panel changes (`Left panel: GTA3, …`, the column follows); no chat line, and the background, colour and capture sources stay as they were (`!changepanel SWAT4` and `GTA5` on 2026-10-12 logged only the left-panel line; the column is still to see)
81. [ ] `!changepanel default` shows the default panels; `!changepanel nonsense` logs `No game or left panel "nonsense"` and changes nothing
82. [ ] `!changecapture KTANE`: `Capture setup: KTANE (461492)` and the capture lines; no chat line, and the background, colour and left panel stay
83. [ ] `!changecapture 999`: `999 not in games.json, capture sources left as is`, and nothing changes

## Dashboard column: names and missing groups

84. [ ] `!changepanel GTA3`: the split-list panels read `Splits: Finished at`, `Splits: Run Delta`, … and the single-group ones their group (`Splits Key Count`)
85. [ ] With a game whose entry names a group OBS lacks: once OBS is connected, exactly the lines with such a group are red (the panel up keeps its `>` and timer), matching the `has groups OBS doesn't` log line; the `_LP:` and `_LPC:` groups that do exist show and hide in OBS

## Moving the left panel on

86. [ ] `!nextpanelitem` during `!changepanel GTA3`: the next panel comes up at once in OBS and in the column (timer from 0); during a SWAT4 previous split it moves to the next previous-split panel, and after the last one back to the interrupted panel
87. [ ] `!nextpanelitem` after `!stoppaneltimer` logs `No left panel rotation to move on`, and changes nothing

## Copies and prefixes

88. [ ] With OBS open, `!changepanel GTASA`: no `Error LPN2` lines, and the `!Scene: Left Panel` item in `!Scene: Left Panel Colored` gets hidden
89. [ ] The two `Splits Current` panels in a row (Current Mission, then Current Split) use different copies in OBS: the second appears with its own values, with no flash of the first's
90. [ ] In the split lists, each panel moves labels and values together to the other copy number, and the labels (names, `Splits` header) come along unchanged while the values column changes; no flash of old content
91. [ ] An `_LP:` group placed in the coloured scene, or an `_LPC:` group in the plain one, is never shown or hidden by the bot

## SWAT4's panels


## Split colours

98. [ ] Close and reopen OBS while SWAT4's panels run: after `Connected to OBS` the highlights are coloured again

## Kept copies


## Borders

101. [ ] `!changegame SWAT4`: the log shows `Borders → Borders_Inner.png, Borders_Outer.png` (5x4 has no images of its own), and with the attachments OBS shows the 5:4 frame (the log line seen 2026-10-14; the frame is still to see)
102. [ ] `!changegame GTA3` (no `Aspect`): `Borders → Borders_Inner_16x9.png, Borders_Outer_16x9.png`, and the 16:9 frame is back; the same for a category not in `games.json` (with GTA4 on 2026-10-14: the line, and all four border sources at `_16x9` in OBS's saved scene; a category not in `games.json` is still to try)
103. [ ] During the colour animation that follows each of those changes, the frame doesn't flash the other shape (the `(Transitionary)` borders changed too)
104. [ ] With `!Scene: Layout 5x4` added to `Basic Stream Layout`, `!changegame SWAT4`: `Basic Stream Layout → !Scene: Layout 5x4` and `→ !Scene: Games 5x4`, and only those two have their eye on; `!changegame GTA3` swaps both back to `16x9` (both lines, both ways, on 2026-10-14; OBS's saved scene, after GTA4, has only the two 16x9 eyes on; the 5x4 side is still to see)
105. [ ] After each, the game's capture slots are shown in both games scenes and the unused ones hidden in both, with no `Couldn't set` lines (no `Couldn't set` lines; OBS's saved scene, after GTA4, has it right in both; SWAT4's side is still to see)
106. [ ] `!changecolor red` with either layout up: the colour fades in as before, the attachments change with the borders, no `Error Obs29` and no `Couldn't colour` / `Couldn't show` lines (no errors from a colour change since 00:48 on 2026-10-14, with 5x4 up (before that, `Error Obs29` at 00:15 and `Couldn't colour … Attachment` at 00:34–00:38); the fade, and 16x9 with `!changecolor`, are still to see)

## Dynamic sidebar

109. [ ] Close and reopen OBS mid-run, having split while it was closed: right after `Connected to OBS` both sidebar images show the current mission's
