# Gameplay Integration

Phase 5 connects new Codex token usage to the Pokemon companion lifecycle. It does not implement Pokédex, Catch Log, wallet, shop, or Bag behavior.

## Orchestration Layer

`CompanionGameService` is the application-level owner for live gameplay. It coordinates:

- `ICodexUsageProvider`
- `IAsyncHatchService`
- `GameStateStore`
- `ProgressionEngine`
- `IClock`
- local logging

The pure `Core/Game` domain remains synchronous and deterministic. It does not know about Codex, HTTP, PokéAPI, WPF, AppData, or JSON storage.

## Async Hatch Bridge

`ProgressionEngine` remains synchronous. When an egg will cross the hatch threshold, `CompanionGameService` first awaits `IAsyncHatchService.HatchAsync`. If that succeeds, it passes the resolved `HatchResult` into the synchronous engine through `ResolvedHatchService`.

No `.Result`, `.Wait()`, or sync-over-async is used.

If hatching cannot be prepared because PokéAPI/cache data is unavailable, the game stores egg progress above the threshold and retries on a later refresh. Once hatch data is available, the service replays the stored egg progress through the engine so overflow carries into the newly hatched Pokemon.

## First-Run Baseline

The first successful Codex snapshot establishes the install baseline:

```text
InstallBaselineSet = true
ClaimedLocalDate = current local date
ClaimedTodayTokens = snapshot.Today.TotalTokens
UsedSinceInstall unchanged
Applied gameplay delta = 0
```

Historical tokens do not hatch eggs, evolve Pokemon, or award progress.

Example:

- First launch sees Today `50M`: applied progress `0`.
- Next refresh sees Today `60M`: applied progress `10M`.
- Next unchanged refresh sees Today `60M`: applied progress `0`.

## Claimed Token Accounting

Gameplay delta is:

```text
max(0, currentTodayTokens - claimedTodayTokens)
```

After the local day changes, `claimedTodayTokens` is reset to zero for the new `yyyy-MM-dd` local date. If the new day starts with Codex Today `3M`, the game applies `3M`, not `3M - yesterday`.

If the observed daily counter decreases, no negative XP is applied and the stored claimed value is not rolled back.

## Persistence

Gameplay state is stored at:

```text
%APPDATA%\PokeTokenBar\state.json
```

Schema version:

```text
1
```

Persisted fields include:

- current `CompanionState`
- egg/active Pokemon progress
- planned path species IDs
- stage index
- rarity, nature, shiny
- hatch timestamp
- species name cache for display
- baseline and claimed token state
- used-since-install total
- last graduation snapshot for Phase 6 consumption
- last recoverable error

The save file stores stable IDs and simple gameplay data. It does not store PokéAPI DTOs, sprite bytes, raw Codex records, prompts, project paths, or source content.

## Crash Consistency

Token claim state and companion progression are committed together in one JSON save. The storage layer writes a temp file, keeps the previous valid file as `state.previous.json`, then replaces `state.json`.

`CompanionGameService` updates its in-memory committed state only after the combined save succeeds for baseline and normal progression. If save fails, the exception remains visible and the service does not report the token transaction as safely committed.

Filesystem atomicity is limited to the local filesystem behavior available to `File.Move(..., overwrite: true)`, plus the previous-file backup.

## Save Recovery

On load:

1. `state.json` is attempted.
2. If it fails, `state.previous.json` is attempted.
3. If both fail, the error is logged and surfaced.

The corrupt primary file is not silently destroyed.

## Token Delta Flow

Refresh flow:

```text
Codex refresh
-> local-day claim reconciliation
-> post-baseline delta
-> optional async hatch preparation
-> ProgressionEngine.ApplyProgress
-> domain events
-> one combined state save
-> UI refresh
```

Refreshes are serialized with a `SemaphoreSlim` so repeated tray refresh clicks do not overlap.

## Lifecycle

New post-baseline Codex tokens:

```text
Egg progress
-> hatch at 5M
-> overflow into Pokemon stage progress
-> evolution at stage threshold
-> overflow into next stage
-> graduation at final threshold
-> fresh egg at zero progress
```

Graduation overflow is discarded, matching the Phase 3 upstream-aligned behavior.

## Event Processing

The service consumes explicit domain events:

- `Hatched`
- `Evolved`
- `Graduated`

These events are logged with sanitized species IDs and are available for future notification and collection handling. Phase 5 stores the last graduation snapshot so Phase 6 can build Pokédex/Catch Log behavior without relying on UI diffing.

## UI And Sprites

`MainViewModel` now displays actual gameplay state:

- Egg or current Pokemon name
- sprite or placeholder
- shiny marker
- rarity
- nature
- stage count
- current progress and threshold
- Codex Today, Last 5 hours, Week, Month, Lifetime

`PokemonSpriteService` still returns paths only. `WpfImageLoader` converts cached files into WPF `ImageSource` with `BitmapCacheOption.OnLoad` so sprite files are not held open.

Sprite failures do not block gameplay.

## Periodic Refresh

The app starts a 2-minute periodic refresh loop at startup. The loop is cancellation-aware and stops on application shutdown. Refresh work remains async and is marshaled through the WPF dispatcher for UI-bound updates.

## Known Limitations

- Pokédex and Catch Log archival are deferred to Phase 6.
- Wallet, Shop, Bag, purchased egg tiers, and Shiny Charm inventory are deferred to Phase 7.
- Ditto's upstream special reveal/disguise mechanic is not implemented yet; Ditto remains excluded from normal egg starts as documented in Phase 4.
- Manual real-token progression was not forced during automated tests because tests must not mutate the user's production gameplay save.
