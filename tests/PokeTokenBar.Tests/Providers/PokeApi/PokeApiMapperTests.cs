using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.PokeApi;

namespace PokeTokenBar.Tests.Providers.PokeApi;

public sealed class PokeApiMapperTests
{
    [Fact]
    public void SpeciesDtoMapsToDomainSpecies()
    {
        var dto = PokeApiTestDtos.Species(1, "bulbasaur", captureRate: 45, generation: "generation-i");

        var species = PokeApiMapper.ToDomainSpecies(dto);

        Assert.Equal(1, species.NationalDexId);
        Assert.Equal("Bulbasaur", species.Name);
        Assert.Equal(1, species.Generation);
        Assert.Equal(Rarity.Rare, species.Rarity);
    }

    [Fact]
    public void LegendaryAndMythicalMapToLegendaryRarity()
    {
        Assert.Equal(Rarity.Legendary, PokeApiMapper.ToDomainSpecies(PokeApiTestDtos.Species(144, "articuno", legendary: true)).Rarity);
        Assert.Equal(Rarity.Legendary, PokeApiMapper.ToDomainSpecies(PokeApiTestDtos.Species(151, "mew", mythical: true)).Rarity);
    }

    [Fact]
    public void EligibilityRequiresBaseGenOneToFiveAndNotDitto()
    {
        Assert.True(PokeApiMapper.IsEligibleBase(PokeApiTestDtos.Species(1, "bulbasaur")));
        Assert.False(PokeApiMapper.IsEligibleBase(PokeApiTestDtos.Species(2, "ivysaur", evolvesFrom: 1)));
        Assert.False(PokeApiMapper.IsEligibleBase(PokeApiTestDtos.Species(650, "chespin", generation: "generation-vi")));
        Assert.False(PokeApiMapper.IsEligibleBase(PokeApiTestDtos.Species(132, "ditto")));
    }
}
