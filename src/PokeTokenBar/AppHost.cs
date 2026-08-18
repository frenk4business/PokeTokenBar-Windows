using System.Threading;
using System.Net.Http;
using System.Windows;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Cache;
using PokeTokenBar.Services.Floating;
using PokeTokenBar.Services.ImportExport;
using PokeTokenBar.Services.Notifications;
using PokeTokenBar.Services.PokeApi;
using PokeTokenBar.Services.Settings;
using PokeTokenBar.Services.Sprites;
using PokeTokenBar.Services.Startup;
using PokeTokenBar.Services.Storage;
using PokeTokenBar.Services.System;
using PokeTokenBar.Tray;
using PokeTokenBar.ViewModels;
using PokeTokenBar.Views;
using PokeTokenBar.Providers.Codex;

namespace PokeTokenBar;

public partial class App
{
    private Mutex? _singleInstanceMutex;
    private TrayService? _trayService;
    private FloatingCompanionService? _floatingCompanionService;
    private MainWindow? _mainWindow;
    private IAppLogger? _logger;
    private CancellationTokenSource? _refreshCancellation;
    private HttpClient? _httpClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "PokeTokenBar.Windows.SingleInstance", out var ownsMutex);
        if (!ownsMutex)
        {
            Shutdown();
            return;
        }

        var clock = new SystemClock();
        var paths = new WindowsAppPathProvider();
        _logger = new FileAppLogger(paths, clock);
        var storage = new JsonFileStorage(_logger);
        var settingsStore = new SettingsStore(paths, storage, _logger);
        var settings = await settingsStore.LoadAsync();
        var codexUsageProvider = new CodexUsageProvider(new CodexPathResolver(), paths, storage, _logger, clock);
        _httpClient = new HttpClient();
        var pokeApiClient = new PokeApiHttpClient(_httpClient);
        var pokemonRepository = new PokeApiPokemonRepository(pokeApiClient, paths, _logger);
        var randomSource = new Core.Game.SystemRandomSource();
        var natureService = new Core.Game.NatureService();
        var hatchService = new PokeApiHatchService(
            pokemonRepository,
            new WeightedPokemonSelector(),
            new Core.Game.EvolutionPathSelector(),
            natureService,
            new Core.Game.ShinyRoller(),
            randomSource);
        var spriteService = new PokemonSpriteService(_httpClient, paths, _logger);
        var gameStore = new GameStateStore(paths, storage, _logger);
        var gameService = new CompanionGameService(codexUsageProvider, hatchService, gameStore, clock, _logger);
        var shopService = new ShopService(gameService);
        var bagService = new BagService(gameService, natureService, randomSource);
        var saveTransferService = new SaveTransferService(gameStore, settingsStore, paths, clock);
        var cacheMaintenanceService = new CacheMaintenanceService(paths);
        var folderLauncher = new FolderLauncher();
        var notificationService = new NotificationService();

        await _logger.LogAsync(AppLogLevel.Information, "Application starting.");

        var viewModel = new MainViewModel(
            clock,
            _logger,
            gameService,
            spriteService,
            new WpfImageLoader(),
            shopService,
            bagService,
            settingsStore,
            new WindowsStartupRegistration(),
            saveTransferService,
            cacheMaintenanceService,
            folderLauncher,
            notificationService,
            paths,
            settings);
        _mainWindow = new MainWindow(viewModel);
        _floatingCompanionService = new FloatingCompanionService(viewModel, () =>
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }, _logger);
        _trayService = new TrayService(_mainWindow, viewModel, _logger, notificationService);
        _refreshCancellation = new CancellationTokenSource();
        await viewModel.LoadInitialAsync(_refreshCancellation.Token);
        _floatingCompanionService.Sync();
        if (!viewModel.CurrentSettings.StartMinimizedToTray)
        {
            _mainWindow.Show();
        }

        _ = RunPeriodicRefreshAsync(viewModel, _refreshCancellation.Token);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose();
        }

        _trayService?.Dispose();
        _floatingCompanionService?.Dispose();
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _httpClient?.Dispose();

        if (_logger is not null)
        {
            await _logger.LogAsync(AppLogLevel.Information, "Application exiting.");
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private async Task RunPeriodicRefreshAsync(MainViewModel viewModel, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var settings = viewModel.CurrentSettings;
                if (!settings.AutoRefreshEnabled || settings.RefreshIntervalMinutes <= 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    continue;
                }

                await Task.Delay(TimeSpan.FromMinutes(settings.RefreshIntervalMinutes), cancellationToken);
                await await Dispatcher.InvokeAsync(() => viewModel.RefreshAsync(cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
