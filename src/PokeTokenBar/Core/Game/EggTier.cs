namespace PokeTokenBar.Core.Game;

public enum EggTier
{
    Normal,
    Uncommon,
    Rare
}

public static class EggTierExtensions
{
    public static bool Allows(this EggTier tier, Rarity rarity) => tier switch
    {
        EggTier.Normal => true,
        EggTier.Uncommon => rarity is Rarity.Uncommon or Rarity.Rare or Rarity.Legendary,
        EggTier.Rare => rarity is Rarity.Rare or Rarity.Legendary,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
    };
}
