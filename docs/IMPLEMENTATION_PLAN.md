# Implementation Plan

This plan follows the requested phased process. Phase 0 is complete when this file, `REFERENCE_NOTES.md`, and `ARCHITECTURE.md` exist and the investigation is summarized.

## Phase 0 - Investigation

Status: complete.

Completed:

- Inspected the requested upstream files from `chattymin/PokeTokenBar`.
- Inspected local Codex session directory structure.
- Inspected Windows/.NET environment.
- Documented upstream mechanics, Windows architecture, and implementation phases.

Findings:

- Upstream mechanics are rich enough to use as the domain reference.
- Windows UI and platform integrations must be reimplemented.
- Local Codex files use nested `event_msg.payload.type == "token_count"` records with `payload.info.last_token_usage` and `payload.info.total_token_usage`.
- The local Codex session root exists and contains active files; one active JSONL was locked by the Codex process under normal read mode.
- No .NET SDK is installed, only runtimes.

## Phase 1 - Skeleton

Goal: create a buildable WPF app and test project with no full gameplay implementation.

Prerequisite:

- Install or expose a .NET SDK, preferably .NET 8 SDK or newer stable SDK.

Tasks:

1. Create solution and projects:
   - `src/PokeTokenBar/PokeTokenBar.csproj`
   - `tests/PokeTokenBar.Tests/PokeTokenBar.Tests.csproj`
   - `PokeTokenBar.Windows.sln`

2. Configure WPF app:
   - Target `net8.0-windows10.0.19041.0` if using .NET 8 SDK.
   - Enable nullable reference types.
   - Set x64 target.
   - Add `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`.

3. Add initial folder structure:
   - `Core/Game`
   - `Core/Models`
   - `Core/Interfaces`
   - `Providers/Codex`
   - `Services/Logging`
   - `Services/Storage`
   - `Services/PokeApi`
   - `Services/Sprites`
   - `Services/Notifications`
   - `Services/Startup`
   - `Tray`
   - `ViewModels`
   - `Views`
   - `Resources`

4. Add lightweight app services:
   - Composition root.
   - Local path resolver.
   - JSON storage abstraction.
   - Sanitized file logger.
   - Clock abstraction for tests.

5. Add tray skeleton:
   - `NotifyIcon` with static embedded icon.
   - Left click toggles popup.
   - Right click menu: Open, Refresh, Settings, Exit.
   - Closing window hides instead of exits.

6. Add basic popup:
   - Compact window sizing.
   - Placeholder Home tab.
   - Placeholder Today/Week/Month values.
   - Placeholder companion sprite area.
   - Placeholder bottom navigation.

7. Add initial tests:
   - Smoke test for storage path resolver.
   - Smoke test for JSON save/load abstraction using temp directory.

8. Verify:
   - `dotnet build`
   - `dotnet test`
   - Manual launch if a desktop session is available.

Exit criteria:

- App builds.
- Tests run.
- Tray icon appears.
- Popup opens/closes.
- Window close does not terminate the app.

## Phase 2 - Codex Usage

Goal: parse Codex usage credibly before connecting gameplay.

Tasks:

1. Implement `CodexUsageProvider`.
2. Implement line parser for current nested schema:
   - Top-level `type == "event_msg"`.
   - `payload.type == "token_count"`.
   - `payload.info.last_token_usage`.
   - `payload.info.total_token_usage`.
3. Support compatibility for any top-level `token_count` shape if encountered later.
4. Implement safe shared reads:
   - `FileShare.ReadWrite`.
   - Malformed/partial line tolerance.
   - Locked file fallback.
5. Implement deduplication:
   - Session ID from `session_meta.payload.id`.
   - Fingerprint from cumulative usage vector.
   - Epoch increments when cumulative usage decreases.
   - Stable event IDs.
6. Implement period aggregations:
   - Today using local timezone.
   - Last 5 hours from event timestamps.
   - Current week.
   - Current month.
   - Observed lifetime.
7. Implement incremental index:
   - File size.
   - Last write time.
   - Last processed offset.
   - Session ID knowledge.
   - Known event IDs/fingerprints.
8. Add fixtures:
   - Normal session.
   - Multiple sessions.
   - Duplicate cumulative state.
   - Malformed line.
   - Partially-written line.
   - Events spanning midnight.
   - Week and month boundaries.
   - Fork/replay scenario if enough upstream behavior is reproduced in fixtures.
9. Compare against local sessions with sanitized counts only.

Exit criteria:

- Tests pass.
- Today/week/month values look plausible against local files.
- No prompt text or project paths are logged or displayed.

## Phase 3 - Pokemon Domain Model

Goal: implement deterministic game logic independent of UI and network.

Tasks:

1. Add balance constants.
2. Add rarity classification.
3. Add 25 natures.
4. Add evolution tree/path models.
5. Add companion state.
6. Add progression engine.
7. Add graduation archiving.
8. Add unit tests for:
   - Common/Uncommon/Rare/Legendary thresholds.
   - One-, two-, and three-stage lines.
   - Overflow into next stage.
   - Multiple evolutions from large token jumps.
   - Graduation.
   - Rare Candy.
   - Egg hatch threshold.

Exit criteria:

- Domain tests pass without network or WPF.

## Phase 4 - PokéAPI

Goal: fetch and cache Pokemon metadata.

Tasks:

1. Implement PokéAPI client with cancellation.
2. Cache species metadata.
3. Cache evolution chains.
4. Build Gen 1-5 starting species index.
5. Apply rarity classification.
6. Implement weighted egg selection.
7. Implement offline behavior from cache.
8. Verify known species:
   - Bulbasaur three-stage common.
   - Eevee branching.
   - Lapras one-stage.
   - Articuno legendary.
   - Ditto special handling decision.

Exit criteria:

- Cached data supports hatching and evolution after network is unavailable.

## Phase 5 - Gameplay Integration

Goal: connect token usage to companion state.

Tasks:

1. Apply install baseline.
2. Feed post-baseline token deltas into egg/active progression.
3. Hatch with overflow.
4. Evolve with overflow.
5. Graduate and archive.
6. Persist after every state mutation.
7. Show basic Home UI with real values.

Exit criteria:

- Local Codex token increases move the companion correctly.
- Restart preserves state.

## Phase 6 - Collection

Goal: implement Pokedex and Catch Log.

Tasks:

1. Define species-level Pokedex entries.
2. Define individual catch log entries.
3. Record species encountered during active/evolution lifecycle.
4. Record individual hatch/evolution/graduation history.
5. Add search, rarity filter, and generation filter.
6. Display undiscovered species dimmed or hidden according to final UX choice.

Exit criteria:

- Pokedex and Catch Log are distinct and persisted.

## Phase 7 - Economy

Goal: implement wallet, bag, and shop.

Tasks:

1. Implement `TokenWallet`.
2. Implement item inventory.
3. Implement Rare Candy.
4. Implement Mint.
5. Implement Shiny Charm.
6. Implement Normal, Uncommon, and Rare eggs.
7. Add shop UI with confirmations.
8. Add bag UI with disabled states.
9. Add economy tests.

Exit criteria:

- Purchases affect `spentTokens` and inventory only.
- Growth is not reduced by spending.
- Items persist immediately.

## Phase 8 - Polish

Goal: make the app feel like a proper tray companion.

Tasks:

1. Improve visual design.
2. Add notifications.
3. Add launch at Windows startup.
4. Add settings.
5. Add empty and error states.
6. Add save import/export.
7. Add cache reset with confirmation.
8. Add single-instance guard.
9. Improve tray icon using current static sprite if stable.

Exit criteria:

- App handles normal daily use without feeling like a prototype.

## Phase 9 - Packaging

Goal: create a distributable Windows build.

Tasks:

1. Create publish command.
2. Decide self-contained vs framework-dependent release.
3. Add release README instructions.
4. Consider simple installer after core app is stable.

Likely publish command:

```powershell
dotnet publish src/PokeTokenBar/PokeTokenBar.csproj -c Release -r win-x64 --self-contained true
```

Exit criteria:

- A Windows 10/11 x64 user can run the app from the release output.

## Phase 1 Task List Proposed Next

Exact next tasks after Phase 0:

1. Install or expose a .NET SDK.
2. Create the solution, WPF project, and xUnit test project.
3. Add folder structure and basic project settings.
4. Add local path, logger, clock, and JSON storage abstractions.
5. Add tray icon skeleton and compact popup.
6. Add placeholder Home view and navigation shell.
7. Add storage smoke tests.
8. Run `dotnet build` and `dotnet test`.
