using PokeTokenBar.Services.PokeApi;

namespace PokeTokenBar.Tests.Providers.PokeApi;

internal static class PokeApiTestDtos
{
    public static PokemonSpeciesDto Species(
        int id,
        string name,
        int captureRate = 255,
        string generation = "generation-i",
        bool legendary = false,
        bool mythical = false,
        int chainId = 1,
        int? evolvesFrom = null)
    {
        return new PokemonSpeciesDto(
            id,
            name,
            captureRate,
            legendary,
            mythical,
            new[] { new LocalizedNameDto(ToTitle(name), new NamedApiResource("en", null)) },
            new NamedApiResource(generation, null),
            evolvesFrom is null ? null : new NamedApiResource($"species-{evolvesFrom}", SpeciesUrl(evolvesFrom.Value)),
            new UrlResource($"https://pokeapi.co/api/v2/evolution-chain/{chainId}/"));
    }

    public static EvolutionChainDto Chain(int id, EvolutionChainLink root) => new(id, root);

    public static EvolutionChainLink Link(int id, params EvolutionChainLink[] children)
    {
        return new EvolutionChainLink(new NamedApiResource($"species-{id}", SpeciesUrl(id)), children);
    }

    private static string SpeciesUrl(int id) => $"https://pokeapi.co/api/v2/pokemon-species/{id}/";

    private static string ToTitle(string value) => string.Concat(value[..1].ToUpperInvariant(), value[1..]);
}
