using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Tests.Infrastructure;

internal sealed class FailingJsonStorage : IJsonFileStorage
{
    public Task<T> LoadOrDefaultAsync<T>(string path, T defaultValue, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(defaultValue);
    }

    public Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("save failed");
    }
}
