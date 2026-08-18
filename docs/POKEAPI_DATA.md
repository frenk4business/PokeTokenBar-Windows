# PokéAPI Data Layer

Phase 4 adds the real Pokemon metadata layer used by hatching and sprites. It remains separate from the pure game domain and does not connect live Codex usage to progression.

## Endpoints

The data layer uses:

- `POST https://graphql.pokeapi.co/v1beta2` for the starting-species index.
- `GET https://pokeapi.co/api/v2/pokemon-species/{id}` for selected species metadata and evolution-chain references.
- `GET https://pokeapi.co/api/v2/evolution-chain/{id}` for real evolution trees.
- `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/...` for sprites.

No token usage, prompts, project paths, or local Codex data are sent to these services.

## DTO Mapping

PokéAPI DTOs live under `Services/PokeApi` and do not enter `Core/Game`.

Mapping into domain models:

- `PokemonSpeciesDto` maps to `PokemonSpecies`.
- `EvolutionChainDto` maps to an `EvolutionNode` tree.
- Base index rows map to `BaseSpeciesEntry`.
- Rarity is always calculated through `RarityClassifier`.

English species names are used for Phase 4. DTOs preserve enough shape to add localization later.

## Supported Scope

The supported species range is National Dex `1..649`, matching upstream's Gen V animated sprite boundary.

The starting-species query filters:

- `evolves_from_species_id IS NULL`
- `id <= 649`
- `id != 132`

Ditto `#132` is excluded to match upstream, where Ditto is reserved for a special reveal/disguise path rather than normal egg hatching.

Real diagnostic result on August 18, 2026:

- Eligible starts with Ditto excluded: `328`
- Base starts including Ditto: `329`

This explains the upstream README/comment wording of approximately `329` starts.

## Candidate Counts

Real current PokéAPI GraphQL counts with Ditto excluded:

- Total: `328`
- Gen 1: `67`
- Gen 2: `54`
- Gen 3: `73`
- Gen 4: `52`
- Gen 5: `82`

Rarity counts:

- Common: `175`
- Uncommon: `33`
- Rare: `72`
- Legendary/Mythical: `48`

These counts depend on the current PokéAPI data and PokeTokenBar rarity thresholds.

## Rarity

Rarity rules are inherited from the Phase 3 domain:

- Legendary when `is_legendary` or `is_mythical`.
- Rare when capture rate is `<= 45`.
- Uncommon when capture rate is `<= 120`.
- Common otherwise.

This means some familiar starter/base species such as Bulbasaur and Eevee classify as Rare because PokéAPI gives them capture rate `45`.

## Egg Filtering

Egg tiers filter the eligible base pool before rolling:

- Normal: all rarities.
- Uncommon: Uncommon, Rare, Legendary.
- Rare: Rare, Legendary.

There is no Legendary-only egg. Legendary and Mythical species remain eligible in Uncommon and Rare eggs.

## Weighted Selection

The selector mirrors upstream's weighted philosophy:

```text
weight = max(1, captureRate)
```

If a base has already been collected, upstream halves its weight. The Windows selector supports this with collected final keys:

```text
weight = max(1, captureRate / 2)
```

Higher capture rate means more common and therefore more likely. Lower capture rate species remain possible but rarer.

## Evolution Trees

Evolution chains preserve PokéAPI tree structure:

- One-stage species remain one-node trees.
- Two-stage and three-stage chains preserve nesting.
- Branching chains preserve all child branches.
- Evolution conditions such as trade, item, time of day, stats, personality, or gender are ignored, matching the upstream game model.

At hatch time, `EvolutionPathSelector` chooses one complete path through the tree and stores it as ordered species IDs. The path is not rerolled later.

## Special Cases

- Eevee: kept as a branching base; one branch is selected at hatch.
- Tyrogue: kept as a branching base; one final path is selected at hatch.
- Wurmple: kept as a branching base; one branch is selected at hatch.
- Nincada/Shedinja: represented according to PokéAPI's evolution tree; conditions are not simulated.
- Ditto: excluded from normal egg candidates to match upstream's special Ditto reveal behavior.
- Unown: one-stage base if represented as base species by PokéAPI.
- Baby Pokemon: included when PokéAPI marks them as base species.
- Trade/item/split branches: conditions are ignored; tree branches are preserved and one path is selected.

## Cache

Cache root:

```text
%LOCALAPPDATA%\PokeTokenBar\Cache\pokeapi\
```

Files:

- `starting-species-index-v1.json`
- `species/{id}.json`
- `evolution-chains/{id}.json`

The cache is disposable. Schema versioning is on the starting index. Corrupt JSON cache entries are ignored and refreshed from network when possible.

## Offline Behavior

Offline behavior is cache-first:

- Cached starting index can be loaded without network.
- Cached species metadata can be loaded without network.
- Cached evolution chains can be loaded without network.
- If required metadata is missing and network is unavailable, the repository throws a recoverable `PokeApiException`.

Fully offline hatching requires:

- a cached starting index;
- cached species metadata for the selected base;
- cached evolution-chain data for that base;
- cached species metadata for species in the selected chain.

Sprites are not required for gameplay.

## Sprites

Sprite cache root:

```text
%LOCALAPPDATA%\PokeTokenBar\Cache\sprites\
```

Sprite URLs:

- Static normal: `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png`
- Static shiny: `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/shiny/{id}.png`
- Gen V animated: `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-v/black-white/animated/{id}.gif`
- Gen V animated shiny: `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-v/black-white/animated/shiny/{id}.gif`
- Egg: `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/egg.png`

Cache names:

- `{id}.png`
- `{id}-shiny.png`
- `{id}.gif`
- `{id}-shiny.gif`
- `egg.png`

Animated sprite lookup falls back to static. Shiny lookup falls back to normal if the shiny asset is unavailable. Zero-byte cache files are treated as corrupt and replaced.

The low-level sprite service returns file paths and metadata, not WPF image objects.

## Shiny And Nature

`PokeApiHatchService` uses the Phase 3 `NatureService` and `ShinyRoller`.

Shiny odds:

- Normal: `1 / 64`
- Shiny Charm: `1 / 48`

Phase 7 inventory is not implemented yet, so `HatchRequest` carries `ShinyCharmActive` for future callers.
