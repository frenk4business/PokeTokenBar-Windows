using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.PokeApi;

public sealed class PokeApiHatchService : IAsyncHatchService
{
    private readonly IPokemonDataRepository _repository;
    private readonly WeightedPokemonSelector _selector;
    private readonly EvolutionPathSelector _pathSelector;
    private readonly NatureService _natureService;
    private readonly ShinyRoller _shinyRoller;
    private readonly IRandomSource _randomSource;

    public PokeApiHatchService(
        IPokemonDataRepository repository,
        WeightedPokemonSelector selector,
        EvolutionPathSelector pathSelector,
        NatureService natureService,
        ShinyRoller shinyRoller,
        IRandomSource randomSource)
    {
        _repository = repository;
        _selector = selector;
        _pathSelector = pathSelector;
        _natureService = natureService;
        _shinyRoller = shinyRoller;
        _randomSource = randomSource;
    }

    public async Task<HatchResult> HatchAsync(HatchRequest request, CancellationToken cancellationToken = default)
    {
        var index = await _repository.GetStartingSpeciesIndexAsync(cancellationToken).ConfigureAwait(false);
        var baseSpecies = _selector.Select(index.Entries, request.Tier, _randomSource, request.CollectedFinalKeys);
        var tree = await _repository.GetEvolutionTreeAsync(baseSpecies.Id, cancellationToken).ConfigureAwait(false);
        var path = _pathSelector.SelectPath(tree, _randomSource);
        var nature = _natureService.SelectNature(_randomSource);
        var shiny = _shinyRoller.Roll(_randomSource, request.ShinyCharmActive);
        return new HatchResult(path, baseSpecies.Rarity, nature, shiny);
    }
}
