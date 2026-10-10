# Gotchas

Things that look harmless and aren't, gathered in one place. Each line says what bites and where the
full explanation is. Skim this before making a change you think is small.

## Code that goes live

- **`TwitchIRCManager.SendMessage` posts to the real channel, as `lotsofs`.** So does anything that
  calls it: `LayoutColoring` (also drives OBS), `TestEventRunner`, `ChatterList`, the connect and
  disconnect notices. Scratch programs and tests must not reach them. ([development.md](development.md))
- **`!test <script>` runs the real handlers**: sounds play, TTS speaks, chat messages go out.
  ([twitch.md](twitch.md#the-test-harness))
- **Each reconnect talks in chat** (`🟣 Connected`, `🟣 ES Connected`, `💥 ES Disconnected`). A flapping
  EventSub connection spams the channel every few seconds. Twitch's daily planned reconnect is silent
  when it succeeds. ([twitch.md](twitch.md#planned-reconnects))
- **The colour announcement is posted when the request is queued**, nine seconds per request ahead of it
  before anything changes, and also when OBS is closed and nothing will change.
  ([obs.md](obs.md#layout-recolouring))
- **Admin status is just the login `lotsofs` or `botsofs`.** The bot is logged in as `lotsofs` itself.
  ([twitch.md](twitch.md#chat-handling))

## Concurrency

- **Start the bot from the thread pool, never from the UI thread.** WPF's synchronization context would
  otherwise swallow every fire-and-forget loop. ([architecture.md](architecture.md#startup-and-shutdown))
- **Handlers run on the IRC and EventSub read loops.** Don't block in them; start async work and return.
- **Read EventSub fields with `ReadString`/`ReadInt`/`ReadBool`/`ReadElement`, not `GetProperty`.** Twitch sends `null` for
  absent values (an anonymous gifter's login and totals), which `GetInt32()` and friends throw on.
  ([architecture.md](architecture.md#codeutil))
- **Read everything you need from an EventSub `JsonElement` before the first `await`, or `Clone()` it.**
  The document is disposed at the end of the loop iteration. ([twitch.md](twitch.md#adding-an-eventsub-event))
- **Exceptions inside `_ = SomeAsync()` vanish.** Only code before the discarded call reaches the
  surrounding catch, so give an async body its own try/catch. EventSub handlers go through
  `FireForget.Run`, which does this (one `Error TEH_…` code per handler).
- **There is no scheduler.** Time-based work is a loop in the owning module with its own try/catch.
  ([infrastructure.md](infrastructure.md#periodic-work))
- **Static state has no locks.** Assume one thread unless you check.

## Twitch and Helix

- **Only rewards created by the bot's own client id can have redemptions fulfilled or cancelled.**
  Recreating one in the dashboard breaks refunds with a 403 and `Error CP1`. ([color.md](color.md))
- **Tokens are never refreshed.** An expired one means reconnect loops every 3 s on both transports.
  ([twitch.md](twitch.md#credentials-and-identity))
- **The IRC "ping age" is really time since *any* line.** ([twitch.md](twitch.md#irc))
- **IRC tags are ignored.** The bot knows nothing about badges, moderators or display names.
- **`channel.follow` is subscribed but not handled**, so every follow logs a "not handled" line.
- **`CheckForCommands` matches and then sends nothing**; the public FAQ commands are off.
  ([legacy.md](legacy.md))
- **A silent connection is recycled, not just shown red**: IRC after 7 minutes, EventSub after 20 s.
  The EventSub one has been seen live; the IRC one hasn't yet. ([TODO.md](TODO.md), item DCD)

## Colour (details in [color.md](color.md))

- **Hex is always lowercase `#rrggbb`.**
- **The triple parser ranks by *mean* separator weight, not the sum.** Don't simplify it.
- **All-space input is ambiguous on purpose**: `red green blue` is `red` + `green blue`.
- **Category names in scheme files must be one word.**
- **Strict matching ignores case, spaces and punctuation but not accents**; loose matching is a
  fallback that never beats an exact spelling anywhere.

## Startup and environment

- **Run from the project folder.** `paths.json` and `secrets.json.example` resolve against the working
  directory.
- **`secrets.json` lives outside the repo**, in `Directories.BotInput`, a different git repo that must
  ignore it. Never print or log its values.
- **The TTS voice `Microsoft Catherine` must exist**, and the first use is at the *end* of startup, so a
  missing voice shows up as `Error PRG1` after everything else is already running.
  ([infrastructure.md](infrastructure.md#text-to-speech))
- **One path is hardcoded outside `paths.json`**: the alert-sounds folder. ([reference.md](reference.md))
- **Scene, source and filter names in OBS are strings in code**; renaming one in OBS silently breaks that
  call. ([reference.md](reference.md#obs-names))
- **Windows only**: SAPI, WPF, drive letters, `System.Drawing` colour names.

## Building and tooling

- **A running bot locks `bin/`.** Build to a scratch folder with `-p:BaseOutputPath=…`, and let the owner
  start the app with F5. ([development.md](development.md#working-next-to-a-running-bot))
- **The build is warning-free.** Keep it that way rather than suppressing.
- **`code/` is lowercase and a case-only rename needs `git mv`**, with VS Code holding a handle on the
  folder itself; move subfolders instead. ([CLAUDE.md](../CLAUDE.md))
- **Don't clean up the commented-out code or finish either migration unasked.**
  ([legacy.md](legacy.md))
- **`Fold` relies on `string.Normalize`**, which silently does nothing under `InvariantGlobalization`.

## Logging

- **Each log line opens, appends and closes the file.** That is what lets a hard kill (stopping the
  debugger) lose almost nothing; it's measured and cheap enough, so don't "optimise" it into a held
  writer. ([infrastructure.md](infrastructure.md#logging))
- **Tests that load colour data write to the real log folder** if the data has errors, so keep fixtures
  valid. ([color.md](color.md#tests-and-verification))
- **`LineLogged` runs on the logging thread.** Subscribers must marshal.
- **Stopping the debugger skips shutdown** entirely; the `Shutting Down` post and `DisableBot` only run
  when the window is closed normally.
