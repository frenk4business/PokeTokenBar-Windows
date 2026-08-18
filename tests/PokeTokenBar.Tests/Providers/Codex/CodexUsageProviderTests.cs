using System.Text.Json;
using PokeTokenBar.Providers.Codex;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Storage;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Providers.Codex;

public sealed class CodexUsageProviderTests
{
    private static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public async Task SingleNormalSessionAggregatesAllPeriods()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-normal.jsonl", SessionMeta("s1"), Token("2026-08-18T10:00:00Z", 100, 10, 120), Token("2026-08-18T11:00:00Z", 200, 20, 220, 340));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(340, snapshot.Today.TotalTokens);
        Assert.Equal(340, snapshot.LastFiveHours.TotalTokens);
        Assert.Equal(340, snapshot.CurrentWeek.TotalTokens);
        Assert.Equal(340, snapshot.CurrentMonth.TotalTokens);
        Assert.Equal(340, snapshot.ObservedLifetime.TotalTokens);
        Assert.Equal(1, snapshot.SessionCount);
    }

    [Fact]
    public async Task MultipleSessionsAreAggregated()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-a.jsonl", SessionMeta("s1"), Token("2026-08-18T10:00:00Z", 100, 0, 100));
        context.WriteSession("2026/08/18/rollout-b.jsonl", SessionMeta("s2"), Token("2026-08-18T11:00:00Z", 50, 0, 50));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(150, snapshot.ObservedLifetime.TotalTokens);
        Assert.Equal(2, snapshot.SessionCount);
    }

    [Fact]
    public async Task TokenCountWithNullInfoIsIgnored()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-null-info.jsonl", SessionMeta("s1"), "{\"timestamp\":\"2026-08-18T10:00:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":null}}", Token("2026-08-18T11:00:00Z", 50, 0, 50));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(50, snapshot.ObservedLifetime.TotalTokens);
        Assert.Equal(1, snapshot.Diagnostics.ValidTokenEvents);
    }

    [Fact]
    public async Task MalformedJsonLineIsSkippedDiagnostically()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-malformed.jsonl", SessionMeta("s1"), "{not-json", Token("2026-08-18T11:00:00Z", 50, 0, 50));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(50, snapshot.ObservedLifetime.TotalTokens);
        Assert.Equal(1, snapshot.Diagnostics.MalformedLinesIgnored);
    }

    [Fact]
    public async Task IncompleteFinalLineIsRetriedAfterAppend()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        var file = context.SessionPath("2026/08/18/rollout-incomplete.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, SessionMeta("s1") + "\n" + Token("2026-08-18T10:00:00Z", 10, 0, 10) + "\n" + Token("2026-08-18T11:00:00Z", 20, 0, 20, 30)[..80]);

        var first = await context.Provider.RefreshAsync();
        Assert.Equal(10, first.ObservedLifetime.TotalTokens);
        Assert.Equal(1, first.Diagnostics.IncompleteLinesIgnored);

        File.AppendAllText(file, Token("2026-08-18T11:00:00Z", 20, 0, 20, 30)[80..] + "\n");
        var second = await context.Provider.RefreshAsync();

        Assert.Equal(30, second.ObservedLifetime.TotalTokens);
    }

    [Fact]
    public async Task DuplicateCumulativeTokenStateIsIgnored()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        var duplicate = Token("2026-08-18T10:00:00Z", 10, 0, 10);
        context.WriteSession("2026/08/18/rollout-duplicate.jsonl", SessionMeta("s1"), duplicate, duplicate);

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(10, snapshot.ObservedLifetime.TotalTokens);
        Assert.Equal(1, snapshot.Diagnostics.DuplicateStateEventsIgnored);
    }

    [Fact]
    public async Task CumulativeCounterDecreaseStartsNewEpoch()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-epoch.jsonl", SessionMeta("s1"), Token("2026-08-18T10:00:00Z", 100, 0, 100), Token("2026-08-18T11:00:00Z", 10, 0, 10));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(110, snapshot.ObservedLifetime.TotalTokens);
        Assert.Equal(2, snapshot.Diagnostics.ValidTokenEvents);
    }

    [Fact]
    public async Task EventsAcrossLocalMidnightUseLocalDay()
    {
        var context = TestContext.Create("2026-08-18T01:30:00+02:00");
        context.WriteSession("2026/08/18/rollout-midnight.jsonl", SessionMeta("s1"), Token("2026-08-17T21:30:00Z", 100, 0, 100), Token("2026-08-17T22:30:00Z", 200, 0, 200, 300));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(200, snapshot.Today.TotalTokens);
        Assert.Equal(300, snapshot.LastFiveHours.TotalTokens);
        Assert.Equal(300, snapshot.ObservedLifetime.TotalTokens);
    }

    [Fact]
    public async Task CurrentWeekStartsOnMonday()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-week.jsonl", SessionMeta("s1"), Token("2026-08-16T10:00:00Z", 100, 0, 100), Token("2026-08-17T10:00:00Z", 200, 0, 200, 300));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(200, snapshot.CurrentWeek.TotalTokens);
        Assert.Equal(300, snapshot.ObservedLifetime.TotalTokens);
    }

    [Fact]
    public async Task MonthBoundaryUsesLocalMonth()
    {
        var context = TestContext.Create("2026-09-01T02:00:00+02:00");
        context.WriteSession("2026/09/01/rollout-month.jsonl", SessionMeta("s1"), Token("2026-08-31T21:00:00Z", 100, 0, 100), Token("2026-08-31T22:30:00Z", 200, 0, 200, 300));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(200, snapshot.CurrentMonth.TotalTokens);
        Assert.Equal(300, snapshot.ObservedLifetime.TotalTokens);
    }

    [Fact]
    public async Task AppendedFileProcessesOnlyNewBytes()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        var file = context.WriteSession("2026/08/18/rollout-append.jsonl", SessionMeta("s1"), Token("2026-08-18T10:00:00Z", 10, 0, 10));

        var first = await context.Provider.RefreshAsync();
        File.AppendAllText(file, Token("2026-08-18T11:00:00Z", 20, 0, 20, 30) + "\n");
        var second = await context.Provider.RefreshAsync();

        Assert.Equal(10, first.ObservedLifetime.TotalTokens);
        Assert.Equal(30, second.ObservedLifetime.TotalTokens);
        Assert.Equal(1, second.Diagnostics.FilesParsed);
        Assert.True(second.Diagnostics.BytesRead < new FileInfo(file).Length);
    }

    [Fact]
    public async Task UnchangedSecondRefreshSkipsFile()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-unchanged.jsonl", SessionMeta("s1"), Token("2026-08-18T10:00:00Z", 10, 0, 10));

        await context.Provider.RefreshAsync();
        var second = await context.Provider.RefreshAsync();

        Assert.Equal(0, second.Diagnostics.FilesParsed);
        Assert.Equal(1, second.Diagnostics.FilesSkippedUnchanged);
        Assert.Equal(10, second.ObservedLifetime.TotalTokens);
    }

    [Fact]
    public async Task TruncatedFileIsRebuilt()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        var file = context.WriteSession("2026/08/18/rollout-truncated.jsonl", SessionMeta("s1"), Token("2026-08-18T10:00:00Z", 10, 0, 10), Token("2026-08-18T11:00:00Z", 20, 0, 20, 30));
        await context.Provider.RefreshAsync();

        File.WriteAllText(file, SessionMeta("s1") + "\n" + Token("2026-08-18T12:00:00Z", 5, 0, 5) + "\n");
        var second = await context.Provider.RefreshAsync();

        Assert.Equal(5, second.ObservedLifetime.TotalTokens);
        Assert.Equal(1, second.Diagnostics.FilesTruncatedOrRebuilt);
    }

    [Fact]
    public async Task CorruptIndexIsRebuiltFromSource()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-corrupt-index.jsonl", SessionMeta("s1"), Token("2026-08-18T10:00:00Z", 10, 0, 10));
        Directory.CreateDirectory(context.Paths.LocalCacheDirectory);
        File.WriteAllText(Path.Combine(context.Paths.LocalCacheDirectory, "codex-usage-index.json"), "{bad index");

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(10, snapshot.ObservedLifetime.TotalTokens);
    }

    [Fact]
    public async Task ForkReplayPrefixIsNotDoubleCounted()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-parent.jsonl", SessionMeta("parent"), Token("2026-08-18T10:00:00Z", 10, 0, 10), Token("2026-08-18T10:01:00Z", 20, 0, 20, 30));
        context.WriteSession("2026/08/18/rollout-child.jsonl", SessionMeta("child", "parent"), Token("2026-08-18T10:00:00.100Z", 10, 0, 10), Token("2026-08-18T10:01:00.100Z", 20, 0, 20, 30), Token("2026-08-18T11:00:00Z", 7, 0, 7, 37));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(37, snapshot.ObservedLifetime.TotalTokens);
        Assert.True(snapshot.Diagnostics.DuplicateCanonicalEventsIgnored > 0);
    }

    [Fact]
    public async Task SubagentParentMetadataDoesNotTriggerTimingFallback()
    {
        var context = TestContext.Create("2026-08-18T14:00:00+02:00");
        context.WriteSession("2026/08/18/rollout-parent.jsonl", SessionMeta("parent"), Token("2026-08-18T10:00:00Z", 10, 0, 10));
        context.WriteSession("2026/08/18/rollout-subagent.jsonl", SessionMeta("sub", "parent", isSubagent: true), Token("2026-08-18T10:00:00.100Z", 3, 0, 3), Token("2026-08-18T10:00:00.200Z", 4, 0, 4, 7));

        var snapshot = await context.Provider.RefreshAsync();

        Assert.Equal(17, snapshot.ObservedLifetime.TotalTokens);
    }

    private static string SessionMeta(string id, string? parentId = null, bool isSubagent = false)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["timestamp"] = "2026-08-18T10:00:00Z",
            ["originator"] = isSubagent ? "codex_subagent" : "codex_vscode",
            ["source"] = isSubagent ? "subagent" : "vscode",
            ["is_subagent"] = isSubagent
        };
        if (parentId is not null)
        {
            payload["parent_id"] = parentId;
        }

        return JsonSerializer.Serialize(new { timestamp = "2026-08-18T10:00:00Z", type = "session_meta", payload });
    }

    private static string Token(string timestamp, long input, long cached, long total, long? cumulativeTotal = null)
    {
        var last = new
        {
            input_tokens = input,
            cached_input_tokens = cached,
            output_tokens = Math.Max(0, total - input),
            reasoning_output_tokens = 0,
            total_tokens = total
        };
        var cumulative = new
        {
            input_tokens = input,
            cached_input_tokens = cached,
            output_tokens = Math.Max(0, (cumulativeTotal ?? total) - input),
            reasoning_output_tokens = 0,
            total_tokens = cumulativeTotal ?? total
        };
        var payload = new
        {
            type = "token_count",
            info = new
            {
                model_context_window = 272000,
                total_token_usage = cumulative,
                last_token_usage = last
            }
        };
        return JsonSerializer.Serialize(new { timestamp, type = "event_msg", payload });
    }

    private sealed class TestContext
    {
        private TestContext(string root, FakeClock clock)
        {
            Root = root;
            Clock = clock;
            Paths = new TestAppPathProvider(root);
            CodexHome = Path.Combine(root, ".codex");
            Provider = new CodexUsageProvider(
                new CodexPathResolver(name => name == "CODEX_HOME" ? CodexHome : null, () => Path.Combine(root, "User")),
                Paths,
                new JsonFileStorage(new NullAppLogger()),
                new NullAppLogger(),
                Clock,
                Amsterdam);
        }

        public string Root { get; }

        public FakeClock Clock { get; }

        public TestAppPathProvider Paths { get; }

        public string CodexHome { get; }

        public CodexUsageProvider Provider { get; }

        public static TestContext Create(string now)
        {
            var root = Path.Combine(Path.GetTempPath(), "PokeTokenBar.Tests", Guid.NewGuid().ToString("N"));
            return new TestContext(root, new FakeClock(DateTimeOffset.Parse(now)));
        }

        public string WriteSession(string relativePath, params string[] lines)
        {
            var path = SessionPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            return path;
        }

        public string SessionPath(string relativePath) => Path.Combine(CodexHome, "sessions", relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
