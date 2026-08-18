using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.PokeApi;

public sealed record BaseSpeciesEntry(
    int Id,
    string Name,
    int Generation,
    int CaptureRate,
    bool IsLegendary,
    bool IsMythical,
    Rarity Rarity,
    int EvolutionChainId,
    bool EligibleAsStart = true);

public sealed record StartingSpeciesIndex(
    int SchemaVersion,
    DateTimeOffset FetchedAt,
    IReadOnlyList<BaseSpeciesEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;
}
