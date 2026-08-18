using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Tests.Game;

public sealed class BalanceTests
{
    [Theory]
    [InlineData(Rarity.Common, 750_000_000)]
    [InlineData(Rarity.Uncommon, 1_875_000_000)]
    [InlineData(Rarity.Rare, 3_000_000_000)]
    [InlineData(Rarity.Legendary, 6_000_000_000)]
    public void GraduationTotalsMatchUpstream(Rarity rarity, long expected)
    {
        Assert.Equal(expected, GameBalance.GraduationTotal(rarity));
    }

    [Fact]
    public void CommonThreeStageUsesOneTwoThreeWeighting()
    {
        Assert.Equal(125_000_000, GameBalance.PhaseThreshold(Rarity.Common, 3, 0));
        Assert.Equal(250_000_000, GameBalance.PhaseThreshold(Rarity.Common, 3, 1));
        Assert.Equal(375_000_000, GameBalance.PhaseThreshold(Rarity.Common, 3, 2));
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Legendary)]
    public void OneTwoAndThreeStageCostsTotalGraduationTotal(Rarity rarity)
    {
        AssertTotal(rarity, 1);
        AssertTotal(rarity, 2);
        AssertTotal(rarity, 3);
    }

    [Theory]
    [InlineData(Rarity.Common, 1, 750_000_000)]
    [InlineData(Rarity.Common, 2, 250_000_000, 500_000_000)]
    [InlineData(Rarity.Uncommon, 1, 1_875_000_000)]
    [InlineData(Rarity.Uncommon, 2, 625_000_000, 1_250_000_000)]
    [InlineData(Rarity.Rare, 1, 3_000_000_000)]
    [InlineData(Rarity.Rare, 2, 1_000_000_000, 2_000_000_000)]
    [InlineData(Rarity.Legendary, 1, 6_000_000_000)]
    [InlineData(Rarity.Legendary, 2, 2_000_000_000, 4_000_000_000)]
    public void KnownOneAndTwoStageThresholdsAreStable(Rarity rarity, int stages, long first, long second = 0)
    {
        Assert.Equal(first, GameBalance.PhaseThreshold(rarity, stages, 0));
        if (stages == 2)
        {
            Assert.Equal(second, GameBalance.PhaseThreshold(rarity, stages, 1));
        }
    }

    private static void AssertTotal(Rarity rarity, int stages)
    {
        var total = Enumerable.Range(0, stages).Sum(stage => GameBalance.PhaseThreshold(rarity, stages, stage));
        Assert.Equal(GameBalance.GraduationTotal(rarity), total);
    }
}
