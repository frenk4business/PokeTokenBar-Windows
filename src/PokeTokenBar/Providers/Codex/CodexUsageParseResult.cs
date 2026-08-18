namespace PokeTokenBar.Providers.Codex;

internal sealed class CodexUsageParseResult
{
    public CodexIndexedFile File { get; init; } = new();

    public int ValidTokenEvents { get; init; }

    public int DuplicateStateEventsIgnored { get; init; }

    public int MalformedLinesIgnored { get; init; }

    public int IncompleteLinesIgnored { get; init; }

    public long BytesRead { get; init; }
}
