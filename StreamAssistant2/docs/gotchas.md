# Gotchas

Things that look harmless and aren't, gathered in one place. Each line says what bites and where the
full explanation is. Skim this before making a change you think is small.

## Code that goes live

- **`TwitchIRCManager.SendMessage` posts to the real channel, as the bot account (`botsofs`).** So does anything that
  calls it: `LayoutColoring` (also drives OBS), `TestEventRunner`, `ChatterList`, the connect and
  disconnect notices. Scratch programs and tests must not reach them. ([development.md](development.md))
- **`!test <script>` runs the real handlers**: sounds play, TTS speaks, chat messages go out.
  ([twitch.md](twitch.md#the-test-harness))
- **Each reconnect talks in chat** (`🟣 Connected`, `🟣 ES Connected`, `💥 ES Disconnected`). A flapping
  EventSub connection spams the channel every few seconds. Twitch's daily planned reconnect is silent
  when it succeeds. ([twitch.md](twitch.md#planned-reconnects)) StreamElements posts `🟣 SE Connected` /
  `💥 SE Disconnected` only on a real change, never once per retry.
  ([streamelements.md](streamelements.md#session-lifecycle))
- **The colour announcement is posted when the request is queued**, nine seconds per request ahead of it
  before anything changes, and also when OBS is closed and nothing will change.
  ([obs.md](obs.md#layout-recolouring))
- **Admin status is just the login: the broadcaster's or the bot account's, from `secrets.json`
  (`lotsofs`, `botsofs`).** The bot is logged in as `botsofs` (as the broadcaster without `twitchBot`),
  and never sees its own messages.
  ([twitch.md](twitch.md#chat-handling), [credentials](twitch.md#credentials-and-identity))

## Concurrency

- **Start the bot from the thread pool, never from the UI thread.** WPF's synchronization context would
  otherwise swallow every fire-and-forget loop. ([architecture.md](architecture.md#startup-and-shutdown))
- **Handlers run on the IRC, EventSub and StreamElements read loops.** Don't block in them; start async work and return.
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
- **A LiveSplit file's write time doesn't mark every split.** The component (release 8) skips a file whose
  contents haven't changed and writes the rest on a background thread, so a repeated sign keeps the
  earlier split's time. ([obs.md](obs.md#splits-from-livesplit))
- **A stale LiveSplit path shows blank, not an error.** Release 8 empties every file in its folders that it
  doesn't write, so a text source or `leftpanel.json` path that a release moved still exists, empty.
  ([obs.md](obs.md#where-the-data-came-from))

## Twitch and Helix

- **Only rewards created by the bot's own client id can have redemptions fulfilled or cancelled.**
  Recreating one in the dashboard breaks refunds with a 403 and `Error CP1`. ([color.md](color.md))
- **Tokens are never refreshed.** An expired one means reconnect loops every 3 s on both transports.
  ([twitch.md](twitch.md#credentials-and-identity))
- **The IRC "ping age" is really time since *any* line.** ([twitch.md](twitch.md#irc))
- **The StreamElements age is the bot's own probe, not StreamElements' keepalive.** Astro's pings are
  WebSocket control frames that .NET 8 answers but never shows, so the bot re-subscribes every 30 s and
  counts the replies. The reply is an *error*, `err_bad_request` / `already subscribed to topic`, which
  means success. Don't treat it as a failure: that would spam the log and break resumed reconnects.
  ([streamelements.md](streamelements.md#keepalive-the-probe))
- **A StreamElements tip carries the tipper's email, and the name is free text.** The dump and log go
  through `Redact`; the name goes through `LanguageFilter` before chat and TTS. ([events.md](events.md#tips))
- **The money goal only counts tips the bot sees.** A tip that arrives while it isn't running is never
  counted, and `!test tip` deliberately isn't. Inside `Tips`, `Money(...)` is the amount formatter, not
  the goal: that is `MoneyGoal`. ([obs.md](obs.md#money-goal))
- **A refused StreamElements token stops that connection until restart** (`Error SES3`, then
  `StreamElements stopped`); the rest of the bot carries on. ([streamelements.md](streamelements.md#session-lifecycle))
- **IRC tags are ignored.** The bot knows nothing about badges, moderators or display names.
- **`channel.follow` is subscribed but not handled**, so every follow logs a "not handled" line.
- **The FAQ commands answer the broadcaster too**, natural-language questions included, so your own
  chat can trigger them. Each answers a user at most once a minute, and a message gets only the first
  that matches.
  ([twitch.md](twitch.md#public-commands))
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
- **`secrets.json` lives outside the repo**, in `BotInput`, a different git repo that must
  ignore it. Never print or log its values.
- **The TTS voice in `Speech.Voice` (`Microsoft Catherine`) must exist**; a missing one logs `Error 5`
  for every utterance, the first at the *end* of startup, after everything else is already running.
  ([infrastructure.md](infrastructure.md#text-to-speech))
- **`paths.json` holds only `BotInput`; every other directory is in `settings.json`**, and an empty one
  turns its feature off. A first start creates `settings.json` from the example, whose `BotOutput` is
  `%APPDATA%\StreamAssistant2\Bot Output`, so the log and the database go there until it's changed. ([infrastructure.md](infrastructure.md#settings))
- **A broken `settings.json` stops startup** with a message box, but on `!reloaddata` only logs
  `Error SET1` and keeps the old settings. A misspelt key counts as broken. ([infrastructure.md](infrastructure.md#settings))
- **Code must read `Settings.Data` at each use**, never keep a value in a field, or `!reloaddata` won't
  reach it. `BotOutput` alone waits for a restart. ([infrastructure.md](infrastructure.md#settings))
- **Scene, source and filter names in OBS are strings in `settings.json`**; renaming one in OBS silently
  breaks that call until the setting is changed too. ([reference.md](reference.md#layout))
- **In a `leftpanel.json` panel that shows a copy family, every `Text` and `File` source gets the copy's
  number**, so a source shared by all copies, or in another group, can't be set there.
  ([obs.md](obs.md#copies))
- **A panel's `Keep` isn't checked.** A kept copy family stays on screen, so if the panel changes it after
  all, the change shows in place. ([obs.md](obs.md#copies))
- **Windows only**: SAPI, WPF, drive letters, `System.Drawing` colour names.

## Building and tooling

- **A running bot locks `bin/`.** Build to a scratch folder with `-p:BaseOutputPath=…`, and let the owner
  start the app with F5. ([development.md](development.md#working-next-to-a-running-bot))
- **The build is warning-free.** Keep it that way rather than suppressing.
- **`code/` is lowercase and a case-only rename needs `git mv`**, with VS Code holding a handle on the
  folder itself; move subfolders instead. ([CLAUDE.md](../CLAUDE.md))
- **The test project mirrors `code/`'s folders, but `ColorData.cs` must stay at its root**: it finds the
  bot's `paths.json` two folders up from itself (and the `settings.json` it leads to), so moving it skips
  the real-data tests.
  ([development.md](development.md#unit-tests))
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
