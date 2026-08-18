namespace PokeTokenBar.Providers.Codex;

public sealed record CodexUsageRefreshDiagnostics(
    int FilesDiscovered,
    int FilesParsed,
    int FilesSkippedUnchanged,
    int FilesTruncatedOrRebuilt,
    int ReadErrorFiles,
    int ValidTokenEvents,
    int DuplicateStateEventsIgnored,
    int DuplicateCanonicalEventsIgnored,
    int MalformedLinesIgnored,
    int IncompleteLinesIgnored,
    long BytesRead,
    TimeSpan Elapsed)
{
    public static CodexUsageRefreshDiagnostics Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero);
}
