# Economy, Bag, Shop, and Purchased Eggs

Phase 7 adds the PokeTokenBar economy on top of the live gameplay and collection state.

## Wallet

Codex tokens keep their upstream dual-use behavior:

- New post-baseline Codex usage progresses the current egg or Pokemon.
- The same post-baseline usage also becomes spendable shop currency.

The wallet is:

```text
AvailableBalance = max(0, UsedSinceInstall - SpentTokens)
```

Purchases only increase `SpentTokens`. They never subtract growth that was already applied to the companion, and they never reduce `UsedSinceInstall`.

Historical Codex usage from before the install baseline still does not become spendable currency.

## Save Schema v3

The gameplay save schema is now version `3`.

New persisted fields:

- `SpentTokens`
- `Inventory.RareCandyCount`
- `Inventory.MintCount`
- `Inventory.HasShinyCharm`
- purchased egg tier through the existing egg `GuaranteedMinimumRarity`

Version 2 saves migrate to version 3 with zero spent tokens and an empty inventory while preserving baseline state, claimed tokens, companion state, collection data, individual IDs, nature, shiny status, and progress. Migration does not replay XP or award currency.

## Shop Catalog

Prices are centralized in `EconomyCatalog` and mirror upstream:

```text
Mint                  100,000,000
Rare Candy            500,000,000
Normal Pokemon Egg  1,000,000,000
Uncommon Egg        2,500,000,000
Shiny Charm         3,000,000,000
Rare Egg            4,000,000,000
```

Purchases are validated in `ShopService`, not in WPF. The service rejects insufficient balance and rejects a second Shiny Charm purchase. Shop operations are serialized so rapid double-clicks cannot spend the same balance twice.

## Inventory and Bag

Inventory is persisted as small ledger fields:

- Rare Candy is countable and consumable.
- Mint is countable and consumable.
- Shiny Charm is permanent and passive.

`BagService` owns item use rules. Bag operations are serialized and return user-readable failures.

## Rare Candy

Rare Candy adds exactly `100,000,000` bonus progression by calling the same gameplay progression path as real Codex usage. There is no separate Rare Candy evolution algorithm.

Rare Candy can hatch an egg, evolve a Pokemon, or graduate a Pokemon. It does not change `ClaimedTodayTokens`, `UsedSinceInstall`, or the Codex baseline.

If Pokemon metadata is unavailable when a Rare Candy crosses the hatch threshold, the candy is consumed and its 100M bonus remains preserved in egg progress above the hatch threshold. A later refresh/manual progression retry can resolve the hatch and carry the overflow forward.

## Mint

Mint requires an active Pokemon and cannot be used on an egg.

Using a Mint:

- decrements `MintCount`
- rerolls nature through `NatureService.RerollDifferent`
- preserves `IndividualId`
- updates the active lifecycle record so the eventual Catch Log entry uses the current nature

Nature remains cosmetic.

## Shiny Charm

Shiny Charm is permanent and passive. It can effectively be purchased once.

It affects future hatches only:

```text
normal odds:      1 / 64
with charm odds:  1 / 48
```

The active companion is not retroactively changed. The hatch request passes `ShinyCharmActive` from inventory into the real hatch service.

## Purchased Eggs

Buying an egg replaces the current companion with a fresh egg at zero progress. The egg still requires the normal `5,000,000` token hatch threshold.

Supported shop eggs:

- Normal Egg: no rarity guarantee
- Uncommon Egg: Uncommon or better
- Rare Egg: Rare or Legendary

The purchased guarantee is stored on the egg state and survives restart. It is consumed only by a successful hatch. If hatching is deferred because Pokemon metadata is unavailable, the egg progress and tier entitlement remain intact.

After graduation, the normal lifecycle still creates a fresh default egg with no purchased tier.

## Discard Behavior

Buying an egg while a Pokemon is active is destructive and requires confirmation. If the active Pokemon is shiny, the UI shows a stronger warning.

On confirmed egg purchase:

- the active individual is finalized with `CompanionLifecycleStatus.Discarded`
- its `IndividualId`, nature, shiny status, hatch time, realized species, and progress are preserved
- it is added to the Catch Log exactly once
- already encountered species remain in the Pokedex
- no future/unrealized species are marked owned

Replacing an existing egg simply resets the egg and tier.

## Atomicity

Economy actions mutate one aggregate `GameSaveState` and save through the existing safe JSON storage:

- purchase ledger
- inventory
- companion state
- discarded lifecycle/Catch Log
- purchased egg tier

If saving fails, services return failure and `CompanionGameService` does not advance its in-memory committed state. Filesystem atomicity is limited to the temp-file/replace behavior of `JsonFileStorage`.

## UI

The Bag and Shop placeholders are now real WPF views.

Shop shows available balance, item catalog, prices, buy states, and egg replacement confirmation. Bag shows Rare Candy, Mint, and Shiny Charm state. Home and Collection refresh after purchases and item use.
