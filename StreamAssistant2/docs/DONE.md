# DONE — log of finished work

Finished work, newest first: one row per finished item, and per piece of finished work that never had a code
(`—`). The open items are in [TODO.md](TODO.md). A finished item's detail section is deleted with it, and git
history keeps it: `git log -S "## CODE " -- docs/` finds the commit. Work finished before 2026-10-23 isn't
logged here, only in git history.

| Date | Code | Area | Change | Why / how |
|---|---|---|---|---|
| 2026-10-25 | RLD | All | Add `!reloaddata`: reload the colour, games and left-panel files without a restart, then redo the game setup | The data files were read only at startup, and the colour registries were refilled in place, with one broken file throwing. They now swap in whole loads, and a broken file keeps its old data and logs `Error CLR1`. Builds warning-free and the tests pass; not yet seen live (LIV 137–139). |
| 2026-10-25 | TRS | OBS | Hide a train left showing when OBS connects, and a running one at shutdown | The hide ran only if the bot was still up 62 s later, so closing it mid-train left the train on screen. Each OBS connect now hides a train OBS shows while none runs, and closing the window hides a running one. Builds warning-free, tests pass; not yet seen live (LIV 134–136). |
| 2026-10-25 | — | Docs | Set up the DONE log and the changelog | Finished work had no record but git history. The log starts with this session's work, and the changelog waits for release 1. |
| 2026-10-25 | — | Docs | Move CLAUDE.md's test list and dashboard detail to their docs, and add the single-test and running-bot commands | Both passages repeated development.md and dashboard.md and went stale. The test classes the table missed got rows. Links checked. |
| 2026-10-25 | OTF | OBS | Have the previous-split and split list highlight panels set their sources' files | OBS kept those files itself, pointing at deleted release 7 files or at whatever its missing-files prompt was given. `List On` and `Previous split` now set them. Paths checked against release 8's file names; not yet seen live (LIV 133). |
| 2026-10-25 | — | OBS | Number only the sources OBS has numbered copies of | A panel that shows copies put the copy number on every source it set, so a lone source got a name OBS doesn't have. The rotation now reads OBS's input names once. Unit-tested. |
| 2026-10-25 | — | OBS | Colour the split list highlights under their new `SL Highlights` names | The sources were renamed in OBS, so the old names would fail on every rotation start. Unit-tested. |
| 2026-10-23 | — | LiveSplit | Follow Output to File release 8's split index and sign | Release 8 dropped the index file and moved the sign, so no split was seen. The index now comes from the current split's number and the completed count. Unit-tested; not yet seen live (LIV 130–132). |
| 2026-10-23 | PBL | OBS | Colour a finish's sign like the same sign mid-run | Release 8 renamed the finish values. Reading them as their in-run sign also gives the old `PBLost` its negative segment colour. Unit-tested. |
| 2026-10-23 | — | OBS | Point leftpanel.json's LiveSplit paths at release 8's files | Release 8 moved the comparison files and emptied the old ones, so those panels showed blank. Every path checked against release 8's file names. |
| 2026-10-23 | PGD | OBS | Portal2's per-map gold panel gets values | Release 8 writes gold files at every level, not only for all splits. Read in the component, not seen live (LIV 126). |
