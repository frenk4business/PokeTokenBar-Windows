namespace PokeTokenBar.Core.Game;

public sealed class NatureService
{
    private static readonly PokemonNature[] AllNatures = Enum.GetValues<PokemonNature>();

    public PokemonNature SelectNature(IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        return AllNatures[randomSource.NextInt32(AllNatures.Length)];
    }

    public PokemonNature RerollDifferent(PokemonNature current, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        var pool = AllNatures.Where(nature => nature != current).ToArray();
        return pool[randomSource.NextInt32(pool.Length)];
    }
}
