using System.IO;

namespace PokeTokenBar.Providers.Codex;

public sealed class CodexPathResolver
{
    private readonly Func<string, string?> _environment;
    private readonly Func<string> _userProfile;

    public CodexPathResolver()
        : this(
            name => Environment.GetEnvironmentVariable(name),
            () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    public CodexPathResolver(Func<string, string?> environment, Func<string> userProfile)
    {
        _environment = environment;
        _userProfile = userProfile;
    }

    public string CodexHome
    {
        get
        {
            var overrideHome = _environment("CODEX_HOME");
            if (!string.IsNullOrWhiteSpace(overrideHome))
            {
                return Environment.ExpandEnvironmentVariables(overrideHome);
            }

            return Path.Combine(_userProfile(), ".codex");
        }
    }

    public string SessionsDirectory => Path.Combine(CodexHome, "sessions");
}
