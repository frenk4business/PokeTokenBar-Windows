using System.Globalization;
using System.Net.Http;
using PokeTokenBar.Core.Game;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Providers.Codex;
using PokeTokenBar.Services.PokeApi;

namespace PokeTokenBar.Services.Gameplay;

public sealed class CompanionGameService
{
    private readonly ICodexUsageProvider _usageProvider;
    private readonly IAsyncHatchService _hatchService;
    private readonly GameStateStore _store;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly CollectionUpdater _collectionUpdater = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private GameSaveState _state = GameSaveState.New();
    private CodexUsageSnapshot? _lastSnapshot;

    public CompanionGameService(
        ICodexUsageProvider usageProvider,
        IAsyncHatchService hatchService,
        GameStateStore store,
        IClock clock,
        IAppLogger logger)
    {
        _usageProvider = usageProvider;
        _hatchService = hatchService;
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    public GameSaveState State => _state;

    public CodexUsageSnapshot? LastSnapshot => _lastSnapshot;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameplayRefreshResult> RefreshUsageAndProgressAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await _usageProvider.RefreshAsync(cancellationToken).ConfigureAwait(false);
            _lastSnapshot = snapshot;
            return await ApplySnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<GameplayRefreshResult> ApplySnapshotAsync(CodexUsageSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var today = snapshot.Today.TotalTokens;
        var localDate = LocalDateKey(_clock.Now);
        var baselineInitialized = false;

        if (!_state.InstallBaselineSet)
        {
            var next = _state with
            {
                InstallBaselineSet = true,
                ClaimedLocalDate = localDate,
                ClaimedTodayTokens = today,
                LastError = null
            };
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            _state = next;
            await _logger.LogAsync(AppLogLevel.Information, "Gameplay baseline initialized.", cancellationToken: cancellationToken).ConfigureAwait(false);
            return new GameplayRefreshResult(_state, snapshot, 0, Array.Empty<CompanionProgressEvent>(), BaselineInitialized: true, HatchDeferred: false, snapshot.StatusMessage ?? "Baseline initialized");
        }

        var claimState = _state;
        if (_state.ClaimedLocalDate != localDate)
        {
            claimState = claimState with { ClaimedLocalDate = localDate, ClaimedTodayTokens = 0 };
        }

        var delta = Math.Max(0, today - claimState.ClaimedTodayTokens);
        if (today < claimState.ClaimedTodayTokens)
        {
            await _logger.LogAsync(AppLogLevel.Warning, "Codex daily token counter decreased; no negative gameplay progress applied.", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        if (delta == 0)
        {
            _state = claimState;
            if (claimState.Companion.Egg?.ProgressTokens >= GameBalance.EggHatchThreshold)
            {
                return await ApplyProgressTransactionAsync(0, today, snapshot, cancellationToken).ConfigureAwait(false);
            }

            var next = claimState with { ClaimedTodayTokens = Math.Max(claimState.ClaimedTodayTokens, today), LastError = null };
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            _state = next;
            return new GameplayRefreshResult(_state, snapshot, 0, Array.Empty<CompanionProgressEvent>(), baselineInitialized, HatchDeferred: false, snapshot.StatusMessage ?? "No new gameplay tokens");
        }

        _state = claimState;
        return await ApplyProgressTransactionAsync(delta, today, snapshot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameplayRefreshResult> ApplyManualProgressAsync(long delta, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ApplyProgressTransactionAsync(delta, _state.ClaimedTodayTokens, _lastSnapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<GameplayRefreshResult> ApplyProgressTransactionAsync(
        long delta,
        long newClaimedTodayTokens,
        CodexUsageSnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        var workingState = _state;
        var progressToApply = delta;
        HatchResult? resolvedHatch = null;

        if (workingState.Companion.Egg is not null)
        {
            var totalEggProgress = checked(workingState.Companion.Egg.ProgressTokens + delta);
            if (totalEggProgress >= GameBalance.EggHatchThreshold)
            {
                try
                {
                    resolvedHatch = await _hatchService.HatchAsync(new HatchRequest(EggTierFor(workingState.Companion.Egg), workingState.Inventory.HasShinyCharm), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is PokeApiException or HttpRequestException or TaskCanceledException)
                {
                    workingState = workingState with
                    {
                        Companion = new CompanionState(workingState.Companion.Egg with { ProgressTokens = totalEggProgress }, null),
                        ClaimedTodayTokens = Math.Max(workingState.ClaimedTodayTokens, newClaimedTodayTokens),
                        UsedSinceInstall = checked(workingState.UsedSinceInstall + delta),
                        LastError = "Waiting for Pokémon data"
                    };
                    await _store.SaveAsync(workingState, cancellationToken).ConfigureAwait(false);
                    _state = workingState;
                    await _logger.LogAsync(AppLogLevel.Warning, "Hatch deferred because Pokémon metadata is unavailable.", ex, cancellationToken).ConfigureAwait(false);
                    return new GameplayRefreshResult(_state, snapshot, delta, Array.Empty<CompanionProgressEvent>(), BaselineInitialized: false, HatchDeferred: true, "Waiting for Pokémon data");
                }

                var names = new Dictionary<int, string>(workingState.SpeciesNames);
                AddSpeciesNames(names, resolvedHatch);
                progressToApply = totalEggProgress;
                workingState = workingState with { Companion = CompanionState.FreshEgg(), SpeciesNames = names };
            }
        }

        var engine = new ProgressionEngine(
            resolvedHatch is not null
                ? new ResolvedHatchService(resolvedHatch)
                : new NoHatchService(),
            _clock);

        var result = engine.ApplyProgress(workingState.Companion, progressToApply);
        var next = workingState with
        {
            Companion = result.State,
            ClaimedTodayTokens = Math.Max(workingState.ClaimedTodayTokens, newClaimedTodayTokens),
            UsedSinceInstall = checked(workingState.UsedSinceInstall + delta),
            LastGraduation = result.Events.OfType<Graduated>().LastOrDefault()?.Companion ?? workingState.LastGraduation,
            LastError = null
        };
        next = _collectionUpdater.ApplyEvents(next, result.Events, _clock.Now);

        await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
        _state = next;
        await LogEventsAsync(result.Events, cancellationToken).ConfigureAwait(false);
        return new GameplayRefreshResult(_state, snapshot, delta, result.Events, BaselineInitialized: false, HatchDeferred: false, $"Applied {CodexUsageFormatting.Compact(delta)} gameplay tokens");
    }

    private static void AddSpeciesNames(Dictionary<int, string> names, HatchResult hatch)
    {
        foreach (var species in hatch.SelectedPath.Species)
        {
            names[species.NationalDexId] = species.Name;
        }
    }

    public async Task<EconomyActionResult> ApplyBonusProgressAsync(
        long progressTokens,
        Func<GameSaveState, GameSaveState> mutateBeforeSave,
        CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var workingState = mutateBeforeSave(_state);
            var progressToApply = progressTokens;
            HatchResult? resolvedHatch = null;

            if (workingState.Companion.Egg is not null)
            {
                var totalEggProgress = checked(workingState.Companion.Egg.ProgressTokens + progressTokens);
                if (totalEggProgress >= GameBalance.EggHatchThreshold)
                {
                    try
                    {
                        resolvedHatch = await _hatchService.HatchAsync(new HatchRequest(EggTierFor(workingState.Companion.Egg), workingState.Inventory.HasShinyCharm), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is PokeApiException or HttpRequestException or TaskCanceledException)
                    {
                        var deferred = workingState with
                        {
                            Companion = new CompanionState(workingState.Companion.Egg with { ProgressTokens = totalEggProgress }, null),
                            LastError = "Waiting for Pokémon data"
                        };
                        await _store.SaveAsync(deferred, cancellationToken).ConfigureAwait(false);
                        _state = deferred;
                        await _logger.LogAsync(AppLogLevel.Warning, "Bonus progression hatch deferred because Pokémon metadata is unavailable.", ex, cancellationToken).ConfigureAwait(false);
                        return new EconomyActionResult(true, _state, "Pokémon data unavailable — progress preserved", Array.Empty<CompanionProgressEvent>());
                    }

                    var names = new Dictionary<int, string>(workingState.SpeciesNames);
                    AddSpeciesNames(names, resolvedHatch);
                    progressToApply = totalEggProgress;
                    workingState = workingState with { Companion = CompanionState.FreshEgg(), SpeciesNames = names };
                }
            }

            var engine = new ProgressionEngine(resolvedHatch is not null ? new ResolvedHatchService(resolvedHatch) : new NoHatchService(), _clock);
            var result = engine.ApplyProgress(workingState.Companion, progressToApply);
            var next = workingState with
            {
                Companion = result.State,
                LastGraduation = result.Events.OfType<Graduated>().LastOrDefault()?.Companion ?? workingState.LastGraduation,
                LastError = null
            };
            next = _collectionUpdater.ApplyEvents(next, result.Events, _clock.Now);
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            _state = next;
            await LogEventsAsync(result.Events, cancellationToken).ConfigureAwait(false);
            return new EconomyActionResult(true, _state, $"Applied {CodexUsageFormatting.Compact(progressTokens)} bonus progress", result.Events);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<EconomyActionResult> MutateStateAsync(
        Func<GameSaveState, GameSaveState> mutate,
        string successMessage,
        CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var next = mutate(_state);
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            _state = next;
            return new EconomyActionResult(true, _state, successMessage);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static EggTier EggTierFor(EggState egg)
    {
        return egg.GuaranteedMinimumRarity switch
        {
            Rarity.Uncommon => EggTier.Uncommon,
            Rarity.Rare => EggTier.Rare,
            _ => EggTier.Normal
        };
    }

    private async Task LogEventsAsync(IEnumerable<CompanionProgressEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var message = domainEvent switch
            {
                Hatched hatched => $"Pokemon hatched: #{hatched.SpeciesId}.",
                Evolved evolved => $"Pokemon evolved: #{evolved.FromSpeciesId} -> #{evolved.ToSpeciesId}.",
                Graduated graduated => $"Pokemon graduated: #{graduated.Companion.FinalSpeciesId}.",
                _ => null
            };

            if (message is not null)
            {
                await _logger.LogAsync(AppLogLevel.Information, message, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static string LocalDateKey(DateTimeOffset now)
    {
        return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed class NoHatchService : IHatchService
    {
        public HatchResult Hatch(EggState egg, DateTimeOffset hatchTime)
        {
            throw new InvalidOperationException("A hatch was requested without a resolved hatch result.");
        }
    }
}
