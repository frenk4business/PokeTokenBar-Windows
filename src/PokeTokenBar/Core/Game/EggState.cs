namespace PokeTokenBar.Core.Game;

public sealed record EggState
{
    public EggState(long progressTokens = 0, Rarity? guaranteedMinimumRarity = null)
    {
        if (progressTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(progressTokens), "Egg progress cannot be negative.");
        }

        ProgressTokens = progressTokens;
        GuaranteedMinimumRarity = guaranteedMinimumRarity;
    }

    public long ProgressTokens { get; init; }

    public Rarity? GuaranteedMinimumRarity { get; init; }
}
