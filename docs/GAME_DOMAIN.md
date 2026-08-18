# Game Domain

Phase 3 implements the Pokemon companion rules as a pure domain engine. It does not depend on WPF, Codex usage files, AppData storage, HTTP, PokéAPI, or sprites.

## Rarity

`RarityClassifier` mirrors upstream classification:

- `Legendary` when `isLegendary` or `isMythical` is true.
- `Rare` when capture rate is `<= 45`.
- `Uncommon` when capture rate is `<= 120`.
- `Common` otherwise.

The domain model stores rarity on `PokemonSpecies` and on the active companion state. Phase 4 will map PokéAPI species metadata into this model.

## Balance

`GameBalance` is the single source for balance constants:

- Egg hatch threshold: `5_000_000`.
- Common graduation total: `750_000_000`.
- Uncommon graduation total: `1_875_000_000`.
- Rare graduation total: `3_000_000_000`.
- Legendary graduation total: `6_000_000_000`.
- Rare Candy progression: `100_000_000`.
- Shiny odds denominator: `64`.
- Shiny Charm odds denominator: `48`.

For a selected path containing `k` forms, the stage threshold is:

```text
round(graduationTotal * (stageIndex + 1) / (k * (k + 1) / 2))
```

`stageIndex` is zero-based. A three-stage Common line is therefore `125M`, `250M`, and `375M`, totaling `750M`.

## Species And Evolution Paths

`PokemonSpecies` is a small gameplay model containing stable species information: National Dex ID, name, generation, rarity, and optional capture metadata. It is not a PokéAPI DTO.

`EvolutionNode` represents an evolution tree. `EvolutionPathSelector` walks that tree and chooses one child at each branch using `IRandomSource`. The result is an `EvolutionPath`, an ordered list of species for one companion. Branching is selected once at hatch time and then stored by stable species IDs in `ActiveCompanionState.PlannedPathSpeciesIds`.

## Companion State

`CompanionState` contains either:

- `EggState`, with accumulated egg progress and an optional future guaranteed rarity tier.
- `ActiveCompanionState`, with base/current species IDs, planned path IDs, current stage index, current stage progress, rarity, nature, shiny flag, hatch time, realized species IDs, and total applied Pokemon-stage progress.

The state is serializable-friendly and stores stable IDs rather than network objects.

## Hatching

`ProgressionEngine` does not select Pokemon directly. It uses `IHatchService`, which receives the egg state and hatch time and returns a `HatchResult` containing the selected path, rarity, nature, and shiny flag.

Phase 3 tests use a fixed hatch service. Phase 4/5 can provide a real hatch service backed by cached Pokemon metadata.

## Progression And Events

`ProgressionEngine.ApplyProgress` is the only progression path. Real Codex usage and Rare Candy should both call this method; there is no separate item evolution algorithm.

The engine returns a `ProgressionResult` with the new state and ordered domain events:

- `EggProgressed`
- `Hatched`
- `StageProgressed`
- `Evolved`
- `Graduated`

The UI, notifications, persistence, Pokédex, and Catch Log can later consume these events without diffing old and new state.

## Overflow Behavior

Egg progress fills first. If an egg receives more than `5_000_000` tokens, the overflow is applied immediately to the newly hatched Pokemon.

Stage overflow carries through evolutions. If a stage threshold is crossed, the engine evolves to the next planned species, resets stage progress to zero, and applies remaining overflow to the next stage.

Graduation follows upstream behavior from `CompanionStore.swift`: when the final stage threshold is reached, the companion graduates, state resets to a fresh egg, and the progression loop stops. Overflow after graduation is intentionally discarded rather than applied to the new egg.

## Graduation

Graduation emits a `Graduated` event with a `GraduatedCompanion` snapshot. The snapshot preserves base species, final species, planned and realized paths, rarity, nature, shiny flag, hatch time, graduation time, and total applied stage progress.

After graduation, the current state is a fresh egg with zero egg progress and no active Pokemon details.

## Nature

`PokemonNature` contains all 25 standard natures used by upstream. Nature is cosmetic. `NatureService` selects natures through `IRandomSource`; `RerollDifferent` excludes the current nature, matching upstream Mint behavior.

## Shiny

Shiny status is selected at hatch and stored on `ActiveCompanionState`. The value is carried unchanged through evolutions and into the graduation snapshot.

Phase 3 models the shiny odds constants but does not yet integrate Shiny Charm inventory or hatch probability decisions.
