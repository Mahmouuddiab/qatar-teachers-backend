using System.ComponentModel.DataAnnotations;

namespace QatarLearningBackend.DTOs
{
    public class BookCourseDto
    {
        [Required]
        public int CourseId { get; set; }

        /// <summary>Session start, Qatar local time (e.g. 2026-11-02T16:00:00).</summary>
        [Required]
        public DateTime SessionDate { get; set; }

        /// <summary>"Online" or "InPerson". Required only when the course's SessionMode is "Both".</summary>
        public string? Mode { get; set; }
    }

    public class StudentBookingDto
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public int? CourseId { get; set; }
        public string? Mode { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public decimal TotalPriceQAR { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}