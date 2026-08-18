using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PokeTokenBar.Services.PokeApi;

public sealed class PokeApiHttpClient : IPokeApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public PokeApiHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<PokemonSpeciesDto> GetSpeciesAsync(int id, CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<PokemonSpeciesDto>($"https://pokeapi.co/api/v2/pokemon-species/{id}", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EvolutionChainDto> GetEvolutionChainAsync(int id, CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<EvolutionChainDto>($"https://pokeapi.co/api/v2/evolution-chain/{id}", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BaseSpeciesEntry>> GetBaseSpeciesEntriesAsync(CancellationToken cancellationToken = default)
    {
        var query = "{ pokemonspecies(where: {evolves_from_species_id: {_is_null: true}, id: {_lte: 649, _neq: 132}}, order_by: {id: asc}) { id name capture_rate is_legendary is_mythical generation_id } }";
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://graphql.pokeapi.co/v1beta2")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var dto = await JsonSerializer.DeserializeAsync<GraphQlBaseSpeciesResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return dto?.Data.PokemonSpecies
            .Where(row => row.Id is >= 1 and <= PokeApiMapper.MaxSupportedSpeciesId && row.Id != PokeApiMapper.DittoSpeciesId && row.GenerationId is >= 1 and <= 5)
            .Select(row => new BaseSpeciesEntry(
                row.Id,
                row.Name,
                row.GenerationId,
                row.CaptureRate,
                row.IsLegendary,
                row.IsMythical,
                Core.Game.RarityClassifier.Classify(row.CaptureRate, row.IsLegendary, row.IsMythical),
                EvolutionChainId: 0))
            .ToArray() ?? Array.Empty<BaseSpeciesEntry>();
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new PokeApiException($"PokéAPI returned empty {typeof(T).Name}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new PokeApiException($"PokéAPI request failed for {typeof(T).Name}.", ex);
        }
    }
}
