using PokeTokenBar.Core.Game;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.PokeApi;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Providers.PokeApi;

public sealed class PokeApiHatchServiceTests
{
    [Fact]
    public async Task HatchReturnsBaseSpeciesPathNatureShinyAndRarity()
    {
        using var context = HatchContext.Create(new HatchRandomSource(selectRoll: 0, pathChoices: new[] { 0, 0 }, natureIndex: 13, shinyRoll: 0));

        var result = await context.HatchService.HatchAsync(new HatchRequest(ShinyCharmActive: false));

        Assert.Equal(1, result.BaseSpecies.NationalDexId);
        Assert.Equal(new[] { 1, 2, 3 }, result.SelectedPath.SpeciesIds);
        Assert.Equal(Rarity.Common, result.Rarity);
        Assert.Equal(PokemonNature.Jolly, result.Nature);
        Assert.True(result.IsShiny);
    }

    [Fact]
    public async Task GuaranteedRareTierIsHonoredAndLegendaryRemainsEligible()
    {
        using var context = HatchContext.Create(new HatchRandomSource(selectRoll: 45, pathChoices: Array.Empty<int>(), natureIndex: 0, shinyRoll: 1));

        var result = await context.HatchService.HatchAsync(new HatchRequest(EggTier.Rare));

        Assert.Equal(144, result.BaseSpecies.NationalDexId);
        Assert.Equal(Rarity.Legendary, result.Rarity);
    }

    private sealed class HatchContext : IDisposable
    {
        private readonly string _root;

        private HatchContext(string root, PokeApiHatchService hatchService)
        {
            _root = root;
            HatchService = hatchService;
        }

        public PokeApiHatchService HatchService { get; }

        public static HatchContext Create(IRandomSource randomSource)
        {
            var root = Path.Combine(Path.GetTempPath(), $"ptb-hatch-{Guid.NewGuid():N}");
            var paths = new TestAppPathProvider(root);
            var logger = new FileAppLogger(paths, new FakeClock(DateTimeOffset.UtcNow));
            var client = new FakePokeApiClient()
                .WithBaseIds(1, 144)
                .WithSpecies(PokeApiTestDtos.Species(1, "bulbasaur", 255, chainId: 1))
                .WithSpecies(PokeApiTestDtos.Species(2, "ivysaur", 255, chainId: 1, evolvesFrom: 1))
                .WithSpecies(PokeApiTestDtos.Species(3, "venusaur", 255, chainId: 1, evolvesFrom: 2))
                .WithSpecies(PokeApiTestDtos.Species(144, "articuno", 3, legendary: true, chainId: 5))
                .WithChain(PokeApiTestDtos.Chain(1, PokeApiTestDtos.Link(1, PokeApiTestDtos.Link(2, PokeApiTestDtos.Link(3)))))
                .WithChain(PokeApiTestDtos.Chain(5, PokeApiTestDtos.Link(144)));

            var repository = new PokeApiPokemonRepository(client, paths, logger);
            var service = new PokeApiHatchService(repository, new WeightedPokemonSelector(), new EvolutionPathSelector(), new NatureService(), new ShinyRoller(), randomSource);
            return new HatchContext(root, service);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private sealed class HatchRandomSource : IRandomSource
    {
        private readonly Queue<int> _ints;
        private readonly ulong _shinyRoll;
        private int _uintCalls;

        public HatchRandomSource(ulong selectRoll, IReadOnlyList<int> pathChoices, int natureIndex, ulong shinyRoll)
        {
            _ints = new Queue<int>(pathChoices.Concat(new[] { natureIndex }));
            SelectRoll = selectRoll;
            _shinyRoll = shinyRoll;
        }

        private ulong SelectRoll { get; }

        public int NextInt32(int exclusiveMax) => _ints.Count > 0 ? _ints.Dequeue() % exclusiveMax : 0;

        public ulong NextUInt64()
        {
            _uintCalls++;
            return _uintCalls == 1 ? SelectRoll : _shinyRoll;
        }
    }
}
