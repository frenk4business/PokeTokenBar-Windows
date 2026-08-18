using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.Gameplay;

public sealed class BagService
{
    private readonly CompanionGameService _gameService;
    private readonly NatureService _natureService;
    private readonly IRandomSource _randomSource;
    private readonly SemaphoreSlim _useLock = new(1, 1);

    public BagService(CompanionGameService gameService, NatureService natureService, IRandomSource randomSource)
    {
        _gameService = gameService;
        _natureService = natureService;
        _randomSource = randomSource;
    }

    public async Task<EconomyActionResult> UseRareCandyAsync(CancellationToken cancellationToken = default)
    {
        await _useLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                var state = _gameService.State;
                if (state.Inventory.RareCandyCount <= 0)
                {
                    return new EconomyActionResult(false, state, "No Rare Candy available");
                }

                return await _gameService.ApplyBonusProgressAsync(
                    GameBalance.RareCandyProgress,
                    current => current with { Inventory = current.Inventory with { RareCandyCount = current.Inventory.RareCandyCount - 1 } },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new EconomyActionResult(false, _gameService.State, "Item use could not be saved");
            }
        }
        finally
        {
            _useLock.Release();
        }
    }

    public async Task<EconomyActionResult> UseMintAsync(CancellationToken cancellationToken = default)
    {
        await _useLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        var state = _gameService.State;
        if (state.Inventory.MintCount <= 0)
        {
            return new EconomyActionResult(false, state, "No Mint available");
        }

        if (state.Companion.Active is null)
        {
            return new EconomyActionResult(false, state, "Mint can only be used on a Pokemon");
/*
            return Task.FromResult(new EconomyActionResult(false, state, "Mint can only be used on a Pokémon"));
        }
*/
        }

        return await _gameService.MutateStateAsync(current =>
        {
            var active = current.Companion.Active!;
            var newNature = _natureService.RerollDifferent(active.Nature, _randomSource);
            var updatedActive = active with { Nature = newNature };
            var activeCatch = current.ActiveCatch is null ? null : current.ActiveCatch with { Nature = newNature };
            return current with
            {
                Companion = new CompanionState(null, updatedActive),
                ActiveCatch = activeCatch,
                Inventory = current.Inventory with { MintCount = current.Inventory.MintCount - 1 }
            };
        }, "Mint used", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new EconomyActionResult(false, _gameService.State, "Item use could not be saved");
        }
        finally
        {
            _useLock.Release();
        }
    }
}
