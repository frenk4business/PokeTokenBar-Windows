using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.Gameplay;

public sealed record GameSaveState
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public CompanionState Companion { get; init; } = CompanionState.FreshEgg();

    public bool InstallBaselineSet { get; init; }

    public string? ClaimedLocalDate { get; init; }

    public long ClaimedTodayTokens { get; init; }

    public long UsedSinceInstall { get; init; }

    public Dictionary<int, string> SpeciesNames { get; init; } = [];

    public GraduatedCompanion? LastGraduation { get; init; }

    public Dictionary<int, PokedexSpeciesEntry> Pokedex { get; init; } = [];

    public CatchLogEntry? ActiveCatch { get; init; }

    public IReadOnlyList<CatchLogEntry> CatchLog { get; init; } = Array.Empty<CatchLogEntry>();

    public bool LastGraduationImportedToCatchLog { get; init; }

    public long SpentTokens { get; init; }

    public InventoryState Inventory { get; init; } = new();

    public string? LastError { get; init; }

    public long AvailableBalance => Math.Max(0, UsedSinceInstall - SpentTokens);

    public static GameSaveState New() => new();
}
