using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Tests.Game;

internal static class GameFixtures
{
    public static readonly PokemonSpecies Bulbasaur = new(1, "Bulbasaur", 1, Rarity.Common);
    public static readonly PokemonSpecies Ivysaur = new(2, "Ivysaur", 1, Rarity.Common);
    public static readonly PokemonSpecies Venusaur = new(3, "Venusaur", 1, Rarity.Common);

    public static readonly PokemonSpecies Rattata = new(19, "Rattata", 1, Rarity.Common);
    public static readonly PokemonSpecies Raticate = new(20, "Raticate", 1, Rarity.Common);

    public static readonly PokemonSpecies Lapras = new(131, "Lapras", 1, Rarity.Rare);

    public static readonly PokemonSpecies Eevee = new(133, "Eevee", 1, Rarity.Uncommon);
    public static readonly PokemonSpecies Vaporeon = new(134, "Vaporeon", 1, Rarity.Uncommon);
    public static readonly PokemonSpecies Jolteon = new(135, "Jolteon", 1, Rarity.Uncommon);
    public static readonly PokemonSpecies Flareon = new(136, "Flareon", 1, Rarity.Uncommon);

    public static EvolutionPath BulbasaurPath() => new(new[] { Bulbasaur, Ivysaur, Venusaur });

    public static EvolutionPath RattataPath() => new(new[] { Rattata, Raticate });

    public static EvolutionPath LaprasPath() => new(new[] { Lapras });

    public static HatchResult BulbasaurHatch(bool shiny = false) => new(BulbasaurPath(), Rarity.Common, PokemonNature.Hardy, shiny);

    public static HatchResult RattataHatch() => new(RattataPath(), Rarity.Common, PokemonNature.Bold, false);

    public static HatchResult LaprasHatch() => new(LaprasPath(), Rarity.Rare, PokemonNature.Calm, false);

    public static CompanionState Active(EvolutionPath path, Rarity rarity, int stageIndex = 0, long stageProgress = 0, PokemonNature nature = PokemonNature.Hardy, bool shiny = false)
    {
        var speciesId = path.Species[stageIndex].NationalDexId;
        return new CompanionState(null, new ActiveCompanionState(
            path.Species[0].NationalDexId,
            speciesId,
            path.SpeciesIds,
            stageIndex,
            stageProgress,
            rarity,
            nature,
            shiny,
            new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero)));
    }
}
