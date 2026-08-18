using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.PokeApi;

public static class PokeApiMapper
{
    public const int MaxSupportedSpeciesId = 649;
    public const int DittoSpeciesId = 132;

    public static PokemonSpecies ToDomainSpecies(PokemonSpeciesDto dto)
    {
        return new PokemonSpecies(
            dto.Id,
            EnglishName(dto),
            ParseGeneration(dto.Generation.Name),
            RarityClassifier.Classify(dto.CaptureRate, dto.IsLegendary, dto.IsMythical),
            dto.CaptureRate,
            dto.IsLegendary,
            dto.IsMythical);
    }

    public static BaseSpeciesEntry ToBaseEntry(PokemonSpeciesDto dto)
    {
        return new BaseSpeciesEntry(
            dto.Id,
            EnglishName(dto),
            ParseGeneration(dto.Generation.Name),
            dto.CaptureRate,
            dto.IsLegendary,
            dto.IsMythical,
            RarityClassifier.Classify(dto.CaptureRate, dto.IsLegendary, dto.IsMythical),
            IdFromUrl(dto.EvolutionChain.Url));
    }

    public static bool IsEligibleBase(PokemonSpeciesDto dto)
    {
        return dto.Id is >= 1 and <= MaxSupportedSpeciesId
            && dto.Id != DittoSpeciesId
            && dto.EvolvesFromSpecies is null
            && ParseGeneration(dto.Generation.Name) is >= 1 and <= 5;
    }

    public static int ParseGeneration(string generationName) => generationName.ToLowerInvariant() switch
    {
        "generation-i" => 1,
        "generation-ii" => 2,
        "generation-iii" => 3,
        "generation-iv" => 4,
        "generation-v" => 5,
        _ => 0
    };

    public static string EnglishName(PokemonSpeciesDto dto)
    {
        return dto.Names.FirstOrDefault(name => name.Language.Name == "en")?.Name ?? dto.Name;
    }

    public static int IdFromUrl(string url)
    {
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return int.TryParse(parts.LastOrDefault(), out var id) ? id : 0;
    }
}
