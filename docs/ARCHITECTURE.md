# Architecture

This document proposes the Windows architecture after Phase 0 investigation. It is intentionally implementation-facing but does not create application code yet.

## Environment Findings

Detected on this machine:

- OS: Windows 10, version `10.0.19045`, x64.
- `dotnet` location: `C:\Program Files\dotnet\dotnet.exe`.
- Installed runtimes: .NET 6.0.5 and .NET 8.0.4, including `Microsoft.WindowsDesktop.App`.
- Installed SDKs: none detected by `dotnet --info`.

Immediate consequence: Phase 1 cannot scaffold, build, or test a C#/WPF solution until a .NET SDK is installed or made available on PATH. The appropriate target is .NET 8 unless a newer stable SDK is installed before Phase 1 begins.

## Proposed Repository Layout

```text
PokeTokenBar.Windows/
+-- src/
|   +-- PokeTokenBar/
|       +-- App.xaml
|       +-- App.xaml.cs
|       +-- Core/
|       |   +-- Game/
|       |   +-- Interfaces/
|       |   +-- Models/
|       +-- Providers/
|       |   +-- Codex/
|       +-- Services/
|       |   +-- Logging/
|       |   +-- Notifications/
|       |   +-- PokeApi/
|       |   +-- Sprites/
|       |   +-- Startup/
|       |   +-- Storage/
|       +-- Tray/
|       +-- ViewModels/
|       +-- Views/
|       +-- Resources/
+-- tests/
|   +-- PokeTokenBar.Tests/
+-- docs/
+-- README.md
+-- .gitignore
```

## Target Framework

Preferred target after SDK is available:

```xml
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
<UseWPF>true</UseWPF>
<Nullable>enable</Nullable>
<PlatformTarget>x64</PlatformTarget>
```

Windows 10 build `19041` is a conservative API floor for Windows 10/11 desktop work. The app should build x64 and can later be published self-contained.

## Application Composition

Use WPF with MVVM where it adds clarity:

- `App.xaml.cs` handles startup, single-instance guard, service creation, and graceful shutdown.
- `MainWindow` behaves as a compact tray popup, not a normal long-lived document window.
- ViewModels expose immutable or observable DTOs for Home, Pokedex, Catch Log, Bag, Shop, and Settings.
- Services handle external concerns: Codex files, PokéAPI, sprites, storage, notifications, startup registration, and logging.

A heavyweight dependency injection framework is not necessary initially. A small composition root with explicit constructors should be enough. Add `Microsoft.Extensions.DependencyInjection` only if constructor wiring becomes noisy.

## Core Domain

Core domain code should not depend on WPF.

Suggested model areas:

- `Rarity`
- `PokemonNature`
- `PokemonSpecies`
- `EvolutionNode`
- `EvolutionLine`
- `CompanionState`
- `ActiveCompanion`
- `PokedexSpeciesEntry`
- `CatchLogEntry`
- `Inventory`
- `TokenWallet`
- `GameBalance`
- `ProgressionEngine`
- `EggSelectionService`
- `ShopService`

Balance constants belong in one place, likely `GameBalance`.

The progression engine should be deterministic and unit-testable:

- Apply token delta.
- Incubate egg.
- Hatch with overflow.
- Apply current-stage progression.
- Evolve with overflow.
- Graduate and archive.
- Apply Rare Candy via the same progression path as real token XP.

## Codex Provider

Provider responsibilities:

- Resolve `%USERPROFILE%\.codex\sessions`.
- Recursively discover `*.jsonl` and especially `rollout-*.jsonl`.
- Open active files with `FileShare.ReadWrite`.
- Parse line-by-line using `System.Text.Json`.
- Ignore malformed or incomplete lines.
- Never log prompt text, message text, cwd, project names, or full local paths.
- Derive today, last 5 hours, week, month, and observed lifetime.
- Produce stable event IDs to avoid duplicate counting.
- Cache file offsets, size, mtime, session metadata, and usage fingerprints.

Local observed Codex layout:

```text
%USERPROFILE%\.codex\sessions\YYYY\MM\DD\rollout-*.jsonl
```

Local volume observed during Phase 0:

- Session root exists.
- 9 JSONL files.
- About 168 MB total.
- Date folders observed: April 2026 and August 18, 2026.
- Current session file was actively locked by Codex, confirming the need for shared reads and graceful failure.

Current local event types observed:

- `session_meta`
- `turn_context`
- `response_item`
- `event_msg`
- `compacted`

Current local token-count schema:

```json
{
  "timestamp": "2026-08-18T11:41:14.200Z",
  "type": "event_msg",
  "payload": {
    "type": "token_count",
    "info": {
      "model_context_window": 272000,
      "total_token_usage": {
        "input_tokens": 33489,
        "cached_input_tokens": 0,
        "output_tokens": 263,
        "reasoning_output_tokens": 0,
        "total_tokens": 33752
      },
      "last_token_usage": {
        "input_tokens": 33489,
        "cached_input_tokens": 0,
        "output_tokens": 263,
        "reasoning_output_tokens": 0,
        "total_tokens": 33752
      }
    }
  }
}
```

Some `token_count` records may have empty or missing `info` initially. The parser should ignore those for usage totals.

Deduplication should follow upstream's model:

- Prefer `last_token_usage` as the owned event delta.
- Keep `total_token_usage` as cumulative state for fingerprinting.
- Use session ID from `session_meta.payload.id`.
- Detect cumulative decreases as an epoch boundary.
- Resolve forks/replayed parent history using parent/session metadata when present.
- Avoid double-counting duplicate cumulative states within a session.

## Storage

Use JSON files. SQLite is not justified for the initial state size.

Proposed locations:

- Save/config: `%APPDATA%\PokeTokenBar\`
- Cache: `%LOCALAPPDATA%\PokeTokenBar\Cache\`
- Logs: `%LOCALAPPDATA%\PokeTokenBar\Logs\`

Suggested files:

- `state.json`
- `settings.json`
- `codex-usage-index.json`
- `pokeapi-cache\*.json`
- `sprites\*.png` / `*.gif`
- `logs\poke-token-bar.log`

State writes:

- Serialize to a temp file.
- Flush and replace original atomically where possible.
- Keep a `state.previous.json` backup.
- If load fails, move corrupt file to a timestamped `.corrupt.json` and start with a fresh state while preserving the backup.

## PokéAPI and Sprite Services

Use `HttpClient` with cancellation tokens.

PokéAPI cache responsibilities:

- Species metadata.
- Evolution chains.
- Generation 1-5 eligible base species index.
- Rarity metadata.
- Localized names, starting with English and leaving room for future localization.

Sprite service responsibilities:

- Static normal and shiny sprites.
- Gen V animated GIFs where practical.
- Egg sprite.
- Item sprites where available.
- Memory cache with small LRU.
- Disk cache before network.
- Placeholder when unavailable.

Do not bundle Pokemon assets in the binary.

## System Tray

Initial approach:

- Use `System.Windows.Forms.NotifyIcon` from the WPF app.
- Left click toggles the popup.
- Right click shows Open, Refresh, Settings, Exit.
- Tooltip includes current Pokemon display name and today's tokens.
- Closing the popup hides it; it does not terminate the app.

Tray icon:

- Phase 1 can use a simple embedded app icon.
- Later phases can generate an `.ico` from the current cached static sprite.
- Avoid animated tray icons unless CPU cost and reliability are proven acceptable.

## Notifications

Use Windows toast notifications if practical without heavy dependencies. Fallback to tray balloon tips if toast registration is not ready.

Events:

- Egg hatch.
- Shiny hatch.
- Evolution.
- Graduation.
- Important local errors.

Do not notify ordinary refreshes.

## Settings

Settings should be backed by JSON and exposed through WPF controls:

- Launch with Windows.
- Refresh interval.
- Show token text in tray where possible.
- Notifications.
- Data/cache locations.
- Reset cache.
- Export save.
- Import save.
- About/version.

Startup registration should use the current user's registry Run key or startup shortcut and should not require administrator privileges.

## Privacy and Logging

No telemetry.

Never log:

- Prompt text.
- Source code.
- Project names.
- Local cwd/project paths.
- Raw Codex lines.
- Full token logs.

Logging should include sanitized diagnostics only:

- File read failed for a relative or hashed file identifier.
- Malformed line count.
- API/cache failures.
- Save/load failures.
- Unexpected state transition.

## Testing Strategy

Unit tests should cover:

- Game balance thresholds.
- Progression, overflow, hatch, evolution, graduation.
- Economy spending and item effects.
- Mint reroll excludes current nature.
- Shiny Charm cannot stack.
- Egg purchase resets active state correctly.
- Codex parser fixtures with realistic nested `event_msg.payload.type == "token_count"` lines.
- Duplicate token-count states.
- Malformed and partial JSONL.
- Active file shared-read behavior where feasible.
- Local timezone boundaries for day/week/month.

Use xUnit unless the repo later has a reason to choose MSTest or NUnit.

## Known Phase 0 Blockers

The .NET SDK is not installed. Phase 1 requires installing .NET 8 SDK or a newer stable SDK.
