using System.IO;
using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Services.Cache;

public sealed class CacheMaintenanceService
{
    private readonly IAppPathProvider _paths;

    public CacheMaintenanceService(IAppPathProvider paths)
    {
        _paths = paths;
    }

    public void ClearPokemonCache()
    {
        DeleteDirectory(Path.Combine(_paths.LocalCacheDirectory, "pokeapi"));
        DeleteDirectory(Path.Combine(_paths.LocalCacheDirectory, "sprites"));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
