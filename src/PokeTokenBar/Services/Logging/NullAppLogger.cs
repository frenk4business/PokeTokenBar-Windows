using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Services.Logging;

public sealed class NullAppLogger : IAppLogger
{
    public Task LogAsync(
        AppLogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
