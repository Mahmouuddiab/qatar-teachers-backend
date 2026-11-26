using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using QatarLearningBackend.DTOs;
using QatarLearningBackend.Models;
using QatarLearningBackend.Services;

namespace QatarLearningBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IFcmPushService _fcmPushService;
        private readonly ILogger<StudentController> _logger;

        public StudentController(
            AppDbContext context,
            INotificationService notifications,
            IFcmPushService fcmPushService,
            ILogger<StudentController> logger)
        {
            _context = context;
            _notifications = notifications;
            _fcmPushService = fcmPushService;
            _logger = logger;
        }

        // ---------------------------------------------------------------------
        // Constants / helpers
        // ---------------------------------------------------------------------

        private static class BookingStatus
        {
            public const string Pending = "Pending";
            public const string Confirmed = "Confirmed";
            public const string Completed = "Completed";
            public const string Cancelled = "Cancelled";

            public static readonly string[] All =
            {
                Pending,
                Confirmed,
                Completed,
                Cancelled
            };
        }

        private const string StudentRole = "Student";
        private const string TeacherRole = "Teacher";

        private const int DefaultSessionMinutes = 60;
        private const int MaxPageSize = 100;

        private static DateTime QatarNow =>
            DateTime.UtcNow.AddHours(3);

        private int CurrentUserId
        {
            get
            {
                var value =
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

                return int.TryParse(value, out var id) ? id : 0;
            }
        }

        private static List<string> Split(string? csv)
        {
            return string.IsNullOrWhiteSpace(csv)
                ? new List<string>()
                : csv.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                    .ToList();
        }

        private async Task SendPushSafelyAsync(
            int userId,
            Notification notification,
            int bookingId)
        {
            try
            {
                await _fcmPushService.SendToUserAsync(
                    userId: userId,
                    title: notification.Title,
                    body: notification.Body,
                    type: notification.Type,
                    bookingId: bookingId,
                    notificationId: notification.Id);
            }
            catch (Exception exception)
            {
                // The booking and database notification were already saved.
                // Do not fail the booking request only because FCM failed.
                _logger.LogError(
                    exception,
                    "Notification was saved, but FCM sending failed. " +
                    "UserId: {UserId}, BookingId: {BookingId}, " +
                    "NotificationId: {NotificationId}",
                    userId,
                    bookingId,
                    notification.Id);
            }
        }

        // =====================================================================
        // BROWSE COURSES
        // =====================================================================

        /// <summary>
        /// Browse active courses.
        /// Optional:
        /// ?subject=Science&amp;teacherId=5&amp;search=ielts&amp;page=1&amp;pageSize=20
        /// </summary>
        [HttpGet("courses")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCourses(
            [FromQuery] string? subject,
            [FromQuery] int? teacherId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Courses
                .AsNoTracking()
                .Where(course => course.IsActive);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                var cleanSubject = subject.Trim();
                query = query.Where(
                    course => course.Subject == cleanSubject);
            }

            if (teacherId.HasValue)
            {
                query = query.Where(
                    course => course.TeacherId == teacherId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                query = query.Where(course =>
                    course.Title.Contains(term) ||
                    (course.Description != null &&
                     course.Description.Contains(term)));
            }

            var courses = await query
                .OrderBy(course => course.Subject)
                .ThenBy(course => course.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(course => new CourseResponseDto
                {
                    Id = course.Id,
                    TeacherId = course.TeacherId,

                    TeacherName = _context.Users
                        .Where(user => user.Id == course.TeacherId)
                        .Select(user => user.FullName)
                        .FirstOrDefault(),

                    Title = course.Title,
                    Subject = course.Subject,
                    Description = course.Description,
                    Level = course.Level,
                    PricePerSessionQAR = course.PricePerSessionQAR,
                    DurationMinutes = course.DurationMinutes,
                    SessionMode = course.SessionMode,
                    IsActive = course.IsActive,
                    CreatedAt = course.CreatedAt
                })
                .ToListAsync();

            return Ok(courses);
        }

        [HttpGet("courses/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCourse(int id)
        {
            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(course =>
                    course.Id == id &&
                    course.IsActive);

            if (course == null)
            {
                return NotFound(new
                {
                    message = "Course not found."
                });
            }

            var teacherName = await _context.Users
                .Where(user => user.Id == course.TeacherId)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync();

            return Ok(
                CourseResponseDto.From(course, teacherName));
        }

        /// <summary>
        /// Distinct subjects that currently have active courses.
        /// </summary>
        [HttpGet("subjects")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSubjects()
        {
            var subjects = await _context.Courses
                .AsNoTracking()
                .Where(course => course.IsActive)
                .Select(course => course.Subject)
                .Distinct()
                .OrderBy(subject => subject)
                .ToListAsync();

            return Ok(subjects);
        }

        // =====================================================================
        // VIEW TEACHER
        // =====================================================================

        /// <summary>
        /// Public teacher profile. The id is the teacher's user ID.
        /// </summary>
        [HttpGet("teachers/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTeacher(int id)
        {
            var teacher = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user =>
                    user.Id == id &&
                    user.Role == TeacherRole);

            if (teacher == null)
            {
                return NotFound(new
                {
                    message = "Teacher not found."
                });
            }

            var profile = await _context.TeacherProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile =>
                    profile.UserId == id);

            var completedCount = await _context.Bookings
                .CountAsync(booking =>
                    booking.TeacherId == id &&
                    booking.Status == BookingStatus.Completed);

            return Ok(new TeacherProfileResponseDto
            {
                TeacherId = id,
                Name = teacher.FullName,
                Bio = profile?.Bio,
                Qualifications = profile?.Qualifications,
                HourlyRateQAR = Convert.ToDecimal(
                    (object?)profile?.HourlyRateQAR ?? 0m),
                Subjects = Split(profile?.Subjects),
                Languages = Split(profile?.Languages),
                Rating = Math.Round(
                    Convert.ToDouble(
                        (object?)profile?.Rating ?? 0.0),
                    2),
                YearsOfExperience = Convert.ToInt32(
                    (object?)profile?.YearsOfExperience ?? 0),
                CompletedSessions = completedCount
            });
        }

        // =====================================================================
        // BOOKINGS
        // =====================================================================

        /// <summary>
        /// Book a course at a given date/time.
        /// The teacher must then accept it.
        /// </summary>
        [HttpPost("bookings")]
        [Authorize(Roles = StudentRole)]
        public async Task<IActionResult> BookCourse(
            [FromBody] BookCourseDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var studentId = CurrentUserId;

            if (studentId == 0)
            {
                return Unauthorized(new
                {
                    message =
                        "Invalid token: user id claim is missing."
                });
            }

            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(course =>
                    course.Id == dto.CourseId &&
                    course.IsActive);

            if (course == null)
            {
                return NotFound(new
                {
                    message =
                        "Course not found or no longer available."
                });
            }

            // Online / InPerson.
            // If the course offers both, the student must choose.
            string mode;

            if (course.SessionMode == SessionModes.Both)
            {
                if (!SessionModes.TryParseBookingMode(
                        dto.Mode,
                        out mode))
                {
                    return BadRequest(new
                    {
                        message =
                            "This course offers both options. " +
                            "Mode must be Online or InPerson."
                    });
                }
            }
            else
            {
                mode = course.SessionMode;
            }

            // Treat incoming time as Qatar local time.
            var start = dto.SessionDate.Kind == DateTimeKind.Utc
                ? dto.SessionDate.AddHours(3)
                : dto.SessionDate;

            start = DateTime.SpecifyKind(
                start,
                DateTimeKind.Unspecified);

            if (start <= QatarNow)
            {
                return BadRequest(new
                {
                    message =
                        "Session date must be in the future."
                });
            }

            var end = start.AddMinutes(course.DurationMinutes);

            if (end.Date != start.Date)
            {
                return BadRequest(new
                {
                    message =
                        "Session cannot run past midnight."
                });
            }

            // The session must fit inside one teacher availability slot.
            var date = start.Date;
            var day = start.DayOfWeek;

            var slots = await _context.TeacherAvailabilities
                .AsNoTracking()
                .Where(availability =>
                    availability.TeacherId == course.TeacherId &&
                    (
                        availability.IsRecurring &&
                        availability.DayOfWeek == day
                        ||
                        !availability.IsRecurring &&
                        availability.Date == date
                    ))
                .ToListAsync();

            var timeStart = start.TimeOfDay;
            var timeEnd = end.TimeOfDay;

            if (!slots.Any(slot =>
                    timeStart >= slot.StartTime &&
                    timeEnd <= slot.EndTime))
            {
                return BadRequest(new
                {
                    message =
                        "The teacher is not available at this time."
                });
            }

            // Teacher must not have an overlapping pending/confirmed session.
            var from = start.AddMinutes(
                -DefaultSessionMinutes);

            var teacherBusy = await _context.Bookings
                .AnyAsync(booking =>
                    booking.TeacherId == course.TeacherId &&
                    (
                        booking.Status == BookingStatus.Pending ||
                        booking.Status == BookingStatus.Confirmed
                    ) &&
                    booking.SessionDate > from &&
                    booking.SessionDate < end);

            if (teacherBusy)
            {
                return Conflict(new
                {
                    message =
                        "This time slot is already booked."
                });
            }

            // Student must not be double-booked.
            var studentBusy = await _context.Bookings
                .AnyAsync(booking =>
                    booking.StudentId == studentId &&
                    (
                        booking.Status == BookingStatus.Pending ||
                        booking.Status == BookingStatus.Confirmed
                    ) &&
                    booking.SessionDate > from &&
                    booking.SessionDate < end);

            if (studentBusy)
            {
                return Conflict(new
                {
                    message =
                        "You already have a session at this time."
                });
            }

            var booking = new Booking
            {
                TeacherId = course.TeacherId,
                StudentId = studentId,
                CourseId = course.Id,
                SessionMode = mode,
                Subject = course.Title,
                SessionDate = start,
                TotalPriceQAR = course.PricePerSessionQAR,
                Status = BookingStatus.Pending
            };

            _context.Bookings.Add(booking);

            // This adds the notification to the DbContext.
            var notification =
                await _notifications.BookingRequestedAsync(
                    booking);

            // This saves both the booking and notification.
            // Their IDs are generated here.
            await _context.SaveChangesAsync();

            // Send the push to the teacher after the database save.
            await SendPushSafelyAsync(
                userId: booking.TeacherId,
                notification: notification,
                bookingId: booking.Id);

            var teacherName = await _context.Users
                .Where(user => user.Id == course.TeacherId)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync();

            return CreatedAtAction(
                nameof(GetMyBookings),
                null,
                new StudentBookingDto
                {
                    Id = booking.Id,
                    TeacherId = booking.TeacherId,
                    TeacherName = teacherName,
                    CourseId = booking.CourseId,
                    Mode = booking.SessionMode,
                    Subject = booking.Subject,
                    SessionDate = booking.SessionDate,
                    TotalPriceQAR = booking.TotalPriceQAR,
                    Status = booking.Status
                });
        }

        /// <summary>
        /// My bookings.
        /// </summary>
        [HttpGet("bookings")]
        [Authorize(Roles = StudentRole)]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var studentId = CurrentUserId;

            if (studentId == 0)
            {
                return Unauthorized(new
                {
                    message =
                        "Invalid token: user id claim is missing."
                });
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(
                pageSize,
                1,
                MaxPageSize);

            var query = _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.StudentId == studentId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var match = BookingStatus.All
                    .FirstOrDefault(item =>
                        item.Equals(
                            status,
                            StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    return BadRequest(new
                    {
                        message =
                            "Status must be Pending, Confirmed, " +
                            "Completed or Cancelled."
                    });
                }

                query = query.Where(
                    booking => booking.Status == match);
            }

            var bookings = await query
                .OrderByDescending(booking =>
                    booking.SessionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(booking => new StudentBookingDto
                {
                    Id = booking.Id,
                    TeacherId = booking.TeacherId,

                    TeacherName = _context.Users
                        .Where(user =>
                            user.Id == booking.TeacherId)
                        .Select(user => user.FullName)
                        .FirstOrDefault(),

                    CourseId = booking.CourseId,
                    Mode = booking.SessionMode,
                    Subject = booking.Subject,
                    SessionDate = booking.SessionDate,
                    TotalPriceQAR = booking.TotalPriceQAR,
                    Status = booking.Status
                })
                .ToListAsync();

            return Ok(bookings);
        }

        /// <summary>
/// Full details of one of my bookings.
/// </summary>
[HttpGet("bookings/{id:int}")]
[Authorize(Roles = StudentRole)]
public async Task<IActionResult> GetBookingDetails(int id)
{
    var studentId = CurrentUserId;

    if (studentId == 0)
    {
        return Unauthorized(new
        {
            message = "Invalid token: user id claim is missing."
        });
    }

    var booking = await _context.Bookings
        .AsNoTracking()
        .FirstOrDefaultAsync(item =>
            item.Id == id &&
            item.StudentId == studentId);

    if (booking == null)
    {
        return NotFound(new
        {
            message = "Booking not found."
        });
    }

    var teacherName = await _context.Users
        .AsNoTracking()
        .Where(user => user.Id == booking.TeacherId)
        .Select(user => user.FullName)
        .FirstOrDefaultAsync();

    var profile = await _context.TeacherProfiles
        .AsNoTracking()
        .FirstOrDefaultAsync(item =>
            item.UserId == booking.TeacherId);

    Course? course = null;

    if (booking.CourseId.HasValue)
    {
        course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.Id == booking.CourseId.Value);
    }

    var duration = course?.DurationMinutes ?? DefaultSessionMinutes;

    var canCancel =
        (booking.Status == BookingStatus.Pending ||
         booking.Status == BookingStatus.Confirmed) &&
        booking.SessionDate > QatarNow;

    return Ok(new StudentBookingDetailsDto
    {
        Id = booking.Id,
        Status = booking.Status,
        Mode = booking.SessionMode,
        Subject = booking.Subject,

        SessionDate = booking.SessionDate,
        SessionEndDate = booking.SessionDate.AddMinutes(duration),
        DurationMinutes = duration,

        TotalPriceQAR = booking.TotalPriceQAR,
        CreatedAt = booking.CreatedAt,
        CanCancel = canCancel,

        Teacher = new BookingTeacherDto
        {
            TeacherId = booking.TeacherId,
            Name = teacherName,
            Bio = profile?.Bio,
            Qualifications = profile?.Qualifications,
            Subjects = Split(profile?.Subjects),
            Languages = Split(profile?.Languages),
            Rating = Math.Round(
                Convert.ToDouble((object?)profile?.Rating ?? 0.0), 2),
            YearsOfExperience = Convert.ToInt32(
                (object?)profile?.YearsOfExperience ?? 0)
        },

        Course = course == null
            ? null
            : new BookingCourseDto
            {
                CourseId = course.Id,
                Title = course.Title,
                Subject = course.Subject,
                Description = course.Description,
                Level = course.Level,
                PricePerSessionQAR = course.PricePerSessionQAR,
                DurationMinutes = course.DurationMinutes,
                SessionMode = course.SessionMode,
                IsActive = course.IsActive
            }
    });
}

        /// <summary>
        /// Cancel one of my pending/confirmed bookings.
        /// </summary>
        [HttpPut("bookings/{id:int}/cancel")]
        [Authorize(Roles = StudentRole)]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var studentId = CurrentUserId;

            if (studentId == 0)
            {
                return Unauthorized(new
                {
                    message =
                        "Invalid token: user id claim is missing."
                });
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.StudentId == studentId);

            if (booking == null)
            {
                return NotFound(new
                {
                    message = "Booking not found."
                });
            }

            if (booking.Status != BookingStatus.Pending &&
                booking.Status != BookingStatus.Confirmed)
            {
                return BadRequest(new
                {
                    message =
                        $"Only pending or confirmed bookings can be " +
                        $"cancelled (current: {booking.Status})."
                });
            }

            if (booking.SessionDate <= QatarNow)
            {
                return BadRequest(new
                {
                    message =
                        "This session has already started."
                });
            }

            booking.Status = BookingStatus.Cancelled;

            // Add the notification to the same DbContext.
            var notification =
                await _notifications.BookingCancelledByStudentAsync(
                    booking);

            try
            {
                // Save the cancellation and notification.
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "This booking was modified by someone else. " +
                        "Please refresh and try again."
                });
            }

            // Send the push to the teacher after saving.
            await SendPushSafelyAsync(
                userId: booking.TeacherId,
                notification: notification,
                bookingId: booking.Id);

            return Ok(new
            {
                message = "Booking cancelled.",
                status = booking.Status
            });
        }
    }
}