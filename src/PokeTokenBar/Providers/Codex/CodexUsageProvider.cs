using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Storage;

namespace PokeTokenBar.Providers.Codex;

public sealed class CodexUsageProvider : ICodexUsageProvider
{
    private const string IndexFileName = "codex-usage-index.json";
    private static readonly TimeSpan ForkReplayMaximumGap = TimeSpan.FromSeconds(1);

    private readonly CodexPathResolver _pathResolver;
    private readonly IAppPathProvider _appPaths;
    private readonly IJsonFileStorage _storage;
    private readonly IAppLogger _logger;
    private readonly IClock _clock;
    private readonly TimeZoneInfo _localTimeZone;
    private readonly CodexUsageFileParser _fileParser = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CodexUsageSnapshot? _lastSuccessfulSnapshot;

    public CodexUsageProvider(
        CodexPathResolver pathResolver,
        IAppPathProvider appPaths,
        IJsonFileStorage storage,
        IAppLogger logger,
        IClock clock,
        TimeZoneInfo? localTimeZone = null)
    {
        _pathResolver = pathResolver;
        _appPaths = appPaths;
        _storage = storage;
        _logger = logger;
        _clock = clock;
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
    }

    public async Task<CodexUsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<CodexUsageSnapshot> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var refreshedAt = _clock.Now;
        var sessionsDirectory = _pathResolver.SessionsDirectory;
        if (!Directory.Exists(sessionsDirectory))
        {
            return KeepLastOrEmpty(refreshedAt, "Codex usage not detected");
        }

        var files = Directory
            .EnumerateFiles(sessionsDirectory, "*.jsonl", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith("rollout-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            return KeepLastOrEmpty(refreshedAt, "No Codex usage found");
        }

        var stopwatch = Stopwatch.StartNew();
        var index = await LoadIndexOrRebuildAsync(cancellationToken).ConfigureAwait(false);
        var discoveredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesParsed = 0;
        var filesSkipped = 0;
        var filesRebuilt = 0;
        var readErrors = 0;
        var valid = 0;
        var duplicateState = 0;
        var malformed = 0;
        var incomplete = 0;
        long bytesRead = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathKey = BuildPathKey(sessionsDirectory, file);
            discoveredKeys.Add(pathKey);
            var info = new FileInfo(file);
            index.Files.TryGetValue(pathKey, out var previous);
            var unchanged = previous is not null
                            && previous.Size == info.Length
                            && previous.SafeOffset == info.Length
                            && previous.LastWriteTimeUtc == info.LastWriteTimeUtc;

            if (unchanged)
            {
                filesSkipped++;
                continue;
            }

            if (previous is not null && (info.Length < previous.SafeOffset || info.Length < previous.Size))
            {
                previous = null;
                filesRebuilt++;
            }

            try
            {
                var result = await _fileParser.ParseAsync(file, pathKey, previous, cancellationToken).ConfigureAwait(false);
                index.Files[pathKey] = result.File;
                filesParsed++;
                valid += result.ValidTokenEvents;
                duplicateState += result.DuplicateStateEventsIgnored;
                malformed += result.MalformedLinesIgnored;
                incomplete += result.IncompleteLinesIgnored;
                bytesRead += result.BytesRead;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                readErrors++;
                await _logger.LogAsync(AppLogLevel.Warning, $"Could not read Codex session file {pathKey}.", ex, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var stale in index.Files.Keys.Where(key => !discoveredKeys.Contains(key)).ToList())
        {
            index.Files.Remove(stale);
        }

        stopwatch.Stop();

        var rollouts = index.Files.Values
            .Select(file => new CodexParsedRollout
            {
                PathKey = file.PathKey,
                SessionId = file.SessionId,
                ParentSessionId = file.ParentSessionId,
                IsSubagent = file.IsSubagent,
                Events = file.Events
            })
            .ToList();

        var resolved = ResolveRollouts(rollouts, out var duplicateCanonical);
        var snapshot = BuildSnapshot(
            resolved,
            refreshedAt,
            files.Count,
            new CodexUsageRefreshDiagnostics(
                files.Count,
                filesParsed,
                filesSkipped,
                filesRebuilt,
                readErrors,
                valid,
                duplicateState,
                duplicateCanonical,
                malformed,
                incomplete,
                bytesRead,
                stopwatch.Elapsed));

        await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
        await _logger.LogAsync(
            AppLogLevel.Information,
            $"Codex refresh: files={files.Count}, parsed={filesParsed}, skipped={filesSkipped}, validEvents={valid}, duplicateStates={duplicateState}, canonicalDuplicates={duplicateCanonical}, malformed={malformed}, incomplete={incomplete}, elapsedMs={stopwatch.ElapsedMilliseconds}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _lastSuccessfulSnapshot = snapshot;
        return snapshot;
    }

    private CodexUsageSnapshot KeepLastOrEmpty(DateTimeOffset refreshedAt, string status)
    {
        return _lastSuccessfulSnapshot is not null
            ? _lastSuccessfulSnapshot with { RefreshedAt = refreshedAt, StatusMessage = status }
            : CodexUsageSnapshot.Empty(refreshedAt, status);
    }

    private async Task<CodexUsageIndex> LoadIndexOrRebuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            var index = await _storage.LoadOrDefaultAsync(IndexPath, new CodexUsageIndex(), cancellationToken).ConfigureAwait(false);
            return index.SchemaVersion == CodexUsageIndex.CurrentSchemaVersion
                ? index
                : new CodexUsageIndex();
        }
        catch (JsonStorageException ex)
        {
            await _logger.LogAsync(AppLogLevel.Warning, "Codex usage index is invalid and will be rebuilt.", ex, cancellationToken)
                .ConfigureAwait(false);
            return new CodexUsageIndex();
        }
    }

    private Task SaveIndexAsync(CodexUsageIndex index, CancellationToken cancellationToken)
    {
        index.SchemaVersion = CodexUsageIndex.CurrentSchemaVersion;
        return _storage.SaveAsync(IndexPath, index, cancellationToken);
    }

    private string IndexPath => Path.Combine(_appPaths.EnsureLocalCacheDirectory().FullName, IndexFileName);

    private CodexUsageSnapshot BuildSnapshot(
        IReadOnlyList<CodexIndexedTokenEvent> events,
        DateTimeOffset refreshedAt,
        int sessionCount,
        CodexUsageRefreshDiagnostics diagnostics)
    {
        var localNow = TimeZoneInfo.ConvertTime(refreshedAt, _localTimeZone);
        var today = localNow.Date;
        var weekStart = StartOfWeek(today);
        var monthStart = new DateTimeOffset(localNow.Year, localNow.Month, 1, 0, 0, 0, localNow.Offset);
        var lastFiveHoursStart = localNow - TimeSpan.FromHours(5);

        var todayUsage = CodexTokenUsage.Zero;
        var fiveHourUsage = CodexTokenUsage.Zero;
        var weekUsage = CodexTokenUsage.Zero;
        var monthUsage = CodexTokenUsage.Zero;
        var lifetimeUsage = CodexTokenUsage.Zero;

        foreach (var usageEvent in events)
        {
            var localEventTime = TimeZoneInfo.ConvertTime(usageEvent.TimestampUtc, _localTimeZone);
            lifetimeUsage += usageEvent.Delta;

            if (localEventTime >= lastFiveHoursStart && localEventTime <= localNow)
            {
                fiveHourUsage += usageEvent.Delta;
            }

            if (localEventTime.Date == today)
            {
                todayUsage += usageEvent.Delta;
            }

            if (localEventTime >= weekStart && localEventTime <= localNow)
            {
                weekUsage += usageEvent.Delta;
            }

            if (localEventTime >= monthStart && localEventTime <= localNow)
            {
                monthUsage += usageEvent.Delta;
            }
        }

        return new CodexUsageSnapshot(
            todayUsage,
            fiveHourUsage,
            weekUsage,
            monthUsage,
            lifetimeUsage,
            refreshedAt,
            sessionCount,
            diagnostics);
    }

    private DateTimeOffset StartOfWeek(DateTimeOffset localDate)
    {
        var daysSinceMonday = ((int)localDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return localDate.Date.AddDays(-daysSinceMonday);
    }

    private static IReadOnlyList<CodexIndexedTokenEvent> ResolveRollouts(
        IReadOnlyList<CodexParsedRollout> rollouts,
        out int duplicateCanonicalEventsIgnored)
    {
        var bySession = rollouts
            .Where(rollout => !string.IsNullOrEmpty(rollout.SessionId))
            .GroupBy(rollout => rollout.SessionId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(rollout => rollout.PathKey, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

        var memo = new Dictionary<string, List<CodexIndexedTokenEvent>>(StringComparer.OrdinalIgnoreCase);

        List<CodexIndexedTokenEvent> Resolve(CodexParsedRollout rollout, HashSet<string> visiting)
        {
            if (memo.TryGetValue(rollout.PathKey, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(rollout.PathKey))
            {
                return rollout.Events.Skip(FallbackReplayCount(rollout)).ToList();
            }

            try
            {
                var replayCount = 0;
                IReadOnlyList<CodexIndexedTokenEvent> inherited = [];

                if (!string.IsNullOrEmpty(rollout.ParentSessionId)
                    && bySession.TryGetValue(rollout.ParentSessionId, out var candidates))
                {
                    foreach (var candidate in candidates.Where(candidate => !string.Equals(candidate.PathKey, rollout.PathKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        var resolvedParent = Resolve(candidate, visiting);
                        var comparable = ComparableUsagePrefixCount(rollout.Events, resolvedParent);
                        if (comparable is > 0 && comparable > replayCount)
                        {
                            replayCount = comparable.Value;
                            inherited = resolvedParent.Take(replayCount).ToList();
                        }
                    }
                }

                if (replayCount == 0 && !string.IsNullOrEmpty(rollout.ParentSessionId))
                {
                    replayCount = FallbackReplayCount(rollout);
                }

                var resolved = inherited.Concat(rollout.Events.Skip(replayCount)).ToList();
                memo[rollout.PathKey] = resolved;
                return resolved;
            }
            finally
            {
                visiting.Remove(rollout.PathKey);
            }
        }

        var all = new List<CodexIndexedTokenEvent>();
        foreach (var rollout in rollouts.OrderBy(rollout => rollout.PathKey, StringComparer.OrdinalIgnoreCase))
        {
            all.AddRange(Resolve(rollout, []));
        }

        var byCanonical = new Dictionary<string, CodexIndexedTokenEvent>(StringComparer.Ordinal);
        foreach (var usageEvent in all)
        {
            if (byCanonical.TryGetValue(usageEvent.CanonicalId, out var existing))
            {
                if (usageEvent.TimestampUtc < existing.TimestampUtc)
                {
                    byCanonical[usageEvent.CanonicalId] = usageEvent;
                }
            }
            else
            {
                byCanonical.Add(usageEvent.CanonicalId, usageEvent);
            }
        }

        duplicateCanonicalEventsIgnored = all.Count - byCanonical.Count;
        return byCanonical.Values.OrderBy(usageEvent => usageEvent.TimestampUtc).ToList();
    }

    private static int? ComparableUsagePrefixCount(
        IReadOnlyList<CodexIndexedTokenEvent> child,
        IReadOnlyList<CodexIndexedTokenEvent> parent)
    {
        if (child.Count == 0)
        {
            return 0;
        }

        if (parent.Count == 0)
        {
            return null;
        }

        var count = 0;
        while (count < child.Count && count < parent.Count)
        {
            if (string.IsNullOrEmpty(child[count].UsageStateFingerprint)
                || string.IsNullOrEmpty(parent[count].UsageStateFingerprint))
            {
                return null;
            }

            if (child[count].UsageStateFingerprint != parent[count].UsageStateFingerprint)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int FallbackReplayCount(CodexParsedRollout rollout)
    {
        if (rollout.IsSubagent || rollout.Events.Count < 2)
        {
            return 0;
        }

        for (var i = 1; i < rollout.Events.Count; i++)
        {
            if (rollout.Events[i].TimestampUtc - rollout.Events[i - 1].TimestampUtc > ForkReplayMaximumGap)
            {
                return i;
            }
        }

        return 0;
    }

    private static string BuildPathKey(string root, string path)
    {
        try
        {
            return Path.GetRelativePath(root, path).Replace('\\', '/');
        }
        catch
        {
            return Path.GetFileName(path);
        }
    }
}
