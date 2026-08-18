# PokeTokenBar for Windows

PokeTokenBar for Windows turns local Codex token usage into progress for a Pokemon companion. Tokens hatch eggs, grow Pokemon through real evolution lines, unlock shiny companions, fill a Pokedex, and power a small token shop.

This is an unofficial Windows fan project inspired by `chattymin/PokeTokenBar`.

## Main Features

- Local Codex token tracking
- Gen 1-5 Pokemon pool
- Eggs, growth, evolution, graduation
- Shiny Pokemon and 25 Natures
- Species-level Pokedex and individual Catch Log
- Bag and Token Shop
- Rare Candy, Mint, Shiny Charm
- Normal, Uncommon, and Rare purchased eggs
- Optional desktop Pokemon companion
- Windows tray app
- Settings, notifications, save import/export

## Requirements

- Windows 10 or Windows 11, x64
- Codex local session files for usage tracking
- Internet on first uncached Pokemon metadata/sprite use

The portable release is self-contained. No .NET runtime, .NET SDK, Visual Studio, or VS Code installation is required.

## Portable Install

1. Download `PokeTokenBar-Windows-v0.1.0-win-x64.zip`.
2. Extract it to a folder you control.
3. Run `PokeTokenBar.exe`.

If you move the portable folder after enabling Launch with Windows, toggle that setting off and on again so the startup path refreshes.

## Installer

If using the installer build, it installs per-user under:

`%LOCALAPPDATA%\Programs\PokeTokenBar`

The app itself manages Launch with Windows from Settings.

## First Launch Baseline

Historical Codex usage is displayed for analytics, but it is not retroactively awarded as new Pokemon progression. Only newly observed tokens after the first-run baseline progress the current egg/Pokemon and increase spendable shop currency.

## Codex Data

The app reads local Codex session JSONL files under the user's Codex home, normally:

`%USERPROFILE%\.codex\sessions`

The username and drive are resolved dynamically.

## Privacy

- Codex token usage is processed locally.
- Prompts, source code, project names, file paths, and raw Codex logs are not uploaded.
- No telemetry is sent.
- The app contacts PokeAPI and PokeAPI sprite sources for Pokemon metadata and sprites.

## Data Locations

Save/settings:

`%APPDATA%\PokeTokenBar`

Cache/logs:

`%LOCALAPPDATA%\PokeTokenBar`

Settings -> Export Save creates a portable backup package. Import validates the package and backs up the current save first.

## Desktop Pokemon

The optional desktop Pokemon belongs to the Windows desktop layer. It is visible when the desktop is visible, but normal applications such as Chrome, VS Code, or File Explorer should cover it. Press Win+D or minimize windows to see it again.

You can enable it in Settings or from the tray menu, drag it, resize it from its right-click menu, and hide/show it from the tray.

## Uninstall

Uninstalling or deleting the portable folder does not remove game saves by default. To remove all local data manually, delete:

- `%APPDATA%\PokeTokenBar`
- `%LOCALAPPDATA%\PokeTokenBar`

## Known Limitations

- v0.1.0 supports Codex only.
- Pokemon metadata/sprites require network on first uncached use.
- Notifications use Windows tray balloons rather than modern toast notifications.
- Desktop Pokemon uses a reliable non-topmost WPF fallback instead of fragile WorkerW/Progman parenting.
- Ditto special reveal behavior is deferred.

## Attribution

- Original inspiration: `chattymin/PokeTokenBar`
- Pokemon data: [PokeAPI](https://pokeapi.co/)
- Pokemon sprites: PokeAPI sprites repository

PokeTokenBar for Windows is not affiliated with Nintendo, Game Freak, Creatures Inc., The Pokemon Company, OpenAI, or the original PokeTokenBar author. Pokemon names and imagery are trademarks/copyrights of their respective owners and are fetched at runtime from public PokeAPI sources.

## License

MIT. See `LICENSE`.
