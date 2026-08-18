# PokeTokenBar for Windows v0.1.0

## First Windows Release

This is the first native Windows release of PokeTokenBar for Codex users.

## Features

- Local Codex token tracking
- Pokemon companion lifecycle: egg, hatch, evolution, graduation
- Gen 1-5 Pokemon pool from PokeAPI
- Real evolution chains and branching paths
- Rarity classification from capture rate, legendary, and mythical metadata
- 25 Pokemon Natures
- Shiny Pokemon support
- Pokedex and Catch Log
- Bag and Token Shop
- Token wallet economy
- Rare Candy, Mint, Shiny Charm
- Normal, Uncommon, and Rare purchased eggs
- Persistent save/settings
- Windows tray app
- Optional desktop Pokemon companion
- Notifications
- Save import/export

## Performance

Codex usage is parsed incrementally from local JSONL sessions. Unchanged files are skipped on refresh, and changed files resume from cached offsets where safe.

## Privacy

Codex token processing is local. The app does not upload prompts, source code, project names, raw Codex logs, or token records. Network calls are used for Pokemon metadata and sprites from PokeAPI/PokeAPI sprite sources.

## Known Limitations

- Codex is the only supported provider in v0.1.0.
- Pokemon metadata/sprites require network on first uncached use.
- Notifications use tray balloons.
- Desktop Pokemon intentionally behaves like a Windows desktop widget and is covered by normal apps.
- Ditto special reveal behavior is deferred.
- Portable startup registration may need to be toggled off/on after moving the folder.
