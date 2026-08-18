namespace PokeTokenBar.Services.Notifications;

public sealed record AppNotification(string Title, string Message, AppNotificationKind Kind);

public enum AppNotificationKind
{
    Hatch,
    Shiny,
    Evolution,
    Graduation,
    Warning
}
