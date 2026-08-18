using PokeTokenBar.Core.Game;
using PokeTokenBar.Tests.Game;

namespace PokeTokenBar.Tests.Infrastructure;

internal sealed class FixedHatchService : IHatchService
{
    private readonly Queue<HatchResult> _hatches;

    public FixedHatchService(params HatchResult[] hatches)
    {
        _hatches = new Queue<HatchResult>(hatches);
    }

    public int HatchCount { get; private set; }

    public HatchResult Hatch(EggState egg, DateTimeOffset hatchTime)
    {
        HatchCount++;
        return _hatches.Count > 0 ? _hatches.Dequeue() : GameFixtures.BulbasaurHatch();
    }
}
