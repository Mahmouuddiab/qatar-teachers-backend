using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QatarLearningBackend.Models
{
    /// <summary>
    /// An FCM registration token of one device. A user can have several devices,
    /// but a token belongs to exactly one user at a time (unique).
    /// </summary>
    public class DeviceToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        /// <summary>The FCM registration token sent by the Flutter app.</summary>
        [Required, MaxLength(512)]
        public string Token { get; set; } = string.Empty;

        /// <summary>"android" or "ios".</summary>
        [Required, MaxLength(20)]
        public string Platform { get; set; } = string.Empty;

        /// <summary>Qatar local time (UTC+3), same convention as the rest of the API.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(3);

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.AddHours(3);
    }

    public static class DevicePlatforms
    {
        public const string Android = "android";
        public const string Ios = "ios";

        public static bool IsValid(string? platform) =>
            platform == Android || platform == Ios;
    }
}