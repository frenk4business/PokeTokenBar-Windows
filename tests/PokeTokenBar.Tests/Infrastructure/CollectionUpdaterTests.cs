using System.Text.Json;
using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Storage;
using PokeTokenBar.Tests.Game;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class CollectionUpdaterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HatchMarksSpeciesOwnedAndCreatesActiveLifecycle()
    {
        var state = ActiveState(shiny: false);

        var updated = new CollectionUpdater().ApplyEvents(state, new CompanionProgressEvent[]
        {
            new Hatched(5_000_000, 1, Rarity.Common, PokemonNature.Hardy, IsShiny: false)
        }, Now);

        Assert.True(updated.Pokedex[1].Owned);
        Assert.False(updated.Pokedex[1].ShinyOwned);
        Assert.NotNull(updated.ActiveCatch);
        Assert.Equal(updated.Companion.Active!.IndividualId, updated.ActiveCatch!.IndividualId);
    }

    [Fact]
    public void ShinyHatchSetsShinyOwned()
    {
        var state = ActiveState(shiny: true);

        var updated = new CollectionUpdater().ApplyEvents(state, new CompanionProgressEvent[]
        {
            new Hatched(5_000_000, 1, Rarity.Common, PokemonNature.Hardy, IsShiny: true)
        }, Now);

        Assert.True(updated.Pokedex[1].Owned);
        Assert.True(updated.Pokedex[1].ShinyOwned);
    }

    [Fact]
    public void EvolutionMarksNewSpeciesOwnedAndAppendsHistory()
    {
        var state = new CollectionUpdater().ApplyEvents(ActiveState(shiny: true), new CompanionProgressEvent[]
        {
            new Hatched(5_000_000, 1, Rarity.Common, PokemonNature.Hardy, IsShiny: true)
        }, Now);

        state = state with { Companion = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageIndex: 1, shiny: true) };
        var updated = new CollectionUpdater().ApplyEvents(state, new CompanionProgressEvent[]
        {
            new Evolved(125_000_000, 1, 2, 1)
        }, Now.AddMinutes(5));

        Assert.True(updated.Pokedex[2].Owned);
        Assert.True(updated.Pokedex[2].ShinyOwned);
        Assert.Equal(Now.AddMinutes(5), updated.Pokedex[2].LatestEncounteredAt);
        Assert.Equal(new[] { 1, 2 }, updated.ActiveCatch!.EncounteredSpeciesIds);
        Assert.Equal(2, updated.ActiveCatch.EvolutionHistory.Count);
    }

    [Fact]
    public void RepeatOwnershipDoesNotDuplicatePokedexEntry()
    {
        var updater = new CollectionUpdater();
        var state = ActiveState();
        state = updater.ApplyEvents(state, new CompanionProgressEvent[] { new Hatched(5_000_000, 1, Rarity.Common, PokemonNature.Hardy, false) }, Now);
        state = updater.ApplyEvents(state, new CompanionProgressEvent[] { new Hatched(5_000_000, 1, Rarity.Common, PokemonNature.Hardy, false) }, Now.AddMinutes(1));

        Assert.Single(state.Pokedex);
        Assert.True(state.Pokedex[1].Owned);
    }

    [Fact]
    public void GraduationCreatesOneArchivedCatchLogEntry()
    {
        var updater = new CollectionUpdater();
        var state = updater.ApplyEvents(ActiveState(), new CompanionProgressEvent[]
        {
            new Hatched(5_000_000, 1, Rarity.Common, PokemonNature.Hardy, false)
        }, Now);
        var active = state.Companion.Active!;
        var graduated = new Graduated(375_000_000, new GraduatedCompanion(
            1,
            3,
            new[] { 1, 2, 3 },
            new[] { 1, 2, 3 },
            Rarity.Common,
            PokemonNature.Hardy,
            false,
            active.HatchTime,
            Now.AddHours(1),
            750_000_000,
            active.IndividualId));

        var once = updater.ApplyEvents(state, new CompanionProgressEvent[] { graduated }, Now.AddHours(1));
        var twice = updater.ApplyEvents(once, new CompanionProgressEvent[] { graduated }, Now.AddHours(1));

        Assert.Null(twice.ActiveCatch);
        Assert.Single(twice.CatchLog);
        Assert.Equal(CompanionLifecycleStatus.Graduated, twice.CatchLog.Single().Status);
    }

    [Fact]
    public void CatchLogSortsNewestFirstByGraduation()
    {
        var oldEntry = Entry("old", Now);
        var newEntry = Entry("new", Now.AddDays(1));
        var sorted = new[] { oldEntry, newEntry }.OrderByDescending(entry => entry.GraduationTime ?? entry.HatchTime).ToArray();

        Assert.Equal("new", sorted[0].IndividualId);
    }

    [Fact]
    public async Task VersionOneSaveMigratesToVersionTwoWithoutResettingBaseline()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ptb-migrate-{Guid.NewGuid():N}");
        try
        {
            var paths = new TestAppPathProvider(root);
            Directory.CreateDirectory(paths.RoamingStateDirectory);
            var logger = new FileAppLogger(paths, new FakeClock(Now));
            var storage = new JsonFileStorage(logger);
            var store = new GameStateStore(paths, storage, logger);
            var activeState = ActiveState(shiny: true) with
            {
                SchemaVersion = 1,
                InstallBaselineSet = true,
                ClaimedLocalDate = "2026-08-18",
                ClaimedTodayTokens = 123,
                UsedSinceInstall = 456
            };
            var json = JsonSerializer.Serialize(activeState, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            await File.WriteAllTextAsync(store.StatePath, json);

            var migrated = await store.LoadAsync();

            Assert.Equal(GameSaveState.CurrentSchemaVersion, migrated.SchemaVersion);
            Assert.True(migrated.InstallBaselineSet);
            Assert.Equal(123, migrated.ClaimedTodayTokens);
            Assert.Equal(456, migrated.UsedSinceInstall);
            Assert.NotNull(migrated.ActiveCatch);
            Assert.Equal(migrated.Companion.Active!.IndividualId, migrated.ActiveCatch!.IndividualId);
            Assert.True(migrated.Pokedex[1].ShinyOwned);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static GameSaveState ActiveState(bool shiny = false)
    {
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, shiny: shiny);
        return GameSaveState.New() with
        {
            Companion = state,
            SpeciesNames = new Dictionary<int, string>
            {
                [1] = "Bulbasaur",
                [2] = "Ivysaur",
                [3] = "Venusaur"
            }
        };
    }

    private static CatchLogEntry Entry(string id, DateTimeOffset graduation)
    {
        return new CatchLogEntry
        {
            IndividualId = id,
            BaseSpeciesId = 1,
            FinalSpeciesId = 3,
            Rarity = Rarity.Common,
            Nature = PokemonNature.Hardy,
            HatchTime = graduation.AddDays(-1),
            GraduationTime = graduation,
            Status = CompanionLifecycleStatus.Graduated
        };
    }
}
