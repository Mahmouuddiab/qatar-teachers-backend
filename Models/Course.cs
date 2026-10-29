using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QatarLearningBackend.Models
{
    /// <summary>A course offered by a teacher (e.g. "Grade 9 Science", "IELTS English").</summary>
    public class Course
    {
        public int Id { get; set; }

        /// <summary>User Id of the teacher who owns this course.</summary>
        public int TeacherId { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Category used for filtering, e.g. "Science", "English", "Math".</summary>
        [Required, MaxLength(100)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>Free text, e.g. "Beginner", "Grade 9", "University".</summary>
        [MaxLength(50)]
        public string? Level { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PricePerSessionQAR { get; set; }

        public int DurationMinutes { get; set; } = 60;

        /// <summary>"Online", "InPerson" or "Both" (see SessionModes).</summary>
        [Required, MaxLength(20)]
        public string SessionMode { get; set; } = SessionModes.Online;

        /// <summary>Inactive courses are hidden from students but kept for booking history.</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}