namespace PokeTokenBar.Core.Game;

public static class GameBalance
{
    public const long EggHatchThreshold = 5_000_000;
    public const long RareCandyProgress = 100_000_000;
    public const ulong ShinyDenominator = 64;
    public const ulong ShinyCharmDenominator = 48;

    public static long GraduationTotal(Rarity rarity) => rarity switch
    {
        Rarity.Common => 750_000_000,
        Rarity.Uncommon => 1_875_000_000,
        Rarity.Rare => 3_000_000_000,
        Rarity.Legendary => 6_000_000_000,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null)
    };

    public static long PhaseThreshold(Rarity rarity, int totalForms, int stageIndex)
    {
        if (totalForms < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalForms), "Evolution paths must contain at least one form.");
        }

        if (stageIndex < 0 || stageIndex >= totalForms)
        {
            throw new ArgumentOutOfRangeException(nameof(stageIndex), "Stage index must be inside the selected evolution path.");
        }

        var denominator = totalForms * (totalForms + 1L) / 2L;
        var numerator = (decimal)GraduationTotal(rarity) * (stageIndex + 1L);
        return (long)Math.Round(numerator / denominator, MidpointRounding.AwayFromZero);
    }
}
