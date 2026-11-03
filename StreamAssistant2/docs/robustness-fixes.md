# Robustness fixes

Known defects and loose ends in the tree as it stands, each with a plan. Mostly independent of each
other: each can be done, tested and committed on its own, except where an item says otherwise.

Items are indexed in [TODO.md](TODO.md): summaries, severities and the suggested order of work live
there, along with the conventions these codes follow. This file holds the detail and the plans.

Each plan ends with the docs to touch. When an item is finished, delete its section here and its row in
TODO.md, and log it in [DONE.md](DONE.md), per the conventions there.

---

## TOK — Refresh the Twitch tokens at runtime

**Parked** by the owner on 2026-11-02, until a token actually expires. The research and design below were
done that day; nothing is built.

`secrets.json` holds two Twitch accounts in the same shape (`login`, `accessToken`, `refreshToken`,
`clientId`): `twitchAuth`, the broadcaster, whose token EventSub and Helix use, and `twitchBot`, whose
token IRC uses. Nothing reads either `refreshToken`, or `twitchBot`'s `clientId`. Each access token is
used until it stops working, and the expected symptoms are in
[twitch.md § Credentials and identity](twitch.md#credentials-and-identity): IRC retrying every 3 s
(`Error 1` / `Error TIRC2`) for the bot's token, and EventSub restarting every 3 s on a 401
(`Error TES1`) for the broadcaster's, with the redemption PATCH failing too. The fix today is to replace
the token in `secrets.json` by hand and restart the bot. Read only; no expiry has been seen.

### Where the tokens come from

The tokens came from a token generator site (owner, 2026-11-02), and `secrets.json` has no client secret, so
the bot can't use Twitch's own refresh call and has to go through the site. The design assumes
twitchtokengenerator.com, which hands out this set of access token, refresh token and client id; confirm that
first. Another site changes only the URL and `ParseRefresh`.

The design stays on the generator rather than moving to an own app. The colour and flush rewards belong to the
current client id ([color.md](color.md)), so an own app would need new authorisation for both accounts,
recreated rewards and new reward ids. The price is relying on the site, whose logs may hold the refresh token,
since it goes in the URL path. The refresh call stays in one method, so changing later is small.

Read in outside documentation on 2026-11-02; none of it has been run:

- `GET https://twitchtokengenerator.com/api/refresh/<refresh token>` returns
  `{"success":true,"token":…,"refresh":…,"client_id":…}`. The
  [README](https://github.com/swiftyspiffy/twitch-token-generator) gives only the URL; the method and fields
  come from a [2021 GitHub issue](https://github.com/swiftyspiffy/twitch-token-generator/issues/10), so the
  first live refresh is the real proof. The token lifetime isn't documented.
- That `client_id` was wrong in the issue (the site's API app, not the one the token works with), and Helix
  refused the pair. Ignore it and use the one from `validate`.
- A refresh [may return a new refresh token](https://dev.twitch.tv/docs/authentication/refresh-tokens/), so the
  old one may be spent and a lost response can lose the tokens.
- [`validate`](https://dev.twitch.tv/docs/authentication/validate-tokens/) gives `expires_in`. The page doesn't
  define 0: treat it as no scheduled refresh; a 401 still renews.
- Twitch asks apps to validate at startup and then hourly, and says it audits for this. The bot validates only
  before an account's first name colour change.
- A 401 needn't mean an expired token: Helix gives one for a missing scope too. Validate before refreshing, or
  a scope problem would call the generator every minute through the name colours.

### Plan

1. **Check.** A new static `code/Twitch/TwitchTokens.cs` covers the broadcaster and, when it's a different
   account, `TwitchIRCManager.ChatAccount`. `await TwitchTokens.StartAsync()` just before `EnableBot()` checks
   both in parallel, so IRC's first login has a good token, then a loop repeats that hourly. A check validates
   (`TwitchHelixApi.ValidateTokenAsync`, whose `TwitchToken` gains `ExpiresIn` and which throws a
   `TokenRefusedException` on a 401, with today's message), logs the time left and warns when the login doesn't
   match or the bot lacks `chat:read` / `chat:edit`. It sets `ClientId` from `validate`, warning if that differs,
   since refunds depend on it, and refreshes a refused token or one with under 90 minutes left. That margin is
   over the hourly interval, so a token can't expire between checks. `_userHttp`'s timeout drops to 15 s, so a
   dead Twitch can't hold up startup for long.
2. **Refresh.** One `SemaphoreSlim` for all accounts, so callers never race for a refresh token. Skip when the
   token already changed, the refresh token was refused, or the last try is under a minute old. Set the new
   tokens on the existing `TwitchAuthConfig` objects, which `NameColor` holds, and save them **before anything
   else can fail**; validate after that, only to report. Use its own `HttpClient`, 15 s timeout, no redirects.
   `Error TOK1` is a passing failure, tried again at the next check; a response without the fields logs its key
   names only. `Error TOK2`, once, is a refused refresh token: it says to get new tokens and restart, and nothing
   tries again until then. Never log tokens, the refresh URL or a raw body.
3. **Renew when refused.** `TwitchTokens.SendAsync(http, account, build)` sends; on a 401 it validates, refreshes
   a refused token and resends once. `build` is a factory, since a request can't be sent twice. All Helix calls
   and EventSub's `Subscribe` go through it, with per-request headers from a new `TwitchHelixApi.AsAccount`. That
   drops the `HttpClient` default headers, which aren't thread-safe to change while requests run, and the token
   copies in both classes. `SetChatColorAsync` takes the `TwitchAuthConfig`. IRC keeps the account instead of
   `_oauth`, reads its token in `ConnectOnce`, and on a pure `IsLoginFailure(line)` (`:tmi.twitch.tv NOTICE * :`
   plus `Login authentication failed`, `Improperly formatted auth` or `Login unsuccessful`) renews and throws into
   the normal reconnect. A scheduled refresh doesn't reconnect IRC: it only uses the token at login, and a
   reconnect would post `🟣 Connected` and drop chat for nothing.
4. **Save.** `Config.SaveTokens(account)` reads the file fresh, writes `secrets.json.tmp` beside it and moves it
   over, deleting the tmp in a `finally`; a failure logs `Error TOK3` and the bot carries on with the new tokens.
   A pure `WithTokens(byte[] utf8, block, access, refresh)` finds the two values with `Utf8JsonReader` and splices
   them in, so the rest of the file stays byte-identical, BOM and line endings included. Add
   `/Bot Input/secrets.json.tmp` to Stream-Resources' `.gitignore`, so a tmp left by a crash can't be committed.
5. **`!refreshtokens`**, an admin command through `FireForget.Run`, refreshes both accounts whatever the time
   left, for verifying and recovering.
6. **Tests.** The pure parts: `IsRefreshDue`, `ParseRefresh`, `MissingScopes`, `IsLoginFailure`, `WithTokens`, the
   time-left format. Then a loopback fake with overridable `RefreshUrl` and `ValidateUrl`, in `EventSubLoopTests`'
   collection: a refresh adopts and saves; `success:false` stops trying; a 5xx tries again after the pause;
   concurrent 401s refresh once; a still-valid token isn't refreshed on a 401; the response's `client_id` is
   ignored; a 401 on subscribe refreshes, resends and delivers. Replace `Save` in every test, so none can reach
   the real `secrets.json`.

**Risk:** if the response's fields have changed since 2021, the first refresh logs `Error TOK1` with the key names
and its new tokens are lost. Recovery is what it is today: fresh tokens from the site by hand.

**Verify:** F5 logs the time left on both tokens, which also shows the generator's real lifetime.
`!refreshtokens` logs both refreshed; chat, a channel-point refund and the name colours keep working;
`secrets.json` differs only in the token values, and the next F5 starts on them.

**Docs when done:** delete this section and the TODO row, and log it in [DONE.md](DONE.md). Update the credentials
table, the expiry paragraph and the TOK links in [twitch.md](twitch.md#credentials-and-identity), with a section on
the tokens; [infrastructure.md § Config](infrastructure.md#config) (the bot now writes `secrets.json`);
[reference.md](reference.md) for the log lines, error codes, URLs, constants and `_userHttp`'s timeout;
[architecture.md](architecture.md) for the startup order and file map; [development.md](development.md) for the
test list; [gotchas.md](gotchas.md) (a saved refresh token replaces the one in the file, and hand-pasted tokens
need a restart); [live-checks.md](live-checks.md) and the LIV row for the checks under **Verify**; and in CLAUDE.md
the "never refreshed at runtime" sentence, the startup order, the admin commands and `secrets.json` being written.

## EDR — Reconnected sessions may drop more in the evening

Counted from the real bot's logs, 2026-10-08 19:00 to 2026-11-02. Each dropped connection (`ConnectionLost`, or
`None` before that reason existed) goes to the kind of session it ended: fresh (since a `session_welcome`) or
reconnected (since `EventSub reconnected`).

| Local time | Fresh | Reconnected |
|---|---|---|
| 09:00 to 18:00 | 211 h connected, 2 drops, 0.2 per 24 h | 9 h, no drops |
| 18:00 to 01:00 | 110 h, 1 drop, 0.2 per 24 h | 61 h, 5 drops, 2.0 per 24 h |
| 01:00 to 09:00 | 168 h, 35 drops, 5.0 per 24 h | 26 h, 8 drops, 7.5 per 24 h |

Overnight is the main pattern, and it hits both kinds alike. The evening row is the open question: five drops
against one is too few to call, but it is about nine times the rate in the same hours. There is no fixed delay:
reconnect to drop took 0.5 to 10.4 h, median 2.1 h, against a median of 2.4 h for a fresh session to drop. One
of the quick ones, 10-23 at 01:01, was a local outage (`No such host is known` for about 8 hours), not Twitch.

A planned reconnect has no gap of its own, so the worst case is a deferred drop. A drop costs about 5 s from
the drop to `EventSub subscribed`, at most 10 s: the 3 s wait, then connecting and subscribing. Events in that
gap are lost, and chat gets a disconnect and a connect line.

**Plan:**

1. Count again once a few weeks of new logs exist. If reconnected sessions stay well above fresh ones between
   18:00 and 01:00, it's real, and worth asking whether Twitch's `reconnect_url` hosts are the cause.
2. Whatever the count says, consider skipping the 3 s wait on the first retry after `ConnectionLost`, which
   would about halve every drop's gap. `SessionExitReason` already says which exits those are.

**Verify:** the recount, with the same method. For the shorter wait, a test in `EventSubLoopTests` that a dropped
connection reconnects at once and a repeat failure still waits.

**Docs when done:** delete this section and the TODO row, and log it in [DONE.md](DONE.md). A shorter wait also
changes [twitch.md](twitch.md#session-lifecycle), the charts in [eventsub-flow.md](eventsub-flow.md) and
[reference.md](reference.md).

## DCD — Connection timeouts unverified live

Both transports now time out a connection that goes silent without closing, and a connect that hangs:

| Constant | Value | Effect |
|---|---|---|
| `TwitchIRCManager.SilenceTimeout` | 7 min | no IRC line → `Error TIRC2`, `Error 1`, reconnect |
| `TwitchIRCManager.ConnectTimeout` | 15 s | hung IRC connect → `Error 1`, retry |
| `TwitchEventSub.KeepAliveTimeout` | 20 s | no EventSub message → session ends with `KeepAliveTimeout`, reconnect |
| `TwitchEventSub.ConnectTimeout` | 15 s | hung EventSub connect → `Error TES1`, retry |

The helpers are unit-tested against loopback servers (`IrcReadTimeoutTests`, `IrcConnectTimeoutTests`,
`EventSubReceiveTimeoutTests`, `EventSubConnectTimeoutTests`). The EventSub keepalive timeout has now been
seen live: on 2026-10-09 at 03:28, `EventSub quiet for 00:15, reconnecting at 00:20` was followed about 4 s
later by `EventSub session ended: KeepAliveTimeout (socket Aborted, close none)`. A fresh session, a full
resubscribe and `EventSub recovered after 00:19 of silence` followed within 4 s. The cause was not recorded.
The IRC silence timeout and both connect timeouts have not been seen against Twitch: they need a
connection that goes quiet without a TCP close.

Reading `keepalive_timeout_seconds` from the welcome message was considered and dropped: the bot never
asks for a non-default keepalive, and there is no reason to (raising it only slows dead-socket detection;
10 s is Twitch's minimum).

### Verify live (owner, off-stream)

1. Temporarily lower the timeouts: `SilenceTimeout` to 30 s, `KeepAliveTimeout` to 12 s (keep it above
   10 s or normal keepalive gaps will trip it).
2. Start the bot, then block outbound traffic to Twitch without closing the sockets: a Windows Firewall
   outbound block rule on the bot's process.
3. Expect: the EventSub age turns orange with one `EventSub quiet for …` line (connection health), then
   red and, at the timeout, `EventSub session ended: KeepAliveTimeout`, then
   reconnect attempts that fail (`Error TES1: None` and `Error TES3` after 15 s each while blocked, since a
   failed connect sets no exit reason). Likewise IRC: `Error TIRC2` with a `TimeoutException` in the file
   log, then `Error 1` retries.
4. Remove the rule: both reconnect, the chat gets `🟣 Connected` / `🟣 ES Connected`, and each
   connection logs one `… recovered after …` line. Lifting the block before the timeout instead should
   give the recovery line with no reconnect.
5. Restore the constants.

**Docs when done:** delete this section and the TODO row; drop "the IRC one hasn't yet" from the bullet
in [gotchas.md](gotchas.md#twitch-and-helix).

---

## TSS — Tests share static state, so parallelism stays off

To revisit later: running the test collections in parallel would shorten the suite, but tests share static state
and rely on running one at a time. `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in
`ColorData.cs` keeps them sequential.

Reproduced 2026-11-03 on the real assembly, forcing it on with `-- xUnit.ParallelizeTestCollections=true`: the
suite went from about 11.8 s to 2.3 s. Of 41 forced runs, about a quarter failed on `StreamElementsSocket.Times`,
one also on the left panel (both below), and one hung for hours until it was stopped. 20 more runs with vstest's
`--blame-hang` didn't hang, so what hung isn't known. The runs that passed had none of the protection below, so they
don't show it's safe.

xUnit's `DiagnosticMessages` start line can't check this. It follows the runner's setting, not the attribute, so a
default run says `parallel test collections = on` and is still sequential. Each test class's results staying in one
block shows a sequential run; forced on, they interleave.

What collides with parallelism on (read, except where it says seen):

- **Left-panel state and log lines.** `GamesGoLiveTests` (`GamesStateCollection`) runs `!changepanel GTA3`
  with no panels loaded and asserts `No left panels loaded` and no `Left panel:` line.
  - `LeftPanelStopTests` (`ColorDataCollection`) loads panels into `LeftPanel`'s static set for each test
    and starts rotations that log `Left panel: GTA3, …`.
  - `LeftPanelRotationTests` (no collection) log `Left panel: … has groups OBS doesn't` and
    `Left panel: can't read …`.
  - The games test captures lines through the global `ConsoleLogger.LineLogged`. So any of those lines
    logged in its window fails it, and so do panels that happen to be loaded at that moment.
  - Seen once: `LeftPanelStopTests.StartAfterStop_IsRestartedByObsConnectingAgain` counted 2 `Left panel: GTA3`
    lines, not 3. Which test got in between isn't known.
- **The colour registries.** `GamesGoLiveTests` reach `Games.Apply`, whose colour step (`GameColor.Set`)
  reads `ColorSchemeRegistry.Sets`. Meanwhile `ColorDataCollection` tests may be reloading it
  (`Sets.Clear()`, then refill). A read during the refill can throw (caught and logged as `Error GMS2`) or
  find nothing.
- **`Config`.** `LeftPanelStopTests` point `Config.Data.Directories.BotInput` at a temporary folder for each test.
- **`StreamElementsSocket.Times`.** `StreamElementsLoopTests` set it for each test, one with a 500 ms silence limit,
  while `StreamElementsRetryTests`, in a collection of its own, checks its defaults. Seen:
  `Defaults_SilenceIsTheHealthDeadline` finding `Times.Silence` at 0.5 s.
- **Other log captures.** `EventSubLoopTests`, `StreamElementsLoopTests`, `GiftlessRecipientTests` and
  `FireForgetTests` capture the global log too. Whether their assertions can catch another test's lines isn't checked.
- **Timing.** The connection-loop tests run against timeouts, so they could turn flaky under load.

### Plan

1. Put every class that touches `LeftPanel`, `Games` or the colour registries in one collection. Move
   `GamesGoLiveTests`, `GamesTests`, `ChatterListTests` and the `LeftPanel*Tests` classes into
   `ColorDataCollection` (or rename it to something broader), and drop `GamesStateCollection`.
2. Put `StreamElementsRetryTests` and `StreamElementsLoopTests` in one collection.
3. Go through the other log captures and move any whose assertions can catch another test's lines into the first
   collection. Together they take about 5 s, so the suite would land between 2 and 7 s (worked out from each
   class's time, not measured).
4. Remove the attribute.

Filtering the games test's captured lines to the ones it causes fixes the log lines but not the shared panel set,
so it isn't enough on its own.

**Verify:** run the suite 20 times in a loop with `--blame-hang` and no failures or hangs, and check that the
results interleave.

**Docs when done:** delete this section and the TODO row; update the parallelism notes in
[development.md](development.md#unit-tests) and [color.md § Tests and verification](color.md#tests-and-verification).

---

## SHR — SplitWatcher's reads refuse LiveSplit's writes

`SplitWatcher.ReadText` reads LiveSplit's files with `File.ReadAllText`, which opens them with `FileShare.Read`.
While that read is open, a writer is refused. Output to File writes with `File.WriteAllText`, and when that fails
its `FileQueue` forgets the file, so the write is only tried again at the component's next full rewrite: the next
split, skip, undo, pause, scroll or reset. A read of `Number.txt`, `Completed.txt`, `RunTime.txt` or a `Sign.txt`
that overlaps the component's write of it could leave that file stale on disk, and in OBS, until then. `SplitWatcher` reads right after each
change event, which is when the component is writing. Read only; never seen. The dashboard's `SplitList`
already reads with `FileShare.ReadWrite | FileShare.Delete` (`SplitList.ReadShared`).

### Plan

Make `ReadText` open the file the way `ReadShared` does, keeping its retry for a file LiveSplit has locked.
Such a read can now land mid-write and see an empty or shorter file. `Index` already ignores a number that isn't
one, and the next change event reads the file again, so `OnChanged` copes. Check `IsSkip` and the sign's
colours on an empty read: an empty `RunTime.txt` counts as a split, and an empty sign colours nothing.

**Verify:** the `SplitWatcher` tests pass, and LIV 158 stays clean over a stream.

**Docs when done:** delete this section and the TODO row, and the `(SHR)` note in
[gotchas.md](gotchas.md).

## DSB — Unused `disabled` colour category

`countryschemes.json` (in `Stream-Resources\Colors\Schemes\`, the Stream-Resources repo) has a `disabled`
category, with `none`'s black-and-white scheme and all-white split colours; `gameschemes.json`'s is gone
already. Under Streamer.Bot, `disabled` was the default of `currentGame` while the bot's game features were
off ("Disabling bot stuff for %currentGame%").

The bot uses only `gameschemes`' `none`: as the `None` game's colour and as the split-colour fallback, which
is always read from `gameschemes`. `disabled` can only be reached by typing it (`!changecolor disabled`).
`countryschemes`' own `none` is unused too; its split colours are all white where `gameschemes`' are not,
but a lookup without a set name, which may find either `none`, only takes the scheme, and that is the same.
Read only.

### Plan

Delete `disabled` and `none` from `countryschemes.json`, keeping `gameschemes`' `none`.

**Verify:** F5 logs no new colour-scheme lines, `!changecolor none` still works, and the real-data smoke
tests pass.

**Docs when done:** delete this section and the TODO row.

## DIR — Ask for the BotInput folder at first boot

Wanted by the owner on 2026-10-25: at first boot, prompt for the `BotInput` folder rather than needing
`paths.json` edited by hand. Not built.

`paths.json` holds only `BotInput`; every other directory is in `settings.json` there
([infrastructure.md § Config](infrastructure.md#config)). When `paths.json` is missing, the csproj's
`BeforeBuild` target copies `paths.json.example`, whose `BotInput` is `%APPDATA%\StreamAssistant2\Bot Input`.
`Config.Load` then creates that folder, copies the secrets template into it and stops with a message box.
So a fresh install runs from `%APPDATA%` unless the user edits `paths.json`.

### Decisions

- **What counts as a first boot:** only a missing `paths.json`, or also a `BotInput` that doesn't exist
  or holds no `secrets.json`.
- **The build-time copy:** drop the `BeforeBuild` target, so a missing `paths.json` means a first boot;
  or keep it and ask while `BotInput` is still the example's value.

### Plan

1. In `Main`, before `Config.Load()`, on a first boot: show WPF's `Microsoft.Win32.OpenFolderDialog` (new
   in .NET 8), starting at `%APPDATA%\StreamAssistant2\Bot Input`. Cancelling exits.
2. Write `paths.json` with the chosen folder as its only key, then carry on into `Config.Load()`, which
   creates `secrets.json` there from the template as it does today.
3. Keep the decision (is this a first boot?) in a pure function, and test it.

**Verify:** with `paths.json` moved away, F5 shows the folder picker. Picking a folder writes `paths.json`
and stops at the `secrets.json` message; cancelling exits. Put the file back.

**Docs when done:** delete this section and the TODO row; [infrastructure.md § Config](infrastructure.md#config),
CLAUDE.md's config files, the prerequisites in [development.md](development.md), and
[gotchas.md](gotchas.md#startup-and-environment).

## SED — Edit the settings in the app

Wanted by the owner on 2026-10-25: change the settings from the bot's window rather than by editing
`settings.json`. Not built.

Every setting is in `settings.json` in `BotInput`, created from `settings.json.example` at the first
start, edited by hand and applied with `!reloaddata` ([infrastructure.md § Settings](infrastructure.md#settings)).
`Settings.Parse` already rejects a misspelt key, a `null` or a number out of range, naming the key.

### Decisions

- **Where:** a settings window opened from the dashboard, or a panel in it
  ([dashboard.md](dashboard.md#adding-a-panel)).
- **Saving:** writing the file back with `System.Text.Json` drops its comments and layout. Either keep the
  descriptions in the editor (attributes on `SettingsModel`'s properties) and write the file plain, or
  change only the edited values in the file's text.
- **Applying:** a save reloads at once, as `!reloaddata` does for the settings, or waits for a button.

### Plan

1. Build the form from `SettingsModel` by reflection: a group per section, a field per key. Text for
   names and ids, a folder picker for directories, numbers checked as `Settings.CheckRanges` does.
2. On save, check the result through `Settings.Parse` before writing, so the file is never left broken;
   then write it and reload. Keep a changed `BotOutput`'s "after a restart" rule.
3. Keep the mapping between the model and the form pure, and test it.

**Verify:** change an OBS name and a timing in the editor and save: the log shows `Loaded settings.json,
changed …`, both take effect, and the file holds them. An out-of-range value is refused, with the reason,
before anything is written.

**Docs when done:** delete this section and the TODO row; [dashboard.md](dashboard.md),
[infrastructure.md § Settings](infrastructure.md#settings), CLAUDE.md's dashboard and config-files
paragraphs, and the "Change a setting" recipe in [development.md](development.md).

## TXT — Texts and sound delays as settings

Wanted by the owner on 2026-10-25: look later at making the chat and spoken texts, and the delays
matched to sounds, settings too. The owner's other values are settings in `settings.json`
([infrastructure.md § Settings](infrastructure.md#settings)); these stayed in code. Not built.

What is still in code:
- **Texts:**
  - the FAQ commands (`ChatHandler._commands`: name, regex, answer)
  - the ad warnings and nags (`Ads`), with their 5, 3, 2 and 1-minute schedule
  - the train, flush and retrieve chat lines (`ChannelPoints`)
  - the disk-space alerts (`DiskSpace`'s `*_CHAT` and `SPAM_SPEAK`)
  - the sub, resub, gift and bomb sentences (`SubscriptionMessages`, which also names the community
    "Lots Of Ess's")
  - the tip lines (`Tips`), the raid line, and the startup and shutdown lines
  - the clock, uptime and money-goal OBS text formats
- **Delays matched to sounds:**
  - speech 4.2 s after the tribal hymn (subs, resubs, gifts)
  - the hymn 1 s after the clap (gifts)
  - a bomb's 66 ms between hymns, 3.8 s to the clap and 3 s to the speech
  - speech 5 s after the applause (cheers) and 6 s after the anthem (tips)

### Decisions

- **Where texts live:** `settings.json`, or a file of their own (`texts.json`, `faq.json`), since they
  are content more than settings.
- **Which texts:** all of them, or the FAQ answers first, as the most likely to change.
- **Delays:** settings beside each sound, or taken from the sound file's length (NAudio's
  `AudioFileReader.TotalTime`), so a new file needs no new delay.

### Plan

1. Settle the decisions and list the texts that move.
2. Move them the way the settings work: a model with today's values as defaults, read at each use,
   reloaded by `!reloaddata`, an example file, and a test that the example equals the defaults.
3. Keep the sentence builders pure and tested, with the texts passed in.

**Docs when done:** delete this section and the TODO row; [reference.md](reference.md) (the TXT rows in
§ Constants, and § Settings), [infrastructure.md § Settings](infrastructure.md#settings),
[events.md](events.md), and the FAQ in [twitch.md](twitch.md).

---

## PSL — A previous-split sign per loop panel

Shelved by the owner on 2026-10-13. A game's entry has one `Sign`. The previous split's differences
(`Livesplit: Previous Run`, `Segment` and `Gold` in `_LPC: Splits Previous Values`) read
`PreviousSplit\AllSplits\…` files, fixed in OBS. A game whose loop shows lists at different levels (GTASA's
per-city and per-mission lists) might want the previous split shown, and coloured, at the level of the list
the split interrupted. Only SWAT4 has `Previous` panels today.

### Decisions

- **Scope:** is a `Sign` on a loop panel sticky (in force until another panel names one) or per panel
  (the panel's own, else the entry's)?
- **Files:** do the three differences' files switch level with it? The sign's folder holds
  `RunDifference.txt`, `SegmentDifference.txt` and, for `AllSplits` only, `GoldDifference.txt`. The previous
  split's name is one folder up.

### Plan

1. Add `Sign` to `Panel`. `RunAsync` keeps track of the sign in force for the loop panel up, and
   `PlayPrevious` passes it to `ColorPrevious` instead of the entry's.
2. If the files follow, set them on the three sources through `SetSetting` before the `Previous` panels
   show, and reset them when the rotation starts.

**Docs when done:** delete this section and the TODO row; [obs.md](obs.md#the-panel-file) and
[obs.md § Split colours](obs.md#split-colours).
