namespace PokeTokenBar.Core.Game;

public sealed class EvolutionPathSelector
{
    public EvolutionPath SelectPath(EvolutionNode root, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(randomSource);

        var selected = new List<PokemonSpecies>();
        var current = root;

        while (true)
        {
            selected.Add(current.Species);
            if (current.Children.Count == 0)
            {
                return new EvolutionPath(selected);
            }

            current = current.Children[randomSource.NextInt32(current.Children.Count)];
        }
    }
}
