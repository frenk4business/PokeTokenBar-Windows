namespace PokeTokenBar.Providers.Codex;

public sealed record CodexUsageSnapshot(
    CodexTokenUsage Today,
    CodexTokenUsage LastFiveHours,
    CodexTokenUsage CurrentWeek,
    CodexTokenUsage CurrentMonth,
    CodexTokenUsage ObservedLifetime,
    DateTimeOffset RefreshedAt,
    int SessionCount,
    CodexUsageRefreshDiagnostics Diagnostics,
    string? StatusMessage = null,
    string? ErrorMessage = null)
{
    public static CodexUsageSnapshot Empty(DateTimeOffset refreshedAt, string statusMessage) =>
        new(
            CodexTokenUsage.Zero,
            CodexTokenUsage.Zero,
            CodexTokenUsage.Zero,
            CodexTokenUsage.Zero,
            CodexTokenUsage.Zero,
            refreshedAt,
            0,
            CodexUsageRefreshDiagnostics.Empty,
            statusMessage);
}
