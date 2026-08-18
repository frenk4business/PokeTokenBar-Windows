using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Tests.Infrastructure;

internal sealed class TestAppPathProvider : IAppPathProvider
{
    public TestAppPathProvider(string root)
    {
        RoamingStateDirectory = Path.Combine(root, "Roaming");
        LocalCacheDirectory = Path.Combine(root, "Local", "Cache");
        LogsDirectory = Path.Combine(root, "Local", "Logs");
    }

    public string RoamingStateDirectory { get; }

    public string LocalCacheDirectory { get; }

    public string LogsDirectory { get; }

    public DirectoryInfo EnsureRoamingStateDirectory() => Directory.CreateDirectory(RoamingStateDirectory);

    public DirectoryInfo EnsureLocalCacheDirectory() => Directory.CreateDirectory(LocalCacheDirectory);

    public DirectoryInfo EnsureLogsDirectory() => Directory.CreateDirectory(LogsDirectory);
}
