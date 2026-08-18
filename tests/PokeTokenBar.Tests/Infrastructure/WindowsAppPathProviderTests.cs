using PokeTokenBar.Services.Storage;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class WindowsAppPathProviderTests
{
    [Fact]
    public void ResolvesExpectedApplicationDirectories()
    {
        var paths = new WindowsAppPathProvider(@"R:\Roaming", @"L:\Local");

        Assert.Equal(@"R:\Roaming\PokeTokenBar", paths.RoamingStateDirectory);
        Assert.Equal(@"L:\Local\PokeTokenBar\Cache", paths.LocalCacheDirectory);
        Assert.Equal(@"L:\Local\PokeTokenBar\Logs", paths.LogsDirectory);
    }

    [Fact]
    public void UsesInjectedRootsWithoutHardCodedUsername()
    {
        var paths = new WindowsAppPathProvider(@"X:\RoamingRoot", @"Y:\LocalRoot");
        var combined = string.Join("|", paths.RoamingStateDirectory, paths.LocalCacheDirectory, paths.LogsDirectory);

        Assert.DoesNotContain("frenk", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\Users\", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatesDirectoriesOnlyWhenEnsureIsCalled()
    {
        var root = Path.Combine(Path.GetTempPath(), "PokeTokenBar.Tests", Guid.NewGuid().ToString("N"));
        var roaming = Path.Combine(root, "Roaming");
        var local = Path.Combine(root, "Local");
        var paths = new WindowsAppPathProvider(roaming, local);

        Assert.False(Directory.Exists(paths.RoamingStateDirectory));
        Assert.False(Directory.Exists(paths.LocalCacheDirectory));
        Assert.False(Directory.Exists(paths.LogsDirectory));

        paths.EnsureRoamingStateDirectory();
        paths.EnsureLocalCacheDirectory();
        paths.EnsureLogsDirectory();

        Assert.True(Directory.Exists(paths.RoamingStateDirectory));
        Assert.True(Directory.Exists(paths.LocalCacheDirectory));
        Assert.True(Directory.Exists(paths.LogsDirectory));
    }
}
