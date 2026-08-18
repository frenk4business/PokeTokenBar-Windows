using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Tests.Infrastructure;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }
}
