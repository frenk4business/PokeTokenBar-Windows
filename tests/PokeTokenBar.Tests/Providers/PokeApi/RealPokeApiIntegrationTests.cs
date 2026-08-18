using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.PokeApi;
using PokeTokenBar.Services.Sprites;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Providers.PokeApi;

public sealed class RealPokeApiIntegrationTests
{
    [Fact]
    public async Task RealPokeApiKnownSpeciesValidation()
    {
        if (Environment.GetEnvironmentVariable("POKETOKENBAR_RUN_POKEAPI_TESTS") != "1")
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"ptb-real-pokeapi-{Guid.NewGuid():N}");
        try
        {
            var paths = new TestAppPathProvider(root);
            var logger = new FileAppLogger(paths, new FakeClock(DateTimeOffset.UtcNow));
            using var httpClient = new HttpClient();
            var api = new PokeApiHttpClient(httpClient);
            var repository = new PokeApiPokemonRepository(api, paths, logger);
            var sprites = new PokemonSpriteService(httpClient, paths, logger);

            var index = await repository.GetStartingSpeciesIndexAsync();
            Assert.True(index.Entries.Count >= 300);

            await AssertKnownAsync(repository, sprites, 1, Rarity.Rare, 1, minPathLength: 3);
            await AssertKnownAsync(repository, sprites, 133, Rarity.Rare, 1, minPathLength: 2);
            await AssertKnownAsync(repository, sprites, 131, Rarity.Rare, 1, minPathLength: 1);
            await AssertKnownAsync(repository, sprites, 144, Rarity.Legendary, 1, minPathLength: 1);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task AssertKnownAsync(
        IPokemonDataRepository repository,
        PokemonSpriteService sprites,
        int id,
        Rarity expectedRarity,
        int expectedGeneration,
        int minPathLength)
    {
        var species = await repository.GetSpeciesAsync(id);
        var tree = await repository.GetEvolutionTreeAsync(id);
        var sprite = await sprites.GetPokemonSpriteAsync(id);

        Assert.Equal(expectedGeneration, species.Generation);
        Assert.Equal(expectedRarity, species.Rarity);
        Assert.True(tree.Species.NationalDexId == id);
        Assert.True(tree.Depth() >= minPathLength);
        Assert.NotNull(sprite);
    }
}

internal static class EvolutionNodeTestExtensions
{
    public static int Depth(this EvolutionNode node)
    {
        return 1 + (node.Children.Select(Depth).DefaultIfEmpty(0).Max());
    }
}
