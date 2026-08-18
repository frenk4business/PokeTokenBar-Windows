using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.Gameplay;

public sealed record PokedexSpeciesEntry
{
    public int SpeciesId { get; init; }

    public string DisplayName { get; init; } = "";

    public bool Owned { get; init; }

    public bool ShinyOwned { get; init; }

    public DateTimeOffset FirstEncounteredAt { get; init; }

    public DateTimeOffset LatestEncounteredAt { get; init; }

    public int Generation { get; init; }

    public Rarity Rarity { get; init; }
}
