using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.Gameplay;

public sealed class ShopService
{
    private readonly CompanionGameService _gameService;
    private readonly SemaphoreSlim _purchaseLock = new(1, 1);

    public ShopService(CompanionGameService gameService)
    {
        _gameService = gameService;
    }

    public IReadOnlyList<ShopCatalogItem> Catalog => EconomyCatalog.Items;

    public bool CanBuy(ShopItemKind kind, GameSaveState? state = null)
    {
        state ??= _gameService.State;
        var item = EconomyCatalog.Get(kind);
        if (state.AvailableBalance < item.Price)
        {
            return false;
        }

        return kind != ShopItemKind.ShinyCharm || !state.Inventory.HasShinyCharm;
    }

    public async Task<EconomyActionResult> BuyAsync(ShopItemKind kind, bool confirmedDestructiveEggPurchase = false, CancellationToken cancellationToken = default)
    {
        await _purchaseLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                var item = EconomyCatalog.Get(kind);
                var state = _gameService.State;
                if (!CanBuy(kind, state))
                {
                    return new EconomyActionResult(false, state, kind == ShopItemKind.ShinyCharm && state.Inventory.HasShinyCharm ? "Shiny Charm already owned" : "Not enough tokens");
                }

                if (item.EggTier is not null && state.Companion.Active is not null && !confirmedDestructiveEggPurchase)
                {
                    return new EconomyActionResult(false, state, "Buying an egg will discard the current companion", RequiresConfirmation: true, ShinyDiscardWarning: state.Companion.Active.IsShiny);
                }

                return item.EggTier is not null
                    ? await BuyEggAsync(item, cancellationToken).ConfigureAwait(false)
                    : await BuyInventoryItemAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new EconomyActionResult(false, _gameService.State, "Purchase could not be saved");
            }
        }
        finally
        {
            _purchaseLock.Release();
        }
    }

    private Task<EconomyActionResult> BuyInventoryItemAsync(ShopCatalogItem item, CancellationToken cancellationToken)
    {
        return _gameService.MutateStateAsync(state =>
        {
            var inventory = state.Inventory;
            inventory = item.InventoryItem switch
            {
                InventoryItemKind.RareCandy => inventory with { RareCandyCount = inventory.RareCandyCount + 1 },
                InventoryItemKind.Mint => inventory with { MintCount = inventory.MintCount + 1 },
                InventoryItemKind.ShinyCharm => inventory with { HasShinyCharm = true },
                _ => inventory
            };

            return state with { Inventory = inventory, SpentTokens = checked(state.SpentTokens + item.Price) };
        }, $"Purchased {item.DisplayName}", cancellationToken);
    }

    private Task<EconomyActionResult> BuyEggAsync(ShopCatalogItem item, CancellationToken cancellationToken)
    {
        return _gameService.MutateStateAsync(state =>
        {
            var next = DiscardActiveIfNeeded(state);
            return next with
            {
                SpentTokens = checked(next.SpentTokens + item.Price),
                Companion = CompanionState.FreshEgg(GuaranteedRarityFor(item.EggTier!.Value)),
                LastError = null
            };
        }, $"Purchased {item.DisplayName}", cancellationToken);
    }

    private static GameSaveState DiscardActiveIfNeeded(GameSaveState state)
    {
        if (state.Companion.Active is null)
        {
            return state;
        }

        var active = state.Companion.Active;
        var source = state.ActiveCatch ?? new CatchLogEntry
        {
            IndividualId = active.IndividualId,
            BaseSpeciesId = active.BaseSpeciesId,
            PlannedPathSpeciesIds = active.PlannedPathSpeciesIds,
            EncounteredSpeciesIds = active.RealizedSpeciesIds,
            FinalSpeciesId = active.CurrentSpeciesId,
            Rarity = active.Rarity,
            Nature = active.Nature,
            IsShiny = active.IsShiny,
            HatchTime = active.HatchTime,
            Status = CompanionLifecycleStatus.Active,
            TotalAppliedProgressTokens = active.TotalAppliedProgressTokens,
            EvolutionHistory = active.RealizedSpeciesIds.Select((id, index) =>
                new EvolutionHistoryEntry(id, state.SpeciesNames.TryGetValue(id, out var name) ? name : $"#{id:000}", active.HatchTime, index)).ToArray()
        };

        var entry = source with
        {
            Status = CompanionLifecycleStatus.Discarded,
            GraduationTime = null,
            FinalSpeciesId = active.CurrentSpeciesId,
            EncounteredSpeciesIds = active.RealizedSpeciesIds,
            Nature = active.Nature,
            TotalAppliedProgressTokens = active.TotalAppliedProgressTokens
        };
        var catchLog = state.CatchLog.Where(existing => existing.IndividualId != entry.IndividualId).Concat(new[] { entry }).ToArray();
        return state with { ActiveCatch = null, CatchLog = catchLog };
    }

    private static Rarity? GuaranteedRarityFor(EggTier tier) => tier switch
    {
        EggTier.Uncommon => Rarity.Uncommon,
        EggTier.Rare => Rarity.Rare,
        _ => null
    };
}
