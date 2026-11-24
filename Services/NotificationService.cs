using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using QatarLearningBackend.Models;

namespace QatarLearningBackend.Services;

public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> BookingRequestedAsync(Booking booking)
    {
        var studentName = await GetNameAsync(booking.StudentId);

        var notification = new Notification
        {
            UserId = booking.TeacherId,
            Type = NotificationTypes.BookingRequested,
            Title = "طلب حجز جديد",
            Body =
                $"{studentName} طلب حجز حصة {booking.Subject} بتاريخ {Format(booking.SessionDate)}",
            Booking = booking,
            IsRead = false,
            CreatedAt = QatarNow
        };

        _context.Notifications.Add(notification);

        return notification;
    }

    public async Task<Notification> BookingDecisionAsync(
        Booking booking,
        bool accepted)
    {
        var teacherName = await GetNameAsync(booking.TeacherId);

        var notification = accepted
            ? new Notification
            {
                UserId = booking.StudentId,
                Type = NotificationTypes.BookingConfirmed,
                Title = "تم تأكيد حجزك",
                Body =
                    $"أكد الأستاذ {teacherName} حجزك لحصة {booking.Subject} بتاريخ {Format(booking.SessionDate)}",
                Booking = booking,
                IsRead = false,
                CreatedAt = QatarNow
            }
            : new Notification
            {
                UserId = booking.StudentId,
                Type = NotificationTypes.BookingRejected,
                Title = "تم رفض طلب الحجز",
                Body =
                    $"اعتذر الأستاذ {teacherName} عن حصة {booking.Subject} بتاريخ {Format(booking.SessionDate)}",
                Booking = booking,
                IsRead = false,
                CreatedAt = QatarNow
            };

        _context.Notifications.Add(notification);

        return notification;
    }

    public async Task<Notification> BookingCancelledByStudentAsync(
        Booking booking)
    {
        var studentName = await GetNameAsync(booking.StudentId);

        var notification = new Notification
        {
            UserId = booking.TeacherId,
            Type = NotificationTypes.BookingCancelled,
            Title = "تم إلغاء حجز",
            Body =
                $"ألغى الطالب {studentName} حجز حصة {booking.Subject} بتاريخ {Format(booking.SessionDate)}",
            Booking = booking,
            IsRead = false,
            CreatedAt = QatarNow
        };

        _context.Notifications.Add(notification);

        return notification;
    }

    public async Task<Notification> SessionCompletedAsync(Booking booking)
    {
        var teacherName = await GetNameAsync(booking.TeacherId);

        var notification = new Notification
        {
            UserId = booking.StudentId,
            Type = NotificationTypes.SessionCompleted,
            Title = "اكتملت الحصة",
            Body =
                $"تم تسجيل حصة {booking.Subject} مع الأستاذ {teacherName} كمكتملة",
            Booking = booking,
            IsRead = false,
            CreatedAt = QatarNow
        };

        _context.Notifications.Add(notification);

        return notification;
    }

    private async Task<string> GetNameAsync(int userId)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync() ?? string.Empty;
    }

    private static DateTime QatarNow =>
        DateTime.UtcNow.AddHours(3);

    private static string Format(DateTime date)
    {
        return date.ToString(
            "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture);
    }
}