namespace PokeTokenBar.Core.Game;

public sealed record GraduatedCompanion(
    int BaseSpeciesId,
    int FinalSpeciesId,
    IReadOnlyList<int> PlannedPathSpeciesIds,
    IReadOnlyList<int> RealizedSpeciesIds,
    Rarity Rarity,
    PokemonNature Nature,
    bool IsShiny,
    DateTimeOffset HatchTime,
    DateTimeOffset GraduationTime,
    long TotalAppliedProgressTokens,
    string? IndividualId = null);
