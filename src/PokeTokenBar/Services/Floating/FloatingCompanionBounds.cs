namespace PokeTokenBar.Services.Floating;

public readonly record struct DesktopBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public readonly record struct FloatingCompanionPlacement(double Left, double Top, int Size);

public static class FloatingCompanionBounds
{
    private const double Margin = 8;

    public static FloatingCompanionPlacement EnsureVisible(FloatingCompanionPlacement placement, DesktopBounds bounds)
    {
        var size = NormalizeSize(placement.Size);
        var maxLeft = Math.Max(bounds.Left + Margin, bounds.Right - size - Margin);
        var maxTop = Math.Max(bounds.Top + Margin, bounds.Bottom - size - Margin);
        var left = Clamp(FiniteOrDefault(placement.Left, bounds.Left + Margin), bounds.Left + Margin, maxLeft);
        var top = Clamp(FiniteOrDefault(placement.Top, bounds.Top + Margin), bounds.Top + Margin, maxTop);
        return new FloatingCompanionPlacement(left, top, size);
    }

    public static int NormalizeSize(int size) => PokeTokenBar.Services.Settings.DesktopCompanionSizes.Normalize(size);

    private static double FiniteOrDefault(double value, double fallback) => double.IsFinite(value) ? value : fallback;

    private static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);
}
