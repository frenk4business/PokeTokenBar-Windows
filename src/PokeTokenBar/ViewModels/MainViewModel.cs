using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using PokeTokenBar.Core.Game;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Providers.Codex;
using PokeTokenBar.Services.Cache;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.ImportExport;
using PokeTokenBar.Services.Notifications;
using PokeTokenBar.Services.Settings;
using PokeTokenBar.Services.Sprites;
using PokeTokenBar.Services.Startup;
using PokeTokenBar.Services.System;

namespace PokeTokenBar.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly CompanionGameService _gameService;
    private readonly PokemonSpriteService _spriteService;
    private readonly WpfImageLoader _imageLoader;
    private readonly ShopService _shopService;
    private readonly BagService _bagService;
    private readonly SettingsStore _settingsStore;
    private readonly IStartupRegistration _startupRegistration;
    private readonly SaveTransferService _saveTransferService;
    private readonly CacheMaintenanceService _cacheMaintenanceService;
    private readonly FolderLauncher _folderLauncher;
    private readonly INotificationService _notificationService;
    private readonly IAppPathProvider _paths;
    private AppSettings _settings;
    private string _selectedSection = "Home";
    private string _statusText = "Ready";
    private string _todayTokens = "--";
    private string _lastFiveHoursTokens = "--";
    private string _weekTokens = "--";
    private string _monthTokens = "--";
    private string _lifetimeTokens = "--";
    private string _companionName = "Egg";
    private string _companionSubtitle = "0 / 5M tokens";
    private string _companionDetail = "Incubating";
    private string _progressText = "0%";
    private double _progressValue;
    private ImageSource? _companionImage;
    private string? _companionSpritePath;
    private string _collectionSearchText = "";
    private string _generationFilter = "All";
    private string _rarityFilter = "All";
    private string _ownershipFilter = "All";
    private int _collectionPage = 1;
    private string _collectionSummary = "Owned species: 0 / 649";
    private string _catchLogSummary = "No graduated Pokémon yet.";
    private string _walletBalance = "0";
    private string _walletDetail = "Used since install: 0 · Spent: 0";
    private string _economyMessage = "";
    private string _lastUpdatedText = "Not updated yet";
    private string _nextGoalText = "";
    private string _settingsMessage = "";
    private ShopItemKind? _pendingEggPurchase;
    private bool _pendingEggShinyWarning;
    private bool _isRefreshing;

    public MainViewModel(
        IClock clock,
        IAppLogger logger,
        CompanionGameService gameService,
        PokemonSpriteService spriteService,
        WpfImageLoader imageLoader,
        ShopService shopService,
        BagService bagService,
        SettingsStore settingsStore,
        IStartupRegistration startupRegistration,
        SaveTransferService saveTransferService,
        CacheMaintenanceService cacheMaintenanceService,
        FolderLauncher folderLauncher,
        INotificationService notificationService,
        IAppPathProvider paths,
        AppSettings settings)
    {
        _clock = clock;
        _logger = logger;
        _gameService = gameService;
        _spriteService = spriteService;
        _imageLoader = imageLoader;
        _shopService = shopService;
        _bagService = bagService;
        _settingsStore = settingsStore;
        _startupRegistration = startupRegistration;
        _saveTransferService = saveTransferService;
        _cacheMaintenanceService = cacheMaintenanceService;
        _folderLauncher = folderLauncher;
        _notificationService = notificationService;
        _paths = paths;
        _settings = settings;
        NavigateCommand = new RelayCommand(parameter => SelectSection(parameter?.ToString() ?? "Home"));
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsRefreshing);
        PreviousCollectionPageCommand = new RelayCommand(_ => _ = ChangeCollectionPageAsync(-1), _ => CollectionPage > 1);
        NextCollectionPageCommand = new RelayCommand(_ => _ = ChangeCollectionPageAsync(1), _ => CollectionPage < TotalCollectionPages);
        BuyCommand = new RelayCommand(parameter => _ = BuyAsync(parameter?.ToString()));
        ConfirmEggPurchaseCommand = new RelayCommand(_ => _ = ConfirmEggPurchaseAsync(), _ => _pendingEggPurchase is not null);
        CancelEggPurchaseCommand = new RelayCommand(_ => ClearPendingEggPurchase());
        UseBagItemCommand = new RelayCommand(parameter => _ = UseBagItemAsync(parameter?.ToString()));
        ExportSaveCommand = new RelayCommand(_ => _ = ExportSaveAsync());
        ImportSaveCommand = new RelayCommand(_ => _ = ImportSaveAsync());
        ClearCacheCommand = new RelayCommand(_ => ClearCache());
        OpenDataFolderCommand = new RelayCommand(_ => OpenFolder(_paths.RoamingStateDirectory));
        OpenCacheFolderCommand = new RelayCommand(_ => OpenFolder(_paths.LocalCacheDirectory));
        OpenLogsFolderCommand = new RelayCommand(_ => OpenFolder(_paths.LogsDirectory));
    }

    public IReadOnlyList<string> Sections { get; } = ["Home", "Pokedex", "Bag", "Shop", "Settings"];
    public IReadOnlyList<string> GenerationFilters { get; } = ["All", "Gen 1", "Gen 2", "Gen 3", "Gen 4", "Gen 5"];
    public IReadOnlyList<string> RarityFilters { get; } = ["All", "Common", "Uncommon", "Rare", "Legendary"];
    public IReadOnlyList<string> OwnershipFilters { get; } = ["All", "Owned", "Missing", "Shiny owned"];
    public ObservableCollection<CollectionSpeciesViewModel> PokedexPageItems { get; } = [];
    public ObservableCollection<CatchLogEntryViewModel> CatchLogItems { get; } = [];
    public ObservableCollection<ShopItemViewModel> ShopItems { get; } = [];
    public ObservableCollection<BagItemViewModel> BagItems { get; } = [];
    public IReadOnlyList<RefreshIntervalOption> RefreshIntervalOptions => RefreshIntervals.Options;

    public IReadOnlyList<CompanionSizeOption> DesktopCompanionSizeOptions => DesktopCompanionSizes.Options;

    public string SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(SectionTitle));
                OnPropertyChanged(nameof(SectionDescription));
                OnPropertyChanged(nameof(IsHomeSelected));
                OnPropertyChanged(nameof(IsCollectionSelected));
                OnPropertyChanged(nameof(IsBagSelected));
                OnPropertyChanged(nameof(IsShopSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public string SectionTitle => SelectedSection switch
    {
        "Home" => CompanionName,
        "Pokedex" => "Collection",
        "Bag" => "Bag",
        "Shop" => "Token Shop",
        "Settings" => "Settings",
        _ => "PokeTokenBar"
    };

    public string SectionDescription => SelectedSection switch
    {
        "Home" => CompanionDetail,
        "Pokedex" => CollectionSummary,
        "Bag" => EconomyMessage,
        "Shop" => WalletDetail,
        "Settings" => SettingsMessage,
        _ => string.Empty
    };

    public bool IsHomeSelected => SelectedSection == "Home";

    public bool IsCollectionSelected => SelectedSection == "Pokedex";

    public bool IsBagSelected => SelectedSection == "Bag";

    public bool IsShopSelected => SelectedSection == "Shop";

    public bool IsSettingsSelected => SelectedSection == "Settings";

    public string TodayTokens
    {
        get => _todayTokens;
        private set
        {
            if (SetProperty(ref _todayTokens, value))
            {
                OnPropertyChanged(nameof(TrayTooltipText));
            }
        }
    }

    public string LastFiveHoursTokens
    {
        get => _lastFiveHoursTokens;
        private set => SetProperty(ref _lastFiveHoursTokens, value);
    }

    public string WeekTokens
    {
        get => _weekTokens;
        private set => SetProperty(ref _weekTokens, value);
    }

    public string MonthTokens
    {
        get => _monthTokens;
        private set => SetProperty(ref _monthTokens, value);
    }

    public string LifetimeTokens
    {
        get => _lifetimeTokens;
        private set => SetProperty(ref _lifetimeTokens, value);
    }

    public string CompanionName
    {
        get => _companionName;
        private set
        {
            if (SetProperty(ref _companionName, value))
            {
                OnPropertyChanged(nameof(SectionTitle));
                OnPropertyChanged(nameof(TrayTooltipText));
                OnPropertyChanged(nameof(FloatingCompanionTooltip));
            }
        }
    }

    public string CompanionSubtitle
    {
        get => _companionSubtitle;
        private set
        {
            if (SetProperty(ref _companionSubtitle, value))
            {
                OnPropertyChanged(nameof(FloatingCompanionTooltip));
            }
        }
    }

    public string CompanionDetail
    {
        get => _companionDetail;
        private set
        {
            if (SetProperty(ref _companionDetail, value))
            {
                OnPropertyChanged(nameof(SectionDescription));
            }
        }
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public ImageSource? CompanionImage
    {
        get => _companionImage;
        private set => SetProperty(ref _companionImage, value);
    }

    public string? CompanionSpritePath
    {
        get => _companionSpritePath;
        private set => SetProperty(ref _companionSpritePath, value);
    }

    public string CollectionSearchText
    {
        get => _collectionSearchText;
        set
        {
            if (SetProperty(ref _collectionSearchText, value))
            {
                _collectionPage = 1;
                _ = RefreshCollectionDisplayAsync();
            }
        }
    }

    public string GenerationFilter
    {
        get => _generationFilter;
        set
        {
            if (SetProperty(ref _generationFilter, value))
            {
                _collectionPage = 1;
                _ = RefreshCollectionDisplayAsync();
            }
        }
    }

    public string RarityFilter
    {
        get => _rarityFilter;
        set
        {
            if (SetProperty(ref _rarityFilter, value))
            {
                _collectionPage = 1;
                _ = RefreshCollectionDisplayAsync();
            }
        }
    }

    public string OwnershipFilter
    {
        get => _ownershipFilter;
        set
        {
            if (SetProperty(ref _ownershipFilter, value))
            {
                _collectionPage = 1;
                _ = RefreshCollectionDisplayAsync();
            }
        }
    }

    public int CollectionPage
    {
        get => _collectionPage;
        private set
        {
            if (SetProperty(ref _collectionPage, value))
            {
                OnPropertyChanged(nameof(CollectionPageText));
                if (PreviousCollectionPageCommand is RelayCommand previous) previous.RaiseCanExecuteChanged();
                if (NextCollectionPageCommand is RelayCommand next) next.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalCollectionPages { get; private set; } = 1;

    public string CollectionPageText => $"Page {CollectionPage} / {TotalCollectionPages}";

    public string CollectionSummary
    {
        get => _collectionSummary;
        private set
        {
            if (SetProperty(ref _collectionSummary, value))
            {
                OnPropertyChanged(nameof(SectionDescription));
            }
        }
    }

    public string CatchLogSummary
    {
        get => _catchLogSummary;
        private set => SetProperty(ref _catchLogSummary, value);
    }

    public string WalletBalance
    {
        get => _walletBalance;
        private set => SetProperty(ref _walletBalance, value);
    }

    public string WalletDetail
    {
        get => _walletDetail;
        private set
        {
            if (SetProperty(ref _walletDetail, value))
            {
                OnPropertyChanged(nameof(SectionDescription));
            }
        }
    }

    public string EconomyMessage
    {
        get => _economyMessage;
        private set
        {
            if (SetProperty(ref _economyMessage, value))
            {
                OnPropertyChanged(nameof(SectionDescription));
            }
        }
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public string NextGoalText
    {
        get => _nextGoalText;
        private set
        {
            if (SetProperty(ref _nextGoalText, value))
            {
                OnPropertyChanged(nameof(FloatingCompanionTooltip));
            }
        }
    }

    public string SettingsMessage
    {
        get => _settingsMessage;
        private set
        {
            if (SetProperty(ref _settingsMessage, value))
            {
                OnPropertyChanged(nameof(SectionDescription));
            }
        }
    }

    public bool LaunchWithWindows
    {
        get => _settings.LaunchWithWindows;
        set => _ = UpdateSettingsAsync(_settings with { LaunchWithWindows = value }, applyStartup: true);
    }

    public bool AutoRefreshEnabled
    {
        get => _settings.AutoRefreshEnabled;
        set => _ = UpdateSettingsAsync(_settings with { AutoRefreshEnabled = value && _settings.RefreshIntervalMinutes > 0 });
    }

    public int RefreshIntervalMinutes
    {
        get => _settings.RefreshIntervalMinutes;
        set => _ = UpdateSettingsAsync(_settings with { RefreshIntervalMinutes = value, AutoRefreshEnabled = value > 0 });
    }

    public bool NotificationsEnabled
    {
        get => _settings.NotificationsEnabled;
        set => _ = UpdateSettingsAsync(_settings with { NotificationsEnabled = value });
    }

    public bool HatchNotifications
    {
        get => _settings.HatchNotifications;
        set => _ = UpdateSettingsAsync(_settings with { HatchNotifications = value });
    }

    public bool EvolutionNotifications
    {
        get => _settings.EvolutionNotifications;
        set => _ = UpdateSettingsAsync(_settings with { EvolutionNotifications = value });
    }

    public bool GraduationNotifications
    {
        get => _settings.GraduationNotifications;
        set => _ = UpdateSettingsAsync(_settings with { GraduationNotifications = value });
    }

    public bool ShinyNotifications
    {
        get => _settings.ShinyNotifications;
        set => _ = UpdateSettingsAsync(_settings with { ShinyNotifications = value });
    }

    public bool ShowTokenUsageInTray
    {
        get => _settings.ShowTokenUsageInTray;
        set => _ = UpdateSettingsAsync(_settings with { ShowTokenUsageInTray = value });
    }

    public bool StartMinimizedToTray
    {
        get => _settings.StartMinimizedToTray;
        set => _ = UpdateSettingsAsync(_settings with { StartMinimizedToTray = value });
    }

    public bool ShowDesktopCompanion
    {
        get => _settings.ShowDesktopCompanion;
        set => _ = UpdateSettingsAsync(_settings with { ShowDesktopCompanion = value });
    }

    public bool DesktopCompanionAlwaysOnTop
    {
        get => _settings.DesktopCompanionAlwaysOnTop;
        set => _ = UpdateSettingsAsync(_settings with { DesktopCompanionAlwaysOnTop = value });
    }

    public int DesktopCompanionSize
    {
        get => _settings.DesktopCompanionSize;
        set => _ = UpdateSettingsAsync(_settings with { DesktopCompanionSize = value });
    }

    public double DesktopCompanionLeft => _settings.DesktopCompanionLeft;

    public double DesktopCompanionTop => _settings.DesktopCompanionTop;

    public string FloatingCompanionTooltip => $"{CompanionName}\n{CompanionSubtitle}\n{NextGoalText}";

    public string DataLocation => _paths.RoamingStateDirectory;

    public string CacheLocation => _paths.LocalCacheDirectory;

    public string LogsLocation => _paths.LogsDirectory;

    public string AboutText => $"PokeTokenBar for Windows v{AppVersion}\nOriginal inspiration: chattymin/PokeTokenBar\nPokemon data: PokeAPI\nUnofficial fan project; unaffiliated with Nintendo, Game Freak, Creatures, The Pokemon Company, OpenAI, or the original author.";

    public AppSettings CurrentSettings => _settings;

    public string TrayTooltipText => _settings.ShowTokenUsageInTray
        ? $"PokeTokenBar\n{CompanionName} - {TodayTokens} today"
        : $"PokeTokenBar\n{CompanionName}";

    public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    public bool HasPendingEggPurchase => _pendingEggPurchase is not null;

    public string PendingEggPurchaseText => _pendingEggPurchase is null
        ? ""
        : _pendingEggShinyWarning
            ? "This will discard your current shiny companion. Confirm?"
            : "This will discard your current companion/progress. Confirm?";

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetProperty(ref _isRefreshing, value) && RefreshCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public ICommand NavigateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand PreviousCollectionPageCommand { get; }
    public ICommand NextCollectionPageCommand { get; }
    public ICommand BuyCommand { get; }
    public ICommand ConfirmEggPurchaseCommand { get; }
    public ICommand CancelEggPurchaseCommand { get; }
    public ICommand UseBagItemCommand { get; }
    public ICommand ExportSaveCommand { get; }
    public ICommand ImportSaveCommand { get; }
    public ICommand ClearCacheCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand OpenCacheFolderCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }

    public void SelectSection(string section)
    {
        SelectedSection = Sections.Contains(section) ? section : "Home";
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsRefreshing)
        {
            return;
        }

            IsRefreshing = true;
        StatusText = "Refreshing...";
        try
        {
            var result = await _gameService.RefreshUsageAndProgressAsync(cancellationToken);
            var snapshot = result.UsageSnapshot;
            if (snapshot is null)
            {
                return;
            }

            TodayTokens = CodexUsageFormatting.Compact(snapshot.Today.TotalTokens);
            LastFiveHoursTokens = CodexUsageFormatting.Compact(snapshot.LastFiveHours.TotalTokens);
            WeekTokens = CodexUsageFormatting.Compact(snapshot.CurrentWeek.TotalTokens);
            MonthTokens = CodexUsageFormatting.Compact(snapshot.CurrentMonth.TotalTokens);
            LifetimeTokens = CodexUsageFormatting.Compact(snapshot.ObservedLifetime.TotalTokens);
            StatusText = result.StatusMessage;
            LastUpdatedText = $"Updated {_clock.Now:t}";
            await RefreshCompanionDisplayAsync(cancellationToken);
            await RefreshCollectionDisplayAsync(cancellationToken);
            await RefreshEconomyDisplayAsync(cancellationToken);
            PublishNotifications(result.Events);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusText = "Refresh failed; keeping previous totals";
            await _logger.LogAsync(AppLogLevel.Error, "Codex refresh failed.", ex, cancellationToken);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task LoadInitialAsync(CancellationToken cancellationToken = default)
    {
        await _gameService.InitializeAsync(cancellationToken);
        SyncLaunchWithWindowsFromRegistry();
        await RefreshCompanionDisplayAsync(cancellationToken);
        await RefreshCollectionDisplayAsync(cancellationToken);
        await RefreshEconomyDisplayAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private async Task RefreshCompanionDisplayAsync(CancellationToken cancellationToken)
    {
        var state = _gameService.State;
        if (state.Companion.Egg is not null)
        {
            var progress = state.Companion.Egg.ProgressTokens;
            CompanionName = "Egg";
            CompanionSubtitle = $"{CodexUsageFormatting.Compact(progress)} / {CodexUsageFormatting.Compact(GameBalance.EggHatchThreshold)} tokens";
            CompanionDetail = state.LastError ?? "Incubating";
            NextGoalText = $"{CodexUsageFormatting.Compact(Math.Max(0, GameBalance.EggHatchThreshold - progress))} until hatch";
            ProgressValue = Math.Clamp((double)progress / GameBalance.EggHatchThreshold, 0, 1);
            ProgressText = $"{ProgressValue:P0}";
            var egg = await _spriteService.GetEggSpriteAsync(cancellationToken);
            CompanionSpritePath = egg?.Path;
            CompanionImage = _imageLoader.Load(egg?.Path);
            return;
        }

        var active = state.Companion.Active;
        if (active is null)
        {
            CompanionName = "Egg";
            CompanionSubtitle = "--";
            CompanionDetail = "Ready";
            ProgressValue = 0;
            ProgressText = "0%";
            CompanionSpritePath = null;
            CompanionImage = null;
            return;
        }

        var threshold = GameBalance.PhaseThreshold(active.Rarity, active.PlannedPathSpeciesIds.Count, active.StageIndex);
        var remaining = Math.Max(0, threshold - active.StageProgressTokens);
        var goal = active.StageIndex == active.PlannedPathSpeciesIds.Count - 1 ? "graduation" : "evolution";
        var shiny = active.IsShiny ? " ✨" : string.Empty;
        CompanionName = $"{NameFor(active.CurrentSpeciesId, state)}{shiny}";
        CompanionSubtitle = $"{CodexUsageFormatting.Compact(active.StageProgressTokens)} / {CodexUsageFormatting.Compact(threshold)} tokens";
        CompanionDetail = $"{active.Rarity} · {active.Nature} · Stage {active.StageIndex + 1}/{active.PlannedPathSpeciesIds.Count}";
        NextGoalText = $"{CodexUsageFormatting.Compact(remaining)} until {goal}";
        ProgressValue = Math.Clamp((double)active.StageProgressTokens / threshold, 0, 1);
        ProgressText = $"{ProgressValue:P0}";
        var sprite = await _spriteService.GetPokemonSpriteAsync(active.CurrentSpeciesId, active.IsShiny, animated: true, cancellationToken);
        CompanionSpritePath = sprite?.Path;
        CompanionImage = _imageLoader.Load(sprite?.Path);
    }

    private static string NameFor(int speciesId, GameSaveState state)
    {
        return state.SpeciesNames.TryGetValue(speciesId, out var name) ? name : $"#{speciesId}";
    }

    private async Task ChangeCollectionPageAsync(int offset)
    {
        CollectionPage = Math.Clamp(CollectionPage + offset, 1, TotalCollectionPages);
        await RefreshCollectionDisplayAsync();
    }

    private async Task RefreshCollectionDisplayAsync(CancellationToken cancellationToken = default)
    {
        var state = _gameService.State;
        var filtered = Enumerable.Range(1, 649)
            .Select(id => BuildSpeciesProjection(id, state))
            .Where(item => PokedexQuery.Passes(item.SpeciesId, item.DisplayName, item.Rarity, item.Generation, item.Owned, item.ShinyOwned, new PokedexFilter(CollectionSearchText, GenerationFilter, RarityFilter, OwnershipFilter)))
            .ToArray();

        TotalCollectionPages = Math.Max(1, (int)Math.Ceiling(filtered.Length / 24d));
        if (CollectionPage > TotalCollectionPages)
        {
            CollectionPage = TotalCollectionPages;
        }
        else
        {
            OnPropertyChanged(nameof(CollectionPageText));
        }

        PokedexPageItems.Clear();
        foreach (var item in filtered.Skip((CollectionPage - 1) * 24).Take(24))
        {
            ImageSource? sprite = null;
            if (item.Owned)
            {
                var spriteResult = await _spriteService.GetPokemonSpriteAsync(item.SpeciesId, item.ShinyOwned, animated: false, cancellationToken);
                sprite = _imageLoader.Load(spriteResult?.Path);
            }

            PokedexPageItems.Add(item with { Sprite = sprite });
        }

        var owned = state.Pokedex.Values.Count(entry => entry.Owned);
        var shiny = state.Pokedex.Values.Count(entry => entry.ShinyOwned);
        var graduated = state.CatchLog.Count(entry => entry.Status == CompanionLifecycleStatus.Graduated);
        CollectionSummary = $"Owned species: {owned} / 649 · Shiny: {shiny} · Graduated: {graduated}";

        CatchLogItems.Clear();
        foreach (var entry in state.CatchLog.OrderByDescending(entry => entry.GraduationTime ?? entry.HatchTime))
        {
            var spriteId = entry.FinalSpeciesId == 0 ? entry.BaseSpeciesId : entry.FinalSpeciesId;
            var spriteResult = await _spriteService.GetPokemonSpriteAsync(spriteId, entry.IsShiny, animated: false, cancellationToken);
            var path = string.Join(" -> ", entry.EncounteredSpeciesIds.Select(id => NameFor(id, state)));
            CatchLogItems.Add(new CatchLogEntryViewModel(
                entry.IndividualId,
                $"{NameFor(spriteId, state)}{(entry.IsShiny ? " ✨" : "")}",
                path,
                $"{entry.Rarity} · {entry.Nature} · Hatched {entry.HatchTime:g} · {(entry.GraduationTime is null ? entry.Status : $"Graduated {entry.GraduationTime:g}")}",
                _imageLoader.Load(spriteResult?.Path)));
        }

        CatchLogSummary = CatchLogItems.Count == 0 ? "No graduated Pokémon yet." : $"{CatchLogItems.Count} individual companion(s)";
        if (PreviousCollectionPageCommand is RelayCommand previous) previous.RaiseCanExecuteChanged();
        if (NextCollectionPageCommand is RelayCommand next) next.RaiseCanExecuteChanged();
    }

    private CollectionSpeciesViewModel BuildSpeciesProjection(int id, GameSaveState state)
    {
        if (state.Pokedex.TryGetValue(id, out var entry))
        {
            return new CollectionSpeciesViewModel(
                id,
                $"#{id:000}",
                entry.DisplayName,
                entry.Rarity.ToString(),
                $"Gen {entry.Generation}",
                entry.Owned,
                entry.ShinyOwned,
                null);
        }

        return new CollectionSpeciesViewModel(id, $"#{id:000}", "???", "", $"Gen {GenerationFor(id)}", Owned: false, ShinyOwned: false, null);
    }

    private static int GenerationFor(int speciesId) => speciesId switch
    {
        >= 1 and <= 151 => 1,
        <= 251 => 2,
        <= 386 => 3,
        <= 493 => 4,
        <= 649 => 5,
        _ => 0
    };

    private async Task RefreshEconomyDisplayAsync(CancellationToken cancellationToken = default)
    {
        var state = _gameService.State;
        WalletBalance = CodexUsageFormatting.Compact(state.AvailableBalance);
        WalletDetail = $"Used since install: {CodexUsageFormatting.Compact(state.UsedSinceInstall)} · Spent: {CodexUsageFormatting.Compact(state.SpentTokens)}";

        ShopItems.Clear();
        foreach (var item in _shopService.Catalog)
        {
            var owned = item.Kind == ShopItemKind.ShinyCharm && state.Inventory.HasShinyCharm;
            ShopItems.Add(new ShopItemViewModel(
                item.Kind.ToString(),
                IconFor(item.Kind),
                item.DisplayName,
                item.Description,
                CodexUsageFormatting.Compact(item.Price),
                _shopService.CanBuy(item.Kind, state),
                owned ? "Owned" : "Buy",
                item.EggTier is not null,
                state.Companion.Active?.IsShiny == true,
                await LoadShopIconAsync(item.Kind, cancellationToken)));
        }

        BagItems.Clear();
        BagItems.Add(new BagItemViewModel("RareCandy", "◆", "Rare Candy", "+100M growth", $"x{state.Inventory.RareCandyCount}", state.Inventory.RareCandyCount > 0, "Use"));
        BagItems.Add(new BagItemViewModel("Mint", "*", "Mint", "Reroll Nature", $"x{state.Inventory.MintCount}", state.Inventory.MintCount > 0 && state.Companion.Active is not null, "Use"));
        BagItems.Add(new BagItemViewModel("ShinyCharm", "✨", "Shiny Charm", "Passive shiny odds boost", state.Inventory.HasShinyCharm ? "Active" : "Not owned", false, "Passive"));

        if (string.IsNullOrWhiteSpace(EconomyMessage))
        {
            EconomyMessage = state.Inventory.RareCandyCount == 0 && state.Inventory.MintCount == 0 && !state.Inventory.HasShinyCharm
                ? "Your bag is empty."
                : "Inventory ready.";
        }

        OnPropertyChanged(nameof(HasPendingEggPurchase));
        OnPropertyChanged(nameof(PendingEggPurchaseText));
        if (ConfirmEggPurchaseCommand is RelayCommand confirm)
        {
            confirm.RaiseCanExecuteChanged();
        }
    }

    private async Task BuyAsync(string? rawKind)
    {
        if (!Enum.TryParse<ShopItemKind>(rawKind, out var kind))
        {
            return;
        }

        var result = await _shopService.BuyAsync(kind);
        EconomyMessage = result.Message;
        if (result.RequiresConfirmation)
        {
            _pendingEggPurchase = kind;
            _pendingEggShinyWarning = result.ShinyDiscardWarning;
            await RefreshEconomyDisplayAsync();
            return;
        }

        await RefreshAfterEconomyActionAsync();
        PublishNotifications(result.Events ?? Array.Empty<CompanionProgressEvent>());
    }

    private async Task ConfirmEggPurchaseAsync()
    {
        if (_pendingEggPurchase is null)
        {
            return;
        }

        var kind = _pendingEggPurchase.Value;
        ClearPendingEggPurchase();
        var result = await _shopService.BuyAsync(kind, confirmedDestructiveEggPurchase: true);
        EconomyMessage = result.Message;
        await RefreshAfterEconomyActionAsync();
        PublishNotifications(result.Events ?? Array.Empty<CompanionProgressEvent>());
    }

    private void ClearPendingEggPurchase()
    {
        _pendingEggPurchase = null;
        _pendingEggShinyWarning = false;
        OnPropertyChanged(nameof(HasPendingEggPurchase));
        OnPropertyChanged(nameof(PendingEggPurchaseText));
        if (ConfirmEggPurchaseCommand is RelayCommand confirm)
        {
            confirm.RaiseCanExecuteChanged();
        }
    }

    private async Task UseBagItemAsync(string? rawKind)
    {
        var result = rawKind switch
        {
            "RareCandy" => await _bagService.UseRareCandyAsync(),
            "Mint" => await _bagService.UseMintAsync(),
            _ => new EconomyActionResult(false, _gameService.State, "Item cannot be used")
        };

        EconomyMessage = result.Message;
        await RefreshAfterEconomyActionAsync();
        PublishNotifications(result.Events ?? Array.Empty<CompanionProgressEvent>());
    }

    private async Task RefreshAfterEconomyActionAsync()
    {
        await RefreshCompanionDisplayAsync(CancellationToken.None);
        await RefreshCollectionDisplayAsync(CancellationToken.None);
        await RefreshEconomyDisplayAsync();
    }

    private static string IconFor(ShopItemKind kind) => kind switch
    {
        ShopItemKind.RareCandy => "◆",
        ShopItemKind.Mint => "*",
        ShopItemKind.ShinyCharm => "✨",
        ShopItemKind.NormalEgg => "○",
        ShopItemKind.UncommonEgg => "◐",
        ShopItemKind.RareEgg => "●",
        _ => "?"
    };

    private async Task<ImageSource?> LoadShopIconAsync(ShopItemKind kind, CancellationToken cancellationToken)
    {
        return kind switch
        {
            ShopItemKind.RareCandy => await LoadItemIconAsync("rare-candy", cancellationToken),
            ShopItemKind.Mint => await LoadItemIconAsync("modest-mint", cancellationToken),
            ShopItemKind.ShinyCharm => await LoadItemIconAsync("shiny-charm", cancellationToken),
            ShopItemKind.NormalEgg or ShopItemKind.UncommonEgg or ShopItemKind.RareEgg => await LoadEggIconAsync(cancellationToken),
            _ => null
        };
    }

    private async Task<ImageSource?> LoadItemIconAsync(string itemKey, CancellationToken cancellationToken)
    {
        var sprite = await _spriteService.GetItemSpriteAsync(itemKey, cancellationToken);
        return _imageLoader.Load(sprite?.Path) ?? LoadLocalItemFallback(itemKey);
    }

    private ImageSource? LoadLocalItemFallback(string itemKey)
    {
        var fileName = itemKey switch
        {
            "modest-mint" => "Mint.png",
            _ => null
        };

        return fileName is null
            ? null
            : _imageLoader.Load(Path.Combine(AppContext.BaseDirectory, "Resources", "Items", fileName));
    }

    private async Task<ImageSource?> LoadEggIconAsync(CancellationToken cancellationToken)
    {
        var sprite = await _spriteService.GetEggSpriteAsync(cancellationToken);
        return _imageLoader.Load(sprite?.Path);
    }

    private async Task UpdateSettingsAsync(AppSettings settings, bool applyStartup = false)
    {
        var normalized = SettingsStore.Normalize(settings);
        try
        {
            if (applyStartup)
            {
                var executable = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "PokeTokenBar.exe";
                _startupRegistration.SetEnabled("PokeTokenBar", executable, normalized.LaunchWithWindows);
            }

            await _settingsStore.SaveAsync(normalized);
            _settings = normalized;
            SettingsMessage = "Settings saved.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SettingsMessage = "Settings could not be saved.";
            await _logger.LogAsync(AppLogLevel.Error, "Settings save failed.", ex);
        }

        OnSettingsChanged();
    }

    public void UpdateDesktopCompanionPosition(double left, double top)
    {
        _ = UpdateSettingsAsync(_settings with { DesktopCompanionLeft = left, DesktopCompanionTop = top });
    }

    public void UpdateDesktopCompanionPlacement(double left, double top, int size)
    {
        _ = UpdateSettingsAsync(_settings with { DesktopCompanionLeft = left, DesktopCompanionTop = top, DesktopCompanionSize = size });
    }

    private void SyncLaunchWithWindowsFromRegistry()
    {
        try
        {
            var executable = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "PokeTokenBar.exe";
            _settings = _settings with { LaunchWithWindows = _startupRegistration.IsEnabled("PokeTokenBar", executable) };
        }
        catch (Exception ex)
        {
            SettingsMessage = "Launch-at-login state could not be checked.";
            _ = _logger.LogAsync(AppLogLevel.Warning, "Startup registration check failed.", ex);
        }

        OnSettingsChanged();
    }

    private async Task ExportSaveAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PokeTokenBar backup (*.json)|*.json",
            FileName = $"poketokenbar-backup-{_clock.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _saveTransferService.ExportAsync(dialog.FileName, _gameService.State, _settings, AppVersion).ConfigureAwait(false);
            SettingsMessage = "Save exported.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SettingsMessage = "Save export failed.";
            await _logger.LogAsync(AppLogLevel.Error, "Save export failed.", ex).ConfigureAwait(false);
        }
    }

    private async Task ImportSaveAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PokeTokenBar backup (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var validation = await _saveTransferService.ValidateImportAsync(dialog.FileName).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            SettingsMessage = validation.Message;
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            "Importing will replace the current save after creating a backup. Continue?",
            "Import Save",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var result = await _saveTransferService.ImportAsync(dialog.FileName, includeSettings: true).ConfigureAwait(false);
        SettingsMessage = result.Message;
        if (result.IsValid)
        {
            await _gameService.InitializeAsync().ConfigureAwait(false);
            if (result.Package?.Settings is not null)
            {
                _settings = SettingsStore.Normalize(result.Package.Settings);
            }

            await RefreshAfterEconomyActionAsync().ConfigureAwait(false);
            OnSettingsChanged();
        }
    }

    private void ClearCache()
    {
        var confirmed = System.Windows.MessageBox.Show(
            "Clear cached Pokemon metadata and sprites? Your save, collection, wallet, and settings stay untouched.",
            "Clear Cache",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _cacheMaintenanceService.ClearPokemonCache();
            SettingsMessage = "Pokemon cache cleared.";
        }
        catch (Exception ex)
        {
            SettingsMessage = "Cache clear failed.";
            _ = _logger.LogAsync(AppLogLevel.Error, "Cache clear failed.", ex);
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            _folderLauncher.Open(path);
        }
        catch (Exception ex)
        {
            SettingsMessage = "Folder could not be opened.";
            _ = _logger.LogAsync(AppLogLevel.Warning, "Folder open failed.", ex);
        }
    }

    private void PublishNotifications(IReadOnlyList<CompanionProgressEvent> events)
    {
        foreach (var notification in GameplayNotificationMapper.Map(events, _gameService.State, _settings))
        {
            _notificationService.Publish(notification);
        }
    }

    private void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(LaunchWithWindows));
        OnPropertyChanged(nameof(AutoRefreshEnabled));
        OnPropertyChanged(nameof(RefreshIntervalMinutes));
        OnPropertyChanged(nameof(NotificationsEnabled));
        OnPropertyChanged(nameof(HatchNotifications));
        OnPropertyChanged(nameof(EvolutionNotifications));
        OnPropertyChanged(nameof(GraduationNotifications));
        OnPropertyChanged(nameof(ShinyNotifications));
        OnPropertyChanged(nameof(ShowTokenUsageInTray));
        OnPropertyChanged(nameof(StartMinimizedToTray));
        OnPropertyChanged(nameof(ShowDesktopCompanion));
        OnPropertyChanged(nameof(DesktopCompanionAlwaysOnTop));
        OnPropertyChanged(nameof(DesktopCompanionSize));
        OnPropertyChanged(nameof(DesktopCompanionLeft));
        OnPropertyChanged(nameof(DesktopCompanionTop));
        OnPropertyChanged(nameof(FloatingCompanionTooltip));
        OnPropertyChanged(nameof(CurrentSettings));
        OnPropertyChanged(nameof(TrayTooltipText));
    }
}
