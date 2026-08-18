namespace PokeTokenBar.Services.Startup;

public interface IStartupRegistration
{
    bool IsEnabled(string appName, string executablePath);

    void SetEnabled(string appName, string executablePath, bool enabled);
}
