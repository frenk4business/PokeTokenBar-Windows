using System.IO;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Storage;

namespace PokeTokenBar.Services.Settings;

public sealed class SettingsStore
{
    private readonly IAppPathProvider _paths;
    private readonly IJsonFileStorage _storage;
    private readonly IAppLogger _logger;

    public SettingsStore(IAppPathProvider paths, IJsonFileStorage storage, IAppLogger logger)
    {
        _paths = paths;
        _storage = storage;
        _logger = logger;
    }

    public string SettingsPath => Path.Combine(_paths.RoamingStateDirectory, "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _storage.LoadOrDefaultAsync(SettingsPath, AppSettings.Default, cancellationToken).ConfigureAwait(false);
            return Normalize(settings);
        }
        catch (JsonStorageException ex)
        {
            await _logger.LogAsync(AppLogLevel.Warning, "Settings could not be loaded; using defaults.", ex, cancellationToken)
                .ConfigureAwait(false);
            return AppSettings.Default;
        }
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return _storage.SaveAsync(SettingsPath, Normalize(settings), cancellationToken);
    }

    public static AppSettings Normalize(AppSettings settings)
    {
        if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
        {
            return AppSettings.Default;
        }

        var normalizedInterval = RefreshIntervals.Normalize(settings.RefreshIntervalMinutes);
        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            RefreshIntervalMinutes = normalizedInterval,
            AutoRefreshEnabled = settings.AutoRefreshEnabled && normalizedInterval > 0,
            DesktopCompanionAlwaysOnTop = false,
            DesktopCompanionSize = DesktopCompanionSizes.Normalize(settings.DesktopCompanionSize),
            DesktopCompanionLeft = double.IsFinite(settings.DesktopCompanionLeft) ? settings.DesktopCompanionLeft : AppSettings.Default.DesktopCompanionLeft,
            DesktopCompanionTop = double.IsFinite(settings.DesktopCompanionTop) ? settings.DesktopCompanionTop : AppSettings.Default.DesktopCompanionTop
        };
    }
}
