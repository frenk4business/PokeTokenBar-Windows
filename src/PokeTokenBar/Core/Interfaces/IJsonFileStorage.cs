namespace PokeTokenBar.Core.Interfaces;

public interface IJsonFileStorage
{
    Task<T> LoadOrDefaultAsync<T>(string path, T defaultValue, CancellationToken cancellationToken = default);
    Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default);
}
