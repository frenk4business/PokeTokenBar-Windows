namespace PokeTokenBar.Core.Game;

public interface IHatchService
{
    HatchResult Hatch(EggState egg, DateTimeOffset hatchTime);
}
