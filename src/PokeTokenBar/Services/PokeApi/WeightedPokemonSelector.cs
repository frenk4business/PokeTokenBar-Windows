using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.PokeApi;

public sealed class WeightedPokemonSelector
{
    public BaseSpeciesEntry Select(
        IReadOnlyList<BaseSpeciesEntry> candidates,
        EggTier tier,
        IRandomSource randomSource,
        IReadOnlySet<string>? collectedFinalKeys = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(randomSource);

        var pool = candidates.Where(candidate => candidate.EligibleAsStart && tier.Allows(candidate.Rarity)).ToArray();
        if (pool.Length == 0)
        {
            throw new PokeApiException($"No eligible Pokémon candidates exist for {tier} egg.");
        }

        var weights = pool.Select(candidate => Weight(candidate, collectedFinalKeys)).ToArray();
        var total = weights.Sum();
        var roll = (long)(randomSource.NextUInt64() % (ulong)total);
        for (var i = 0; i < pool.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                return pool[i];
            }
        }

        return pool[^1];
    }

    public static long Weight(BaseSpeciesEntry candidate, IReadOnlySet<string>? collectedFinalKeys = null)
    {
        var alreadyCollected = collectedFinalKeys?.Any(key => key.StartsWith($"{candidate.Id}:", StringComparison.Ordinal)) == true;
        var captureRate = Math.Max(1, candidate.CaptureRate);
        return alreadyCollected ? Math.Max(1, captureRate / 2) : captureRate;
    }
}
