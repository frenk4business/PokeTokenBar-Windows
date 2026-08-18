using PokeTokenBar.Core.Game;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Game;

public sealed class NatureServiceTests
{
    [Fact]
    public void ContainsAllTwentyFiveNatures()
    {
        Assert.Equal(25, Enum.GetValues<PokemonNature>().Length);
    }

    [Fact]
    public void SelectNatureUsesInjectedRandomSource()
    {
        var service = new NatureService();

        var nature = service.SelectNature(new TestRandomSource(13));

        Assert.Equal(PokemonNature.Jolly, nature);
    }

    [Fact]
    public void MintRerollExcludesCurrentNature()
    {
        var service = new NatureService();

        var nature = service.RerollDifferent(PokemonNature.Hardy, new TestRandomSource(0));

        Assert.NotEqual(PokemonNature.Hardy, nature);
        Assert.Equal(PokemonNature.Lonely, nature);
    }
}
