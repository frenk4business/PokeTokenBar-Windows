using System.IO;
using System.Net.Http;
using System.Text.Json;
using PokeTokenBar.Core.Game;
using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Services.PokeApi;

public sealed class PokeApiPokemonRepository : IPokemonDataRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IPokeApiClient _client;
    private readonly IAppPathProvider _paths;
    private readonly IAppLogger _logger;
    private readonly Dictionary<int, PokemonSpeciesDto> _speciesMemory = [];
    private readonly Dictionary<int, EvolutionChainDto> _chainMemory = [];
    private StartingSpeciesIndex? _indexMemory;

    public PokeApiPokemonRepository(IPokeApiClient client, IAppPathProvider paths, IAppLogger logger)
    {
        _client = client;
        _paths = paths;
        _logger = logger;
    }

    public async Task<StartingSpeciesIndex> GetStartingSpeciesIndexAsync(CancellationToken cancellationToken = default)
    {
        if (_indexMemory is not null)
        {
            return _indexMemory;
        }

        var indexPath = IndexPath();
        var cached = await TryReadJsonAsync<StartingSpeciesIndex>(indexPath, cancellationToken).ConfigureAwait(false);
        if (cached is { SchemaVersion: StartingSpeciesIndex.CurrentSchemaVersion } && cached.Entries.Count > 0)
        {
            _indexMemory = cached;
            return cached;
        }

        try
        {
            var entries = (await _client.GetBaseSpeciesEntriesAsync(cancellationToken).ConfigureAwait(false))
                .Where(entry => entry.EligibleAsStart && entry.Id is >= 1 and <= PokeApiMapper.MaxSupportedSpeciesId && entry.Id != PokeApiMapper.DittoSpeciesId && entry.Generation is >= 1 and <= 5)
                .OrderBy(entry => entry.Id)
                .ToArray();

            if (entries.Length == 0)
            {
                throw new PokeApiException("PokéAPI returned no eligible starting species.");
            }

            var index = new StartingSpeciesIndex(
                StartingSpeciesIndex.CurrentSchemaVersion,
                DateTimeOffset.UtcNow,
                entries);

            await WriteJsonAtomicAsync(indexPath, index, cancellationToken).ConfigureAwait(false);
            _indexMemory = index;
            return index;
        }
        catch (Exception ex) when (ex is PokeApiException or HttpRequestException or IOException or JsonException)
        {
            if (cached is not null && cached.Entries.Count > 0)
            {
                await _logger.LogAsync(AppLogLevel.Warning, "PokéAPI index refresh failed; using cached starting index.", ex, cancellationToken)
                    .ConfigureAwait(false);
                _indexMemory = cached;
                return cached;
            }

            throw new PokeApiException("Could not build the Pokémon starting-species index and no usable cache exists.", ex);
        }
    }

    public async Task<PokemonSpecies> GetSpeciesAsync(int id, CancellationToken cancellationToken = default)
    {
        var dto = await GetSpeciesDtoAsync(id, cancellationToken).ConfigureAwait(false);
        return PokeApiMapper.ToDomainSpecies(dto);
    }

    public async Task<EvolutionNode> GetEvolutionTreeAsync(int baseSpeciesId, CancellationToken cancellationToken = default)
    {
        var baseSpecies = await GetSpeciesDtoAsync(baseSpeciesId, cancellationToken).ConfigureAwait(false);
        var chainId = PokeApiMapper.IdFromUrl(baseSpecies.EvolutionChain.Url);
        var chain = await GetEvolutionChainDtoAsync(chainId, cancellationToken).ConfigureAwait(false);
        var node = await MapNodeAsync(chain.Chain, cancellationToken).ConfigureAwait(false);
        return node ?? new EvolutionNode(PokeApiMapper.ToDomainSpecies(baseSpecies));
    }

    private async Task<PokemonSpeciesDto> GetSpeciesDtoAsync(int id, CancellationToken cancellationToken)
    {
        if (_speciesMemory.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var path = SpeciesPath(id);
        var disk = await TryReadJsonAsync<PokemonSpeciesDto>(path, cancellationToken).ConfigureAwait(false);
        if (disk is not null)
        {
            _speciesMemory[id] = disk;
            return disk;
        }

        try
        {
            var dto = await _client.GetSpeciesAsync(id, cancellationToken).ConfigureAwait(false);
            await WriteJsonAtomicAsync(path, dto, cancellationToken).ConfigureAwait(false);
            _speciesMemory[id] = dto;
            return dto;
        }
        catch (Exception ex) when (ex is PokeApiException or IOException or JsonException)
        {
            throw new PokeApiException($"Pokemon species #{id} is unavailable and is not cached.", ex);
        }
    }

    private async Task<EvolutionChainDto> GetEvolutionChainDtoAsync(int id, CancellationToken cancellationToken)
    {
        if (_chainMemory.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var path = EvolutionPath(id);
        var disk = await TryReadJsonAsync<EvolutionChainDto>(path, cancellationToken).ConfigureAwait(false);
        if (disk is not null)
        {
            _chainMemory[id] = disk;
            return disk;
        }

        try
        {
            var dto = await _client.GetEvolutionChainAsync(id, cancellationToken).ConfigureAwait(false);
            await WriteJsonAtomicAsync(path, dto, cancellationToken).ConfigureAwait(false);
            _chainMemory[id] = dto;
            return dto;
        }
        catch (Exception ex) when (ex is PokeApiException or IOException or JsonException)
        {
            throw new PokeApiException($"Evolution chain #{id} is unavailable and is not cached.", ex);
        }
    }

    private async Task<EvolutionNode?> MapNodeAsync(EvolutionChainLink link, CancellationToken cancellationToken)
    {
        var id = PokeApiMapper.IdFromUrl(link.Species.Url ?? string.Empty);
        if (id is < 1 or > PokeApiMapper.MaxSupportedSpeciesId)
        {
            return null;
        }

        var species = await GetSpeciesAsync(id, cancellationToken).ConfigureAwait(false);
        var children = new List<EvolutionNode>();
        foreach (var child in link.EvolvesTo)
        {
            var mapped = await MapNodeAsync(child, cancellationToken).ConfigureAwait(false);
            if (mapped is not null)
            {
                children.Add(mapped);
            }
        }

        return new EvolutionNode(species, children);
    }

    private string PokeApiCacheDirectory()
    {
        var root = Path.Combine(_paths.LocalCacheDirectory, "pokeapi");
        Directory.CreateDirectory(Path.Combine(root, "species"));
        Directory.CreateDirectory(Path.Combine(root, "evolution-chains"));
        return root;
    }

    private string IndexPath() => Path.Combine(PokeApiCacheDirectory(), "starting-species-index-v1.json");

    private string SpeciesPath(int id) => Path.Combine(PokeApiCacheDirectory(), "species", $"{id}.json");

    private string EvolutionPath(int id) => Path.Combine(PokeApiCacheDirectory(), "evolution-chains", $"{id}.json");

    private async Task<T?> TryReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, useAsync: true);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            await _logger.LogAsync(AppLogLevel.Warning, $"PokéAPI cache entry {Path.GetFileName(path)} is invalid and will be refreshed.", ex, cancellationToken)
                .ConfigureAwait(false);
            return default;
        }
    }

    private static async Task WriteJsonAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, path, overwrite: true);
    }
}
