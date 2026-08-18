namespace PokeTokenBar.Core.Interfaces;

public interface IClock
{
    DateTimeOffset Now { get; }
}
