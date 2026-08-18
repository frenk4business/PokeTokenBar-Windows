namespace PokeTokenBar.Core.Game;

public sealed class ShinyRoller
{
    public bool Roll(IRandomSource randomSource, bool shinyCharmActive)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        var denominator = shinyCharmActive ? GameBalance.ShinyCharmDenominator : GameBalance.ShinyDenominator;
        return randomSource.NextUInt64() % denominator == 0;
    }
}
