# Reference Notes

Phase 0 inspected the current upstream PokeTokenBar repository at:

https://github.com/chattymin/PokeTokenBar

The Windows version will not port AppKit or SwiftUI code. It will reimplement the mechanics and data contracts in C#/WPF while preserving upstream terminology and balance where those are not platform-specific.

## Upstream Files Inspected

Minimum requested files inspected:

- `Sources/PokeTokenBar/Core/CompanionModel.swift`
- `Sources/PokeTokenBar/Core/CompanionStore.swift`
- `Sources/PokeTokenBar/Core/LocalUsageReader.swift`
- `Sources/PokeTokenBar/Core/LocalUsageProvider.swift`
- `Sources/PokeTokenBar/UI/CompanionView.swift`
- `Sources/PokeTokenBar/UI/PopoverView.swift`
- `Sources/PokeTokenBar/UI/ShopView.swift`
- `Sources/PokeTokenBar/UI/BagView.swift`
- `Sources/PokeTokenBar/UI/SpriteLoader.swift`
- `README.md`

## Mechanics To Reuse Conceptually

### Rarity and Balance

Source: `CompanionModel.swift`

Upstream rarity derives from PokéAPI species metadata:

- Legendary if `is_legendary` or `is_mythical`.
- Rare if `capture_rate <= 45`.
- Uncommon if `capture_rate <= 120`.
- Common otherwise.

Balance constants to mirror:

- Egg hatch threshold: `5,000,000` tokens.
- Common graduation total: `750,000,000`.
- Uncommon graduation total: `1,875,000,000`.
- Rare graduation total: `3,000,000,000`.
- Legendary graduation total: `6,000,000,000`.

Per-stage threshold formula:

```text
phaseThreshold = round(graduationTotal * (stageIndex + 1) / (k * (k + 1) / 2))
```

where `k` is the number of forms in the selected evolution path.

### Economy

Source: `CompanionModel.swift`, `CompanionStore.swift`, `ShopView.swift`, `BagView.swift`

Tokens have two independent roles:

- Growth/progression is based on usage and is not reduced by purchases.
- Shop currency is `usedSinceInstall - spentTokens`.

Upstream shop values to mirror:

- Rare Candy: price `500,000,000`, effect `+100,000,000` progression XP.
- Mint: price `100,000,000`, cosmetic nature reroll.
- Shiny Charm: price `3,000,000,000`, passive permanent item, one effective purchase.
- Normal Egg: `1,000,000,000`.
- Uncommon Egg: `2,500,000,000`.
- Rare Egg: `4,000,000,000`.

Egg prices are derived upstream by applying the rarity graduation multiplier to the base fresh egg price.

Buying an egg discards the current active companion, resets to a new egg, persists the purchased guaranteed rarity tier, clears pending hatch species, resets egg usage, and charges only `spentTokens`. Upstream also adds stronger confirmation if the current companion is shiny.

### Nature and Shiny

Source: `CompanionModel.swift`, `CompanionStore.swift`, `BagView.swift`, `ShopView.swift`

Upstream uses all 25 mainline Pokemon natures:

`Hardy`, `Lonely`, `Brave`, `Adamant`, `Naughty`, `Bold`, `Docile`, `Relaxed`, `Impish`, `Lax`, `Timid`, `Hasty`, `Serious`, `Jolly`, `Naive`, `Modest`, `Mild`, `Quiet`, `Bashful`, `Rash`, `Calm`, `Gentle`, `Sassy`, `Careful`, `Quirky`.

Nature is cosmetic. Mint rerolls to a different nature when an active Pokemon exists.

Shiny odds:

- Normal: `1 / 64`.
- With Shiny Charm: `1 / 48`.

Shiny status is determined on hatch and is preserved across evolution.

### Lifecycle

Source: `CompanionStore.swift`

Lifecycle to mirror:

```text
Egg -> hatch -> active Pokemon -> evolution(s) -> graduation -> archived collection entry -> new egg
```

Important behavior:

- Token overflow after hatching carries into the newly hatched Pokemon.
- Token overflow after evolution carries into the next stage.
- Final-form threshold triggers graduation.
- Graduation archives the individual and resets to an egg with `eggUsage = 0`.
- If evolution metadata is temporarily unavailable, usage is still accumulated and evolution decisions resume after the line is loaded.

### Evolution Lines and Branching

Source: `CompanionModel.swift`, `CompanionStore.swift`

Upstream represents evolution as an `EvoNode` tree and selects a planned path at hatch time. The active state stores:

- Base species ID.
- Realized `pathIDs`.
- Full `plannedPathIDs`.
- Current stage index.
- Current stage usage.
- Rarity.
- Total forms in the planned path.
- Shiny and nature.

Branching choices use upstream's planned-path approach. Windows should preserve this idea: choose the full branch at hatch, store it, and reuse it after restart without consuming more random numbers.

### Pokemon Pool and Weighted Eggs

Source: `CompanionStore.swift`, `README.md`

Upstream uses Gen 1-5 animated-sprite-supported species, national dex IDs `1..649`, with a dynamically fetched base-species pool rather than a hard-coded list. The README currently states 329 possible starting species.

Selection philosophy:

- Use PokéAPI metadata and evolution chain data.
- Use starting species only.
- Weight by capture rate.
- Preserve legendary rarity.
- For guaranteed eggs, filter the candidate index using the same rarity ceiling logic.
- Do not offer a legendary-only egg.

Upstream has a fallback REST path that can hatch when the GraphQL index is unavailable, but the fallback omits capture-rate weighting. Windows should prefer a cached index so the normal path remains weighted and offline-friendly.

### Codex Usage

Source: `LocalUsageReader.swift`, `LocalUsageProvider.swift`, local Codex session inspection.

Upstream reads:

```text
~/.codex/sessions/**/rollout-*.jsonl
```

The upstream parser expects Codex usage as `event_msg.payload.type == "token_count"` and sums `payload.info.last_token_usage` turn deltas. It maps:

- `input_tokens`
- `output_tokens`
- `cached_input_tokens`
- `reasoning_output_tokens`
- `total_tokens`

It also keeps cumulative `total_token_usage` state for deduplication and fork replay handling. Current upstream has specific logic for:

- Parent session closure expansion.
- Session metadata discovery.
- Fork/replay prefix comparison.
- Subagent behavior where current Codex versions may include parent metadata but not replay token counts.
- Stable event IDs based on session ID, epoch, and cumulative usage fingerprint.

Windows should not use a naive sum of every token-count line. It should implement a C# equivalent of the upstream cumulative-state deduplication model.

### Local Installation Baseline

Source: `CompanionStore.swift`, `CompanionModel.swift`

Upstream does not grant all historical local tokens as shop currency on first run. It sets an install baseline using the current day's observed tokens:

- `installBaselineSet = true`
- `claimedTodayTokens = todayTokens`
- `usedSinceInstall` remains at the existing saved value

After the baseline is set, only increases beyond `claimedTodayTokens` are applied to `usedSinceInstall`, wallet balance, egg usage, or active progression. On a local day change, `claimedTodayTokens` resets to `0`.

Windows should preserve this philosophy. Historical sessions may be shown in analytics, but economy/progression should begin after first-run baseline.

### Storage and Cache Concepts

Source: `CompanionModel.swift`, `SpriteLoader.swift`, README

Upstream stores application state under user application support, writes JSON atomically, tolerates partially corrupt state, and backs up before imports.

Windows equivalents:

- Save/config: `%APPDATA%\PokeTokenBar\`
- Cache: `%LOCALAPPDATA%\PokeTokenBar\Cache\`
- Logs: `%LOCALAPPDATA%\PokeTokenBar\Logs\`

Sprite cache behavior to mirror conceptually:

- Runtime downloads only.
- Static sprites from `raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png`.
- Shiny static from `/shiny/{id}.png`.
- Gen V animated GIFs from `/versions/generation-v/black-white/animated/{id}.gif`.
- Shiny animated GIFs from `/animated/shiny/{id}.gif`.
- Egg sprite from `/pokemon/egg.png`.
- Item sprites from `/sprites/items/{name}.png`, where available.
- Disk cache before network, graceful placeholder on failure.

### UI Concepts

Source: `CompanionView.swift`, `PopoverView.swift`, `ShopView.swift`, `BagView.swift`, README

Conceptual UI behavior to preserve:

- Compact popup focused on the companion.
- Home, Shop, Bag, and Collection/Pokedex navigation.
- Today, week, and month usage summaries.
- Active companion card with sprite, stage progress, rarity, nature, shiny marker, and evolution line.
- Shop wallet header showing spendable tokens.
- Inline confirmation for purchases/uses, especially destructive egg rerolls.
- Bag disables consumables when not usable.
- Collection distinguishes species Pokedex from individual catch log.
- Shiny indicator appears consistently.

Platform-specific UI must be reimplemented in WPF.

## Concepts Not Initially Reused

The initial Windows scope is Codex only. The following upstream areas are not Phase 1-7 requirements unless later requested:

- Claude, Gemini, Antigravity, OpenCode, Hermes, Cursor, Grok, Copilot, and Kiro providers.
- macOS menu bar implementation.
- AppKit `NSPopover`, `NSStatusItem`, Keychain, SwiftUI observation.
- macOS update mechanism.
- macOS floating desktop pet behavior unless added during polish.
- Official Codex limit display via `codex app-server`, unless added after core local token tracking.

## Attribution

The Windows project must acknowledge:

- Original project: `chattymin/PokeTokenBar`, MIT License.
- Pokemon data and sprites: PokéAPI and PokeAPI sprite repository.
- This project must be described as unofficial and unaffiliated with Nintendo, Game Freak, Creatures Inc., The Pokemon Company, OpenAI, or the original PokeTokenBar author.
