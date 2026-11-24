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
    /// Push notification device tokens, shared by the teacher and student apps.
    /// POST /api/notifications/device-token
    /// </summary>
    [Route("api/notifications")]
    [ApiController]
    [Authorize] // any logged-in user (Teacher or Student)
    public class DeviceTokensController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeviceTokensController(AppDbContext context)
        {
            _context = context;
        }

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

        /// <summary>
        /// Register this device's FCM token for the logged-in user.
        /// Safe to call on every app start / login / token refresh: if the token already
        /// exists it is updated (and moved to the current user if someone else was logged in
        /// on the same device before), otherwise it is created.
        /// </summary>
        [HttpPost("device-token")]
        public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = CurrentUserId;
            if (userId == 0)
                return Unauthorized(new { message = "Invalid token: user id claim is missing." });

            var token = dto.Token.Trim();
            var platform = dto.Platform.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Token is required." });

            if (!DevicePlatforms.IsValid(platform))
                return BadRequest(new { message = "Platform must be android or ios." });

            var now = QatarNow;

            var existing = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token);
            if (existing == null)
            {
                _context.DeviceTokens.Add(new DeviceToken
                {
                    UserId = userId,
                    Token = token,
                    Platform = platform,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.UserId = userId;
                existing.Platform = platform;
                existing.UpdatedAt = now;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two requests with the same new token at the same time (unique index).
                return Conflict(new { message = "Device token is being registered. Please try again." });
            }

            return Ok(new { message = "Device token registered." });
        }
    }
}