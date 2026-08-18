using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Gameplay;

namespace PokeTokenBar.Services.ImportExport;

public static class SaveValidator
{
    public static ImportValidationResult Validate(SaveExportPackage package)
    {
        if (package.PackageVersion is < 1 or > SaveExportPackage.CurrentPackageVersion)
        {
            return Invalid("Unsupported backup package version.");
        }

        var state = package.GameState;
        if (state.SchemaVersion > GameSaveState.CurrentSchemaVersion)
        {
            return Invalid("Unsupported future save schema.");
        }

        if (state.UsedSinceInstall < 0 || state.SpentTokens < 0 || state.ClaimedTodayTokens < 0)
        {
            return Invalid("Token ledger values must be nonnegative.");
        }

        if (state.Inventory is null)
        {
            return Invalid("Inventory is missing.");
        }

        if (state.Inventory.RareCandyCount < 0 || state.Inventory.MintCount < 0)
        {
            return Invalid("Inventory counts must be nonnegative.");
        }

        if (state.Companion.Egg is not null && state.Companion.Egg.ProgressTokens < 0)
        {
            return Invalid("Egg progress must be nonnegative.");
        }

        if (state.Companion.Active is not null && !ValidateActive(state.Companion.Active))
        {
            return Invalid("Active companion data is invalid.");
        }

        foreach (var id in state.Pokedex.Keys)
        {
            if (!IsValidSpeciesId(id))
            {
                return Invalid("Pokedex contains an invalid species ID.");
            }
        }

        foreach (var entry in state.CatchLog)
        {
            if (!IsValidSpeciesId(entry.BaseSpeciesId) || !entry.PlannedPathSpeciesIds.All(IsValidSpeciesId) || !entry.EncounteredSpeciesIds.All(IsValidSpeciesId))
            {
                return Invalid("Catch Log contains an invalid species ID.");
            }
        }

        return new ImportValidationResult(true, "Backup is valid.", package);
    }

    private static bool ValidateActive(ActiveCompanionState active)
    {
        return IsValidSpeciesId(active.BaseSpeciesId)
            && IsValidSpeciesId(active.CurrentSpeciesId)
            && active.PlannedPathSpeciesIds.Count > 0
            && active.PlannedPathSpeciesIds.All(IsValidSpeciesId)
            && active.StageIndex >= 0
            && active.StageIndex < active.PlannedPathSpeciesIds.Count
            && active.StageProgressTokens >= 0
            && active.TotalAppliedProgressTokens >= 0;
    }

    private static bool IsValidSpeciesId(int speciesId) => speciesId is >= 1 and <= 649;

    private static ImportValidationResult Invalid(string message) => new(false, message);
}
