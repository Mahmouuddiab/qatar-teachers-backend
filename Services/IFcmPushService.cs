namespace QatarLearningBackend.Services;

public interface IFcmPushService
{
    Task SendToUserAsync(
        int userId,
        string title,
        string body,
        string type,
        int? bookingId = null,
        int? notificationId = null);
}