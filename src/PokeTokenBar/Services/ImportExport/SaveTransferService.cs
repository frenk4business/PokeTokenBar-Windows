using System.IO;
using System.Text.Json;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Settings;
using PokeTokenBar.Services.Storage;

namespace PokeTokenBar.Services.ImportExport;

public sealed class SaveTransferService
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly GameStateStore _gameStore;
    private readonly SettingsStore _settingsStore;
    private readonly IAppPathProvider _paths;
    private readonly IClock _clock;

    public SaveTransferService(GameStateStore gameStore, SettingsStore settingsStore, IAppPathProvider paths, IClock clock)
    {
        _gameStore = gameStore;
        _settingsStore = settingsStore;
        _paths = paths;
        _clock = clock;
    }

    public async Task ExportAsync(
        string destinationPath,
        GameSaveState gameState,
        AppSettings settings,
        string appVersion,
        CancellationToken cancellationToken = default)
    {
        var package = new SaveExportPackage
        {
            ExportedAt = _clock.Now,
            AppVersion = appVersion,
            GameState = gameState,
            Settings = settings
        };

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, package, Options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportValidationResult> ValidateImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, useAsync: true);
            var package = await JsonSerializer.DeserializeAsync<SaveExportPackage>(stream, Options, cancellationToken).ConfigureAwait(false);
            return package is null ? new ImportValidationResult(false, "Backup file is empty.") : SaveValidator.Validate(package);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ImportValidationResult(false, "Backup file is not valid JSON.");
        }
    }

    public async Task<ImportValidationResult> ImportAsync(string sourcePath, bool includeSettings, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateImportAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid || validation.Package is null)
        {
            return validation;
        }

        CreateTimestampedBackup();
        var importedState = GameStateStore.Migrate(validation.Package.GameState);
        await _gameStore.SaveAsync(importedState, cancellationToken).ConfigureAwait(false);
        if (includeSettings && validation.Package.Settings is not null)
        {
            await _settingsStore.SaveAsync(validation.Package.Settings, cancellationToken).ConfigureAwait(false);
        }

        return validation with { Message = "Save imported." };
    }

    private void CreateTimestampedBackup()
    {
        var source = _gameStore.StatePath;
        if (!File.Exists(source))
        {
            return;
        }

        var backupDirectory = Path.Combine(_paths.EnsureRoamingStateDirectory().FullName, "Backups");
        Directory.CreateDirectory(backupDirectory);
        var stamp = _clock.Now.ToString("yyyy-MM-dd-HHmmss");
        File.Copy(source, Path.Combine(backupDirectory, $"state-backup-{stamp}.json"), overwrite: false);
    }
}
