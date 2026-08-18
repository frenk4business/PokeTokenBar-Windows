using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Tests.Game;

public sealed class RarityClassifierTests
{
    [Theory]
    [InlineData(255, false, false, Rarity.Common)]
    [InlineData(121, false, false, Rarity.Common)]
    [InlineData(120, false, false, Rarity.Uncommon)]
    [InlineData(46, false, false, Rarity.Uncommon)]
    [InlineData(45, false, false, Rarity.Rare)]
    [InlineData(255, true, false, Rarity.Legendary)]
    [InlineData(255, false, true, Rarity.Legendary)]
    public void ClassifiesCaptureRateBoundaries(int captureRate, bool legendary, bool mythical, Rarity expected)
    {
        Assert.Equal(expected, RarityClassifier.Classify(captureRate, legendary, mythical));
    }
}
