# Live checks

Things built and unit-tested but not yet seen working in the real bot, OBS and Twitch. Indexed in
[TODO.md](TODO.md) as LIV. Tick a box when it checks out; when every box is ticked, delete this file and
the LIV row. Checks are numbered through the whole file, so `LIV 12` names one. If something fails,
note it here and raise it.

## OBS setup (before the per-game checks)

1. [ ] In `!Scene: Games 1920x1080`, create `Game: Game Capture 0`, `1`, `2`
2. [ ] … `Game: Window Capture 0`, `1`, `2`
3. [ ] … `Audio 5: App Capture 0`, `1`, `2`
4. [ ] Window captures: Window Match Priority → *Match window of same executable*
5. [ ] Game captures: Mode → *Capture specific window*, and match by executable
6. [ ] App audio captures: match by executable

## games.json

7. [ ] Fill `GameCaptureExecutable` / `WindowCaptureExecutable` for at least one game you can launch (copy the `example` entry's shape)

## Bot start (F5, OBS open)

8. [ ] Log shows `Loaded 22 games, default volume -4 dB`
9. [ ] Log shows `HELIX> channel info: 200` and `Boot: category <id>` (no `Boot: OBS not ready` line, since OBS is open)
10. [ ] Log shows `Game setup: <name> (<id>)`
11. [ ] `Image: Background` switches to that game's PNG, and the log shows `Image: Background → <name>.png` in orange (`SceneChanges`)
12. [ ] Chat shows `Changing to color …` and the layout animates to the game's scheme
13. [ ] Log shows a `<source> → <exe>, shown, <dB> dB` line per used slot, and one `→ none, hidden: …` line listing the rest by kind (`Game: Game Capture 1, 2; …`), nine slots in all
14. [ ] In OBS, a set source's window field shows `PLACEHOLDER-TITLE:PLACEHOLDER-CLASS:<exe>`
15. [ ] A shown source actually captures the running game
16. [ ] Unused slots are hidden (eye off)

## Category changes (on Twitch)

17–22 can also be driven with `!changegame <id or game name>` (e.g. `!changegame KTANE`,
`!changegame 999` for one not in `games.json`) instead of changing the category on Twitch; 19 still
needs a real title edit.

17. [ ] Changing category posts `Stream category change from <old> to <new>`
18. [ ] … and switches background, colour and capture sources
19. [ ] Editing only the title does nothing
20. [ ] A category not in `games.json`: `No background for None, using Template.png`, then `Image: Background → Template.png`, the `none` colour scheme applies, then `999 not in games.json, capture sources left as is`, and the capture sources in OBS stay as they were
21. [ ] Ready or Not: `No colour scheme for RoN, colour left as is`
22. [ ] A missing or misnamed source logs `Couldn't set <source>: …` and the rest still apply (also try it with `Image: Background` renamed: colour and captures must still change)

## OBS connecting late (optional)

23. [ ] Start the bot with OBS closed, open OBS minutes later: the game setup runs as soon as OBS connects
24. [ ] … and only once (close and reopen OBS: no second `Game setup:` from the boot)

## Stream start

25. [ ] Log shows `OBS started streaming`, then `Chatter list reset @ n`, then `Game setup: …`
26. [ ] Next chatter gets `Test 1`
27. [ ] Your own first message also gets `YOOO BRO`
28. [ ] Stopping the stream logs `OBS stopped streaming`

## Train

29. [ ] `!train` twice a few seconds apart: the first logs `Choo choo!` in orange, the second posts `🚂 A train is already on the tracks 🚂`
30. [ ] The first train image stays up its full 62 s
31. [ ] Redeeming the train reward while a train runs refunds the points (or logs `Error CP1` if the reward wasn't created by the bot)
32. [ ] An accepted train reward is marked fulfilled

## Gift bombs

33. [ ] `!test bomb 3`: no `💣` lines in chat; the spoken list still plays

## Capture volumes

34. [ ] After a game change, a shown source's volume slider in OBS sits at its slot's dB, or at the `default` (−4 dB) when the slot sets none
35. [ ] A slot with its own `"Volume"` (e.g. `{ "Exe": "…", "Volume": -10 }`) gets exactly that, and a hidden slot's volume is left alone

## EventSub

36. [x] After a Twitch connection reset (most days, no need to cause one), the log shows `EventSub connection lost (ConnectionClosedPrematurely): An existing connection was forcibly closed by the remote host.`, then `EventSub session ended: ConnectionLost (socket Aborted, close none)` and a fresh session, with no `Error TES1` or `Error TES3` (2026-10-11, five resets)

## Tips (StreamElements)

First add a `streamElements` block to `Bot Input\secrets.json`, with `channelId` and `jwt` from the
StreamElements dashboard: avatar → the channel → *Show secrets*, copied while switched to the Twitch
account. The shape is in `secrets.json.example`.

37. [ ] Before adding it, F5: the log shows `StreamElements: no channelId or jwt in secrets.json, tips are off`, the status bar's third segment shows `off`, and everything else starts as normal
38. [x] With it, F5: the log shows `Connecting to wss://astro.streamelements.com`, `StreamElements welcome`, `StreamElements subscribed to channel.tips` (teal), and chat shows `🟣 SE Connected` (2026-10-10 run)
39. [x] **The probe gets replies:** every probe was answered (`err_bad_request` / `already subscribed to topic`) and no session hit `SilenceTimeout` in 45 minutes (2026-10-10 run)
40. [x] No `Error SES…` lines; in particular no `Error SES3: StreamElements refused the token` (2026-10-10 run)
41. [ ] `!test tip 4.20 msg hello there`: the anthem plays, chat shows `💸 testtipper tipped 4.20 EUR 💸`, and about 6 s later TTS says "testtipper donated 4.20 EUR: hello there"
42. [ ] Emulate a tip from the StreamElements activity feed: note whether `SE notification: channel.tips` appears at all (the docs don't say whether emulated tips reach this topic)
43. [ ] The first real tip: alerted once, dumped as `AssistantLogs\EventSubs\se.channel.tips_<stamp>.log` with no `email` in it, and the `Tip: …` line shows its `status` and `approved`. Note those values here, and whether a moderated tip arrives twice (the second would log `already alerted, skipped`)
44. [ ] `!test replay se.channel.tips_<that stamp>` replays it
45. [ ] Closing the bot (the window, not the debugger) posts no `💥 SE Disconnected`
46. [x] The log file no longer has a `StreamElements probe-N: err_bad_request already subscribed to topic` line every 30 s, and `StreamElements subscribed to channel.tips` appears once per session (2026-10-11 run, 19.5 h)
47. [ ] A StreamElements `reconnect`, if one is ever seen (`StreamElements sent reconnect`): `Connecting to wss://astro.streamelements.com (resuming)`, then `StreamElements subscribed to channel.tips`, with no `Error SES3` and neither chat line

## Disk space

48. [ ] Off-stream, temporarily raise `SPAM_GB_THRESHOLD` in `DiskSpace.cs` above A:'s free space and F5: at the next whole minute the warning beep plays, chat shows the `Alert: Less than …` line, and TTS speaks the alert, with no `Error DSK1`. Restore the constant afterwards

## Clock

49. [ ] With OBS open, F5: by the next whole minute `Text: Time Of Day` shows the current `yyyy-MM-dd HH:mm`, and it then changes right on each minute boundary, with no `Error CLK1`. Close and reopen OBS: it's right again by the next minute
50. [ ] At local midnight, chat gets the date (`yyyy-MM-dd`) once and the OBS clock shows `00:00`, with no `Error CLK2`

## Twitch stream events

51. [ ] `!test online` logs `Twitch: stream online (<now>)`, and `!test offline` logs `Twitch: stream offline`
52. [ ] At the next real stream, the connect's single `EventSub subscribed (202): …` line includes `stream.online` and `stream.offline` (no `Subscription … failed`), and going live and ending log the two lines. Note here how long after `OBS started streaming` the online line came

## Uptime text

53. [ ] With OBS open, F5: within a minute `Text: Stream Uptime` shows `U: Offline`. Close and reopen OBS: it's right again within a minute, with no `Trying to do an OBS action…` line every minute while OBS was closed
54. [ ] Type something else into `Text: Stream Uptime` in OBS, then `!test online`: within a second or so it's back to `U: Offline` (the wake polled at once)
55. [ ] At a real stream, the text shows the uptime once decapi has it, then changes just after each uptime minute (within a second or so of the stream's own minute). Note whether the go-live polls (OBS's start, Twitch's online) already found it live

## EventSub subscribe line

56. [x] On F5, the EventSub connect logs one `EventSub subscribed (202): …` line listing every subscription, instead of an `EventSub attempt to …: 202` line each (2026-10-11 run, at boot and after each reset)

## Raids

57. [ ] `!test raid 42`: chat shows `Raid: TestRaider (42)`, with no sound or speech
58. [ ] `!test raid_2026-06-12 23-46-46.480` (a real one, from another channel): chat shows `Raid: Dana_TV (13)`
59. [ ] The first real raid: chat shows its `Raid:` line, and the log has no `Unhandled chat notice event` for it

## Left panel

60. [ ] On F5, the log shows `Loaded left panels for 9 games and a default` just after the `Loaded 22 games…` line, with no `Error LPN1`
61. [ ] With OBS open, F5: after `Game setup: …` the log shows `Left panel: <game>, <n> panels` (or `(default)`), and in OBS only that game's `Always` groups and first panel are visible in `!Scene: Left Panel` and `!Scene: Left Panel Colored`
62. [ ] `!changegame GTA3`: Splits Basics stays up, the split lists change every 50 s (Finished at, Run Delta, Seg. Delta, Golds Delta), then Key Count, a 1 s blank, Sellout Goals, and back to the start
63. [ ] On those swaps the new values appear at once, with no flash of the old column (the double-buffered `Values 1` / `Values 2`)
64. [ ] `!changegame KTANE` (no entry of its own): `Left panel: KTANE (default), 2 panels`; Sellout Basics with Key Count for 220 s, then Sellout Goals for 30 s
65. [ ] `!changegame SWAT4`: one `Left panel: SWAT4 has groups OBS doesn't: Splits Main List Highlights Gold, Splits Main List Big Delta, Splits Main List Little Delta` line, then the rest rotates, with no `Error LPN2`
66. [ ] Close OBS mid-rotation: no `Trying to do an OBS action…` lines while it's closed. Reopen it: right after `Connected to OBS` the log shows `Left panel: <game>, …` again, the rotation starts over from its first panel, and only `Always` and that panel are up, with nothing left over from before the restart
67. [ ] `!stoppaneltimer` mid-rotation: the log shows `Left panel rotation stopped` and the current panel stays up past its time. Close and reopen OBS: no `Left panel: …` line after `Connected to OBS`
68. [ ] Then `!startpaneltimer`: `Left panel: <game>, …`, and the rotation starts over from its first panel
69. [ ] `!stoppaneltimer`, then `!changegame <another game>`: that game's rotation runs, and closing and reopening OBS restarts it again

## Previous-split panel

70. [ ] On F5, the log shows `Watching LiveSplit's split index`, with no `previous-split panel off` line
71. [ ] `!changegame SWAT4`: `Left panel: SWAT4, 10 panels, 3 on a split`
72. [ ] With LiveSplit running SWAT4 splits during the rotation, a split: the panel goes blank (Splits Basics only) for 2 s, shows the previous split (`Splits Previous` with its run, segment and gold differences) for 30 s, goes blank for 2 s, then shows the panel that was cut short again, for its full time
73. [ ] Starting a run, undoing a split, skipping a split and resetting don't show it; a split during the previous-split panel starts it over
74. [ ] In another game (say `!changegame GTA3` while LiveSplit runs), a split changes nothing in the rotation

## Dashboard left-panel column

75. [ ] On F5, before the first game setup, the right-hand column reads `Left panel: none`
76. [ ] With a rotation running (`!changegame GTA3`): the title, then `Always` in orange, `Loop` with every panel in white, the one in OBS in orange with `> ` and a `<n> / 50 s` timer counting up each second, and the marker moving on as the panel changes in OBS
77. [ ] `!changegame SWAT4`: a `Previous split` section at the bottom. On a split, the playing previous-split panel turns orange with `> ` and its own timer, the loop panel it interrupted keeps a white `> `, and afterwards that loop panel is orange again with its timer from 0
78. [ ] `!stoppaneltimer` adds `, stopped` to the title, the marker stays put and the timer goes; `!changegame KTANE` shows `(default)`
79. [ ] The column doesn't flicker (it redraws once a second for the timer), and long panel lines wrap without a horizontal scrollbar

## Partial game setup commands

80. [ ] `!changepanel GTA3`: only the left panel changes (`Left panel: GTA3, …`, the column follows); no chat line, and the background, colour and capture sources stay as they were
81. [ ] `!changepanel default` shows the default panels; `!changepanel nonsense` logs `No game or left panel "nonsense"` and changes nothing
82. [ ] `!changecapture KTANE`: `Capture setup: KTANE (461492)` and the capture lines; no chat line, and the background, colour and left panel stay
83. [ ] `!changecapture 999`: `999 not in games.json, capture sources left as is`, and nothing changes

## Dashboard column: names and missing groups

84. [ ] `!changepanel GTA3`: the split-list panels read `Splits: Finished at`, `Splits: Run Delta`, … and the single-group ones their group (`Splits Key Count`)
85. [ ] `!changepanel SWAT4`: once OBS is connected, every line with `Splits Main List Highlights Gold`, `Big Delta` or `Little Delta` is red (the panel up keeps its `>` and timer), while `Splits Basics`, `Splits Current` and the previous-split panels are not
