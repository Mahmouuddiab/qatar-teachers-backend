using System.Security.Claims;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Data;

namespace QatarLearningBackend.Controllers;

[ApiController]
[Route("api/debug")]
[Authorize]
public class PushDebugController : ControllerBase
{
    private readonly AppDbContext _context;

    public PushDebugController(AppDbContext context)
    {
        _context = context;
    }

    // TEMPORARY: delete this controller after debugging.
    [HttpPost("test-push")]
    public async Task<IActionResult> TestPush()
    {
        var userIdText =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value;

        if (!int.TryParse(userIdText, out var userId))
            return BadRequest("No user id in JWT.");

        var firebaseReady = FirebaseApp.DefaultInstance != null;

        var tokens = await _context.DeviceTokens
            .Where(d => d.UserId == userId)
            .Select(d => d.Token)
            .ToListAsync();

        var results = new List<string>();

        if (firebaseReady)
        {
            foreach (var token in tokens)
            {
                try
                {
                    var id = await FirebaseMessaging.DefaultInstance.SendAsync(
                        new Message
                        {
                            Token = token,
                            Notification = new Notification
                            {
                                Title = "Backend test",
                                Body = "Push from the server works"
                            },
                            Android = new AndroidConfig
                            {
                                Priority = Priority.High,
                                Notification = new AndroidNotification
                                {
                                    ChannelId = "high_importance_channel",
                                    Sound = "default"
                                }
                            }
                        });

                    results.Add($"{token[..12]}... OK {id}");
                }
                catch (FirebaseMessagingException ex)
                {
                    results.Add(
                        $"{token[..Math.Min(12, token.Length)]}... " +
                        $"FAILED {ex.MessagingErrorCode}: {ex.Message}");
                }
            }
        }

        return Ok(new
        {
            userId,
            firebaseInitialized = firebaseReady,
            tokenCount = tokens.Count,
            results
        });
    }
}