using System.IO;
using System.Net.Http;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.PokeApi;

namespace PokeTokenBar.Services.Sprites;

public sealed class PokemonSpriteService
{
    private const string BaseUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon";
    private readonly HttpClient _httpClient;
    private readonly IAppPathProvider _paths;
    private readonly IAppLogger _logger;

    public PokemonSpriteService(HttpClient httpClient, IAppPathProvider paths, IAppLogger logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _paths = paths;
        _logger = logger;
    }

    public async Task<SpriteResult?> GetPokemonSpriteAsync(
        int speciesId,
        bool shiny = false,
        bool animated = false,
        CancellationToken cancellationToken = default)
    {
        if (animated)
        {
            var animatedResult = await TryGetAsync(speciesId, shiny, SpriteKind.Animated, cancellationToken).ConfigureAwait(false);
            if (animatedResult is not null)
            {
                return animatedResult;
            }
        }

        var staticResult = await TryGetAsync(speciesId, shiny, SpriteKind.Static, cancellationToken).ConfigureAwait(false);
        if (staticResult is not null)
        {
            return staticResult;
        }

        return shiny
            ? await TryGetAsync(speciesId, shiny: false, SpriteKind.Static, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public Task<SpriteResult?> GetEggSpriteAsync(CancellationToken cancellationToken = default)
    {
        return TryDownloadNamedAsync("egg", $"{BaseUrl}/egg.png", "egg.png", cancellationToken);
    }

    public Task<SpriteResult?> GetItemSpriteAsync(string itemKey, CancellationToken cancellationToken = default)
    {
        return TryDownloadNamedAsync($"item-{itemKey}", $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/{itemKey}.png", $"item-{itemKey}.png", cancellationToken);
    }

    private async Task<SpriteResult?> TryGetAsync(int speciesId, bool shiny, SpriteKind kind, CancellationToken cancellationToken)
    {
        if (kind == SpriteKind.Animated && speciesId is < 1 or > PokeApiMapper.MaxSupportedSpeciesId)
        {
            return null;
        }

        var path = SpritePath(speciesId, shiny, kind);
        if (IsValidFile(path))
        {
            return new SpriteResult(path, kind, shiny, FromCache: true);
        }

        DeleteIfExists(path);
        var url = SpriteUrl(speciesId, shiny, kind);
        return await TryDownloadNamedAsync($"{speciesId}", url, Path.GetFileName(path), cancellationToken).ConfigureAwait(false);
    }

    private async Task<SpriteResult?> TryDownloadNamedAsync(string logKey, string url, string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(SpriteCacheDirectory(), fileName);
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            var temp = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return new SpriteResult(path, Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase) ? SpriteKind.Animated : SpriteKind.Static, fileName.Contains("shiny", StringComparison.OrdinalIgnoreCase), FromCache: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException)
        {
            await _logger.LogAsync(AppLogLevel.Warning, $"Sprite fetch failed for {logKey}.", ex, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private string SpriteCacheDirectory()
    {
        var directory = Path.Combine(_paths.LocalCacheDirectory, "sprites");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private string SpritePath(int speciesId, bool shiny, SpriteKind kind)
    {
        var extension = kind == SpriteKind.Animated ? "gif" : "png";
        var suffix = shiny ? "-shiny" : string.Empty;
        return Path.Combine(SpriteCacheDirectory(), $"{speciesId}{suffix}.{extension}");
    }

    private static string SpriteUrl(int speciesId, bool shiny, SpriteKind kind) => (kind, shiny) switch
    {
        (SpriteKind.Animated, false) => $"{BaseUrl}/versions/generation-v/black-white/animated/{speciesId}.gif",
        (SpriteKind.Animated, true) => $"{BaseUrl}/versions/generation-v/black-white/animated/shiny/{speciesId}.gif",
        (SpriteKind.Static, false) => $"{BaseUrl}/{speciesId}.png",
        (SpriteKind.Static, true) => $"{BaseUrl}/shiny/{speciesId}.png",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static bool IsValidFile(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cache cleanup is best effort; a later fetch can retry.
        }
    }
}
