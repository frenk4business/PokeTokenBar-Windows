namespace PokeTokenBar.Core.Game;

public sealed record HatchResult(EvolutionPath SelectedPath, Rarity Rarity, PokemonNature Nature, bool IsShiny)
{
    public PokemonSpecies BaseSpecies => SelectedPath.Species[0];
}
