using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QatarLearningBackend.Data;

namespace QatarLearningBackend.Controllers
{
    /// <summary>
    /// Student notifications (bell icon in the student app).
    /// GET  /api/Student/notifications
    /// GET  /api/Student/notifications/unread-count
    /// PUT  /api/Student/notifications/{id}/read
    /// PUT  /api/Student/notifications/read-all
    /// </summary>
    [Route("api/Student/notifications")]
    [Authorize(Roles = "Student")]
    public class StudentNotificationsController : NotificationsControllerBase
    {
        public StudentNotificationsController(AppDbContext context) : base(context)
        {
        }
    }
}