# Contributing to Skillbooks

## Setup

1. Own a Vintage Story install (any recent stable release works for building; the game
   version(s) actually tested against are listed in `modinfo.json`).
2. Set the `VINTAGE_STORY` environment variable to that install's directory — it's how the
   project resolves `VintagestoryAPI.dll` and friends at build time.
3. `dotnet build Skillbooks/Skillbooks.csproj -c Release`. Output lands in
   `Skillbooks/bin/Release/Mods/mod` — copy that folder's *contents* (not the folder itself)
   into your `Mods` directory to test it, or zip them for a release.

There's no automated test suite — this is a game mod, most of what matters (does a book
actually grant the trait, does the trader hook fire, does discovery pick up a third-party
mod's traits) only really shows itself against a live server with real mods loaded. If your
change touches gameplay behavior rather than pure refactoring, test it against a real instance
before opening a PR, and say what you tested in the PR description.

## Code style

- No comments unless the *why* is genuinely non-obvious — a hidden engine constraint, a
  workaround for a specific decompiled behavior, something that would surprise a reader.
  Well-named methods and variables should carry the *what* on their own.
- Don't guess at engine behavior. If a PR's reasoning depends on how some Vintage Story
  internal actually works (an event's call site, whether a field is public, what a method
  mutates), verify it against the decompiled source rather than assuming — the codebase has
  been burned by wrong assumptions about the engine before (see `SkillBookFlavour.cs` and
  `ItemSkillBook.cs` for examples of decompile-confirmed behavior called out in comments).
  [ilspycmd](https://github.com/icsharpcode/ILSpy) against the game's own DLLs is the tool
  used throughout this project.
- Don't add abstractions, config options, or defensive error handling for scenarios that can't
  actually happen. Three similar lines beat a premature abstraction.

## Mod-supplied flavour overrides

`SkillBookFlavour`'s tier 1 (`assets/<moddomain>/config/skillbooks/<traitcode>.json`) is
public surface other mod authors rely on directly, documented in the [README](README.md#for-mod-authors-supplying-your-own-flavour-text)
-- not just an internal implementation detail. Changing its JSON shape or lookup path is a
breaking change under the versioning policy below, not a routine refactor.

The `config/` prefix is load-bearing, not decoration: `AssetManager.InitAndLoadBaseAssets`
(confirmed via decompile) only scans the fixed set of `AssetCategory` folder names per domain
-- `blocktypes`, `config`, `lang`, and so on. A path outside that set is never indexed at all,
so `IAssetManager.TryGet` silently finds nothing no matter how correctly the file is placed.
This bit us for real: the original tier 1 path omitted `config/` and had never actually been
exercised end-to-end, so the whole mechanism was dead on arrival until this got caught.

## Versioning

Strict [semver](https://semver.org/) (`MAJOR.MINOR.PATCH`), currently in the `0.x` range —
no compatibility guarantees yet on the public API surface (trait registry, flavour resolver,
book item base class) that addon mods like Stats build against.

- **Patch**: bug fixes, no behavior or API changes.
- **Minor**: additive features, new config options with safe defaults, new curated flavour
  entries — anything that doesn't break an existing install or an addon compiled against the
  previous version.
- **Major** (once past `0.x`): breaking changes to the public API surface or to config
  schema/semantics.

If your change touches the public surface Stats (or a future addon) compiles against, flag
that explicitly in the PR — it affects what version bump the release needs, and whether the
`Skillbooks.Core` NuGet package needs republishing before the addon repos can pick it up.

## Pull requests

Fork, branch, make your change, open a PR against `main` with a description of what changed
and why, and what you tested it against. Keep PRs scoped to one logical change — easier to
review, easier to revert if something's wrong with just that piece.
