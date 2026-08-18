using PokeTokenBar.Services.Floating;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class FloatingCompanionBoundsTests
{
    [Fact]
    public void OffScreenPlacementIsMovedInsideVisibleBounds()
    {
        var placement = FloatingCompanionBounds.EnsureVisible(
            new FloatingCompanionPlacement(5_000, -500, 96),
            new DesktopBounds(0, 0, 1920, 1080));

        Assert.InRange(placement.Left, 8, 1920 - 96 - 8);
        Assert.InRange(placement.Top, 8, 1080 - 96 - 8);
    }

    [Fact]
    public void InvalidSizeNormalizes()
    {
        var placement = FloatingCompanionBounds.EnsureVisible(
            new FloatingCompanionPlacement(50, 60, 999),
            new DesktopBounds(0, 0, 1920, 1080));

        Assert.Equal(96, placement.Size);
    }

    [Fact]
    public void ValidPlacementIsPreserved()
    {
        var placement = FloatingCompanionBounds.EnsureVisible(
            new FloatingCompanionPlacement(120, 160, 128),
            new DesktopBounds(0, 0, 1920, 1080));

        Assert.Equal(120, placement.Left);
        Assert.Equal(160, placement.Top);
        Assert.Equal(128, placement.Size);
    }
}
