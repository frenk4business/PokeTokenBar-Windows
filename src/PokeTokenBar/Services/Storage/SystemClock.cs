using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Services.Storage;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
