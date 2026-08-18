namespace PokeTokenBar.Core.Game;

public static class RarityClassifier
{
    public static Rarity Classify(int captureRate, bool isLegendary, bool isMythical)
    {
        if (isLegendary || isMythical)
        {
            return Rarity.Legendary;
        }

        if (captureRate <= 45)
        {
            return Rarity.Rare;
        }

        return captureRate <= 120 ? Rarity.Uncommon : Rarity.Common;
    }
}
