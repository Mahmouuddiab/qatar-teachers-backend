using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QatarLearningBackend.Data;

namespace QatarLearningBackend.Controllers
{
    /// <summary>
    /// Teacher notifications (bell icon in the teacher app).
    /// GET  /api/Teacher/notifications
    /// GET  /api/Teacher/notifications/unread-count
    /// PUT  /api/Teacher/notifications/{id}/read
    /// PUT  /api/Teacher/notifications/read-all
    /// </summary>
    [Route("api/Teacher/notifications")]
    [Authorize(Roles = "Teacher")]
    public class TeacherNotificationsController : NotificationsControllerBase
    {
        public TeacherNotificationsController(AppDbContext context) : base(context)
        {
        }
    }
}