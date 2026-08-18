# Collection

Phase 6 adds two separate persistent collection concepts:

- Pokédex: species-level ownership.
- Catch Log: individual companion history.

These live in the main gameplay save so collection updates are committed atomically with gameplay progression.

## Pokédex Semantics

The Pokédex has one entry per species ID. Species ID is authoritative.

Stored fields include:

- species ID
- cached display name
- owned flag
- shiny-owned flag
- first encountered timestamp
- latest encountered timestamp
- generation
- rarity

Species become owned immediately when encountered:

- hatch marks the hatched species owned;
- evolution marks the evolved species owned;
- graduation is not required.

This follows upstream timing semantics: collection updates happen as the companion hatches and evolves, not only at the end of the lifecycle.

## Shiny Ownership

Shiny ownership is tracked on the same species row:

```text
Owned = true
ShinyOwned = true
```

A shiny hatch marks the hatch species shiny-owned. A shiny evolution marks each evolved species shiny-owned. Normal and shiny forms do not create duplicate Pokédex rows.

If shiny is encountered first, normal ownership is implicitly true.

## Catch Log Semantics

The Catch Log stores individual companion lifecycles. Each hatched companion receives a stable `IndividualId` from `ActiveCompanionState`.

Stored fields include:

- individual ID
- base species ID
- planned evolution path IDs
- encountered species IDs
- final species ID
- rarity
- nature
- shiny flag
- hatch timestamp
- graduation timestamp
- status
- total applied progress
- evolution history entries

Statuses currently include:

- `Active`
- `Graduated`
- `Discarded`

`Discarded` is reserved for Phase 7 egg-reroll behavior and is not produced in Phase 6.

## Event-Driven Updates

Collection updates are driven from domain events:

- `Hatched`
- `Evolved`
- `Graduated`

The UI does not infer collection changes by comparing species IDs.

On `Hatched`:

- active lifecycle is created;
- hatch species is added to Pokédex;
- shiny ownership is applied if needed.

On `Evolved`:

- new species is appended to active lifecycle history;
- new species is marked owned in Pokédex;
- shiny ownership carries forward for shiny companions.

On `Graduated`:

- the active lifecycle becomes a permanent Catch Log entry;
- graduation time and final species are recorded;
- active lifecycle is cleared.

## Idempotency

Graduation archival is protected by `IndividualId`. Reprocessing the same graduation event does not create duplicate Catch Log entries.

Repeated species ownership updates update existing Pokédex rows instead of adding duplicate species rows.

## Save Schema V2

Phase 6 upgrades `GameSaveState` to schema version `2`.

New fields:

- `Pokedex`
- `ActiveCatch`
- `CatchLog`
- `LastGraduationImportedToCatchLog`

The existing Phase 5 fields remain:

- companion state
- claimed token baseline
- used-since-install
- species names
- last graduation snapshot

## Migration

Version 1 saves are migrated on load:

- baseline and claimed tokens are preserved;
- current companion progress is preserved;
- active companion individual ID is preserved if present or generated if missing;
- active lifecycle is initialized from current active companion;
- existing last graduation snapshot is imported into Catch Log once when possible;
- no tokens are re-applied.

Future-version saves are not accepted as authoritative by this version.

## Persistence

Collection state remains in:

```text
%APPDATA%\PokeTokenBar\state.json
```

Gameplay state and collection state are saved together through the existing safe JSON storage path:

```text
state mutation
-> temp file
-> previous backup
-> replace state.json
```

This avoids a split-brain state where a Pokémon evolves but the Pokédex does not record it.

## Pokédex UI

The Collection tab contains a Pokédex page.

Behavior:

- represents National Dex `1..649`;
- default sort is National Dex number ascending;
- 24 species per page;
- undiscovered species are dimmed and shown by dex number;
- owned species show cached name, rarity, sprite, and shiny marker when applicable.

Filters:

- generation: All, Gen 1-5
- rarity: All, Common, Uncommon, Rare, Legendary
- ownership: All, Owned, Missing, Shiny owned

Search:

- case-insensitive name search;
- dex-number search, including values like `25` or `#025`.

## Catch Log UI

The Collection tab also contains a Catch Log page.

Rows are sorted newest-first by graduation timestamp, falling back to hatch timestamp.

Each row shows:

- final/current sprite;
- title with shiny marker;
- actual encountered path only;
- rarity;
- nature;
- hatch and graduation/status details.

New installs show an empty state: `No graduated Pokémon yet.`

## Sprite Strategy

Home may request animated sprites. Collection views request static sprites only.

Only the current 24 Pokédex page items request sprites. Missing species do not request sprites. Sprite failures show placeholders and never affect collection data.

## Offline Behavior

Ownership and Catch Log history are local and remain visible offline.

If metadata/sprites are missing while offline:

- known saved species names are used;
- otherwise entries fall back to `#001`-style labels;
- collection records are not hidden or deleted.

## Known Limitations

- Full localized species metadata for undiscovered species is not prewarmed.
- Missing/unowned species use generation derived from National Dex ranges for filtering.
- Rarity filtering for missing/unowned species only applies when rarity metadata is already known.
- Pokédex/Catch Log are implemented, but shop, wallet, Bag, and discard status production are deferred to Phase 7.
