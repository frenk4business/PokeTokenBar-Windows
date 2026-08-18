namespace PokeTokenBar.Core.Game;

public sealed record ActiveCompanionState
{
    public ActiveCompanionState(
        int baseSpeciesId,
        int currentSpeciesId,
        IReadOnlyList<int> plannedPathSpeciesIds,
        int stageIndex,
        long stageProgressTokens,
        Rarity rarity,
        PokemonNature nature,
        bool isShiny,
        DateTimeOffset hatchTime,
        long totalAppliedProgressTokens = 0,
        IReadOnlyList<int>? realizedSpeciesIds = null,
        string? individualId = null)
    {
        if (plannedPathSpeciesIds.Count == 0)
        {
            throw new ArgumentException("A planned path must contain at least one species.", nameof(plannedPathSpeciesIds));
        }

        if (stageIndex < 0 || stageIndex >= plannedPathSpeciesIds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(stageIndex), "Stage index must be inside the planned path.");
        }

        if (stageProgressTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stageProgressTokens), "Stage progress cannot be negative.");
        }

        if (totalAppliedProgressTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAppliedProgressTokens), "Total progress cannot be negative.");
        }

        BaseSpeciesId = baseSpeciesId;
        CurrentSpeciesId = currentSpeciesId;
        PlannedPathSpeciesIds = plannedPathSpeciesIds.ToArray();
        StageIndex = stageIndex;
        StageProgressTokens = stageProgressTokens;
        Rarity = rarity;
        Nature = nature;
        IsShiny = isShiny;
        HatchTime = hatchTime;
        TotalAppliedProgressTokens = totalAppliedProgressTokens;
        RealizedSpeciesIds = realizedSpeciesIds?.ToArray() ?? PlannedPathSpeciesIds.Take(stageIndex + 1).ToArray();
        IndividualId = string.IsNullOrWhiteSpace(individualId) ? Guid.NewGuid().ToString("N") : individualId;
    }

    public int BaseSpeciesId { get; init; }

    public int CurrentSpeciesId { get; init; }

    public IReadOnlyList<int> PlannedPathSpeciesIds { get; init; }

    public int StageIndex { get; init; }

    public long StageProgressTokens { get; init; }

    public Rarity Rarity { get; init; }

    public PokemonNature Nature { get; init; }

    public bool IsShiny { get; init; }

    public DateTimeOffset HatchTime { get; init; }

    public long TotalAppliedProgressTokens { get; init; }

    public IReadOnlyList<int> RealizedSpeciesIds { get; init; }

    public string IndividualId { get; init; }
}
