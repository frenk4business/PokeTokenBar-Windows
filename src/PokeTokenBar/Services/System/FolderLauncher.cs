using System.Diagnostics;
using System.IO;

namespace PokeTokenBar.Services.System;

public sealed class FolderLauncher
{
    public void Open(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
