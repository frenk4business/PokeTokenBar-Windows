using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Settings;
using PokeTokenBar.Services.Startup;
using PokeTokenBar.Services.Storage;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task MissingSettingsReturnDefaults()
    {
        using var context = SettingsContext.Create();

        var settings = await context.Store.LoadAsync();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.True(settings.AutoRefreshEnabled);
        Assert.Equal(2, settings.RefreshIntervalMinutes);
        Assert.True(settings.NotificationsEnabled);
        Assert.True(settings.StartMinimizedToTray);
        Assert.False(settings.ShowDesktopCompanion);
        Assert.Equal(96, settings.DesktopCompanionSize);
    }

    [Fact]
    public async Task SaveAndLoadSettingsRoundTrip()
    {
        using var context = SettingsContext.Create();
        var saved = AppSettings.Default with
        {
            LaunchWithWindows = true,
            AutoRefreshEnabled = false,
            RefreshIntervalMinutes = 0,
            NotificationsEnabled = false,
            ShinyNotifications = false,
            StartMinimizedToTray = false,
            ShowDesktopCompanion = true,
            DesktopCompanionAlwaysOnTop = true,
            DesktopCompanionSize = 128,
            DesktopCompanionLeft = 321,
            DesktopCompanionTop = 222
        };

        await context.Store.SaveAsync(saved);
        var loaded = await context.Store.LoadAsync();

        Assert.True(loaded.LaunchWithWindows);
        Assert.False(loaded.AutoRefreshEnabled);
        Assert.Equal(0, loaded.RefreshIntervalMinutes);
        Assert.False(loaded.NotificationsEnabled);
        Assert.False(loaded.ShinyNotifications);
        Assert.False(loaded.StartMinimizedToTray);
        Assert.True(loaded.ShowDesktopCompanion);
        Assert.False(loaded.DesktopCompanionAlwaysOnTop);
        Assert.Equal(128, loaded.DesktopCompanionSize);
        Assert.Equal(321, loaded.DesktopCompanionLeft);
        Assert.Equal(222, loaded.DesktopCompanionTop);
    }

    [Fact]
    public async Task InvalidRefreshIntervalNormalizesToDefault()
    {
        using var context = SettingsContext.Create();

        await context.Store.SaveAsync(AppSettings.Default with { RefreshIntervalMinutes = 999 });
        var loaded = await context.Store.LoadAsync();

        Assert.Equal(2, loaded.RefreshIntervalMinutes);
        Assert.True(loaded.AutoRefreshEnabled);
    }

    [Fact]
    public async Task InvalidDesktopCompanionSizeNormalizesToDefault()
    {
        using var context = SettingsContext.Create();

        await context.Store.SaveAsync(AppSettings.Default with { DesktopCompanionSize = 999 });
        var loaded = await context.Store.LoadAsync();

        Assert.Equal(96, loaded.DesktopCompanionSize);
    }

    [Fact]
    public async Task CorruptSettingsFallbackToDefaults()
    {
        using var context = SettingsContext.Create();
        Directory.CreateDirectory(context.Paths.RoamingStateDirectory);
        await File.WriteAllTextAsync(context.Store.SettingsPath, "{not json");

        var loaded = await context.Store.LoadAsync();

        Assert.Equal(AppSettings.Default, loaded);
    }

    [Fact]
    public void StartupRunValueQuotesExecutablePath()
    {
        var value = WindowsStartupRegistration.BuildRunValue(@"C:\Program Files\PokeTokenBar\PokeTokenBar.exe");

        Assert.Equal("\"C:\\Program Files\\PokeTokenBar\\PokeTokenBar.exe\"", value);
    }

    private sealed class SettingsContext : IDisposable
    {
        private readonly string _root;

        private SettingsContext(string root)
        {
            _root = root;
            Paths = new TestAppPathProvider(root);
            Store = new SettingsStore(Paths, new JsonFileStorage(new NullAppLogger()), new NullAppLogger());
        }

        public TestAppPathProvider Paths { get; }

        public SettingsStore Store { get; }

        public static SettingsContext Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"ptb-settings-{Guid.NewGuid():N}");
            return new SettingsContext(root);
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
