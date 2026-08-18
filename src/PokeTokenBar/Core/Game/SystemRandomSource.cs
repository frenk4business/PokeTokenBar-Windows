namespace PokeTokenBar.Core.Game;

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource()
        : this(Random.Shared)
    {
    }

    public SystemRandomSource(Random random)
    {
        _random = random;
    }

    public int NextInt32(int exclusiveMax) => _random.Next(exclusiveMax);

    public ulong NextUInt64()
    {
        Span<byte> bytes = stackalloc byte[8];
        _random.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}
