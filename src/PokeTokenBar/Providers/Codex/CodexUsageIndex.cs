namespace PokeTokenBar.Providers.Codex;

internal sealed class CodexUsageIndex
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Dictionary<string, CodexIndexedFile> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class CodexIndexedFile
{
    public string PathKey { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTimeOffset LastWriteTimeUtc { get; set; }

    public long SafeOffset { get; set; }

    public string? SessionId { get; set; }

    public string? ParentSessionId { get; set; }

    public bool IsSubagent { get; set; }

    public int Epoch { get; set; }

    public CodexTokenUsage? LastCumulativeUsage { get; set; }

    public HashSet<string> SeenStateFingerprints { get; set; } = new(StringComparer.Ordinal);

    public List<CodexIndexedTokenEvent> Events { get; set; } = [];
}

internal sealed record CodexIndexedTokenEvent
{
    public string LocalEventId { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; }

    public CodexTokenUsage Delta { get; init; }

    public CodexTokenUsage? Cumulative { get; init; }

    public CodexTokenUsage? Last { get; init; }

    public int Epoch { get; init; }

    public string? SessionId { get; init; }

    public string? ParentSessionId { get; init; }

    public bool IsSubagent { get; init; }

    public string UsageStateFingerprint =>
        Cumulative is null || Last is null ? string.Empty : Cumulative.Value.Fingerprint + "|" + Last.Value.Fingerprint;

    public string CanonicalId =>
        string.IsNullOrEmpty(UsageStateFingerprint)
            ? LocalEventId
            : "codex|" + Epoch + "|" + UsageStateFingerprint;
}

internal sealed class CodexParsedRollout
{
    public string PathKey { get; init; } = string.Empty;

    public string? SessionId { get; init; }

    public string? ParentSessionId { get; init; }

    public bool IsSubagent { get; init; }

    public IReadOnlyList<CodexIndexedTokenEvent> Events { get; init; } = [];
}
