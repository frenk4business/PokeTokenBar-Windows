using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Settings;

namespace PokeTokenBar.Services.ImportExport;

public sealed record SaveExportPackage
{
    public const int CurrentPackageVersion = 1;

    public int PackageVersion { get; init; } = CurrentPackageVersion;

    public DateTimeOffset ExportedAt { get; init; }

    public string AppVersion { get; init; } = "";

    public GameSaveState GameState { get; init; } = GameSaveState.New();

    public AppSettings? Settings { get; init; }
}

public sealed record ImportValidationResult(bool IsValid, string Message, SaveExportPackage? Package = null);
