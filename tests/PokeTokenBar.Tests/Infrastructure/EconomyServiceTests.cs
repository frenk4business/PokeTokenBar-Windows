using System.Text.Json;
using PokeTokenBar.Core.Game;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Storage;
using PokeTokenBar.Tests.Game;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class EconomyServiceTests
{
    [Fact]
    public async Task BaselineStartsWithZeroWalletAndNewTokensIncreaseBalance()
    {
        using var context = EconomyContext.Create();
        context.Usage.EnqueueToday(50_000_000);
        context.Usage.EnqueueToday(60_000_000);

        await context.Game.RefreshUsageAndProgressAsync();
        Assert.Equal(0, context.Game.State.AvailableBalance);

        await context.Game.RefreshUsageAndProgressAsync();

        Assert.Equal(10_000_000, context.Game.State.UsedSinceInstall);
        Assert.Equal(10_000_000, context.Game.State.AvailableBalance);
    }

    [Fact]
    public async Task SpendingReducesBalanceButDoesNotReduceProgressOrUsedSinceInstall()
    {
        using var context = EconomyContext.Create();
        await context.SeedActiveAsync(EconomyCatalog.ShinyCharmPrice, stageProgress: 42_000_000);

        var beforeProgress = context.Game.State.Companion.Active!.StageProgressTokens;
        var result = await context.Shop.BuyAsync(ShopItemKind.ShinyCharm);

        Assert.True(result.Success);
        Assert.Equal(EconomyCatalog.ShinyCharmPrice, context.Game.State.UsedSinceInstall);
        Assert.Equal(EconomyCatalog.ShinyCharmPrice, context.Game.State.SpentTokens);
        Assert.Equal(0, context.Game.State.AvailableBalance);
        Assert.Equal(beforeProgress, context.Game.State.Companion.Active!.StageProgressTokens);
    }

    [Fact]
    public async Task RareCandyPurchaseAndUseAppliesBonusProgressWithoutChangingUsageLedger()
    {
        using var context = EconomyContext.Create();
        await context.SeedEggAsync(EconomyCatalog.RareCandyPrice);
        await context.Shop.BuyAsync(ShopItemKind.RareCandy);
        var usedSinceInstall = context.Game.State.UsedSinceInstall;

        var result = await context.Bag.UseRareCandyAsync();

        Assert.True(result.Success);
        Assert.Equal(0, context.Game.State.Inventory.RareCandyCount);
        Assert.Equal(usedSinceInstall, context.Game.State.UsedSinceInstall);
        Assert.Equal(95_000_000, context.Game.State.Companion.Active!.StageProgressTokens);
    }

    [Fact]
    public async Task RareCandyFailurePreservesBonusProgressForLaterHatchRetry()
    {
        using var context = EconomyContext.Create();
        await context.SeedEggAsync(0, inventory: new InventoryState { RareCandyCount = 1 });
        context.Hatch.Fail = true;

        var failed = await context.Bag.UseRareCandyAsync();

        Assert.True(failed.Success);
        Assert.Equal(0, context.Game.State.Inventory.RareCandyCount);
        Assert.Equal(GameBalance.RareCandyProgress, context.Game.State.Companion.Egg!.ProgressTokens);
        Assert.NotNull(context.Game.State.LastError);

        context.Hatch.Fail = false;
        await context.Game.ApplyManualProgressAsync(0);

        Assert.Equal(95_000_000, context.Game.State.Companion.Active!.StageProgressTokens);
    }

    [Fact]
    public async Task MintCannotBeUsedOnEggAndRerollsActiveNatureWithoutChangingIndividualId()
    {
        using var eggContext = EconomyContext.Create();
        await eggContext.SeedEggAsync(0, inventory: new InventoryState { MintCount = 1 });

        var rejected = await eggContext.Bag.UseMintAsync();

        Assert.False(rejected.Success);
        Assert.Equal(1, eggContext.Game.State.Inventory.MintCount);

        using var activeContext = EconomyContext.Create(random: new TestRandomSource(0));
        await activeContext.SeedActiveAsync(0, inventory: new InventoryState { MintCount = 1 });
        var oldId = activeContext.Game.State.Companion.Active!.IndividualId;

        var used = await activeContext.Bag.UseMintAsync();

        Assert.True(used.Success);
        Assert.Equal(0, activeContext.Game.State.Inventory.MintCount);
        Assert.Equal(oldId, activeContext.Game.State.Companion.Active!.IndividualId);
        Assert.Equal(PokemonNature.Lonely, activeContext.Game.State.Companion.Active.Nature);
        Assert.Equal(PokemonNature.Lonely, activeContext.Game.State.ActiveCatch!.Nature);
    }

    [Fact]
    public async Task ShinyCharmCanOnlyBePurchasedOnceAndFutureHatchUsesCharmOdds()
    {
        using var context = EconomyContext.Create();
        await context.SeedEggAsync(EconomyCatalog.ShinyCharmPrice);

        var bought = await context.Shop.BuyAsync(ShopItemKind.ShinyCharm);
        var duplicate = await context.Shop.BuyAsync(ShopItemKind.ShinyCharm);
        await context.Game.ApplyManualProgressAsync(GameBalance.EggHatchThreshold);

        Assert.True(bought.Success);
        Assert.False(duplicate.Success);
        Assert.True(context.Game.State.Inventory.HasShinyCharm);
        Assert.True(context.Hatch.LastRequest!.ShinyCharmActive);
    }

    [Fact]
    public async Task EggPurchaseRequiresConfirmationAndDiscardsActiveCompanion()
    {
        using var context = EconomyContext.Create();
        await context.SeedActiveAsync(EconomyCatalog.RareEggPrice, shiny: true);
        var individualId = context.Game.State.Companion.Active!.IndividualId;

        var warning = await context.Shop.BuyAsync(ShopItemKind.RareEgg);

        Assert.False(warning.Success);
        Assert.True(warning.RequiresConfirmation);
        Assert.True(warning.ShinyDiscardWarning);
        Assert.NotNull(context.Game.State.Companion.Active);

        var bought = await context.Shop.BuyAsync(ShopItemKind.RareEgg, confirmedDestructiveEggPurchase: true);

        Assert.True(bought.Success);
        Assert.Null(context.Game.State.Companion.Active);
        Assert.Equal(Rarity.Rare, context.Game.State.Companion.Egg!.GuaranteedMinimumRarity);
        Assert.Equal(EconomyCatalog.RareEggPrice, context.Game.State.SpentTokens);
        var discarded = Assert.Single(context.Game.State.CatchLog);
        Assert.Equal(individualId, discarded.IndividualId);
        Assert.Equal(CompanionLifecycleStatus.Discarded, discarded.Status);
        Assert.True(discarded.IsShiny);
    }

    [Fact]
    public async Task PurchasedRareEggTierPersistsAndIsSentToHatchServiceThenClears()
    {
        using var context = EconomyContext.Create();
        context.Hatch.Enqueue(GameFixtures.LaprasHatch());
        await context.SeedEggAsync(EconomyCatalog.RareEggPrice);
        await context.Shop.BuyAsync(ShopItemKind.RareEgg, confirmedDestructiveEggPurchase: true);

        var restarted = context.Restart();
        restarted.Hatch.Enqueue(GameFixtures.LaprasHatch());
        await restarted.Game.ApplyManualProgressAsync(GameBalance.EggHatchThreshold);

        Assert.Equal(EggTier.Rare, restarted.Hatch.LastRequest!.Tier);
        Assert.Equal(Rarity.Rare, restarted.Game.State.Companion.Active!.Rarity);
        Assert.Null(restarted.Game.State.Companion.Egg);
    }

    [Fact]
    public async Task GraduationAfterPurchasedEggCreatesNormalFreshEgg()
    {
        using var context = EconomyContext.Create();
        context.Hatch.Enqueue(GameFixtures.LaprasHatch());
        await context.SeedEggAsync(EconomyCatalog.RareEggPrice);
        await context.Shop.BuyAsync(ShopItemKind.RareEgg, confirmedDestructiveEggPurchase: true);

        await context.Game.ApplyManualProgressAsync(GameBalance.EggHatchThreshold + GameBalance.GraduationTotal(Rarity.Rare));

        Assert.NotNull(context.Game.State.Companion.Egg);
        Assert.Null(context.Game.State.Companion.Egg!.GuaranteedMinimumRarity);
    }

    [Fact]
    public async Task InsufficientFundsAndDoublePurchaseAreProtected()
    {
        using var context = EconomyContext.Create();
        await context.SeedEggAsync(EconomyCatalog.RareCandyPrice);

        var expensive = await context.Shop.BuyAsync(ShopItemKind.RareEgg);
        var first = context.Shop.BuyAsync(ShopItemKind.RareCandy);
        var second = context.Shop.BuyAsync(ShopItemKind.RareCandy);
        var results = await Task.WhenAll(first, second);

        Assert.False(expensive.Success);
        Assert.Single(results.Where(result => result.Success));
        Assert.Equal(EconomyCatalog.RareCandyPrice, context.Game.State.SpentTokens);
        Assert.Equal(1, context.Game.State.Inventory.RareCandyCount);
        Assert.Equal(0, context.Game.State.AvailableBalance);
    }

    [Fact]
    public async Task SaveFailureDoesNotCompletePurchase()
    {
        var state = GameSaveState.New() with { UsedSinceInstall = EconomyCatalog.RareCandyPrice };
        var service = EconomyContext.CreateWithStorage(new LoadSeedFailSaveStorage(state));
        var shop = new ShopService(service);

        var result = await shop.BuyAsync(ShopItemKind.RareCandy);

        Assert.False(result.Success);
        Assert.Equal(0, service.State.SpentTokens);
        Assert.Equal(0, service.State.Inventory.RareCandyCount);
    }

    [Fact]
    public async Task VersionTwoSaveMigratesToVersionThreeInventoryDefaults()
    {
        using var context = EconomyContext.Create();
        var active = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, shiny: true);
        var v2 = GameSaveState.New() with
        {
            SchemaVersion = 2,
            Companion = active,
            UsedSinceInstall = 123,
            ClaimedTodayTokens = 456,
            Inventory = null!
        };
        Directory.CreateDirectory(context.Paths.RoamingStateDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(context.Paths.RoamingStateDirectory, "state.json"),
            JsonSerializer.Serialize(v2, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var restarted = context.Restart();

        Assert.Equal(GameSaveState.CurrentSchemaVersion, restarted.Game.State.SchemaVersion);
        Assert.NotNull(restarted.Game.State.Inventory);
        Assert.Equal(0, restarted.Game.State.Inventory.RareCandyCount);
        Assert.False(restarted.Game.State.Inventory.HasShinyCharm);
        Assert.Equal(active.Active!.IndividualId, restarted.Game.State.Companion.Active!.IndividualId);
        Assert.Equal(123, restarted.Game.State.UsedSinceInstall);
        Assert.Equal(456, restarted.Game.State.ClaimedTodayTokens);
    }

    [Fact]
    public async Task FullEconomyScenarioPersistsAcrossRestart()
    {
        using var context = EconomyContext.Create(random: new TestRandomSource(0));
        context.Usage.EnqueueToday(0);
        await context.Game.RefreshUsageAndProgressAsync();
        await context.SeedActiveAsync(9_000_000_000);
        var individualId = context.Game.State.Companion.Active!.IndividualId;

        await context.Shop.BuyAsync(ShopItemKind.Mint);
        await context.Bag.UseMintAsync();
        await context.Shop.BuyAsync(ShopItemKind.RareCandy);
        await context.Bag.UseRareCandyAsync();
        await context.Shop.BuyAsync(ShopItemKind.ShinyCharm);
        var rareEggWarning = await context.Shop.BuyAsync(ShopItemKind.RareEgg);
        Assert.True(rareEggWarning.RequiresConfirmation);
        context.Hatch.Clear();
        context.Hatch.Enqueue(GameFixtures.LaprasHatch());
        await context.Shop.BuyAsync(ShopItemKind.RareEgg, confirmedDestructiveEggPurchase: true);
        await context.Game.ApplyManualProgressAsync(GameBalance.EggHatchThreshold);

        var restarted = context.Restart();

        Assert.True(restarted.Game.State.InstallBaselineSet);
        Assert.Equal(7_600_000_000, restarted.Game.State.SpentTokens);
        Assert.Equal(1_405_000_000, restarted.Game.State.AvailableBalance);
        Assert.True(restarted.Game.State.Inventory.HasShinyCharm);
        Assert.Equal(Rarity.Rare, restarted.Game.State.Companion.Active!.Rarity);
        Assert.Contains(restarted.Game.State.CatchLog, entry => entry.IndividualId == individualId && entry.Status == CompanionLifecycleStatus.Discarded);
    }

    private sealed class EconomyContext : IDisposable
    {
        private readonly string _root;

        private EconomyContext(string root, TestAppPathProvider paths, FakeCodexUsageProvider usage, FakeAsyncHatchService hatch, FakeClock clock, IRandomSource random)
        {
            _root = root;
            Paths = paths;
            Usage = usage;
            Hatch = hatch;
            Clock = clock;
            Game = CreateGameService(usage, hatch, clock);
            Game.InitializeAsync().GetAwaiter().GetResult();
            Shop = new ShopService(Game);
            Bag = new BagService(Game, new NatureService(), random);
        }

        public TestAppPathProvider Paths { get; }

        public FakeCodexUsageProvider Usage { get; }

        public FakeAsyncHatchService Hatch { get; }

        public FakeClock Clock { get; }

        public CompanionGameService Game { get; }

        public ShopService Shop { get; }

        public BagService Bag { get; }

        public static EconomyContext Create(IRandomSource? random = null)
        {
            var root = Path.Combine(Path.GetTempPath(), $"ptb-economy-{Guid.NewGuid():N}");
            var hatch = new FakeAsyncHatchService();
            hatch.Enqueue(GameFixtures.BulbasaurHatch());
            hatch.Enqueue(GameFixtures.LaprasHatch());
            return new EconomyContext(root, new TestAppPathProvider(root), new FakeCodexUsageProvider(), hatch, new FakeClock(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero)), random ?? new TestRandomSource(0));
        }

        public static CompanionGameService CreateWithStorage(IJsonFileStorage storage)
        {
            var root = Path.Combine(Path.GetTempPath(), $"ptb-economy-fail-{Guid.NewGuid():N}");
            var paths = new TestAppPathProvider(root);
            var service = new CompanionGameService(new FakeCodexUsageProvider(), new FakeAsyncHatchService(), new GameStateStore(paths, storage, new NullAppLogger()), new FakeClock(DateTimeOffset.UtcNow), new NullAppLogger());
            service.InitializeAsync().GetAwaiter().GetResult();
            return service;
        }

        public async Task SeedEggAsync(long usedSinceInstall, InventoryState? inventory = null)
        {
            await Game.MutateStateAsync(state => state with
            {
                UsedSinceInstall = usedSinceInstall,
                SpentTokens = 0,
                Inventory = inventory ?? new InventoryState(),
                Companion = CompanionState.FreshEgg(),
                LastError = null
            }, "seed");
        }

        public async Task SeedActiveAsync(long usedSinceInstall, long stageProgress = 0, bool shiny = false, InventoryState? inventory = null)
        {
            var active = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageProgress: stageProgress, shiny: shiny);
            await Game.MutateStateAsync(state =>
            {
                var named = state with
                {
                    UsedSinceInstall = usedSinceInstall,
                    SpentTokens = 0,
                    Inventory = inventory ?? new InventoryState(),
                    Companion = active,
                    SpeciesNames = new Dictionary<int, string>
                    {
                        [1] = "Bulbasaur",
                        [2] = "Ivysaur",
                        [3] = "Venusaur"
                    },
                    LastError = null
                };
                return new CollectionUpdater().EnsureActiveLifecycle(named, Clock.Now);
            }, "seed");
        }

        public EconomyContext Restart()
        {
            return new EconomyContext(_root, Paths, new FakeCodexUsageProvider(), new FakeAsyncHatchService(), Clock, new TestRandomSource(0));
        }

        private CompanionGameService CreateGameService(FakeCodexUsageProvider usage, FakeAsyncHatchService hatch, FakeClock clock)
        {
            var logger = new FileAppLogger(Paths, clock);
            return new CompanionGameService(usage, hatch, new GameStateStore(Paths, new JsonFileStorage(logger), logger), clock, logger);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private sealed class LoadSeedFailSaveStorage : IJsonFileStorage
    {
        private readonly GameSaveState _state;

        public LoadSeedFailSaveStorage(GameSaveState state)
        {
            _state = state;
        }

        public Task<T> LoadOrDefaultAsync<T>(string path, T defaultValue, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((T)(object)_state);
        }

        public Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("save failed");
        }
    }
}
