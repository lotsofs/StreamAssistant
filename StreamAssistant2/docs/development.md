# Development

How to build, run, test and verify, how to work next to a bot that is already running, and recipes for
the changes this codebase sees most. Conventions (formatting, error handling, the warning-free build)
are in [CLAUDE.md](../CLAUDE.md) and aren't repeated here.

## Prerequisites

- Windows, and the .NET 8 SDK. The app is `net8.0-windows` WPF; SAPI speech, drive letters and the
  Windows-only `System.Drawing` colour names make it Windows-only.
- The `Microsoft Catherine` SAPI voice installed (see [infrastructure.md](infrastructure.md#text-to-speech)).
- A `secrets.json` in `Directories.BotInput`; the first run creates it from the template and stops.
- Optional at runtime: OBS with its websocket server on port 4455. The bot copes with OBS being closed.

## Commands

Run all of these from `StreamAssistant2/` (the project folder). The app resolves `paths.json` and
`secrets.json.example` relative to the working directory.

```bash
dotnet build StreamAssistant2.csproj                 # bin/<Config>/net8.0-windows/
dotnet build StreamAssistant2.csproj -c Release
dotnet run --project StreamAssistant2.csproj
dotnet test ../StreamAssistant2.Tests/StreamAssistant2.Tests.csproj
```

VS Code's `build` task and the `.NET Console Launch` configuration in `.vscode/` do the same. The launch
configuration uses the internal console (the app has no console of its own) and sets `cwd` to the
workspace folder.

## Working next to a running bot

The owner runs the bot from the editor with F5, often while a stream is on. So:

- **Never stop or relaunch the running bot.** While it runs it holds `bin/` open, so a normal build
  can't overwrite the DLL.
- **Build to a scratch folder** when the bot may be running:
  `dotnet build StreamAssistant2.csproj -p:BaseOutputPath=<scratch>/`. It still shares `obj/`, which is
  harmless.
- **Don't open an extra window.** Only `Program.Main` creates the dashboard; scratch programs that
  reference the assembly never do. Don't launch the bot to look at it.
- **Writing to the real log folder is fine.**
- **Scratch programs and tests must never touch Twitch or OBS.** Anything that reaches
  `TwitchIRCManager.SendMessage`, `LayoutColoring` (which posts to chat *and* drives OBS) or
  `TestEventRunner.Run` (which runs real handlers) is off limits unless the owner asks.
- **Don't run `git add`, `commit` or `push`** unless explicitly asked. Reading git state is fine.

## Verification, by area

`code/Color/`, the scheduling logic (disk alerts, the midnight calculation, ad warnings) and the IRC and
EventSub timeouts have automated tests. Everything else is checked by running the app and reading the dashboard.

### Unit tests

xUnit project `../StreamAssistant2.Tests` (a sibling folder so the bot's default file globbing doesn't
compile it). The test project references the bot's project, and the bot grants it
`InternalsVisibleTo`.

| File | Covers |
|---|---|
| `ColorNameComparerTests` | strict equality (case, spacing, punctuation) and `Fold` |
| `ColorUtilTests` | hex normalisation, `Darken`/`Lighten`, `ToHex`, `ToOBS`, `HsvToRgb` |
| `RegistryTests` | table and scheme loading, duplicates, prefix forms, random pick |
| `SingleColorParserTests` | every input form, loose matching, `random`, misses |
| `TripleColorParserTests` | the separator-weight rule, slot padding, the token cap |
| `RealDataSmokeTest` | loads the real colour data named in `paths.json`; skipped if absent |
| `ColorData.cs` | fixture loading helpers and the `RealDataFact` attribute |
| `DiskAlertTests` | `DiskSpace.AlertState`: immediate first and worsening alerts, repeats on the clock marks, the early-wake case, recovery |
| `ClockMarksTests` | `ClockMarks`: window numbering on the marks, `UntilNextMinute` and its floor, `NextLocalMidnight` |
| `AdsScheduleTests` | `Ads.RunScheduleAsync`: order, cancelling, a throwing sender, isolation from other work. Uses fake senders, never `SendMessage` |
| `IrcReadTimeoutTests` | `TwitchIRCManager.ReadLineOrTimeoutAsync` over a loopback `TcpListener`: silence times out, outer cancellation stays a cancellation, lines and a close come through |
| `EventSubReceiveTimeoutTests` | `TwitchEventSub.ReceiveFullMessage` over a loopback `HttpListener` WebSocket: silence and a stall mid-message time out, split frames join, a Close frame and a dead socket give their reasons, outer cancellation throws |
| `EventSubConnectTimeoutTests` | `TwitchEventSub.ConnectOrTimeoutAsync`: a connect to a non-routable address times out, outer cancellation stays a cancellation, a loopback `HttpListener` connects |
| `GiftlessRecipientTests` | bomb recipients whose bomb never arrives are reported once (through the real `HandleSubGiftNotif` bomb path, with a short `GiftlessWait`), and a bomb that did arrive isn't |
| `SubscriptionMessageTests` | the `SubscriptionMessages.Build…Message` sentences (sub, resub, targeted gift, bomb, including the month separator, the bomb tier and the "and N other people" count, and the giftless chat line), `CommunityGiftSub` waiting (recipients before or after the count, and a timeout with a partial list), and `SweepOldBombs` |
| `ConnectionHealthTests` | `ConnectionHealth`: band boundaries for both connections, dead equals the reconnect timeouts, and the tracker (silent without data, one line on going bad, none for wobble or Warn alone, one recovery line with the longest silence, the reconnect gap ignored) |
| `EventSubLoopTests` | the real `TwitchEventSub.StartConnectionLoop` against a loopback fake of Twitch (WebSocket sessions plus the subscribe endpoint), through the `EventSubUrl` / `SubscriptionsUrl` / `RetryDelay` / `NotificationHandler` overrides: fresh session, planned reconnect, refused reconnect, 409 and 500 on subscribe. Events go to a capture, never the real handlers |
| `EventSubReconnectTests` | `TwitchEventSub.ConnectToReconnectUrlAsync` and `DrainOldSocketAsync` against a scripted loopback server: welcome, 4007 close, no welcome, wrong first message, missing id, hung connect, cancellation; draining handles notifications and stops on close or the limit |
| `IrcConnectTimeoutTests` | `TwitchIRCManager.ConnectOrTimeoutAsync`: a connect to a non-routable address times out, outer cancellation stays a cancellation, a loopback listener connects |

Fixtures live in `Fixtures/Colors/` and `Fixtures/ColorSchemes/`. Because the registries are static,
test parallelism is off for the assembly; a test that loads other data must reload the fixtures in a
`finally`. Full rules, including `Skip = "<code>: …"` for tests of open bugs, are in
[color.md](color.md#tests-and-verification).

### Everything else: scratch programs

To exercise the real compiled assembly without running the bot:

1. Build the bot to a scratch output (above).
2. Create a throwaway console project (in the scratchpad, not the repo) that references
   `StreamAssistant2.dll`. Target `net8.0-windows`.
3. Internal types need reflection (`Assembly.Load("StreamAssistant2")`, `GetType("StreamAssistant2.Ads")`,
   and so on); public members such as `ConsoleLogger`, `Coloring` and the parsers can be called directly.
4. Read results from the console, from `ConsoleLogger.LineLogged`, or from the log file.

The "reproduced" label on a TODO item means exactly this: run through the real assembly, not just read.

Static state is process-wide, so one scenario per process.

## Recipes

### Add an admin chat command

Add a `case` to `ChatHandler.CheckForAdminCommands`. The command is the first word, case-sensitive; the
argument is the rest, trimmed. Reach the bot's capabilities through the modules
(`LayoutColoring`, `TwitchIRCManager.SendMessage`, `TextToSpeech.EnqueueSpeech`).

### Turn on a public chat command

The FAQ commands in `ChatHandler._commands` already match. In `CheckForCommands`, replace the commented
`MsgQueue.Enqueue(MsgTypes.ChatMsg, outputMsg)` with `TwitchIRCManager.SendMessage(outputMsg)`. Consider a
per-command cooldown first, since the loop has none and every matching command fires.

### Add an EventSub event

Two edits (descriptor, then handler case); see [twitch.md](twitch.md#adding-an-eventsub-event). Then make
a replay file for it ([twitch.md](twitch.md#the-test-harness)) and run `!test <name>` from the
broadcaster account.

### Add a channel-point reward

1. Create the reward. If the bot must fulfil or cancel redemptions, create it **through the API with
   the bot's client id** ([color.md](color.md)); a dashboard-made reward gets a 403.
2. Redeem it once. The bot logs `Unhandled channel point reward redemption id: <id>` and the payload.
3. Add a `REWARD_ID_…` constant and a `case` in `ChannelPoints.ProcessAdd`.
4. Close the redemption with `TwitchHelixApi.UpdateRedemption` if the bot is responsible for it.

### Add an alert sound

Add a member to `Sound.Sounds`, a `case` in `PlaySound(Sounds)` with the file name and a volume, and put
the file in the alert-sounds folder.

### Schedule something

There is no scheduler. Write an `async` loop in the module that owns the work (`DiskSpace.WatchAsync` and
`TwitchUptime.UptimeLoopAsync` are the models): wait, run the work inside a try/catch that logs an `Error`
code, repeat; and start it with `_ = …` from a `Start()` that `Program.StartBotAsync` calls. For one-shot
delayed work, `await Task.Delay` in an async method; to make a group of delayed actions cancellable, give them
one `CancellationTokenSource` as `Ads` does. Keep the *decision* (is it time? what is due?) in a pure function
so it can be tested without waiting.

### Add a log channel

Add the enum member to `ConsoleLogger.ColorType`, then a colour in `MainWindow._logColors`. Update the
tables in [infrastructure.md](infrastructure.md#colour-channels) and [dashboard.md](dashboard.md).

### Add a database table

Add its `CREATE TABLE IF NOT EXISTS` to `Database.InitAsync` and a method per operation, parameterised,
each opening its own connection. Altering an existing table needs an explicit migration step: the
`IF NOT EXISTS` won't do it.

### Add an OBS action

Add a wrapper to `Obs.cs` with the `IsConnected()` guard. Wrap callers that run outside the colour worker
in their own try/catch, since the wrappers don't catch.

### Add a dashboard panel

See [dashboard.md](dashboard.md#adding-a-panel).

### Add colour data

Drop a JSON file into the tables or schemes folder named in `paths.json`; no code. The rules and the
loader's log lines are in [color.md](color.md).

### Add a module

A `static class` under `code/<Area>/` (WPF windows and panels go in `ui/`). Start it from `Program`,
in `StartBotAsync` or `EnableBot`, in the right order. Both the root and `code/` are mid-migration;
don't move existing files unprompted.

## When you change something

- Keep the build at zero warnings, bot and test project.
- Update the docs that describe it. A new or removed hardcoded value goes in
  [reference.md](reference.md); a new error code goes in its table.
- A finished TODO item is **deleted** (index row and detail section), not marked done.
