using Microsoft.Win32;

namespace PokeTokenBar.Services.Startup;

public sealed class WindowsStartupRegistration : IStartupRegistration
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled(string appName, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(key?.GetValue(appName) as string, BuildRunValue(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(string appName, string executablePath, bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(appName, BuildRunValue(executablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(appName, throwOnMissingValue: false);
        }
    }

    public static string BuildRunValue(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        return $"\"{executablePath}\"";
    }
}
