using PokeTokenBar.Services.Cache;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class CacheMaintenanceServiceTests
{
    [Fact]
    public void ClearPokemonCacheRemovesApiAndSpriteCachesOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ptb-cache-{Guid.NewGuid():N}");
        try
        {
            var paths = new TestAppPathProvider(root);
            var pokeApi = Path.Combine(paths.LocalCacheDirectory, "pokeapi");
            var sprites = Path.Combine(paths.LocalCacheDirectory, "sprites");
            Directory.CreateDirectory(pokeApi);
            Directory.CreateDirectory(sprites);
            Directory.CreateDirectory(paths.RoamingStateDirectory);
            File.WriteAllText(Path.Combine(pokeApi, "species.json"), "{}");
            File.WriteAllText(Path.Combine(sprites, "1.png"), "sprite");
            File.WriteAllText(Path.Combine(paths.RoamingStateDirectory, "state.json"), "{}");
            File.WriteAllText(Path.Combine(paths.RoamingStateDirectory, "settings.json"), "{}");

            new CacheMaintenanceService(paths).ClearPokemonCache();

            Assert.False(Directory.Exists(pokeApi));
            Assert.False(Directory.Exists(sprites));
            Assert.True(File.Exists(Path.Combine(paths.RoamingStateDirectory, "state.json")));
            Assert.True(File.Exists(Path.Combine(paths.RoamingStateDirectory, "settings.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
