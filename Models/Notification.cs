using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QatarLearningBackend.Models
{
    /// <summary>In-app notification for a single user (a teacher or a student).</summary>
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The user who receives this notification.</summary>
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Body { get; set; } = string.Empty;

        /// <summary>See NotificationTypes. Lets the app pick an icon / screen to open.</summary>
        [Required, MaxLength(40)]
        public string Type { get; set; } = string.Empty;

        /// <summary>The booking this notification is about (null if not booking-related).</summary>
        public int? BookingId { get; set; }

        [ForeignKey("BookingId")]
        public Booking? Booking { get; set; }

        public bool IsRead { get; set; }

        /// <summary>Qatar local time (UTC+3), same convention as the rest of the API.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(3);
    }

    public static class NotificationTypes
    {
        // Sent to the teacher
        public const string BookingRequested = "BookingRequested";
        public const string BookingCancelled = "BookingCancelled";

        // Sent to the student
        public const string BookingConfirmed = "BookingConfirmed";
        public const string BookingRejected = "BookingRejected";
        public const string SessionCompleted = "SessionCompleted";
    }
}