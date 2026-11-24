using QatarLearningBackend.Models;

namespace QatarLearningBackend.Services;

public interface INotificationService
{
    Task<Notification> BookingRequestedAsync(Booking booking);

    Task<Notification> BookingDecisionAsync(
        Booking booking,
        bool accepted);

    Task<Notification> BookingCancelledByStudentAsync(
        Booking booking);

    Task<Notification> SessionCompletedAsync(
        Booking booking);
}