using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;
using QatarLearningBackend.DTOs;

namespace QatarLearningBackend.Controllers
{
    /// <summary>
    /// Shared logic for the teacher and student notification controllers.
    /// Abstract, so it is not routed itself. TeacherNotificationsController and
    /// StudentNotificationsController add the route and the role.
    /// Every query is filtered by the user id from the JWT, so a user can only ever
    /// see or change their own notifications.
    /// </summary>
    [ApiController]
    public abstract class NotificationsControllerBase : ControllerBase
    {
        private const int MaxPageSize = 100;

        protected readonly AppDbContext _context;

        protected NotificationsControllerBase(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId
        {
            get
            {
                var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
                return int.TryParse(value, out var id) ? id : 0;
            }
        }

        /// <summary>My notifications, newest first. Optional: ?unreadOnly=true&amp;page=1&amp;pageSize=30</summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var userId = CurrentUserId;
            if (userId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);
            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var items = await query
                .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Type = n.Type,
                    BookingId = n.BookingId,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>Number of unread notifications (for the bell badge).</summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = CurrentUserId;
            if (userId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var count = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return Ok(new { count });
        }

        /// <summary>Mark one notification as read.</summary>
        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = CurrentUserId;
            if (userId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification == null)
                return NotFound(new { message = "Notification not found." });

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Notification marked as read." });
        }

        /// <summary>Mark all my notifications as read.</summary>
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = CurrentUserId;
            if (userId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            if (unread.Count > 0)
                await _context.SaveChangesAsync();

            return Ok(new { message = "All notifications marked as read.", updated = unread.Count });
        }
    }
}