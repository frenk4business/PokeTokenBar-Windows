using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.PokeApi;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Providers.PokeApi;

public sealed class WeightedPokemonSelectorTests
{
    private static readonly BaseSpeciesEntry Common = Entry(10, Rarity.Common, 255);
    private static readonly BaseSpeciesEntry Uncommon = Entry(20, Rarity.Uncommon, 120);
    private static readonly BaseSpeciesEntry Rare = Entry(30, Rarity.Rare, 45);
    private static readonly BaseSpeciesEntry Legendary = Entry(40, Rarity.Legendary, 3);

    [Fact]
    public void NormalEggPermitsAllRarities()
    {
        var selected = new WeightedPokemonSelector().Select(new[] { Common, Uncommon, Rare, Legendary }, EggTier.Normal, new FixedUIntRandomSource(255 + 120 + 45 + 2));

        Assert.Equal(Legendary.Id, selected.Id);
    }

    [Fact]
    public void UncommonEggExcludesCommon()
    {
        var selected = new WeightedPokemonSelector().Select(new[] { Common, Uncommon, Rare }, EggTier.Uncommon, new FixedUIntRandomSource(0));

        Assert.NotEqual(Common.Id, selected.Id);
        Assert.Equal(Uncommon.Id, selected.Id);
    }

    [Fact]
    public void RareEggExcludesCommonAndUncommonButKeepsLegendary()
    {
        var selected = new WeightedPokemonSelector().Select(new[] { Common, Uncommon, Rare, Legendary }, EggTier.Rare, new FixedUIntRandomSource(45));

        Assert.Equal(Legendary.Id, selected.Id);
    }

    [Fact]
    public void WeightUsesCaptureRateAndHalvesCollectedBase()
    {
        Assert.Equal(255, WeightedPokemonSelector.Weight(Common));
        Assert.Equal(127, WeightedPokemonSelector.Weight(Common, new HashSet<string> { "10:99" }));
    }

    [Fact]
    public void DeterministicSelectionUsesCumulativeWeights()
    {
        var selected = new WeightedPokemonSelector().Select(new[] { Common, Rare }, EggTier.Normal, new FixedUIntRandomSource(255));

        Assert.Equal(Rare.Id, selected.Id);
    }

    private static BaseSpeciesEntry Entry(int id, Rarity rarity, int captureRate)
    {
        return new BaseSpeciesEntry(id, $"Species {id}", 1, captureRate, rarity == Rarity.Legendary, false, rarity, 1);
    }

    private sealed class FixedUIntRandomSource : IRandomSource
    {
        private readonly ulong _value;

        public FixedUIntRandomSource(ulong value)
        {
            _value = value;
        }

        public int NextInt32(int exclusiveMax) => 0;

        public ulong NextUInt64() => _value;
    }
}
