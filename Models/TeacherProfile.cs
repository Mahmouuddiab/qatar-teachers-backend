using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QatarLearningBackend.Models
{
    public class TeacherProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [MaxLength(500)]
        public string Bio { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Subjects { get; set; } = string.Empty; // e.g. Mathematics, Physics

        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRateQAR { get; set; } // in QAR

        public double Rating { get; set; } = 5.0;

        public int YearsOfExperience { get; set; } = 1;

        // NEW
        [MaxLength(1000)]
        public string? Qualifications { get; set; }

        [MaxLength(200)]
        public string? Languages { get; set; } // e.g. Arabic, English
    }
}