using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using QatarLearningBackend.DTOs;
using QatarLearningBackend.Models;

namespace QatarLearningBackend.Controllers
{
    [Route("api/[controller]")]   // -> /api/Student
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StudentController(AppDbContext context)
        {
            _context = context;
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

            public static readonly string[] All = { Pending, Confirmed, Completed, Cancelled };
        }

        private const string StudentRole = "Student";
        private const string TeacherRole = "Teacher";

        // Length assumed for existing bookings (Booking has no duration field).
        private const int DefaultSessionMinutes = 60;

        private const int MaxPageSize = 100;

        // Qatar has no DST, so UTC+3 year-round.
        private static DateTime QatarNow => DateTime.UtcNow.AddHours(3);

        private int CurrentUserId
        {
            get
            {
                var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
                return int.TryParse(value, out var id) ? id : 0;
            }
        }

        private static List<string> Split(string? csv) =>
            string.IsNullOrWhiteSpace(csv)
                ? new List<string>()
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // =====================================================================
        // BROWSE COURSES (public)
        // =====================================================================

        /// <summary>
        /// Browse active courses.
        /// Optional: ?subject=Science&amp;teacherId=5&amp;search=ielts&amp;page=1&amp;pageSize=20
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

            var query = _context.Courses.AsNoTracking().Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                var s = subject.Trim();
                query = query.Where(c => c.Subject == s);
            }

            if (teacherId.HasValue)
                query = query.Where(c => c.TeacherId == teacherId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c => c.Title.Contains(term) ||
                                         (c.Description != null && c.Description.Contains(term)));
            }

            var courses = await query
                .OrderBy(c => c.Subject).ThenBy(c => c.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    TeacherId = c.TeacherId,
                    TeacherName = _context.Users.Where(u => u.Id == c.TeacherId)
                                                .Select(u => u.FullName).FirstOrDefault(),
                    Title = c.Title,
                    Subject = c.Subject,
                    Description = c.Description,
                    Level = c.Level,
                    PricePerSessionQAR = c.PricePerSessionQAR,
                    DurationMinutes = c.DurationMinutes,
                    SessionMode = c.SessionMode,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            return Ok(courses);
        }

        [HttpGet("courses/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCourse(int id)
        {
            var course = await _context.Courses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
            if (course == null)
                return NotFound(new { message = "Course not found." });

            var teacherName = await _context.Users
                .Where(u => u.Id == course.TeacherId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();

            return Ok(CourseResponseDto.From(course, teacherName));
        }

        /// <summary>Distinct subjects that currently have active courses (for filter dropdowns).</summary>
        [HttpGet("subjects")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSubjects()
        {
            var subjects = await _context.Courses.AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => c.Subject)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            return Ok(subjects);
        }

        // =====================================================================
        // VIEW A TEACHER (public)
        // =====================================================================

        /// <summary>Public teacher profile. {id} is the teacher's User Id.</summary>
        [HttpGet("teachers/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTeacher(int id)
        {
            var teacher = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == TeacherRole);
            if (teacher == null)
                return NotFound(new { message = "Teacher not found." });

            var profile = await _context.TeacherProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == id);

            var completedCount = await _context.Bookings
                .CountAsync(b => b.TeacherId == id && b.Status == BookingStatus.Completed);

            return Ok(new TeacherProfileResponseDto
            {
                TeacherId = id,
                Name = teacher.FullName,
                Bio = profile?.Bio,
                Qualifications = profile?.Qualifications,
                HourlyRateQAR = Convert.ToDecimal((object?)profile?.HourlyRateQAR ?? 0m),
                Subjects = Split(profile?.Subjects),
                Languages = Split(profile?.Languages),
                Rating = Math.Round(Convert.ToDouble((object?)profile?.Rating ?? 0.0), 2),
                YearsOfExperience = Convert.ToInt32((object?)profile?.YearsOfExperience ?? 0),
                CompletedSessions = completedCount
            });
        }

        // =====================================================================
        // BOOKINGS (student only)
        // =====================================================================

        /// <summary>Book a course at a given date/time. The teacher must then accept it.</summary>
        [HttpPost("bookings")]
        [Authorize(Roles = StudentRole)]
        public async Task<IActionResult> BookCourse([FromBody] BookCourseDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var studentId = CurrentUserId;
            if (studentId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var course = await _context.Courses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.IsActive);
            if (course == null)
                return NotFound(new { message = "Course not found or no longer available." });

            // Online / InPerson. If the course offers both, the student must choose.
            string mode;
            if (course.SessionMode == SessionModes.Both)
            {
                if (!SessionModes.TryParseBookingMode(dto.Mode, out mode))
                    return BadRequest(new { message = "This course offers both options. Mode must be Online or InPerson." });
            }
            else
            {
                mode = course.SessionMode;
            }

            // Treat the incoming time as Qatar local time (convert if the client sent UTC).
            var start = dto.SessionDate.Kind == DateTimeKind.Utc
                ? dto.SessionDate.AddHours(3)
                : dto.SessionDate;
            start = DateTime.SpecifyKind(start, DateTimeKind.Unspecified);

            if (start <= QatarNow)
                return BadRequest(new { message = "Session date must be in the future." });

            var end = start.AddMinutes(course.DurationMinutes);
            if (end.Date != start.Date)
                return BadRequest(new { message = "Session cannot run past midnight." });

            // 1) Session must fit inside one of the teacher's availability slots
            var date = start.Date;
            var day = start.DayOfWeek;

            var slots = await _context.TeacherAvailabilities.AsNoTracking()
                .Where(a => a.TeacherId == course.TeacherId &&
                            ((a.IsRecurring && a.DayOfWeek == day) ||
                             (!a.IsRecurring && a.Date == date)))
                .ToListAsync();

            var tStart = start.TimeOfDay;
            var tEnd = end.TimeOfDay;
            if (!slots.Any(s => tStart >= s.StartTime && tEnd <= s.EndTime))
                return BadRequest(new { message = "The teacher is not available at this time." });

            // 2) Teacher must not already have a pending/confirmed session overlapping this one
            var from = start.AddMinutes(-DefaultSessionMinutes);

            var teacherBusy = await _context.Bookings.AnyAsync(b =>
                b.TeacherId == course.TeacherId &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                b.SessionDate > from && b.SessionDate < end);
            if (teacherBusy)
                return Conflict(new { message = "This time slot is already booked." });

            // 3) Student must not be double-booked either
            var studentBusy = await _context.Bookings.AnyAsync(b =>
                b.StudentId == studentId &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                b.SessionDate > from && b.SessionDate < end);
            if (studentBusy)
                return Conflict(new { message = "You already have a session at this time." });

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
            await _context.SaveChangesAsync();

            var teacherName = await _context.Users
                .Where(u => u.Id == course.TeacherId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();

            return CreatedAtAction(nameof(GetMyBookings), null, new StudentBookingDto
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

        /// <summary>My bookings. Optional: ?status=Pending|Confirmed|Completed|Cancelled&amp;page=1&amp;pageSize=50</summary>
        [HttpGet("bookings")]
        [Authorize(Roles = StudentRole)]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var studentId = CurrentUserId;

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Bookings.AsNoTracking().Where(b => b.StudentId == studentId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var match = BookingStatus.All.FirstOrDefault(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return BadRequest(new { message = "Status must be Pending, Confirmed, Completed or Cancelled." });
                query = query.Where(b => b.Status == match);
            }

            var bookings = await query
                .OrderByDescending(b => b.SessionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new StudentBookingDto
                {
                    Id = b.Id,
                    TeacherId = b.TeacherId,
                    TeacherName = _context.Users.Where(u => u.Id == b.TeacherId)
                                                .Select(u => u.FullName).FirstOrDefault(),
                    CourseId = b.CourseId,
                    Mode = b.SessionMode,
                    Subject = b.Subject,
                    SessionDate = b.SessionDate,
                    TotalPriceQAR = b.TotalPriceQAR,
                    Status = b.Status
                })
                .ToListAsync();

            return Ok(bookings);
        }

        /// <summary>Cancel one of my pending/confirmed bookings (before it starts).</summary>
        [HttpPut("bookings/{id:int}/cancel")]
        [Authorize(Roles = StudentRole)]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var studentId = CurrentUserId;

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id && b.StudentId == studentId);
            if (booking == null)
                return NotFound(new { message = "Booking not found." });

            if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
                return BadRequest(new { message = $"Only pending or confirmed bookings can be cancelled (current: {booking.Status})." });

            if (booking.SessionDate <= QatarNow)
                return BadRequest(new { message = "This session has already started." });

            booking.Status = BookingStatus.Cancelled;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "This booking was modified by someone else. Please refresh and try again." });
            }

            return Ok(new { message = "Booking cancelled.", status = booking.Status });
        }
    }
}