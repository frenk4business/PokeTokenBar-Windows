namespace PokeTokenBar.Services.PokeApi;

public sealed class PokeApiException : Exception
{
    public PokeApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
