using PokeTokenBar.Services.Gameplay;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class PokedexQueryTests
{
    [Fact]
    public void SearchMatchesName()
    {
        Assert.True(PokedexQuery.Passes(25, "Pikachu", "Common", "Gen 1", owned: true, shinyOwned: false, new PokedexFilter(SearchText: "pika")));
    }

    [Fact]
    public void SearchMatchesDexNumber()
    {
        Assert.True(PokedexQuery.Passes(25, "Pikachu", "Common", "Gen 1", owned: true, shinyOwned: false, new PokedexFilter(SearchText: "25")));
    }

    [Fact]
    public void GenerationFilterExcludesOtherGenerations()
    {
        Assert.False(PokedexQuery.Passes(25, "Pikachu", "Common", "Gen 1", owned: true, shinyOwned: false, new PokedexFilter(Generation: "Gen 2")));
    }

    [Fact]
    public void RarityFilterExcludesOtherRarities()
    {
        Assert.False(PokedexQuery.Passes(25, "Pikachu", "Common", "Gen 1", owned: true, shinyOwned: false, new PokedexFilter(Rarity: "Rare")));
    }

    [Theory]
    [InlineData("Owned", true, false, true)]
    [InlineData("Missing", false, false, true)]
    [InlineData("Shiny owned", true, true, true)]
    [InlineData("Shiny owned", true, false, false)]
    public void OwnershipFiltersWork(string filter, bool owned, bool shiny, bool expected)
    {
        Assert.Equal(expected, PokedexQuery.Passes(25, "Pikachu", "Common", "Gen 1", owned, shiny, new PokedexFilter(Ownership: filter)));
    }
}
