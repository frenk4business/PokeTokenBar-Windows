using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.Gameplay;

public enum CompanionLifecycleStatus
{
    Active,
    Graduated,
    Discarded
}

public sealed record EvolutionHistoryEntry(
    int SpeciesId,
    string DisplayName,
    DateTimeOffset EncounteredAt,
    int StageIndex);

public sealed record CatchLogEntry
{
    public string IndividualId { get; init; } = "";

    public int BaseSpeciesId { get; init; }

    public IReadOnlyList<int> PlannedPathSpeciesIds { get; init; } = Array.Empty<int>();

    public IReadOnlyList<int> EncounteredSpeciesIds { get; init; } = Array.Empty<int>();

    public int FinalSpeciesId { get; init; }

    public Rarity Rarity { get; init; }

    public PokemonNature Nature { get; init; }

    public bool IsShiny { get; init; }

    public DateTimeOffset HatchTime { get; init; }

    public DateTimeOffset? GraduationTime { get; init; }

    public CompanionLifecycleStatus Status { get; init; }

    public long TotalAppliedProgressTokens { get; init; }

    public IReadOnlyList<EvolutionHistoryEntry> EvolutionHistory { get; init; } = Array.Empty<EvolutionHistoryEntry>();
}
