using System.IO;
using System.Text;
using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Services.Logging;

public sealed class FileAppLogger : IAppLogger
{
    private const long MaxLogBytes = 5 * 1024 * 1024;
    private const int RetainedLogFiles = 4;
    private readonly IAppPathProvider _paths;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileAppLogger(IAppPathProvider paths, IClock clock)
    {
        _paths = paths;
        _clock = clock;
    }

    public async Task LogAsync(
        AppLogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        var safeMessage = Sanitize(message);
        var logPath = Path.Combine(_paths.EnsureLogsDirectory().FullName, "poke-token-bar.log");
        var line = BuildLine(level, safeMessage, exception);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RotateIfNeeded(logPath);
            await File.AppendAllTextAsync(logPath, line, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static void RotateIfNeeded(string logPath)
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length < MaxLogBytes)
        {
            return;
        }

        for (var i = RetainedLogFiles - 1; i >= 1; i--)
        {
            var source = $"{logPath}.{i}";
            var target = $"{logPath}.{i + 1}";
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            if (File.Exists(source))
            {
                File.Move(source, target);
            }
        }

        var first = $"{logPath}.1";
        if (File.Exists(first))
        {
            File.Delete(first);
        }

        File.Move(logPath, first);
    }

    private string BuildLine(AppLogLevel level, string message, Exception? exception)
    {
        var builder = new StringBuilder()
            .Append(_clock.Now.ToString("O"))
            .Append(" [")
            .Append(level)
            .Append("] ")
            .Append(message);

        if (exception is not null)
        {
            builder
                .Append(" | ")
                .Append(exception.GetType().Name)
                .Append(": ")
                .Append(Sanitize(exception.Message));
        }

        return builder.AppendLine().ToString();
    }

    private static string Sanitize(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 2_000 ? normalized : normalized[..2_000] + "...";
    }
}
