using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using QatarLearningBackend.Data;
using QatarLearningBackend.DTOs;
using QatarLearningBackend.Models;

namespace QatarLearningBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BookingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentIdClaim == null)
                return Unauthorized();

            int studentId = int.Parse(studentIdClaim);

            var booking = new Booking
            {
                StudentId = studentId,
                TeacherId = dto.TeacherId,
                Subject = dto.Subject,
                SessionDate = dto.SessionDate,
                TotalPriceQAR = dto.TotalPriceQAR,
                Status = "Pending"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking created successfully in QAR", bookingId = booking.Id });
        }

        [HttpGet("my-bookings")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userIdClaim == null || roleClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            var query = _context.Bookings
                .Include(b => b.Student)
                .Include(b => b.Teacher)
                .AsQueryable();

            if (roleClaim == "Student")
            {
                query = query.Where(b => b.StudentId == userId);
            }
            else if (roleClaim == "Teacher")
            {
                query = query.Where(b => b.TeacherId == userId);
            }

            var bookings = await query.Select(b => new
            {
                b.Id,
                b.Subject,
                b.SessionDate,
                b.Status,
                b.TotalPriceQAR,
                StudentName = b.Student!.FullName,
                TeacherName = b.Teacher!.FullName,
                b.CreatedAt
            }).ToListAsync();

            return Ok(bookings);
        }
    }
}
