using PokeTokenBar.Providers.Codex;

namespace PokeTokenBar.Core.Interfaces;

public interface ICodexUsageProvider
{
    Task<CodexUsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}
