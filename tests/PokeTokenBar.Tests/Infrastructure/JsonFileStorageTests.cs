using PokeTokenBar.Services.Logging;
using PokeTokenBar.Services.Storage;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class JsonFileStorageTests
{
    [Fact]
    public async Task LoadOrDefaultReturnsDefaultWhenFileDoesNotExist()
    {
        var storage = new JsonFileStorage(new NullAppLogger());
        var missingPath = Path.Combine(CreateTempDirectory(), "missing.json");

        var result = await storage.LoadOrDefaultAsync(missingPath, new TestState("fallback", 7));

        Assert.Equal(new TestState("fallback", 7), result);
    }

    [Fact]
    public async Task SaveThenLoadRoundTripsObject()
    {
        var storage = new JsonFileStorage(new NullAppLogger());
        var path = Path.Combine(CreateTempDirectory(), "state.json");

        await storage.SaveAsync(path, new TestState("first", 1));
        var result = await storage.LoadOrDefaultAsync(path, new TestState("fallback", 0));

        Assert.Equal(new TestState("first", 1), result);
    }

    [Fact]
    public async Task SaveOverwritesExistingObjectAndCreatesPreviousBackup()
    {
        var storage = new JsonFileStorage(new NullAppLogger());
        var path = Path.Combine(CreateTempDirectory(), "state.json");

        await storage.SaveAsync(path, new TestState("first", 1));
        await storage.SaveAsync(path, new TestState("second", 2));

        var current = await storage.LoadOrDefaultAsync(path, new TestState("fallback", 0));
        var previous = await storage.LoadOrDefaultAsync(JsonFileStorage.BuildBackupPath(path), new TestState("fallback", 0));

        Assert.Equal(new TestState("second", 2), current);
        Assert.Equal(new TestState("first", 1), previous);
    }

    [Fact]
    public async Task InvalidJsonThrowsStorageException()
    {
        var storage = new JsonFileStorage(new NullAppLogger());
        var path = Path.Combine(CreateTempDirectory(), "state.json");
        await File.WriteAllTextAsync(path, "{ invalid json");

        await Assert.ThrowsAsync<JsonStorageException>(
            () => storage.LoadOrDefaultAsync(path, new TestState("fallback", 0)));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PokeTokenBar.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record TestState(string Name, int Count);
}
