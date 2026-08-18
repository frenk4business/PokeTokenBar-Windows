using PokeTokenBar.Core.Game;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Game;

public sealed class EvolutionPathSelectorTests
{
    [Fact]
    public void SelectsOneBranchAtHatchTime()
    {
        var tree = new EvolutionNode(GameFixtures.Eevee, new[]
        {
            new EvolutionNode(GameFixtures.Vaporeon),
            new EvolutionNode(GameFixtures.Jolteon),
            new EvolutionNode(GameFixtures.Flareon)
        });

        var path = new EvolutionPathSelector().SelectPath(tree, new TestRandomSource(1));

        Assert.Equal(new[] { 133, 135 }, path.SpeciesIds);
    }
}
