namespace PokeTokenBar.Core.Game;

public sealed class ResolvedHatchService : IHatchService
{
    private readonly HatchResult _hatchResult;
    private bool _used;

    public ResolvedHatchService(HatchResult hatchResult)
    {
        _hatchResult = hatchResult;
    }

    public HatchResult Hatch(EggState egg, DateTimeOffset hatchTime)
    {
        if (_used)
        {
            throw new InvalidOperationException("Resolved hatch result can only be consumed once.");
        }

        _used = true;
        return _hatchResult;
    }
}
