namespace PokeTokenBar.Core.Game;

public sealed record HatchRequest(
    EggTier Tier = EggTier.Normal,
    bool ShinyCharmActive = false,
    IReadOnlySet<string>? CollectedFinalKeys = null);
