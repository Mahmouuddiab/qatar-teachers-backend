using System.ComponentModel.DataAnnotations;
using QatarLearningBackend.Models;

namespace QatarLearningBackend.DTOs
{
    public class CreateCourseDto
    {
        [Required, StringLength(150, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 2)]
        public string Subject { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Level { get; set; }

        [Range(1, 100000)]
        public decimal PricePerSessionQAR { get; set; }

        [Range(15, 240)]
        public int DurationMinutes { get; set; } = 60;

        /// <summary>Online, InPerson or Both.</summary>
        [Required]
        [RegularExpression("^(Online|InPerson|Both)$", ErrorMessage = "SessionMode must be Online, InPerson or Both.")]
        public string SessionMode { get; set; } = SessionModes.Online;
    }

    public class UpdateCourseDto : CreateCourseDto
    {
        public bool IsActive { get; set; } = true;
    }

    public class CourseResponseDto
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Level { get; set; }
        public decimal PricePerSessionQAR { get; set; }
        public int DurationMinutes { get; set; }
        public string SessionMode { get; set; } = SessionModes.Online;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public static CourseResponseDto From(Course c, string? teacherName = null) => new()
        {
            Id = c.Id,
            TeacherId = c.TeacherId,
            TeacherName = teacherName,
            Title = c.Title,
            Subject = c.Subject,
            Description = c.Description,
            Level = c.Level,
            PricePerSessionQAR = c.PricePerSessionQAR,
            DurationMinutes = c.DurationMinutes,
            SessionMode = c.SessionMode,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt
        };
    }
}