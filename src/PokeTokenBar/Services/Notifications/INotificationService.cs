namespace PokeTokenBar.Services.Notifications;

public interface INotificationService
{
    event EventHandler<AppNotification>? NotificationRequested;

    void Publish(AppNotification notification);
}
