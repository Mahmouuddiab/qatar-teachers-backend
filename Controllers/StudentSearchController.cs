using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using QatarLearningBackend.DTOs;

namespace QatarLearningBackend.Controllers
{
    /// <summary>
    /// Student search box. One keyword searches active courses by
    /// course title, subject and teacher name.
    /// GET /api/Student/courses/search?keyword=math&amp;page=1&amp;pageSize=20
    /// </summary>
    [Route("api/Student/courses/search")]
    [ApiController]
    [AllowAnonymous] // same as browsing courses: no login needed
    public class StudentSearchController : ControllerBase
    {
        private const string TeacherRole = "Teacher";
        private const int MaxPageSize = 100;
        private const int MaxKeywords = 5;
        private const int MaxKeywordLength = 100;

        private readonly AppDbContext _context;

        public StudentSearchController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Search active courses. Every word of the keyword must match the course title,
        /// the subject or the teacher's name, so "math ahmed" finds Ahmed's math courses.
        /// Same response shape as GET /api/Student/courses.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchCourses(
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(new { message = "Search keyword is required." });

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var words = keyword.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(w => w.Length > MaxKeywordLength ? w[..MaxKeywordLength] : w)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxKeywords)
                .ToList();

            var query = _context.Courses.AsNoTracking().Where(c => c.IsActive);

            foreach (var word in words)
            {
                var w = word; // captured per word

                query = query.Where(c =>
                    c.Title.Contains(w) ||
                    c.Subject.Contains(w) ||
                    _context.Users.Any(u => u.Id == c.TeacherId &&
                                            u.Role == TeacherRole &&
                                            u.FullName.Contains(w)));
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
    }
}