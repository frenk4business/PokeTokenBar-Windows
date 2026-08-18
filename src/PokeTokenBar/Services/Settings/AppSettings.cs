namespace PokeTokenBar.Services.Settings;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public bool LaunchWithWindows { get; init; }

    public bool AutoRefreshEnabled { get; init; } = true;

    public int RefreshIntervalMinutes { get; init; } = 2;

    public bool NotificationsEnabled { get; init; } = true;

    public bool HatchNotifications { get; init; } = true;

    public bool EvolutionNotifications { get; init; } = true;

    public bool GraduationNotifications { get; init; } = true;

    public bool ShinyNotifications { get; init; } = true;

    public bool ShowTokenUsageInTray { get; init; } = true;

    public bool StartMinimizedToTray { get; init; } = true;

    public bool ShowDesktopCompanion { get; init; }

    public bool DesktopCompanionAlwaysOnTop { get; init; }

    public int DesktopCompanionSize { get; init; } = 96;

    public double DesktopCompanionLeft { get; init; } = 80;

    public double DesktopCompanionTop { get; init; } = 120;

    public static AppSettings Default => new();
}

public sealed record CompanionSizeOption(string Label, int Pixels);

public static class DesktopCompanionSizes
{
    public static IReadOnlyList<CompanionSizeOption> Options { get; } =
    [
        new("48 px", 48),
        new("64 px", 64),
        new("96 px", 96),
        new("128 px", 128),
        new("160 px", 160),
        new("192 px", 192)
    ];

    public static bool IsValid(int pixels) => Options.Any(option => option.Pixels == pixels);

    public static int Normalize(int pixels) => IsValid(pixels) ? pixels : 96;
}

public sealed record RefreshIntervalOption(string Label, int Minutes)
{
    public bool IsManual => Minutes <= 0;
}

public static class RefreshIntervals
{
    public static IReadOnlyList<RefreshIntervalOption> Options { get; } =
    [
        new("Manual only", 0),
        new("1 minute", 1),
        new("2 minutes", 2),
        new("5 minutes", 5),
        new("10 minutes", 10),
        new("15 minutes", 15)
    ];

    public static bool IsValid(int minutes) => Options.Any(option => option.Minutes == minutes);

    public static int Normalize(int minutes) => IsValid(minutes) ? minutes : 2;
}
