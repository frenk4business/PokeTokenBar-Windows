namespace PokeTokenBar.Services.PokeApi;

public interface IPokeApiClient
{
    Task<PokemonSpeciesDto> GetSpeciesAsync(int id, CancellationToken cancellationToken = default);

    Task<EvolutionChainDto> GetEvolutionChainAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BaseSpeciesEntry>> GetBaseSpeciesEntriesAsync(CancellationToken cancellationToken = default);
}
