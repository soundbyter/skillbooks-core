# Skillbooks

A [Vintage Story](https://www.vintagestory.at/) mod: lore-styled, single-use books that
permanently grant a crafting trait when read.

Unlike hand-curated trait-unlock mods, Skillbooks discovers crafting traits dynamically at
server start by scanning loaded recipes for `RequiresTrait`, then generates one book per trait
automatically — including traits added by other class mods. Install a new class mod, restart
the server, and its traits already have books waiting to be found. No maintenance burden when
the mod list changes.

Each book reads as an in-world artifact — a survival guide, a journeyman's notebook, a guild
pamphlet — rather than a mechanical "+1 Bowyer" item. Title and flavour text come from a
curated lookup keyed by trait code, with a sensible procedural fallback for any trait nobody's
written flavour for yet.

## Features

- **Dynamic discovery.** No per-trait configuration needed; new traits from other mods pick up
  books automatically.
- **Curated flavour text.** Hand-written title and description for every vanilla and
  supported-mod trait, with a graceful procedural fallback for anything uncurated.
- **Multiple acquisition paths.** Craft-gated, found in vessel loot, or offered by traders — all
  independently configurable (chance, target blocks, trader types, price).
- **Salvage & reroll.** Turn an unwanted book back into materials, or reroll an illegible book
  into a fresh random trait.
- **Illegible books.** Orphaned traits (from a mod that's since been removed) and
  administratively disabled traits degrade gracefully into a distinct "illegible" item instead
  of silently vanishing or crashing.

## Addon family

Skillbooks is the first of a small family of independently-installable mods:

| Mod | Role |
|---|---|
| **Skillbooks** (this repo) | Crafting-trait books |
| [**Skillbooks: Stats**](https://github.com/soundbyter/skillbooks-stats) | Stat-trait books (Fleetfooted, Hardy, etc.) — works standalone, reuses this mod's flavour resolver when both are installed |

## Installation

Grab the latest release from the [Releases page](https://github.com/soundbyter/skillbooks-core/releases)
and drop the zip into your Vintage Story `Mods` folder (or extract it there).

## Configuration

A config file is generated on first run at `ModConfig/skillbooks.json`. See the file itself for
the full set of options — loot/trader spawn chances, target block patterns, trader offer price,
and which traits are enabled.

## For mod authors: supplying your own flavour text

If your mod adds a trait, you don't have to wait for it to be added to Skillbooks' curated
list — ship a file at `assets/<yourmoddomain>/skillbooks/<traitcode>.json` in your own mod:

```json
{
  "title": "Your Book's Title",
  "blurb": "The in-world description shown when the book is read or inspected."
}
```

This takes priority over Skillbooks' own curated list and its procedural fallback. Either
field can be omitted and falls back independently to whatever the next tier provides.

## Building from source

Requires the .NET SDK matching Vintage Story's target framework, and a local Vintage Story
install (for its API DLLs). Set the `VINTAGE_STORY` environment variable to your install path,
then:

```
dotnet build Skillbooks/Skillbooks.csproj -c Release
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for more, including how Stats (or any future addon)
links against this repo's public API surface.

## License

[GPL-3.0](LICENSE).
