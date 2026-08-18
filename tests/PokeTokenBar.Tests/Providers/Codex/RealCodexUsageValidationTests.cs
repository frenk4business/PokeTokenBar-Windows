using System.Text;
using PokeTokenBar.Providers.Codex;
using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Storage;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Providers.Codex;

public sealed class RealCodexUsageValidationTests
{
    [Fact]
    public async Task RealLocalCodexRefreshProducesSanitizedDiagnosticsWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("POKETOKENBAR_RUN_REAL_CODEX_TESTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var outputPath = Path.Combine(Environment.CurrentDirectory, "TestResults", "codex-real-validation.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var root = Path.Combine(Path.GetTempPath(), "PokeTokenBar.RealCodex", Guid.NewGuid().ToString("N"));
        var paths = new TestAppPathProvider(root);
        var logger = new NullAppLogger();
        var provider = new CodexUsageProvider(
            new CodexPathResolver(),
            paths,
            new JsonFileStorage(logger),
            logger,
            new SystemClock());

        var first = await provider.RefreshAsync();
        var second = await provider.RefreshAsync();

        Assert.True(first.ObservedLifetime.TotalTokens >= first.CurrentMonth.TotalTokens);
        Assert.True(first.CurrentWeek.TotalTokens >= first.Today.TotalTokens);

        var report = new StringBuilder()
            .AppendLine($"FirstFilesDiscovered={first.Diagnostics.FilesDiscovered}")
            .AppendLine($"FirstFilesParsed={first.Diagnostics.FilesParsed}")
            .AppendLine($"FirstFilesSkippedUnchanged={first.Diagnostics.FilesSkippedUnchanged}")
            .AppendLine($"FirstValidTokenEvents={first.Diagnostics.ValidTokenEvents}")
            .AppendLine($"FirstDuplicateStateEventsIgnored={first.Diagnostics.DuplicateStateEventsIgnored}")
            .AppendLine($"FirstDuplicateCanonicalEventsIgnored={first.Diagnostics.DuplicateCanonicalEventsIgnored}")
            .AppendLine($"FirstMalformedLinesIgnored={first.Diagnostics.MalformedLinesIgnored}")
            .AppendLine($"FirstIncompleteLinesIgnored={first.Diagnostics.IncompleteLinesIgnored}")
            .AppendLine($"FirstBytesRead={first.Diagnostics.BytesRead}")
            .AppendLine($"FirstElapsedMs={first.Diagnostics.Elapsed.TotalMilliseconds:0}")
            .AppendLine($"TodayTokens={first.Today.TotalTokens}")
            .AppendLine($"LastFiveHoursTokens={first.LastFiveHours.TotalTokens}")
            .AppendLine($"WeekTokens={first.CurrentWeek.TotalTokens}")
            .AppendLine($"MonthTokens={first.CurrentMonth.TotalTokens}")
            .AppendLine($"ObservedLifetimeTokens={first.ObservedLifetime.TotalTokens}")
            .AppendLine($"SecondFilesDiscovered={second.Diagnostics.FilesDiscovered}")
            .AppendLine($"SecondFilesParsed={second.Diagnostics.FilesParsed}")
            .AppendLine($"SecondFilesSkippedUnchanged={second.Diagnostics.FilesSkippedUnchanged}")
            .AppendLine($"SecondValidTokenEvents={second.Diagnostics.ValidTokenEvents}")
            .AppendLine($"SecondBytesRead={second.Diagnostics.BytesRead}")
            .AppendLine($"SecondElapsedMs={second.Diagnostics.Elapsed.TotalMilliseconds:0}")
            .ToString();

        await File.WriteAllTextAsync(outputPath, report);
    }
}
