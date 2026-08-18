using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.PokeApi;

public interface IPokemonDataRepository
{
    Task<StartingSpeciesIndex> GetStartingSpeciesIndexAsync(CancellationToken cancellationToken = default);

    Task<PokemonSpecies> GetSpeciesAsync(int id, CancellationToken cancellationToken = default);

    Task<EvolutionNode> GetEvolutionTreeAsync(int baseSpeciesId, CancellationToken cancellationToken = default);
}
