namespace PokeTokenBar.Services.Storage;

public sealed class JsonStorageException : Exception
{
    public JsonStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
