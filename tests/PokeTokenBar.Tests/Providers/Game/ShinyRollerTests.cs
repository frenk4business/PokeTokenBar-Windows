using PokeTokenBar.Core.Game;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Game;

public sealed class ShinyRollerTests
{
    [Fact]
    public void NormalShinyRollUsesOneInSixtyFour()
    {
        var roller = new ShinyRoller();

        Assert.True(roller.Roll(new UIntRandomSource(64), shinyCharmActive: false));
        Assert.False(roller.Roll(new UIntRandomSource(63), shinyCharmActive: false));
    }

    [Fact]
    public void ShinyCharmRollUsesOneInFortyEight()
    {
        var roller = new ShinyRoller();

        Assert.True(roller.Roll(new UIntRandomSource(48), shinyCharmActive: true));
        Assert.False(roller.Roll(new UIntRandomSource(47), shinyCharmActive: true));
    }

    private sealed class UIntRandomSource : IRandomSource
    {
        private readonly ulong _value;

        public UIntRandomSource(ulong value)
        {
            _value = value;
        }

        public int NextInt32(int exclusiveMax) => 0;

        public ulong NextUInt64() => _value;
    }
}
