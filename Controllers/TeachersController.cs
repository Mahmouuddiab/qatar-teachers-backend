using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using QatarLearningBackend.DTOs;
using QatarLearningBackend.Models;

namespace QatarLearningBackend.Controllers
{
    /// <summary>
    /// Teacher app. Tabs -> endpoints:
    ///   الرئيسية  (Home)         GET  dashboard
    ///   الحجوزات  (Bookings)     GET  bookings, bookings/counts, bookings/upcoming, bookings/{id}
    ///                            PUT  bookings/{id}/status, bookings/{id}/complete
    ///   المواعيد  (Appointments) GET/POST/DELETE availability
    ///   حسابي     (Account)      GET/PUT profile, and courses (CRUD)
    /// </summary>
    [Route("api/[controller]")]   // -> /api/Teacher
    [ApiController]
    [Authorize(Roles = "Teacher")]
    public class TeacherController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TeacherController(AppDbContext context)
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

        private const string TeacherRole = "Teacher";

        // Assumed length of an existing booking (Booking has no duration field).
        // Used for the "can complete" check and for booking conflict detection.
        private const int SessionLengthMinutes = 60;

        private const int MaxPageSize = 100;

        /// <summary>How many sessions the home screen previews under "الحصص القادمة".</summary>
        private const int HomeUpcomingPreviewCount = 5;

        private static readonly string[] TimeFormats = { @"hh\:mm", @"h\:mm" };

        // Qatar has no DST, so UTC+3 year-round.
        private static DateTime QatarNow => DateTime.UtcNow.AddHours(3);

        /// <summary>
        /// Reads the user id from the JWT. Supports both the mapped NameIdentifier
        /// claim and the raw "sub" claim. Returns 0 if missing/invalid.
        /// </summary>
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

        private static string Join(IEnumerable<string>? items) =>
            items == null
                ? string.Empty
                : string.Join(", ", items.Where(i => !string.IsNullOrWhiteSpace(i))
                                         .Select(i => i.Trim())
                                         .Distinct(StringComparer.OrdinalIgnoreCase));

        private static string? Clean(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Looks up a teacher's display name (Users.FullName) by user id.</summary>
        private Task<string?> GetTeacherNameAsync(int teacherId) =>
            _context.Users.AsNoTracking()
                .Where(u => u.Id == teacherId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();

        // =====================================================================
        // HOME  (الرئيسية)
        // =====================================================================

        /// <summary>
        /// Home screen data: greeting name, pending-requests banner, the four stat cards
        /// (earnings, completed, rating, upcoming) and the next few upcoming sessions.
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var teacherId = CurrentUserId;
            if (teacherId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var now = QatarNow;

            var teacher = await _context.Users.AsNoTracking()
                .Where(u => u.Id == teacherId && u.Role == TeacherRole)
                .Select(u => new { u.FullName })
                .FirstOrDefaultAsync();
            if (teacher == null)
                return NotFound(new { message = "Teacher not found." });

            var profile = await _context.TeacherProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == teacherId);

            var bookings = _context.Bookings.AsNoTracking().Where(b => b.TeacherId == teacherId);
            var completed = bookings.Where(b => b.Status == BookingStatus.Completed);

            var upcomingSessions = await bookings
                .Where(b => b.Status == BookingStatus.Confirmed && b.SessionDate >= now)
                .OrderBy(b => b.SessionDate)
                .Take(HomeUpcomingPreviewCount)
                .Select(b => new UpcomingSessionDto
                {
                    Id = b.Id,
                    StudentId = b.StudentId,
                    StudentName = _context.Users.Where(u => u.Id == b.StudentId)
                                                .Select(u => u.FullName).FirstOrDefault(),
                    Subject = b.Subject,
                    Mode = b.SessionMode,
                    SessionDate = b.SessionDate
                })
                .ToListAsync();

            var dto = new TeacherHomeDto
            {
                TeacherId = teacherId,
                TeacherName = teacher.FullName,
                // Only requests that can still be accepted (session not in the past)
                PendingRequests = await bookings.CountAsync(b => b.Status == BookingStatus.Pending && b.SessionDate > now),
                TotalEarningsQAR = await completed.SumAsync(b => (decimal?)b.TotalPriceQAR) ?? 0m,
                CompletedClasses = await completed.CountAsync(),
                AverageRating = Math.Round(Convert.ToDouble((object?)profile?.Rating ?? 0.0), 2),
                UpcomingClasses = await bookings.CountAsync(b => b.Status == BookingStatus.Confirmed && b.SessionDate >= now),
                UpcomingSessions = upcomingSessions
            };

            return Ok(dto);
        }

        // =====================================================================
        // PROFILE  (حسابي)
        // =====================================================================

        /// <summary>Get the current authenticated teacher's profile using their token.</summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            var id = CurrentUserId;
            if (id == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var dto = await BuildProfile(id);
            if (dto == null)
                return NotFound(new { message = "Teacher not found." });

            return Ok(dto);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateTeacherProfileDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = CurrentUserId;
            if (userId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var profile = await _context.TeacherProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                profile = new TeacherProfile { UserId = userId };
                _context.TeacherProfiles.Add(profile);
            }

            profile.Bio = dto.Bio;
            profile.Qualifications = dto.Qualifications;
            profile.HourlyRateQAR = dto.HourlyRateQAR;
            profile.Subjects = Join(dto.Subjects);
            profile.Languages = Join(dto.Languages);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully." });
        }

        private async Task<TeacherProfileResponseDto?> BuildProfile(int teacherId)
        {
            var teacher = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == TeacherRole);
            if (teacher == null)
                return null;

            var profile = await _context.TeacherProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == teacherId);

            var completedCount = await _context.Bookings
                .CountAsync(b => b.TeacherId == teacherId && b.Status == BookingStatus.Completed);

            return new TeacherProfileResponseDto
            {
                TeacherId = teacherId,
                Name = teacher.FullName,
                Bio = profile?.Bio,
                Qualifications = profile?.Qualifications,
                HourlyRateQAR = Convert.ToDecimal((object?)profile?.HourlyRateQAR ?? 0m),
                Subjects = Split(profile?.Subjects),
                Languages = Split(profile?.Languages),
                Rating = Math.Round(Convert.ToDouble((object?)profile?.Rating ?? 0.0), 2),
                YearsOfExperience = Convert.ToInt32((object?)profile?.YearsOfExperience ?? 0),
                CompletedSessions = completedCount
            };
        }

        // =====================================================================
        // COURSES  (/api/Teacher/courses) - what students browse and book
        // =====================================================================

        /// <summary>Create a new course (e.g. Science, English).</summary>
        [HttpPost("courses")]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var teacherId = CurrentUserId;
            if (teacherId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var title = dto.Title.Trim();

            var duplicate = await _context.Courses.AnyAsync(c =>
                c.TeacherId == teacherId && c.Title == title);
            if (duplicate)
                return Conflict(new { message = "You already have a course with this title." });

            var course = new Course
            {
                TeacherId = teacherId,
                Title = title,
                Subject = dto.Subject.Trim(),
                Description = Clean(dto.Description),
                Level = Clean(dto.Level),
                PricePerSessionQAR = dto.PricePerSessionQAR,
                DurationMinutes = dto.DurationMinutes,
                SessionMode = dto.SessionMode,
                IsActive = true,
                CreatedAt = QatarNow
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            var teacherName = await GetTeacherNameAsync(teacherId);

            return CreatedAtAction(nameof(GetCourse), new { id = course.Id },
                CourseResponseDto.From(course, teacherName));
        }

        /// <summary>List my courses. Optional: ?isActive=true|false</summary>
        [HttpGet("courses")]
        public async Task<IActionResult> GetMyCourses([FromQuery] bool? isActive)
        {
            var teacherId = CurrentUserId;

            var query = _context.Courses.AsNoTracking().Where(c => c.TeacherId == teacherId);
            if (isActive.HasValue)
                query = query.Where(c => c.IsActive == isActive.Value);

            var courses = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // All of these courses belong to the current teacher, so one lookup is enough.
            var teacherName = await GetTeacherNameAsync(teacherId);

            return Ok(courses.Select(c => CourseResponseDto.From(c, teacherName)));
        }

        [HttpGet("courses/{id:int}")]
        public async Task<IActionResult> GetCourse(int id)
        {
            var teacherId = CurrentUserId;

            var course = await _context.Courses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId);
            if (course == null)
                return NotFound(new { message = "Course not found." });

            var teacherName = await GetTeacherNameAsync(teacherId);

            return Ok(CourseResponseDto.From(course, teacherName));
        }

        /// <summary>Update a course. Set IsActive=false to hide it from students.</summary>
        [HttpPut("courses/{id:int}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var teacherId = CurrentUserId;

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId);
            if (course == null)
                return NotFound(new { message = "Course not found." });

            var title = dto.Title.Trim();

            var duplicate = await _context.Courses.AnyAsync(c =>
                c.TeacherId == teacherId && c.Id != id && c.Title == title);
            if (duplicate)
                return Conflict(new { message = "You already have a course with this title." });

            course.Title = title;
            course.Subject = dto.Subject.Trim();
            course.Description = Clean(dto.Description);
            course.Level = Clean(dto.Level);
            course.PricePerSessionQAR = dto.PricePerSessionQAR;
            course.DurationMinutes = dto.DurationMinutes;
            course.SessionMode = dto.SessionMode;
            course.IsActive = dto.IsActive;
            course.UpdatedAt = QatarNow;

            await _context.SaveChangesAsync();

            var teacherName = await GetTeacherNameAsync(teacherId);

            return Ok(CourseResponseDto.From(course, teacherName));
        }

        /// <summary>
        /// Soft delete: the course is deactivated (hidden from students) but kept,
        /// so existing bookings that reference it stay valid.
        /// </summary>
        [HttpDelete("courses/{id:int}")]
        public async Task<IActionResult> DeactivateCourse(int id)
        {
            var teacherId = CurrentUserId;

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId);
            if (course == null)
                return NotFound(new { message = "Course not found." });

            if (!course.IsActive)
                return Ok(new { message = "Course is already inactive." });

            course.IsActive = false;
            course.UpdatedAt = QatarNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Course deactivated." });
        }

        // =====================================================================
        // AVAILABILITY  (المواعيد)
        // =====================================================================

        [HttpPost("availability")]
        public async Task<IActionResult> AddAvailability([FromBody] CreateAvailabilityDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var teacherId = CurrentUserId;
            if (teacherId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            // Strict HH:mm parsing (TimeSpan.TryParse would accept "10" as 10 days, "1.10:00", etc.)
            if (!TimeSpan.TryParseExact(dto.StartTime, TimeFormats, null, out var start) ||
                !TimeSpan.TryParseExact(dto.EndTime, TimeFormats, null, out var end))
                return BadRequest(new { message = "StartTime and EndTime must be in HH:mm format." });

            if (end <= start)
                return BadRequest(new { message = "EndTime must be after StartTime." });

            if (dto.IsRecurring)
            {
                if (dto.DayOfWeek == null)
                    return BadRequest(new { message = "DayOfWeek is required for recurring slots." });
            }
            else
            {
                if (dto.Date == null)
                    return BadRequest(new { message = "Date is required for one-off slots." });
                if (dto.Date.Value.Date < QatarNow.Date)
                    return BadRequest(new { message = "Date cannot be in the past." });
            }

            DateTime? date = dto.IsRecurring ? null : dto.Date!.Value.Date;
            DayOfWeek? day = dto.IsRecurring ? dto.DayOfWeek : null;

            // Reject overlapping slots of the same kind on the same day/date
            var overlaps = await _context.TeacherAvailabilities.AnyAsync(a =>
                a.TeacherId == teacherId &&
                a.IsRecurring == dto.IsRecurring &&
                a.DayOfWeek == day &&
                a.Date == date &&
                a.StartTime < end && start < a.EndTime);

            if (overlaps)
                return Conflict(new { message = "This slot overlaps with an existing one." });

            var slot = new TeacherAvailability
            {
                TeacherId = teacherId,
                IsRecurring = dto.IsRecurring,
                DayOfWeek = day,
                Date = date,
                StartTime = start,
                EndTime = end
            };

            _context.TeacherAvailabilities.Add(slot);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAvailability), null, ToDto(slot, false));
        }

        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability()
        {
            var teacherId = CurrentUserId;
            var today = QatarNow.Date;

            var slots = await _context.TeacherAvailabilities
                .AsNoTracking()
                .Where(a => a.TeacherId == teacherId && (a.IsRecurring || a.Date >= today))
                .OrderBy(a => a.IsRecurring).ThenBy(a => a.Date).ThenBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
                .ToListAsync();

            var activeDates = await ActiveBookingDates(teacherId);

            return Ok(slots.Select(s => ToDto(s, activeDates.Any(d => SlotCovers(s, d)))));
        }

        [HttpDelete("availability/{id:int}")]
        public async Task<IActionResult> DeleteAvailability(int id)
        {
            var teacherId = CurrentUserId;

            var slot = await _context.TeacherAvailabilities
                .FirstOrDefaultAsync(a => a.Id == id && a.TeacherId == teacherId);
            if (slot == null)
                return NotFound(new { message = "Slot not found." });

            var activeDates = await ActiveBookingDates(teacherId);
            if (activeDates.Any(d => SlotCovers(slot, d)))
                return BadRequest(new { message = "Cannot delete a slot that has a pending or confirmed booking." });

            _context.TeacherAvailabilities.Remove(slot);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>Session dates of this teacher's upcoming Pending/Confirmed bookings.</summary>
        private async Task<List<DateTime>> ActiveBookingDates(int teacherId)
        {
            var now = QatarNow;
            return await _context.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == teacherId
                            && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                            && b.SessionDate >= now)
                .Select(b => b.SessionDate)
                .ToListAsync();
        }

        private static bool SlotCovers(TeacherAvailability s, DateTime sessionDate)
        {
            var t = sessionDate.TimeOfDay;
            if (t < s.StartTime || t >= s.EndTime) return false;

            return s.IsRecurring
                ? sessionDate.DayOfWeek == s.DayOfWeek
                : s.Date.HasValue && sessionDate.Date == s.Date.Value.Date;
        }

        private static AvailabilityResponseDto ToDto(TeacherAvailability a, bool isBooked) => new()
        {
            Id = a.Id,
            IsRecurring = a.IsRecurring,
            DayOfWeek = a.DayOfWeek,
            Date = a.Date,
            StartTime = a.StartTime.ToString(@"hh\:mm"),
            EndTime = a.EndTime.ToString(@"hh\:mm"),
            IsBooked = isBooked
        };

        // =====================================================================
        // BOOKINGS  (الحجوزات)
        // =====================================================================

        private IQueryable<TeacherBookingItemDto> ProjectBookings(IQueryable<Booking> query) =>
            query.Select(b => new TeacherBookingItemDto
            {
                Id = b.Id,
                StudentId = b.StudentId,
                StudentName = _context.Users.Where(u => u.Id == b.StudentId)
                                            .Select(u => u.FullName).FirstOrDefault(),
                CourseId = b.CourseId,
                Subject = b.Subject,
                Mode = b.SessionMode,
                SessionDate = b.SessionDate,
                TotalPriceQAR = b.TotalPriceQAR,
                Status = b.Status
            });

        /// <summary>
        /// All my bookings, newest first.
        /// Optional: ?status=Pending|Confirmed|Completed|Cancelled &amp;page=1&amp;pageSize=50
        /// </summary>
        [HttpGet("bookings")]
        public async Task<IActionResult> GetBookings(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var teacherId = CurrentUserId;

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Bookings.AsNoTracking().Where(b => b.TeacherId == teacherId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var match = BookingStatus.All.FirstOrDefault(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return BadRequest(new { message = "Status must be Pending, Confirmed, Completed or Cancelled." });
                query = query.Where(b => b.Status == match);
            }

            var bookings = await ProjectBookings(
                    query.OrderByDescending(b => b.SessionDate)
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize))
                .ToListAsync();

            return Ok(bookings);
        }

        /// <summary>Counts per status, for the tab badges in the bookings screen.</summary>
        [HttpGet("bookings/counts")]
        public async Task<IActionResult> GetBookingCounts()
        {
            var teacherId = CurrentUserId;

            var groups = await _context.Bookings.AsNoTracking()
                .Where(b => b.TeacherId == teacherId)
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int Count(string s) => groups.FirstOrDefault(g => g.Status == s)?.Count ?? 0;

            return Ok(new TeacherBookingCountsDto
            {
                Pending = Count(BookingStatus.Pending),
                Confirmed = Count(BookingStatus.Confirmed),
                Completed = Count(BookingStatus.Completed),
                Cancelled = Count(BookingStatus.Cancelled)
            });
        }

        /// <summary>Confirmed sessions that haven't happened yet, soonest first ("عرض الكل" on the home screen).</summary>
        [HttpGet("bookings/upcoming")]
        public async Task<IActionResult> GetUpcomingBookings(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var teacherId = CurrentUserId;
            var now = QatarNow;

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Bookings.AsNoTracking()
                .Where(b => b.TeacherId == teacherId &&
                            b.Status == BookingStatus.Confirmed &&
                            b.SessionDate >= now)
                .OrderBy(b => b.SessionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return Ok(await ProjectBookings(query).ToListAsync());
        }

        [HttpGet("bookings/{id:int}")]
        public async Task<IActionResult> GetBooking(int id)
        {
            var teacherId = CurrentUserId;

            var booking = await ProjectBookings(
                    _context.Bookings.AsNoTracking().Where(b => b.Id == id && b.TeacherId == teacherId))
                .FirstOrDefaultAsync();
            if (booking == null)
                return NotFound(new { message = "Booking not found." });

            return Ok(booking);
        }

        /// <summary>Accept (Accept = true) or reject (Accept = false) a pending request ("عرض الطلبات").</summary>
        [HttpPut("bookings/{id:int}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Request body is required." });

            var teacherId = CurrentUserId;
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.TeacherId == teacherId);
            if (booking == null)
                return NotFound(new { message = "Booking not found." });

            if (booking.Status != BookingStatus.Pending)
                return BadRequest(new { message = $"Only pending bookings can be accepted or rejected (current: {booking.Status})." });

            if (dto.Accept)
            {
                if (booking.SessionDate <= QatarNow)
                    return BadRequest(new { message = "Cannot accept a booking whose session time has already passed." });

                // Don't allow two confirmed sessions that overlap in time
                var from = booking.SessionDate.AddMinutes(-SessionLengthMinutes);
                var to = booking.SessionDate.AddMinutes(SessionLengthMinutes);

                var conflict = await _context.Bookings.AnyAsync(b =>
                    b.TeacherId == teacherId &&
                    b.Id != booking.Id &&
                    b.Status == BookingStatus.Confirmed &&
                    b.SessionDate > from && b.SessionDate < to);

                if (conflict)
                    return Conflict(new { message = "You already have a confirmed session at this time." });
            }

            booking.Status = dto.Accept ? BookingStatus.Confirmed : BookingStatus.Cancelled;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "This booking was modified by someone else. Please refresh and try again." });
            }

            return Ok(new { message = $"Booking {booking.Status.ToLower()}.", status = booking.Status });
        }

        [HttpPut("bookings/{id:int}/complete")]
        public async Task<IActionResult> CompleteBooking(int id)
        {
            var teacherId = CurrentUserId;
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.TeacherId == teacherId);
            if (booking == null)
                return NotFound(new { message = "Booking not found." });

            if (booking.Status != BookingStatus.Confirmed)
                return BadRequest(new { message = "Only confirmed bookings can be marked as completed." });

            // Session must have actually finished, not just started.
            if (booking.SessionDate.AddMinutes(SessionLengthMinutes) > QatarNow)
                return BadRequest(new { message = "Session has not finished yet." });

            booking.Status = BookingStatus.Completed;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "This booking was modified by someone else. Please refresh and try again." });
            }

            return Ok(new { message = "Session marked as completed.", status = booking.Status });
        }
    }
}