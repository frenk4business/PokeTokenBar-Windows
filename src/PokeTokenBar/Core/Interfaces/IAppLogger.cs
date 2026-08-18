namespace PokeTokenBar.Core.Interfaces;

public interface IAppLogger
{
    Task LogAsync(AppLogLevel level, string message, Exception? exception = null, CancellationToken cancellationToken = default);
}
