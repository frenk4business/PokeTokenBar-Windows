namespace PokeTokenBar.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<AppNotification>? NotificationRequested;

    public void Publish(AppNotification notification)
    {
        NotificationRequested?.Invoke(this, notification);
    }
}
