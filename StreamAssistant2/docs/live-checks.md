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
20. [ ] A category not in `games.json`: `No background for None, left as is`, the `none` colour scheme applies, one `→ none, hidden:` line listing all three kinds with slots `0, 1, 2`
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

36. [ ] After a Twitch connection reset (most days, no need to cause one), the log shows `EventSub connection lost (ConnectionClosedPrematurely): An existing connection was forcibly closed by the remote host.`, then `EventSub session ended: ConnectionLost (socket Aborted, close none)` and a fresh session, with no `Error TES1` or `Error TES3`
