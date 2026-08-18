namespace PokeTokenBar.Core.Game;

public sealed record CompanionState(EggState? Egg, ActiveCompanionState? Active)
{
    public static CompanionState FreshEgg(Rarity? guaranteedMinimumRarity = null) => new(new EggState(0, guaranteedMinimumRarity), null);

    public bool IsEgg => Egg is not null;
}
