namespace PokeTokenBar.Core.Game;

public sealed record EvolutionNode
{
    public EvolutionNode(PokemonSpecies species, IReadOnlyList<EvolutionNode>? children = null)
    {
        Species = species;
        Children = children ?? Array.Empty<EvolutionNode>();
    }

    public PokemonSpecies Species { get; }

    public IReadOnlyList<EvolutionNode> Children { get; }
}
