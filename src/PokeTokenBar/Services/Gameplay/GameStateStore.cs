using System.IO;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Storage;

namespace PokeTokenBar.Services.Gameplay;

public sealed class GameStateStore
{
    private readonly IAppPathProvider _paths;
    private readonly IJsonFileStorage _storage;
    private readonly IAppLogger _logger;

    public GameStateStore(IAppPathProvider paths, IJsonFileStorage storage, IAppLogger logger)
    {
        _paths = paths;
        _storage = storage;
        _logger = logger;
    }

    public string StatePath => Path.Combine(_paths.RoamingStateDirectory, "state.json");

    public async Task<GameSaveState> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _storage.LoadOrDefaultAsync(StatePath, GameSaveState.New(), cancellationToken).ConfigureAwait(false);
            return Migrate(state);
        }
        catch (JsonStorageException ex)
        {
            await _logger.LogAsync(AppLogLevel.Warning, "Primary gameplay save failed to load; trying previous backup.", ex, cancellationToken)
                .ConfigureAwait(false);

            var backupPath = JsonFileStorage.BuildBackupPath(StatePath);
            try
            {
                var backup = await _storage.LoadOrDefaultAsync(backupPath, GameSaveState.New(), cancellationToken).ConfigureAwait(false);
                if (backup.SchemaVersion == GameSaveState.CurrentSchemaVersion)
                {
                    return backup;
                }
            }
            catch (JsonStorageException backupEx)
            {
                await _logger.LogAsync(AppLogLevel.Error, "Gameplay save backup also failed to load.", backupEx, cancellationToken)
                    .ConfigureAwait(false);
            }

            throw;
        }
    }

    public Task SaveAsync(GameSaveState state, CancellationToken cancellationToken = default)
    {
        return _storage.SaveAsync(StatePath, state with { SchemaVersion = GameSaveState.CurrentSchemaVersion }, cancellationToken);
    }

    public static GameSaveState Migrate(GameSaveState state)
    {
        if (state.SchemaVersion > GameSaveState.CurrentSchemaVersion)
        {
            return GameSaveState.New();
        }

        var migrated = state with
        {
            SchemaVersion = GameSaveState.CurrentSchemaVersion,
            Inventory = state.Inventory ?? new InventoryState()
        };
        var updater = new CollectionUpdater();
        migrated = updater.EnsureActiveLifecycle(migrated, DateTimeOffset.UtcNow);

        if (migrated.LastGraduation is not null && !migrated.LastGraduationImportedToCatchLog)
        {
            var individualId = migrated.LastGraduation.IndividualId ?? $"legacy-{migrated.LastGraduation.BaseSpeciesId}-{migrated.LastGraduation.GraduationTime:yyyyMMddHHmmss}";
            if (!migrated.CatchLog.Any(entry => entry.IndividualId == individualId))
            {
                var activeLike = new CatchLogEntry
                {
                    IndividualId = individualId,
                    BaseSpeciesId = migrated.LastGraduation.BaseSpeciesId,
                    PlannedPathSpeciesIds = migrated.LastGraduation.PlannedPathSpeciesIds,
                    EncounteredSpeciesIds = migrated.LastGraduation.RealizedSpeciesIds,
                    FinalSpeciesId = migrated.LastGraduation.FinalSpeciesId,
                    Rarity = migrated.LastGraduation.Rarity,
                    Nature = migrated.LastGraduation.Nature,
                    IsShiny = migrated.LastGraduation.IsShiny,
                    HatchTime = migrated.LastGraduation.HatchTime,
                    GraduationTime = migrated.LastGraduation.GraduationTime,
                    Status = CompanionLifecycleStatus.Graduated,
                    TotalAppliedProgressTokens = migrated.LastGraduation.TotalAppliedProgressTokens,
                    EvolutionHistory = migrated.LastGraduation.RealizedSpeciesIds.Select((id, index) =>
                        new EvolutionHistoryEntry(id, migrated.SpeciesNames.TryGetValue(id, out var name) ? name : $"#{id:000}", index == 0 ? migrated.LastGraduation.HatchTime : migrated.LastGraduation.GraduationTime, index)).ToArray()
                };
                migrated = migrated with { CatchLog = migrated.CatchLog.Concat(new[] { activeLike }).ToArray() };
            }

            migrated = migrated with { LastGraduationImportedToCatchLog = true };
        }

        return migrated;
    }
}
