using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using QatarLearningBackend.Models;

namespace QatarLearningBackend.Services
{
    /// <summary>
    /// Creates notifications for booking events.
    /// NOTE: these methods only ADD the notification to the DbContext, they do not save.
    /// The caller's own SaveChangesAsync() saves the booking change and the notification
    /// together in one transaction, so you never get one without the other.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>Student booked a course -> notify the teacher.</summary>
        Task BookingRequestedAsync(Booking booking);

        /// <summary>Teacher accepted / rejected a request -> notify the student.</summary>
        Task BookingDecisionAsync(Booking booking, bool accepted);

        /// <summary>Student cancelled -> notify the teacher.</summary>
        Task BookingCancelledByStudentAsync(Booking booking);

        /// <summary>Teacher marked the session completed -> notify the student.</summary>
        Task SessionCompletedAsync(Booking booking);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task BookingRequestedAsync(Booking b)
        {
            var student = await GetNameAsync(b.StudentId);

            Add(b.TeacherId, NotificationTypes.BookingRequested,
                "طلب حجز جديد",
                $"{student} طلب حجز حصة {b.Subject} بتاريخ {Format(b.SessionDate)}",
                b);
        }

        public async Task BookingDecisionAsync(Booking b, bool accepted)
        {
            var teacher = await GetNameAsync(b.TeacherId);

            if (accepted)
            {
                Add(b.StudentId, NotificationTypes.BookingConfirmed,
                    "تم تأكيد حجزك",
                    $"أكد الأستاذ {teacher} حجزك لحصة {b.Subject} بتاريخ {Format(b.SessionDate)}",
                    b);
            }
            else
            {
                Add(b.StudentId, NotificationTypes.BookingRejected,
                    "تم رفض طلب الحجز",
                    $"اعتذر الأستاذ {teacher} عن حصة {b.Subject} بتاريخ {Format(b.SessionDate)}",
                    b);
            }
        }

        public async Task BookingCancelledByStudentAsync(Booking b)
        {
            var student = await GetNameAsync(b.StudentId);

            Add(b.TeacherId, NotificationTypes.BookingCancelled,
                "تم إلغاء حجز",
                $"ألغى الطالب {student} حجز حصة {b.Subject} بتاريخ {Format(b.SessionDate)}",
                b);
        }

        public async Task SessionCompletedAsync(Booking b)
        {
            var teacher = await GetNameAsync(b.TeacherId);

            Add(b.StudentId, NotificationTypes.SessionCompleted,
                "اكتملت الحصة",
                $"تم تسجيل حصة {b.Subject} مع الأستاذ {teacher} كمكتملة",
                b);
        }

        // ------------------------------------------------------------------

        private void Add(int userId, string type, string title, string body, Booking booking)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                // Navigation (not BookingId) so it also works for a booking that isn't saved yet.
                Booking = booking,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(3) // Qatar local time
            });
        }

        private async Task<string> GetNameAsync(int userId) =>
            await _context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync() ?? string.Empty;

        private static string Format(DateTime date) =>
            date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}