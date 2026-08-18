using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Providers.Codex;

namespace PokeTokenBar.Tests.Infrastructure;

internal sealed class FakeCodexUsageProvider : ICodexUsageProvider
{
    private readonly Queue<CodexUsageSnapshot> _snapshots = new();

    public void EnqueueToday(long totalTokens)
    {
        var usage = new CodexTokenUsage(0, 0, 0, 0, 0, totalTokens);
        _snapshots.Enqueue(new CodexUsageSnapshot(
            usage,
            usage,
            usage,
            usage,
            usage,
            DateTimeOffset.UtcNow,
            1,
            CodexUsageRefreshDiagnostics.Empty));
    }

    public Task<CodexUsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_snapshots.Dequeue());
    }
}
