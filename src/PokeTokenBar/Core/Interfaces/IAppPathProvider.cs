using System.IO;

namespace PokeTokenBar.Core.Interfaces;

public interface IAppPathProvider
{
    string RoamingStateDirectory { get; }
    string LocalCacheDirectory { get; }
    string LogsDirectory { get; }

    DirectoryInfo EnsureRoamingStateDirectory();
    DirectoryInfo EnsureLocalCacheDirectory();
    DirectoryInfo EnsureLogsDirectory();
}
