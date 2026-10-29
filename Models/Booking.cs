using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QatarLearningBackend.Models
{
    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public User? Student { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public User? Teacher { get; set; }

        /// <summary>The course this booking was made for (null for older bookings).</summary>
        public int? CourseId { get; set; }

        /// <summary>Course title copied at booking time. Same max length as Course.Title.</summary>
        [Required]
        [MaxLength(150)]
        public string Subject { get; set; } = string.Empty;

        /// <summary>"Online" or "InPerson" (null for older bookings).</summary>
        [MaxLength(20)]
        public string? SessionMode { get; set; }

        public DateTime SessionDate { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPriceQAR { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}