namespace PokeTokenBar.Core.Game;

public sealed record EvolutionPath
{
    public EvolutionPath(IReadOnlyList<PokemonSpecies> species)
    {
        if (species.Count == 0)
        {
            throw new ArgumentException("An evolution path must contain at least one species.", nameof(species));
        }

        Species = species.ToArray();
    }

    public IReadOnlyList<PokemonSpecies> Species { get; }

    public IReadOnlyList<int> SpeciesIds => Species.Select(species => species.NationalDexId).ToArray();
}
