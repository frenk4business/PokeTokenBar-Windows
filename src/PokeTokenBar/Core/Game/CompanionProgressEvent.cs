namespace PokeTokenBar.Core.Game;

public abstract record CompanionProgressEvent(long TokensApplied);

public sealed record EggProgressed(long TokensApplied, long EggProgressTokens) : CompanionProgressEvent(TokensApplied);

public sealed record Hatched(long TokensApplied, int SpeciesId, Rarity Rarity, PokemonNature Nature, bool IsShiny) : CompanionProgressEvent(TokensApplied);

public sealed record StageProgressed(long TokensApplied, int SpeciesId, int StageIndex, long StageProgressTokens) : CompanionProgressEvent(TokensApplied);

public sealed record Evolved(long TokensApplied, int FromSpeciesId, int ToSpeciesId, int StageIndex) : CompanionProgressEvent(TokensApplied);

public sealed record Graduated(long TokensApplied, GraduatedCompanion Companion) : CompanionProgressEvent(TokensApplied);
