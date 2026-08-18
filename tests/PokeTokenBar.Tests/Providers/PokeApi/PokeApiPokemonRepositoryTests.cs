using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.PokeApi;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Providers.PokeApi;

public sealed class PokeApiPokemonRepositoryTests
{
    [Fact]
    public async Task BuildsStartingIndexFromBaseSpeciesOnly()
    {
        using var context = RepositoryContext.Create();

        var index = await context.Repository.GetStartingSpeciesIndexAsync();

        Assert.Equal(new[] { 1, 131, 144 }, index.Entries.Select(entry => entry.Id));
        Assert.DoesNotContain(index.Entries, entry => entry.Id == 2);
        Assert.DoesNotContain(index.Entries, entry => entry.Generation > 5);
        Assert.Equal(Rarity.Legendary, index.Entries.Single(entry => entry.Id == 144).Rarity);
    }

    [Fact]
    public async Task MapsThreeStageAndBranchEvolutionTrees()
    {
        using var context = RepositoryContext.Create();

        var bulbasaur = await context.Repository.GetEvolutionTreeAsync(1);
        var eevee = await context.Repository.GetEvolutionTreeAsync(133);

        Assert.Equal(1, bulbasaur.Species.NationalDexId);
        Assert.Equal(2, bulbasaur.Children.Single().Species.NationalDexId);
        Assert.Equal(3, bulbasaur.Children.Single().Children.Single().Species.NationalDexId);
        Assert.Equal(new[] { 134, 135, 136 }, eevee.Children.Select(child => child.Species.NationalDexId));
    }

    [Fact]
    public async Task DiskCacheAllowsOfflineLoadAfterRestart()
    {
        using var context = RepositoryContext.Create();
        var first = await context.Repository.GetStartingSpeciesIndexAsync();
        Assert.NotEmpty(first.Entries);

        context.Client.Offline = true;
        var restarted = context.CreateRepository();

        var second = await restarted.GetStartingSpeciesIndexAsync();

        Assert.Equal(first.Entries.Count, second.Entries.Count);
    }

    [Fact]
    public async Task OfflineCacheMissFailsGracefully()
    {
        using var context = RepositoryContext.Create();
        context.Client.Offline = true;

        await Assert.ThrowsAsync<PokeApiException>(() => context.Repository.GetStartingSpeciesIndexAsync());
    }

    [Fact]
    public async Task CorruptSpeciesCacheIsRefetched()
    {
        using var context = RepositoryContext.Create();
        Directory.CreateDirectory(Path.Combine(context.Paths.LocalCacheDirectory, "pokeapi", "species"));
        await File.WriteAllTextAsync(Path.Combine(context.Paths.LocalCacheDirectory, "pokeapi", "species", "1.json"), "{broken");

        var species = await context.Repository.GetSpeciesAsync(1);

        Assert.Equal("Bulbasaur", species.Name);
        Assert.True(context.Client.SpeciesRequests > 0);
    }

    private sealed class RepositoryContext : IDisposable
    {
        private readonly string _root;

        private RepositoryContext(string root, FakePokeApiClient client)
        {
            _root = root;
            Client = client;
            Paths = new TestAppPathProvider(root);
            Repository = CreateRepository();
        }

        public FakePokeApiClient Client { get; }

        public TestAppPathProvider Paths { get; }

        public PokeApiPokemonRepository Repository { get; }

        public static RepositoryContext Create()
        {
            var client = new FakePokeApiClient()
                .WithBaseIds(1, 2, 131, 132, 144, 650)
                .WithSpecies(PokeApiTestDtos.Species(1, "bulbasaur", 45, chainId: 1))
                .WithSpecies(PokeApiTestDtos.Species(2, "ivysaur", 45, chainId: 1, evolvesFrom: 1))
                .WithSpecies(PokeApiTestDtos.Species(3, "venusaur", 45, chainId: 1, evolvesFrom: 2))
                .WithSpecies(PokeApiTestDtos.Species(131, "lapras", 45, chainId: 2))
                .WithSpecies(PokeApiTestDtos.Species(132, "ditto", 35, chainId: 3))
                .WithSpecies(PokeApiTestDtos.Species(133, "eevee", 45, chainId: 4))
                .WithSpecies(PokeApiTestDtos.Species(134, "vaporeon", 45, chainId: 4, evolvesFrom: 133))
                .WithSpecies(PokeApiTestDtos.Species(135, "jolteon", 45, chainId: 4, evolvesFrom: 133))
                .WithSpecies(PokeApiTestDtos.Species(136, "flareon", 45, chainId: 4, evolvesFrom: 133))
                .WithSpecies(PokeApiTestDtos.Species(144, "articuno", 3, legendary: true, chainId: 5))
                .WithSpecies(PokeApiTestDtos.Species(650, "chespin", 45, generation: "generation-vi", chainId: 6))
                .WithChain(PokeApiTestDtos.Chain(1, PokeApiTestDtos.Link(1, PokeApiTestDtos.Link(2, PokeApiTestDtos.Link(3)))))
                .WithChain(PokeApiTestDtos.Chain(2, PokeApiTestDtos.Link(131)))
                .WithChain(PokeApiTestDtos.Chain(4, PokeApiTestDtos.Link(133, PokeApiTestDtos.Link(134), PokeApiTestDtos.Link(135), PokeApiTestDtos.Link(136))))
                .WithChain(PokeApiTestDtos.Chain(5, PokeApiTestDtos.Link(144)));

            return new RepositoryContext(Path.Combine(Path.GetTempPath(), $"ptb-pokeapi-{Guid.NewGuid():N}"), client);
        }

        public PokeApiPokemonRepository CreateRepository()
        {
            return new PokeApiPokemonRepository(Client, Paths, new FileAppLogger(Paths, new FakeClock(DateTimeOffset.UtcNow)));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
