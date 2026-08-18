using System.Text.Json;
using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.ImportExport;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Settings;
using PokeTokenBar.Services.Storage;
using PokeTokenBar.Tests.Game;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class SaveTransferServiceTests
{
    [Fact]
    public async Task ExportCreatesPortablePackageWithGameplaySettingsAndEconomy()
    {
        using var context = TransferContext.Create();
        var state = RichState();
        var settings = AppSettings.Default with { NotificationsEnabled = false };
        var exportPath = Path.Combine(context.Root, "exports", "backup.json");

        await context.Transfer.ExportAsync(exportPath, state, settings, "0.1.0");

        var json = await File.ReadAllTextAsync(exportPath);
        var package = JsonSerializer.Deserialize<SaveExportPackage>(json, JsonOptions());
        Assert.NotNull(package);
        Assert.Equal("0.1.0", package!.AppVersion);
        Assert.Equal(123_456_789, package.GameState.UsedSinceInstall);
        Assert.Equal(23_000_000, package.GameState.SpentTokens);
        Assert.Equal(2, package.GameState.Inventory.RareCandyCount);
        Assert.Single(package.GameState.CatchLog);
        Assert.False(package.Settings!.NotificationsEnabled);
    }

    [Fact]
    public async Task ValidImportBacksUpCurrentSaveAndReplacesState()
    {
        using var context = TransferContext.Create();
        var current = GameSaveState.New() with { UsedSinceInstall = 1 };
        await context.GameStore.SaveAsync(current);
        var imported = RichState() with { UsedSinceInstall = 777 };
        var importPath = await context.WritePackageAsync(imported);

        var result = await context.Transfer.ImportAsync(importPath, includeSettings: true);
        var loaded = await context.GameStore.LoadAsync();

        Assert.True(result.IsValid);
        Assert.Equal(777, loaded.UsedSinceInstall);
        Assert.True(Directory.EnumerateFiles(Path.Combine(context.Paths.RoamingStateDirectory, "Backups"), "state-backup-*.json").Any());
    }

    [Fact]
    public async Task ImportRejectsInvalidJson()
    {
        using var context = TransferContext.Create();
        var path = Path.Combine(context.Root, "bad.json");
        Directory.CreateDirectory(context.Root);
        await File.WriteAllTextAsync(path, "{not json");

        var result = await context.Transfer.ValidateImportAsync(path);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ImportRejectsFutureSchema()
    {
        using var context = TransferContext.Create();
        var path = await context.WritePackageAsync(GameSaveState.New() with { SchemaVersion = GameSaveState.CurrentSchemaVersion + 1 });

        var result = await context.Transfer.ValidateImportAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains("future", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRejectsNegativeInventory()
    {
        using var context = TransferContext.Create();
        var path = await context.WritePackageAsync(GameSaveState.New() with { Inventory = new InventoryState { RareCandyCount = -1 } });

        var result = await context.Transfer.ValidateImportAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains("Inventory", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRejectsInvalidSpeciesId()
    {
        using var context = TransferContext.Create();
        var active = new ActiveCompanionState(999, 999, new[] { 999 }, 0, 0, Rarity.Common, PokemonNature.Hardy, false, DateTimeOffset.UtcNow);
        var path = await context.WritePackageAsync(GameSaveState.New() with { Companion = new CompanionState(null, active) });

        var result = await context.Transfer.ValidateImportAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains("companion", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedImportValidationLeavesExistingStateIntact()
    {
        using var context = TransferContext.Create();
        await context.GameStore.SaveAsync(GameSaveState.New() with { UsedSinceInstall = 42 });
        var invalidPath = await context.WritePackageAsync(GameSaveState.New() with { SpentTokens = -5 });

        var result = await context.Transfer.ImportAsync(invalidPath, includeSettings: false);
        var loaded = await context.GameStore.LoadAsync();

        Assert.False(result.IsValid);
        Assert.Equal(42, loaded.UsedSinceInstall);
    }

    private static GameSaveState RichState()
    {
        var active = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageProgress: 50, shiny: true).Active!;
        var catchEntry = new CatchLogEntry
        {
            IndividualId = active.IndividualId,
            BaseSpeciesId = 1,
            PlannedPathSpeciesIds = new[] { 1, 2, 3 },
            EncounteredSpeciesIds = new[] { 1 },
            FinalSpeciesId = 1,
            Rarity = Rarity.Common,
            Nature = PokemonNature.Hardy,
            IsShiny = true,
            HatchTime = active.HatchTime,
            Status = CompanionLifecycleStatus.Active
        };

        return GameSaveState.New() with
        {
            Companion = new CompanionState(null, active),
            UsedSinceInstall = 123_456_789,
            SpentTokens = 23_000_000,
            Inventory = new InventoryState { RareCandyCount = 2, MintCount = 1, HasShinyCharm = true },
            SpeciesNames = new Dictionary<int, string> { [1] = "Bulbasaur" },
            ActiveCatch = catchEntry,
            CatchLog = new[] { catchEntry }
        };
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);

    private sealed class TransferContext : IDisposable
    {
        private TransferContext(string root)
        {
            Root = root;
            Paths = new TestAppPathProvider(root);
            var logger = new NullAppLogger();
            var storage = new JsonFileStorage(logger);
            GameStore = new GameStateStore(Paths, storage, logger);
            SettingsStore = new SettingsStore(Paths, storage, logger);
            Transfer = new SaveTransferService(GameStore, SettingsStore, Paths, new FakeClock(new DateTimeOffset(2026, 8, 18, 16, 10, 0, TimeSpan.Zero)));
        }

        public string Root { get; }

        public TestAppPathProvider Paths { get; }

        public GameStateStore GameStore { get; }

        public SettingsStore SettingsStore { get; }

        public SaveTransferService Transfer { get; }

        public static TransferContext Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"ptb-transfer-{Guid.NewGuid():N}");
            return new TransferContext(root);
        }

        public async Task<string> WritePackageAsync(GameSaveState state)
        {
            var path = Path.Combine(Root, $"import-{Guid.NewGuid():N}.json");
            var package = new SaveExportPackage
            {
                ExportedAt = DateTimeOffset.UtcNow,
                AppVersion = "test",
                GameState = state,
                Settings = AppSettings.Default
            };
            Directory.CreateDirectory(Root);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(package, JsonOptions()));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
