namespace PokeTokenBar.Core.Game;

public interface IRandomSource
{
    int NextInt32(int exclusiveMax);

    ulong NextUInt64();
}
