using PokeTokenBar.Services.PokeApi;

namespace PokeTokenBar.Tests.Providers.PokeApi;

internal sealed class FakePokeApiClient : IPokeApiClient
{
    private readonly Dictionary<int, PokemonSpeciesDto> _species = [];
    private readonly Dictionary<int, EvolutionChainDto> _chains = [];
    private IReadOnlyList<int> _baseIds = Array.Empty<int>();

    public int SpeciesRequests { get; private set; }

    public int ChainRequests { get; private set; }

    public bool Offline { get; set; }

    public FakePokeApiClient WithBaseIds(params int[] ids)
    {
        _baseIds = ids;
        return this;
    }

    public FakePokeApiClient WithSpecies(PokemonSpeciesDto species)
    {
        _species[species.Id] = species;
        return this;
    }

    public FakePokeApiClient WithChain(EvolutionChainDto chain)
    {
        _chains[chain.Id] = chain;
        return this;
    }

    public Task<IReadOnlyList<BaseSpeciesEntry>> GetBaseSpeciesEntriesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfOffline();
        var entries = _baseIds
            .Where(id => _species.ContainsKey(id))
            .Where(id => PokeApiMapper.IsEligibleBase(_species[id]))
            .Select(id => PokeApiMapper.ToBaseEntry(_species[id]))
            .ToArray();
        return Task.FromResult<IReadOnlyList<BaseSpeciesEntry>>(entries);
    }

    public Task<EvolutionChainDto> GetEvolutionChainAsync(int id, CancellationToken cancellationToken = default)
    {
        ThrowIfOffline();
        ChainRequests++;
        return Task.FromResult(_chains[id]);
    }

    public Task<PokemonSpeciesDto> GetSpeciesAsync(int id, CancellationToken cancellationToken = default)
    {
        ThrowIfOffline();
        SpeciesRequests++;
        return Task.FromResult(_species[id]);
    }

    private void ThrowIfOffline()
    {
        if (Offline)
        {
            throw new PokeApiException("offline");
        }
    }
}
