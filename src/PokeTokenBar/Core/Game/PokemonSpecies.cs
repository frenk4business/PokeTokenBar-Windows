namespace PokeTokenBar.Core.Game;

public sealed record PokemonSpecies(
    int NationalDexId,
    string Name,
    int Generation,
    Rarity Rarity,
    int? CaptureRate = null,
    bool IsLegendary = false,
    bool IsMythical = false);
