using System.Text.Json.Serialization;

namespace PokeTokenBar.Services.PokeApi;

public sealed record NamedApiResource(string Name, string? Url);

public sealed record PokemonSpeciesDto(
    int Id,
    string Name,
    [property: JsonPropertyName("capture_rate")] int CaptureRate,
    [property: JsonPropertyName("is_legendary")] bool IsLegendary,
    [property: JsonPropertyName("is_mythical")] bool IsMythical,
    IReadOnlyList<LocalizedNameDto> Names,
    NamedApiResource Generation,
    [property: JsonPropertyName("evolves_from_species")] NamedApiResource? EvolvesFromSpecies,
    [property: JsonPropertyName("evolution_chain")] UrlResource EvolutionChain);

public sealed record LocalizedNameDto(string Name, NamedApiResource Language);

public sealed record UrlResource(string Url);

public sealed record EvolutionChainDto(int Id, EvolutionChainLink Chain);

public sealed record EvolutionChainLink(
    NamedApiResource Species,
    [property: JsonPropertyName("evolves_to")] IReadOnlyList<EvolutionChainLink> EvolvesTo);

public sealed record GraphQlBaseSpeciesResponse(GraphQlBaseSpeciesData Data);

public sealed record GraphQlBaseSpeciesData(
    [property: JsonPropertyName("pokemonspecies")] IReadOnlyList<GraphQlBaseSpeciesRow> PokemonSpecies);

public sealed record GraphQlBaseSpeciesRow(
    int Id,
    string Name,
    [property: JsonPropertyName("capture_rate")] int CaptureRate,
    [property: JsonPropertyName("is_legendary")] bool IsLegendary,
    [property: JsonPropertyName("is_mythical")] bool IsMythical,
    [property: JsonPropertyName("generation_id")] int GenerationId);
