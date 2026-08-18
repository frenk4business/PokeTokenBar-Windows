using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Storage;
using PokeTokenBar.Tests.Game;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class CompanionGameServiceTests
{
    [Fact]
    public async Task FirstLaunchEstablishesBaselineWithoutProgress()
    {
        using var context = GameContext.Create();
        context.Usage.EnqueueToday(50_000_000);

        var result = await context.Service.RefreshUsageAndProgressAsync();

        Assert.True(result.BaselineInitialized);
        Assert.Equal(0, result.AppliedDelta);
        Assert.Equal(50_000_000, result.State.ClaimedTodayTokens);
        Assert.Equal(0, result.State.Companion.Egg!.ProgressTokens);
    }

    [Fact]
    public async Task NextRefreshAppliesOnlyNewDeltaAndUnchangedRefreshAppliesZero()
    {
        using var context = GameContext.Create();
        context.Usage.EnqueueToday(50_000_000);
        context.Usage.EnqueueToday(60_000_000);
        context.Usage.EnqueueToday(60_000_000);

        await context.Service.RefreshUsageAndProgressAsync();
        var second = await context.Service.RefreshUsageAndProgressAsync();
        var third = await context.Service.RefreshUsageAndProgressAsync();

        Assert.Equal(10_000_000, second.AppliedDelta);
        Assert.Equal(5_000_000, second.State.Companion.Active!.StageProgressTokens);
        Assert.Equal(0, third.AppliedDelta);
        Assert.Equal(second.State.Companion.Active.StageProgressTokens, third.State.Companion.Active!.StageProgressTokens);
    }

    [Fact]
    public async Task RestartUsesPersistedClaimedTokens()
    {
        using var context = GameContext.Create();
        context.Usage.EnqueueToday(50_000_000);
        context.Usage.EnqueueToday(60_000_000);
        await context.Service.RefreshUsageAndProgressAsync();
        await context.Service.RefreshUsageAndProgressAsync();

        var restartedUsage = new FakeCodexUsageProvider();
        restartedUsage.EnqueueToday(65_000_000);
        var restarted = context.CreateService(restartedUsage, context.Hatch, context.Clock);
        await restarted.InitializeAsync();

        var result = await restarted.RefreshUsageAndProgressAsync();

        Assert.Equal(5_000_000, result.AppliedDelta);
        Assert.Equal(10_000_000, result.State.Companion.Active!.StageProgressTokens);
    }

    [Fact]
    public async Task DayRolloverClaimsNewDayFromZero()
    {
        using var context = GameContext.Create();
        context.Usage.EnqueueToday(180_000_000);
        await context.Service.RefreshUsageAndProgressAsync();
        context.Clock.Now = context.Clock.Now.AddDays(1);
        context.Usage.EnqueueToday(3_000_000);

        var result = await context.Service.RefreshUsageAndProgressAsync();

        Assert.Equal(3_000_000, result.AppliedDelta);
        Assert.Equal(3_000_000, result.State.Companion.Egg!.ProgressTokens);
    }

    [Fact]
    public async Task CounterDecreaseAppliesNoNegativeProgress()
    {
        using var context = GameContext.Create();
        context.Usage.EnqueueToday(50_000_000);
        context.Usage.EnqueueToday(60_000_000);
        context.Usage.EnqueueToday(55_000_000);

        await context.Service.RefreshUsageAndProgressAsync();
        var second = await context.Service.RefreshUsageAndProgressAsync();
        var third = await context.Service.RefreshUsageAndProgressAsync();

        Assert.Equal(10_000_000, second.AppliedDelta);
        Assert.Equal(0, third.AppliedDelta);
        Assert.Equal(second.State.Companion.Active!.StageProgressTokens, third.State.Companion.Active!.StageProgressTokens);
    }

    [Fact]
    public async Task HatchFailurePreservesAllProgressAndRetriesLater()
    {
        using var context = GameContext.Create();
        context.Usage.EnqueueToday(0);
        context.Usage.EnqueueToday(24_000_000);
        await context.Service.RefreshUsageAndProgressAsync();
        context.Hatch.Fail = true;

        var failed = await context.Service.RefreshUsageAndProgressAsync();

        Assert.True(failed.HatchDeferred);
        Assert.Equal(24_000_000, failed.State.Companion.Egg!.ProgressTokens);
        Assert.Equal(24_000_000, failed.State.ClaimedTodayTokens);

        context.Hatch.Fail = false;
        context.Usage.EnqueueToday(24_000_000);
        var retried = await context.Service.RefreshUsageAndProgressAsync();

        Assert.False(retried.HatchDeferred);
        Assert.Equal(19_000_000, retried.State.Companion.Active!.StageProgressTokens);
    }

    [Fact]
    public async Task PersistedActivePokemonDoesNotRerollAfterRestart()
    {
        using var context = GameContext.Create();
        context.Usage.EnqueueToday(0);
        context.Usage.EnqueueToday(GameBalance.EggHatchThreshold + 1);
        await context.Service.RefreshUsageAndProgressAsync();
        var hatched = await context.Service.RefreshUsageAndProgressAsync();

        var restarted = context.CreateService(new FakeCodexUsageProvider(), context.Hatch, context.Clock);
        await restarted.InitializeAsync();

        Assert.Equal(hatched.State.Companion.Active!.PlannedPathSpeciesIds, restarted.State.Companion.Active!.PlannedPathSpeciesIds);
        Assert.Equal(hatched.State.Companion.Active.Nature, restarted.State.Companion.Active.Nature);
        Assert.Equal(hatched.State.Companion.Active.IsShiny, restarted.State.Companion.Active.IsShiny);
    }

    [Fact]
    public async Task SaveFailureDoesNotCommitBaselineInMemory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ptb-game-fail-{Guid.NewGuid():N}");
        var paths = new TestAppPathProvider(root);
        var usage = new FakeCodexUsageProvider();
        usage.EnqueueToday(50_000_000);
        var service = new CompanionGameService(
            usage,
            new FakeAsyncHatchService(),
            new GameStateStore(paths, new FailingJsonStorage(), new NullAppLogger()),
            new FakeClock(DateTimeOffset.UtcNow),
            new NullAppLogger());

        await service.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshUsageAndProgressAsync());

        Assert.False(service.State.InstallBaselineSet);
        Assert.Equal(0, service.State.ClaimedTodayTokens);
    }

    private sealed class GameContext : IDisposable
    {
        private readonly string _root;

        private GameContext(string root, TestAppPathProvider paths, FakeCodexUsageProvider usage, FakeAsyncHatchService hatch, FakeClock clock)
        {
            _root = root;
            Paths = paths;
            Usage = usage;
            Hatch = hatch;
            Clock = clock;
            Service = CreateService(usage, hatch, clock);
            Service.InitializeAsync().GetAwaiter().GetResult();
        }

        public TestAppPathProvider Paths { get; }

        public FakeCodexUsageProvider Usage { get; }

        public FakeAsyncHatchService Hatch { get; }

        public FakeClock Clock { get; }

        public CompanionGameService Service { get; }

        public static GameContext Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"ptb-game-{Guid.NewGuid():N}");
            var hatch = new FakeAsyncHatchService();
            hatch.Enqueue(GameFixtures.BulbasaurHatch());
            hatch.Enqueue(GameFixtures.BulbasaurHatch(shiny: true));
            return new GameContext(root, new TestAppPathProvider(root), new FakeCodexUsageProvider(), hatch, new FakeClock(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero)));
        }

        public CompanionGameService CreateService(FakeCodexUsageProvider usage, FakeAsyncHatchService hatch, FakeClock clock)
        {
            var logger = new FileAppLogger(Paths, clock);
            var storage = new JsonFileStorage(logger);
            return new CompanionGameService(usage, hatch, new GameStateStore(Paths, storage, logger), clock, logger);
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
