using System.Globalization;

namespace PokeTokenBar.Providers.Codex;

public static class CodexUsageFormatting
{
    public static string Compact(long value)
    {
        var abs = Math.Abs(value);
        if (abs < 1_000)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (abs < 1_000_000)
        {
            return Format(value / 1_000d, "K");
        }

        if (abs < 1_000_000_000)
        {
            return Format(value / 1_000_000d, "M");
        }

        return Format(value / 1_000_000_000d, "B");
    }

    private static string Format(double value, string suffix)
    {
        var abs = Math.Abs(value);
        var format = abs >= 100 ? "0" : abs >= 10 ? "0.#" : "0.##";
        return value.ToString(format, CultureInfo.InvariantCulture) + suffix;
    }
}
