using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Tests.Infrastructure;

internal sealed class TestRandomSource : IRandomSource
{
    private readonly Queue<int> _ints;

    public TestRandomSource(params int[] ints)
    {
        _ints = new Queue<int>(ints);
    }

    public int NextInt32(int exclusiveMax)
    {
        var value = _ints.Count > 0 ? _ints.Dequeue() : 0;
        return value % exclusiveMax;
    }

    public ulong NextUInt64() => 0;
}
