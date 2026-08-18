using System.IO;
using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Services.Storage;

public sealed class WindowsAppPathProvider : IAppPathProvider
{
    private const string AppDirectoryName = "PokeTokenBar";

    public WindowsAppPathProvider()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public WindowsAppPathProvider(string roamingRoot, string localRoot)
    {
        if (string.IsNullOrWhiteSpace(roamingRoot))
        {
            throw new ArgumentException("A roaming application data root is required.", nameof(roamingRoot));
        }

        if (string.IsNullOrWhiteSpace(localRoot))
        {
            throw new ArgumentException("A local application data root is required.", nameof(localRoot));
        }

        RoamingStateDirectory = Path.Combine(roamingRoot, AppDirectoryName);
        LocalCacheDirectory = Path.Combine(localRoot, AppDirectoryName, "Cache");
        LogsDirectory = Path.Combine(localRoot, AppDirectoryName, "Logs");
    }

    public string RoamingStateDirectory { get; }
    public string LocalCacheDirectory { get; }
    public string LogsDirectory { get; }

    public DirectoryInfo EnsureRoamingStateDirectory() => Directory.CreateDirectory(RoamingStateDirectory);
    public DirectoryInfo EnsureLocalCacheDirectory() => Directory.CreateDirectory(LocalCacheDirectory);
    public DirectoryInfo EnsureLogsDirectory() => Directory.CreateDirectory(LogsDirectory);
}
