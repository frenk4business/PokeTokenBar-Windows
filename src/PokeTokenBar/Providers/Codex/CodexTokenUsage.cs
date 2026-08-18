namespace PokeTokenBar.Providers.Codex;

public readonly record struct CodexTokenUsage(
    long InputTokens,
    long CachedInputTokens,
    long CacheWriteInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long TotalTokens)
{
    public static CodexTokenUsage Zero { get; } = new(0, 0, 0, 0, 0, 0);

    public static CodexTokenUsage operator +(CodexTokenUsage left, CodexTokenUsage right) =>
        new(
            left.InputTokens + right.InputTokens,
            left.CachedInputTokens + right.CachedInputTokens,
            left.CacheWriteInputTokens + right.CacheWriteInputTokens,
            left.OutputTokens + right.OutputTokens,
            left.ReasoningOutputTokens + right.ReasoningOutputTokens,
            left.TotalTokens + right.TotalTokens);

    public bool IsLowerThan(CodexTokenUsage other) =>
        InputTokens < other.InputTokens
        || CachedInputTokens < other.CachedInputTokens
        || CacheWriteInputTokens < other.CacheWriteInputTokens
        || OutputTokens < other.OutputTokens
        || ReasoningOutputTokens < other.ReasoningOutputTokens
        || TotalTokens < other.TotalTokens;

    public string Fingerprint =>
        string.Join(
            ',',
            InputTokens,
            CachedInputTokens,
            CacheWriteInputTokens,
            OutputTokens,
            ReasoningOutputTokens,
            TotalTokens);
}
