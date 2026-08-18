using System.Net;
using System.Net.Http;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Sprites;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Providers.Sprites;

public sealed class PokemonSpriteServiceTests
{
    [Fact]
    public async Task FirstFetchSavesFileAndSecondFetchSkipsNetwork()
    {
        using var context = SpriteContext.Create();
        context.Handler.Enqueue("/25.png", FakeHttpMessageHandler.Bytes(1, 2, 3));

        var first = await context.Service.GetPokemonSpriteAsync(25);
        var second = await context.Service.GetPokemonSpriteAsync(25);

        Assert.NotNull(first);
        Assert.False(first!.FromCache);
        Assert.True(File.Exists(first.Path));
        Assert.NotNull(second);
        Assert.True(second!.FromCache);
        Assert.Equal(1, context.Handler.RequestCount);
    }

    [Fact]
    public async Task ShinyAndNormalAreSeparateCacheFiles()
    {
        using var context = SpriteContext.Create();
        context.Handler.Enqueue("/25.png", FakeHttpMessageHandler.Bytes(1));
        context.Handler.Enqueue("/shiny/25.png", FakeHttpMessageHandler.Bytes(2));

        var normal = await context.Service.GetPokemonSpriteAsync(25);
        var shiny = await context.Service.GetPokemonSpriteAsync(25, shiny: true);

        Assert.NotEqual(normal!.Path, shiny!.Path);
        Assert.Contains("shiny", shiny.Path);
    }

    [Fact]
    public async Task ZeroByteCacheIsReplaced()
    {
        using var context = SpriteContext.Create();
        var path = Path.Combine(context.Paths.LocalCacheDirectory, "sprites", "25.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());
        context.Handler.Enqueue("/25.png", FakeHttpMessageHandler.Bytes(9));

        var sprite = await context.Service.GetPokemonSpriteAsync(25);

        Assert.NotNull(sprite);
        Assert.Equal(1, new FileInfo(sprite!.Path).Length);
    }

    [Fact]
    public async Task AnimatedFallsBackToStaticWhenGifMissing()
    {
        using var context = SpriteContext.Create();
        context.Handler.Enqueue("/animated/25.gif", new HttpResponseMessage(HttpStatusCode.NotFound));
        context.Handler.Enqueue("/25.png", FakeHttpMessageHandler.Bytes(1));

        var sprite = await context.Service.GetPokemonSpriteAsync(25, animated: true);

        Assert.NotNull(sprite);
        Assert.Equal(SpriteKind.Static, sprite!.Kind);
    }

    private sealed class SpriteContext : IDisposable
    {
        private readonly string _root;

        private SpriteContext(string root, TestAppPathProvider paths, FakeHttpMessageHandler handler, PokemonSpriteService service)
        {
            _root = root;
            Paths = paths;
            Handler = handler;
            Service = service;
        }

        public TestAppPathProvider Paths { get; }

        public FakeHttpMessageHandler Handler { get; }

        public PokemonSpriteService Service { get; }

        public static SpriteContext Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"ptb-sprites-{Guid.NewGuid():N}");
            var paths = new TestAppPathProvider(root);
            var handler = new FakeHttpMessageHandler();
            var logger = new FileAppLogger(paths, new FakeClock(DateTimeOffset.UtcNow));
            var service = new PokemonSpriteService(new HttpClient(handler), paths, logger);
            return new SpriteContext(root, paths, handler, service);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
