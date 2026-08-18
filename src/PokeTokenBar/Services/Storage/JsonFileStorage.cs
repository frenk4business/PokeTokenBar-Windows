using System.IO;
using System.Text.Json;
using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Services.Storage;

public sealed class JsonFileStorage : IJsonFileStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IAppLogger _logger;

    public JsonFileStorage(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<T> LoadOrDefaultAsync<T>(
        string path,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return defaultValue;
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);

            var value = await JsonSerializer.DeserializeAsync<T>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);

            return value is null ? defaultValue : value;
        }
        catch (JsonException ex)
        {
            await _logger.LogAsync(AppLogLevel.Error, $"Invalid JSON in {Path.GetFileName(path)}.", ex, cancellationToken)
                .ConfigureAwait(false);
            throw new JsonStorageException($"Could not read JSON from {Path.GetFileName(path)}.", ex);
        }
        catch (IOException ex)
        {
            await _logger.LogAsync(AppLogLevel.Error, $"Could not read {Path.GetFileName(path)}.", ex, cancellationToken)
                .ConfigureAwait(false);
            throw new JsonStorageException($"Could not read {Path.GetFileName(path)}.", ex);
        }
    }

    public async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(directory ?? ".", $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var backupPath = BuildBackupPath(path);

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(path))
            {
                File.Copy(path, backupPath, overwrite: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            await _logger.LogAsync(AppLogLevel.Error, $"Could not save {Path.GetFileName(path)}.", ex, cancellationToken)
                .ConfigureAwait(false);
            throw new JsonStorageException($"Could not save {Path.GetFileName(path)}.", ex);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    public static string BuildBackupPath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? ".";
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        return Path.Combine(directory, $"{name}.previous{extension}");
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Best-effort cleanup only. Save failures are already reported by the caller.
        }
    }
}
