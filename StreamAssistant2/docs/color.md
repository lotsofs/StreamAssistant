# Colour

How a viewer's colour request becomes a recoloured OBS layout: the code in
[code/Color/](../code/Color/), the data files it reads, the rules it applies, and how to test it.
**Read this before changing anything in `code/Color/`.** Parts of it, the triple-parser rule
especially, can't be worked out from the code alone.

## What it does

Three channel-point rewards in [ChannelPoints.cs](../code/Twitch/ChannelPoints.cs) call
[LayoutColoring.cs](../LayoutColoring.cs) (at the project root):

| Reward | Calls | Input |
|---|---|---|
| `REWARD_ID_COLOR_SINGLE` | `LayoutColoring.TryChangeToSingle` | one colour, via `SingleColorParser` |
| `REWARD_ID_COLOR_TRIPLE` | `LayoutColoring.TryChangeToTriple` | up to three colours, via `TripleColorParser` |
| `REWARD_ID_COLOR_RANDOM` | `LayoutColoring.ChangeToRandom` | none; a random table colour |

Every path ends in a `ColorEntry`: a source, a display name, and three hex colours for the layout's
inner, outer and text parts (`Hex1`/`Hex2`/`Hex3`). `LayoutColoring` posts the
`Changing to color …` chat line, converts the hex with `ColorUtil.ToOBS`, and queues the multi-second
OBS animation. Requests are queued, not run directly, so they never overlap.

## Files

| File | Holds |
|---|---|
| [Coloring.cs](../code/Color/Coloring.cs) | The `ColorEntry` record, `Load()` (both registries), `GetRandomColor()` |
| [SingleColorParser.cs](../code/Color/SingleColorParser.cs) | One colour from text: hex, `rgb()`, `hsv()`, bare `r,g,b`, named, system, `random` |
| [TripleColorParser.cs](../code/Color/TripleColorParser.cs) | Splits text into up to three colours by the separator-weight rule |
| [ColorTableRegistry.cs](../code/Color/ColorTableRegistry.cs) | Loads every colour table and looks colour names up across them |
| [ColorTable.cs](../code/Color/ColorTable.cs) | One table (`NamedColor` entries) plus its loose fallback, `TryGetLoose` |
| [ColorSchemeRegistry.cs](../code/Color/ColorSchemeRegistry.cs) | Loads scheme files (set → category → scheme) and resolves them |
| [ColorNameComparer.cs](../code/Color/ColorNameComparer.cs) | The name comparer (`Instance`) and `Fold`, used by the loose fallback |
| [ColorUtil.cs](../code/Color/ColorUtil.cs) | Hex maths: `TryNormalizeHex`, `Darken`, `Lighten`, `ToHex`, `ToOBS`, `HsvToRgb` |

## Data files

Both directories are configured in `secrets.json` (`Directories.Colors`,
`Directories.ColorSchemes`). Each `*.json` file in them is loaded, and its file name becomes its
name.

**Colour tables** map names to hex. The file name (`crayola`, `encycolorpedia`, …) is the table's
*source*:

```json
{ "Navy Blue": "#0066CC", "Green-Blue": "#2887C8" }
```

**Colour schemes** give a whole inner/outer/text set. The file name is the *set*:

```json
{
  "Categories": {
    "Albania": {
      "ColorSchemes": { "AL": { "Inner": "#000000", "Outer": "#FF0000", "Text": "#FF0000" } },
      "Default": "AL"
    }
  }
}
```

A scheme is reachable as `set category scheme`, as `category scheme`, or as `category` alone, which
falls back to the category's `Default`. Each category also carries a `SplitColors` map, which is
loaded but not used.

Rules for the data:
- **Hex** can be in any case, with or without `#`; it is stored as lowercase `#rrggbb`. A value that
  isn't six hex digits is skipped, and a scheme with any bad channel is dropped whole.
- **Table, set, category and scheme names** are matched strictly (see below), so keep them ASCII.
- **Category names must be one word.** The scheme lookup splits the input on spaces and takes one
  word as the category, so a category named `Dup Cat` can never be reached. This is why the data
  uses `AntiguaAndBarbuda`.
- **Duplicate names.** Two names that are equal under the strict comparer (`Green-Blue` and
  `Green Blue`), whether in one table, among a set's categories or within a category, keep the
  first and drop the later one.

## Loading

`Coloring.Load()` runs from `Program.cs` after `ConsoleLogger.Start()` and its one-second wait for
the viewer. It has to: loading logs on `Important` whenever it drops something, and logging before
the viewer exists hits the NRE bug in `ConsoleLogger`. The possible lines:

```
Colour table <source>: skipped "<name>", "<value>" is not a hex colour
Colour table <source>: skipped "<name>", same name as "<first>"
Colour scheme <set>: skipped "<category>", same name as "<first>"
Colour scheme <set> <category>: skipped "<scheme>", same name as "<first>"
Colour scheme <set> <category>: skipped "<scheme>", not all of "<inner>" "<outer>" "<text>" are hex colours
```

Scheme files are deduplicated as raw JSON (`ColorSchemeRegistry.DropDuplicateNames`) before they
are deserialised. Otherwise Newtonsoft's dictionary indexer would keep the first spelling with the
last entry's colours.

## Resolving one colour

[SingleColorParser.TryParse](../code/Color/SingleColorParser.cs) tries, in order:
1. `#RRGGBB`
2. `rgb(...)`
3. `hsv(...)`
4. bare `r,g,b`
5. named lookups (below)
6. `System.Drawing` known colours. In practice this only adds the WinForms palette (`Control`,
   `Highlight` and friends), since the JSON tables already cover every web name.
7. the `random` keyword

`rgb()`, `hsv()` and bare `r,g,b` accept **either commas or spaces** between components. That's
what lets the triple parser join a span with spaces and still match a literal a viewer wrote with
commas. Out-of-range `rgb` components are clamped.

A single plain colour becomes a triple via `Darken`/`Lighten`; a scheme brings its own three.

## Named lookup and loose matching

Every name dictionary is keyed by [ColorNameComparer.Instance](../code/Color/ColorNameComparer.cs),
which ignores case, spacing and punctuation, but treats accents as significant. That's the
**strict** match.

Colour names *inside a table* also have a **loose** fallback, `ColorTable.TryGetLoose`. The
`ColorTable` constructor builds it from its finished entries: a dictionary keyed by `Fold(name)`
for accented names, plus a list of wildcard regexes for names with letters that don't fold.

- **Accents fold** to their base letter via `ColorNameComparer.Fold` (`é`=`e`, `ō`=`o`), in both
  directions: `café` finds `Cafe`, and `cafe` finds `Café`.
- **Any other non-ASCII letter in a stored name** (`ŧ ł ı ð þ æ œ ß ø`, …) is a wildcard for
  **1 or 2** input letters. So `lodz` finds `Łódź`, `strasse` finds `Straße` and `dhiet` finds
  `Ðiết`. It is deliberately forgiving: `kurmuzu` also finds `Kırmızı`.
- **Only names containing non-ASCII get a loose entry or wildcard**, so for most tables both are
  empty. A name with no ASCII letter at all (e.g. all Cyrillic) gets neither, so it is reachable
  only by exact spelling and can't match everything.
- When several fallbacks could match, the order is: the folded input in the strict `Entries`, then
  the accent dictionary, then the wildcards in load order.

`SingleColorParser.TryGetNamed` tries schemes (strict), then table colours strictly in every table,
and only then table colours loosely (the `loose` flag on `TryGetTableColor`). So an exact spelling
anywhere beats a loose match anywhere, and `Café` and `Cafe` in one table both stay reachable by
their own spelling. In the `table colour` prefix form (`crayola navy blue`) the table name matches
strictly and the colour name may match loosely.

`Fold` uses `string.Normalize`. Under `InvariantGlobalization` that silently does nothing, which
would just push accented letters onto the wildcard path.

## The triple-parser rule

[TripleColorParser.TryParse](../code/Color/TripleColorParser.cs) partitions a chat string into up
to three colours, and the rule it uses is **not guessable from the code**. Read this before
touching it.

Separators are all soft: a colour name may span any of them. But they bind with different
strength: space and tab cost **0**, a comma **1**, a semicolon **2**. The parser walks left to
right. At each position it orders the candidate spans by **mean separator weight ascending**, that
is the weight sum divided by the number of gaps in the span, with a single token counting as 0. It
breaks ties by **longest span first**, takes the first span that resolves, and resumes after it.
Input is capped at 32 tokens.

Averaging rather than summing is load-bearing, so don't "simplify" it. It's what lets a longer
name beat its own shorter prefix (`Zed,Zed,Zed,Zed` over `Zed,Zed,Zed`: both mean 1.0, so length
decides), while still losing to a more tightly bound alternative (`crayola navy blue` at mean 0 over
`crayola,navy` at 1.0). Consequences worth knowing:

- `navy blue` is **one** colour (crayola `Navy Blue`, mean 0); `navy,blue` is **two** (`navy`
  alone has mean 0, the pair 1.0).
- `crayola,navy,blue` is **one** again. Neither `crayola` nor `crayola navy` names a colour, so
  the scan is forced across both commas. This matters because crayola holds `Navy Blue` but
  neither `Navy` nor `Blue`, so splitting commas unconditionally would quietly serve those names
  out of `basiccolors` instead.
- All-space input is **deliberately ambiguous**. `red green blue` yields `{red}` `{green blue}`,
  because crayola has `Green-Blue` and every span has mean 0, so the longest wins the tie.
  `red, green, blue` is the precise way to mean three colours. This is intended, not a bug to fix.
- When fewer than three colours are found, a missing slot is padded from the **first entry's own
  channel for that slot** (`Hex2` for outer, `Hex3` for text), never from its base colour. That
  keeps a scheme's designed Outer/Text intact (`Albania` through the triple reward renders the
  actual flag) and gives a plain colour its `Darken`/`Lighten` pair.

## Hex is always lowercase

Every hex the colour code produces is lowercase `#rrggbb`. Table and scheme values go through
`ColorUtil.TryNormalizeHex` at load; `Darken`, `Lighten` and `ToHex` format with `x2`; and the
literal-hex input path lowercases both the colour and its displayed name. Keep new code to that.
`ToOBS` converts hex to the ABGR long that OBS colour filters expect.

## Tests and verification

`dotnet test ../StreamAssistant2.Tests/StreamAssistant2.Tests.csproj` covers this folder (xUnit).

- **Fixtures:** the tests run against small JSON fixtures in `StreamAssistant2.Tests/Fixtures/`,
  loaded into the static registries by `ColorDataFixture`.
- **No parallelism:** because the registries are static, test parallelism is switched off, and a
  test that loads other data must reload the fixtures in a `finally`.
- **Real-data smoke test:** `RealDataSmokeTest` also loads the real `Stream-Resources` colour data
  and checks every entry survived loading. It is skipped on machines without that folder.
- **Open bugs:** each has a test for the correct behaviour, marked `Skip = "<code>: …"`. Fixing the
  bug means deleting the `Skip`.
- **Keep fixtures valid and collision-free.** An invalid or colliding entry makes the loader call
  `ConsoleLogger`, which writes to the real log directory.
- **Internals:** the main csproj has `InternalsVisibleTo StreamAssistant2.Tests`, so tests can set
  `Config.Data.Directories` and call `ColorSchemeRegistry.DropDuplicateNames`.

**The logging paths** aren't unit-tested. To check them for real, write a scratch program that
references the built `StreamAssistant2.dll` and mirrors `Program.cs`:
1. Copy the published viewer (`bin/Debug/net8.0/logger/`) next to the scratch program.
2. Call `ConsoleLogger.Start()`, then wait a second.
3. Point `Config.Data.Directories` at deliberately broken data and call `Coloring.Load()`.
4. Send `"SHUTDOWN!"` so the viewer closes.

Writing to the real log is fine. Calling `ColoredLine` without `Start()` hits the NRE bug instead,
and fills the log with exception traces.

For behaviour neither covers, a before/after diff still works: a scratch program that loads the
colour data and prints parser results for a fixed list of inputs, run on the build before and after
the change. Never go through `LayoutColoring` from a scratch program, because it posts to chat and
drives OBS.

## Open issues

Tracked in [robustness-fixes.md](robustness-fixes.md):
[RAN](robustness-fixes.md#ran--random-is-case-sensitive) (`Random` is not recognised),
[CAN](robustness-fixes.md#can--system-colours-report-the-raw-input-as-their-name) (system colours
echo the raw input), [RND](robustness-fixes.md#rnd--getrandomcolor-hard-codes-one-table-name)
(`GetRandomColor` hard-codes `encycolorpedia`),
[CMD](robustness-fixes.md#cmd--port-the-colour-chat-commands) (the `!changecolor` chat commands
are stubs) and [VFY](robustness-fixes.md#vfy--finish-verifying-the-colour-paths-in-the-app) (the
paths still need checking through real redemptions).
