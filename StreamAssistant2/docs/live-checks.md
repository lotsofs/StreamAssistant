# Live checks

Things built and unit-tested but not yet seen working in the real bot, OBS and Twitch. Indexed in
[TODO.md](TODO.md) as LIV. Tick a box and strike its line through (`~~…~~`) when it checks out; when every box is ticked, delete this file and
the LIV row. Checks are numbered through the whole file, so `LIV 12` names one. If something fails,
note it here and raise it.

## OBS setup (before the per-game checks)

1. [x] ~~In `!Scene: Games 16x9` and `!Scene: Games 5x4`, create `Game: Game Capture 0`, `1`, `2` (2026-10-14, in OBS's saved scene collection)~~
2. [x] ~~… `Game: Window Capture 0`, `1`, `2` (same)~~
3. [x] ~~… `Audio 5: App Capture 0`, `1`, `2` (same)~~
4. [x] ~~Window captures: Window Match Priority → *Match window of same executable* (saved as priority 2, OBS's `WINDOW_PRIORITY_EXE`)~~
5. [x] ~~Game captures: Mode → *Capture specific window*, and match by executable (mode `window` in all three; their priority is unset, and OBS's default for game capture is by executable)~~
6. [x] ~~App audio captures: match by executable (saved as priority 2)~~

## games.json

7. [x] ~~Fill `GameCaptureExecutable` / `WindowCaptureExecutable` for at least one game you can launch (copy the `example` entry's shape) (HMWOA and SWAT4 have `GameCaptureExecutable`)~~

## Bot start (F5, OBS open)

8. [x] ~~Log shows `Loaded 22 games, default volume -4 dB` (every F5 since 2026-10-10; 23 games now)~~
9. [x] ~~Log shows `HELIX> channel info: 200` and `Boot: category <id>` (no `Boot: OBS not ready` line, since OBS is open) (every F5 since 2026-10-10)~~
10. [x] ~~Log shows `Game setup: <name> (<id>)` (same)~~
11. [x] ~~`Image: Background` switches to that game's PNG, and the log shows `Image: Background → <name>.png` in orange (`SceneChanges`) (on every game change; OBS's saved scene holds `GTA4.png` after the last setup, GTA4's)~~
12. [ ] Chat shows `Changing to color …` and the layout animates to the game's scheme (chat line seen on every game change; the animation is still to watch)
13. [x] ~~Log shows a `<source> → <exe>, shown, <dB> dB` line per used slot, and one `→ none, hidden: …` line listing the rest by kind (`Game: Game Capture 1, 2; …`), nine slots in all (2026-10-14, e.g. SWAT4: Game Capture 0 and 1 shown, the other seven listed as hidden)~~
14. [x] ~~In OBS, a set source's window field shows `PLACEHOLDER-TITLE:PLACEHOLDER-CLASS:<exe>` (2026-10-14 saved scene: `Audio 5: App Capture 0` holds `PLACEHOLDER-TITLE:PLACEHOLDER-CLASS:GTAIV.exe`)~~
15. [ ] A shown source actually captures the running game
16. [x] ~~Unused slots are hidden (eye off) (2026-10-14 saved scene: only GTA4's slot visible, in both games scenes)~~

## Category changes (on Twitch)

17–22 can also be driven with `!changegame <id or game name>` (e.g. `!changegame KTANE`,
`!changegame 999` for one not in `games.json`) instead of changing the category on Twitch; 19 still
needs a real title edit.

17. [x] ~~Changing category posts `Stream category change from <old> to <new>` (real Twitch changes on 2026-10-09, 10 and 11, and `!changegame`)~~
18. [x] ~~… and switches background, colour and capture sources (same)~~
19. [ ] Editing only the title does nothing
20. [ ] A category not in `games.json`: `No background for None, using Template.png`, then `Image: Background → Template.png`, the `none` colour scheme applies, then `999 not in games.json, capture sources left as is`, and the capture sources in OBS stay as they were
21. [x] ~~Ready or Not: `No colour scheme for RoN, colour left as is` (seen as `No colour scheme for HMWOA, colour left as is`, the same path, on every F5)~~
22. [ ] A missing or misnamed source logs `Couldn't set <source>: …` and the rest still apply (also try it with `Image: Background` renamed: colour and captures must still change)

## OBS connecting late (optional)

23. [ ] Start the bot with OBS closed, open OBS minutes later: the game setup runs as soon as OBS connects
24. [x] ~~… and only once (close and reopen OBS: no second `Game setup:` from the boot) (2026-10-10, with OBS open at the start: OBS restarted three times under the bot, no second `Game setup:`)~~

## Stream start

25. [ ] Log shows `OBS started streaming`, then `Chatter list reset @ n`, then `Game setup: …`
26. [ ] Next chatter gets `Test 1`
27. [ ] Your own first message also gets `YOOO BRO`
28. [ ] Stopping the stream logs `OBS stopped streaming`

## Train

29. [x] ~~`!train` twice a few seconds apart: the first logs `Choo choo!` in orange, the second posts `🚂 A train is already on the tracks 🚂` (2026-10-09: the refusal at 00:59, `Choo choo!` at 01:27 once that line was added)~~
30. [ ] The first train image stays up its full 62 s
31. [ ] Redeeming the train reward while a train runs refunds the points (or logs `Error CP1` if the reward wasn't created by the bot)
32. [ ] An accepted train reward is marked fulfilled

## Gift bombs

33. [ ] `!test bomb 3`: no `💣` lines in chat; the spoken list still plays

## Capture volumes

34. [x] ~~After a game change, a shown source's volume slider in OBS sits at its slot's dB, or at the `default` (−4 dB) when the slot sets none (2026-10-14 saved scene: GTA4's `Audio 5: App Capture 0` at −4.0 dB)~~
35. [x] ~~A slot with its own `"Volume"` (e.g. `{ "Exe": "…", "Volume": -10 }`) gets exactly that, and a hidden slot's volume is left alone (AoE2DE's `-13 dB` on 2026-10-10; Game Capture 2 and the window captures, hidden in every setup, still at 0 dB)~~

## EventSub

36. [x] ~~After a Twitch connection reset (most days, no need to cause one), the log shows `EventSub connection lost (ConnectionClosedPrematurely): An existing connection was forcibly closed by the remote host.`, then `EventSub session ended: ConnectionLost (socket Aborted, close none)` and a fresh session, with no `Error TES1` or `Error TES3` (2026-10-11, five resets)~~

## Tips (StreamElements)

First add a `streamElements` block to `Bot Input\secrets.json`, with `channelId` and `jwt` from the
StreamElements dashboard: avatar → the channel → *Show secrets*, copied while switched to the Twitch
account. The shape is in `secrets.json.example`.

37. [ ] Before adding it, F5: the log shows `StreamElements: no channelId or jwt in secrets.json, tips are off`, the status bar's third segment shows `off`, and everything else starts as normal (the log line seen 2026-10-10, everything else starting; the status bar's `off` is still to see)
38. [x] ~~With it, F5: the log shows `Connecting to wss://astro.streamelements.com`, `StreamElements welcome`, `StreamElements subscribed to channel.tips` (teal), and chat shows `🟣 SE Connected` (2026-10-10 run)~~
39. [x] ~~**The probe gets replies:** every probe was answered (`err_bad_request` / `already subscribed to topic`) and no session hit `SilenceTimeout` in 45 minutes (2026-10-10 run)~~
40. [x] ~~No `Error SES…` lines; in particular no `Error SES3: StreamElements refused the token` (2026-10-10 run)~~
41. [ ] `!test tip 4.20 msg hello there`: the anthem plays, chat shows `💸 testtipper tipped 4.20 EUR 💸`, and about 6 s later TTS says "testtipper donated 4.20 EUR: hello there"
42. [ ] Emulate a tip from the StreamElements activity feed: note whether `SE notification: channel.tips` appears at all (the docs don't say whether emulated tips reach this topic)
43. [ ] The first real tip: alerted once, dumped as `AssistantLogs\EventSubs\se.channel.tips_<stamp>.log` with no `email` in it, and the `Tip: …` line shows its `status` and `approved`. Note those values here, and whether a moderated tip arrives twice (the second would log `already alerted, skipped`)
44. [ ] `!test replay se.channel.tips_<that stamp>` replays it
45. [ ] Closing the bot (the window, not the debugger) posts no `💥 SE Disconnected`
46. [x] ~~The log file no longer has a `StreamElements probe-N: err_bad_request already subscribed to topic` line every 30 s, and `StreamElements subscribed to channel.tips` appears once per session (2026-10-11 run, 19.5 h)~~
47. [x] ~~A StreamElements `reconnect`, if one is ever seen (`StreamElements sent reconnect`): `Connecting to wss://astro.streamelements.com (resuming)`, then `StreamElements subscribed to channel.tips`, with no `Error SES3` and neither chat line (2026-10-12 08:14, `The server is shutting down.`)~~

## Disk space

48. [ ] Off-stream, temporarily raise `SPAM_GB_THRESHOLD` in `DiskSpace.cs` above A:'s free space and F5: at the next whole minute the warning beep plays, chat shows the `Alert: Less than …` line, and TTS speaks the alert, with no `Error DSK1`. Restore the constant afterwards (a real alert came 2026-10-11 22:40 at 1.4 GB free: chat line and TTS, no `Error DSK1`, so the constant needn't change; only the beep isn't in the log)

## Clock

49. [ ] With OBS open, F5: by the next whole minute `Text: Time Of Day` shows the current `yyyy-MM-dd HH:mm`, and it then changes right on each minute boundary, with no `Error CLK1`. Close and reopen OBS: it's right again by the next minute (OBS's scene, saved at 02:03 on 2026-10-14, held `2026-10-14 02:03`; no `Error CLK1` ever; the minute changes and an OBS restart are still to see)
50. [ ] At local midnight, chat gets the date (`yyyy-MM-dd`) once and the OBS clock shows `00:00`, with no `Error CLK2` (the date posted once each midnight from 2026-10-05 to 2026-10-14, no `Error CLK2`; the OBS `00:00` is still to see)

## Twitch stream events

51. [ ] `!test online` logs `Twitch: stream online (<now>)`, and `!test offline` logs `Twitch: stream offline`
52. [ ] At the next real stream, the connect's single `EventSub subscribed (202): …` line includes `stream.online` and `stream.offline` (no `Subscription … failed`), and going live and ending log the two lines. Note here how long after `OBS started streaming` the online line came

## Uptime text

53. [ ] With OBS open, F5: within a minute `Text: Stream Uptime` shows `U: Offline`. Close and reopen OBS: it's right again within a minute, with no `Trying to do an OBS action…` line every minute while OBS was closed (OBS's saved scene shows `U: Offline`, no `Trying to do…` line ever; OBS hasn't been restarted under a build with it)
54. [ ] Type something else into `Text: Stream Uptime` in OBS, then `!test online`: within a second or so it's back to `U: Offline` (the wake polled at once)
55. [ ] At a real stream, the text shows the uptime once decapi has it, then changes just after each uptime minute (within a second or so of the stream's own minute). Note whether the go-live polls (OBS's start, Twitch's online) already found it live

## EventSub subscribe line

56. [x] ~~On F5, the EventSub connect logs one `EventSub subscribed (202): …` line listing every subscription, instead of an `EventSub attempt to …: 202` line each (2026-10-11 run, at boot and after each reset)~~

## Raids

57. [ ] `!test raid 42`: chat shows `Raid: TestRaider (42)`, with no sound or speech
58. [ ] `!test raid_2026-06-12 23-46-46.480` (a real one, from another channel): chat shows `Raid: Dana_TV (13)`
59. [ ] The first real raid: chat shows its `Raid:` line, and the log has no `Unhandled chat notice event` for it

## Left panel

60. [x] ~~On F5, the log shows `Loaded left panels for 9 games and a default` just after the `Loaded 22 games…` line, with no `Error LPN1` (every F5 since 2026-10-12; 10 games now)~~
61. [ ] With OBS open, F5: after `Game setup: …` the log shows `Left panel: <game>, <n> panels` (or `(default)`), and in OBS only that game's `Always` groups and first panel are visible in `!Scene: Left Panel` and `!Scene: Left Panel Colored` (the log line follows every `Game setup:`; what OBS shows is still to see)
62. [ ] `!changegame GTA3`: Splits Basics stays up, the split lists change every 50 s (Finished at, Run Delta, Seg. Delta, Golds Delta), then Key Count, a 1 s blank, Sellout Goals, and back to the start
63. [ ] On those swaps the new values appear at once, with no flash of the old column (the double-buffered `Values 1` / `Values 2`)
64. [ ] `!changegame KTANE` (no entry of its own): `Left panel: KTANE (default), 2 panels`; Sellout Basics with Key Count for 220 s, then Sellout List for 30 s
65. [ ] `!changegame SWAT4`: no `Left panel: SWAT4 has groups OBS doesn't: …` line and no `Error LPN2`, and every panel rotates (no missing-groups line or `Error LPN2` for SWAT4 since 2026-10-13; the rotation is still to watch)
66. [ ] Close OBS mid-rotation: no `Trying to do an OBS action…` lines while it's closed. Reopen it: right after `Connected to OBS` the log shows `Left panel: <game>, …` again, the rotation starts over from its first panel, and only `Always` and that panel are up, with nothing left over from before the restart
67. [ ] `!stoppaneltimer` mid-rotation: the log shows `Left panel rotation stopped` and the current panel stays up past its time. Close and reopen OBS: no `Left panel: …` line after `Connected to OBS` (`Left panel rotation stopped` seen 2026-10-12; the panel staying up and the OBS restart are still to see)
68. [ ] Then `!startpaneltimer`: `Left panel: <game>, …`, and the rotation starts over from its first panel (`Left panel: SWAT4, …` seen 2026-10-12; the start-over is still to see)
69. [ ] `!stoppaneltimer`, then `!changegame <another game>`: that game's rotation runs, and closing and reopening OBS restarts it again

## Previous-split panel

70. [x] ~~On F5, the log shows `Watching LiveSplit's split index`, with no `previous-split panel off` line (every F5 since 2026-10-12)~~
71. [x] ~~`!changegame SWAT4`: `Left panel: SWAT4, 10 panels, 3 on a split, split colours from SWAT4` (2026-10-13 onwards)~~
72. [ ] With LiveSplit running SWAT4 splits during the rotation, a split: the panel goes blank (Splits Basics only) for 2 s, shows the previous split (`Splits Previous` with its run, segment and gold differences) for 30 s, goes blank for 2 s, then shows the panel that was cut short again, for its full time (a split at 01:36 on 2026-10-14, during SWAT4's rotation, did run it (see 97); the sequence is still to watch)
73. [ ] Starting a run, undoing a split, skipping a split and resetting don't show it; a split during the previous-split panel starts it over
74. [ ] In another game (say `!changegame GTA3` while LiveSplit runs), a split changes nothing in the rotation

## Dashboard left-panel column

75. [ ] On F5, before the first game setup, the right-hand column reads `Left panel: none`
76. [ ] With a rotation running (`!changegame GTA3`): the title, then `Always` in orange, `Loop` with every panel in white, the one in OBS in orange with `> ` and a `<n> / 50 s` timer counting up each second, and the marker moving on as the panel changes in OBS
77. [ ] `!changegame SWAT4`: a `Previous split` section at the bottom. On a split, the playing previous-split panel turns orange with `> ` and its own timer, the loop panel it interrupted keeps a white `> `, and afterwards that loop panel is orange again with its timer from 0
78. [ ] `!stoppaneltimer` adds `, stopped` to the title, the marker stays put and the timer goes; `!changegame KTANE` shows `(default)`
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

92. [ ] `!changepanel SWAT4` with LiveSplit open: `Splits Current` shows the `Current Split` header and the full list's current split (number out of total, name, run, segment and gold times)
93. [ ] Then the split list's labels come up alone for a second (`Splits` header, names, the current split highlighted), then the values column four times for 15 s each, headed `Finished at`, `Run Delta`, `Seg. Delta` and `Golds Delta`, each with its own column of values; then the labels alone for a second, then Sellout Goals and Sellout List with the highlights, then Key Count
94. [ ] `!changepanel GTAVC`: `Left panel: GTAVC, 4 panels, split colours from none`, with no missing-groups line

## Split colours

95. [ ] `!changepanel SWAT4` with OBS open: the five `Livesplit: Splits List Highlights …` sources' `Color Correction 2` filters take SWAT4's colours (Gold `#ffc800`, AheadGained `#000086`, AheadLost `#4e5fa5`, BehindGained `#bd4142`, BehindLost `#af0000`) (not caught in a saved scene; 96 shows the highlights do get set)
96. [x] ~~`!changepanel GTA3`: the line ends `, split colours from none`, and the highlights take `none`'s split colours (Gold `#ffff00`, AheadGained `#00ff00`, AheadLost `#7f0000`, BehindGained `#007f00`, BehindLost `#ff0000`) (2026-10-14 with GTA4: OBS's saved scene holds exactly those)~~
97. [x] ~~A SWAT4 split during the rotation: when the previous split comes up, `Livesplit: Previous Run`, `Segment` and `Gold` are coloured by its sign (a gold while ahead: run `#4e5fa5`, segment and gold `#ceac35`), there's no `can't read` line, and their filters no longer hold `%splitCol…%` (01:36 on 2026-10-14, sign `LostBehind`: run and segment `#bd4142`, gold `#fffbff`, in OBS's saved scene)~~
98. [ ] Close and reopen OBS while SWAT4's panels run: after `Connected to OBS` the highlights are coloured again

## Kept copies

99. [ ] `!changepanel SWAT4`: from `List On` through `List Off` the labels stay on one `_LP: Splits List Labels <n>` the whole time (no hide and show in OBS, no flicker); only the values column swaps copies
100. [ ] `!changepanel GTA3`: from `Splits: Run Delta` to `Splits: Golds Delta` the labels stay put and only the values swap; `Splits: Finished at` still swaps both

## Borders

101. [ ] `!changegame SWAT4`: the log shows `Borders → Borders_Inner.png, Borders_Outer.png` (5x4 has no images of its own), and with the attachments OBS shows the 5:4 frame (the log line seen 2026-10-14; the frame is still to see)
102. [ ] `!changegame GTA3` (no `Aspect`): `Borders → Borders_Inner_16x9.png, Borders_Outer_16x9.png`, and the 16:9 frame is back; the same for a category not in `games.json` (with GTA4 on 2026-10-14: the line, and all four border sources at `_16x9` in OBS's saved scene; a category not in `games.json` is still to try)
103. [ ] During the colour animation that follows each of those changes, the frame doesn't flash the other shape (the `(Transitionary)` borders changed too)
104. [ ] With `!Scene: Layout 5x4` added to `Basic Stream Layout`, `!changegame SWAT4`: `Basic Stream Layout → !Scene: Layout 5x4` and `→ !Scene: Games 5x4`, and only those two have their eye on; `!changegame GTA3` swaps both back to `16x9` (both lines, both ways, on 2026-10-14; OBS's saved scene, after GTA4, has only the two 16x9 eyes on; the 5x4 side is still to see)
105. [ ] After each, the game's capture slots are shown in both games scenes and the unused ones hidden in both, with no `Couldn't set` lines (no `Couldn't set` lines; OBS's saved scene, after GTA4, has it right in both; SWAT4's side is still to see)
106. [ ] `!changecolor red` with either layout up: the colour fades in as before, the attachments change with the borders, no `Error Obs29` and no `Couldn't colour` / `Couldn't show` lines (no errors from a colour change since 00:48 on 2026-10-14, with 5x4 up (before that, `Error Obs29` at 00:15 and `Couldn't colour … Attachment` at 00:34–00:38); the fade, and 16x9 with `!changecolor`, are still to see)

## Dynamic sidebar

107. [ ] `!changegame SWAT4` with no run going: `Sidebar: Dynamic 1 → SWAT4_00.png` and `Sidebar: Dynamic 2 → SWAT4_2_00.png`, and the 5:4 sidebar shows the officer (a single `Sidebar: Dynamic → SWAT4_09.png` line came on 2026-10-14, before the second source, right for the Deus Ex run LiveSplit had open at split 8; the no-run case is still to see)
108. [ ] Start a SWAT4 run and split: both sidebar images show `_01`, then `_02`, …; an undo or a reset moves them back (a reset to `_00`), and after the last split `_20` stays
109. [ ] Close and reopen OBS mid-run, having split while it was closed: right after `Connected to OBS` both sidebar images show the current mission's
