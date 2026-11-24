using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using FirebaseNotification =
    FirebaseAdmin.Messaging.Notification;

namespace QatarLearningBackend.Services;

public sealed class FcmPushService : IFcmPushService
{
    // Must match the channel id created in the Flutter app.
    private const string AndroidChannelId = "high_importance_channel";

    private readonly AppDbContext _context;
    private readonly ILogger<FcmPushService> _logger;

    public FcmPushService(
        AppDbContext context,
        ILogger<FcmPushService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SendToUserAsync(
        int userId,
        string title,
        string body,
        string type,
        int? bookingId = null,
        int? notificationId = null)
    {
        // Firebase may not be initialized (missing key on the server).
        if (FirebaseApp.DefaultInstance == null)
        {
            _logger.LogWarning(
                "Firebase is not initialized; skipping push for user {UserId}.",
                userId);

            return;
        }

        var deviceTokens = await _context.DeviceTokens
            .Where(device =>
                device.UserId == userId &&
                !string.IsNullOrWhiteSpace(device.Token))
            .ToListAsync();

        if (deviceTokens.Count == 0)
        {
            _logger.LogWarning(
                "No FCM device tokens found for user {UserId}.",
                userId);

            return;
        }

        // Only include keys that have a value.
        var data = new Dictionary<string, string>
        {
            ["type"] = type
        };

        if (notificationId.HasValue)
            data["notificationId"] = notificationId.Value.ToString();

        if (bookingId.HasValue)
            data["bookingId"] = bookingId.Value.ToString();

        // Track IDs so we never need to name the entity type.
        var tokenIdsToRemove = new List<int>();

        foreach (var deviceToken in deviceTokens)
        {
            var message = new Message
            {
                Token = deviceToken.Token,

                Notification = new FirebaseNotification
                {
                    Title = title,
                    Body = body
                },

                Data = data,

                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = AndroidChannelId,
                        Sound = "default"
                    }
                },

                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string>
                    {
                        ["apns-priority"] = "10"
                    },

                    Aps = new Aps
                    {
                        Alert = new ApsAlert
                        {
                            Title = title,
                            Body = body
                        },
                        Sound = "default",
                        Badge = 1
                    }
                }
            };

            try
            {
                var messageId =
                    await FirebaseMessaging.DefaultInstance.SendAsync(message);

                _logger.LogInformation(
                    "FCM message sent successfully. " +
                    "UserId: {UserId}, TokenId: {TokenId}, " +
                    "MessageId: {MessageId}",
                    userId,
                    deviceToken.Id,
                    messageId);
            }
            catch (FirebaseMessagingException exception)
            {
                _logger.LogError(
                    exception,
                    "FCM sending failed. UserId: {UserId}, " +
                    "TokenId: {TokenId}, ErrorCode: {ErrorCode}",
                    userId,
                    deviceToken.Id,
                    exception.MessagingErrorCode);

                switch (exception.MessagingErrorCode)
                {
                    case MessagingErrorCode.Unregistered:
                        // Token is dead (app uninstalled / token rotated).
                        tokenIdsToRemove.Add(deviceToken.Id);
                        break;

                    case MessagingErrorCode.SenderIdMismatch:
                        _logger.LogError(
                            "SenderIdMismatch: the app's Firebase config " +
                            "(google-services.json / GoogleService-Info.plist) " +
                            "belongs to a different project than the " +
                            "service-account key.");
                        break;

                    case MessagingErrorCode.ThirdPartyAuthError:
                        _logger.LogError(
                            "ThirdPartyAuthError: check the APNs key is " +
                            "uploaded in Firebase Console for iOS.");
                        break;
                }
            }
            catch (Exception exception)
            {
                // Never let a push failure break the booking request.
                _logger.LogError(
                    exception,
                    "Unexpected error while sending FCM. " +
                    "UserId: {UserId}, TokenId: {TokenId}",
                    userId,
                    deviceToken.Id);
            }
        }

        if (tokenIdsToRemove.Count > 0)
        {
            try
            {
                var staleTokens = deviceTokens
                    .Where(t => tokenIdsToRemove.Contains(t.Id))
                    .ToList();

                _context.DeviceTokens.RemoveRange(staleTokens);
                await _context.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to remove invalid device tokens for user {UserId}.",
                    userId);
            }
        }
    }
}