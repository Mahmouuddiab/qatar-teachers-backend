using System.ComponentModel.DataAnnotations;

namespace QatarLearningBackend.DTOs
{
    // ----------------------------- Profile -----------------------------

    public class UpdateTeacherProfileDto
    {
        [StringLength(2000)]
        public string? Bio { get; set; }

        [StringLength(1000)]
        public string? Qualifications { get; set; }

        [Range(0, 100000)]
        public decimal HourlyRateQAR { get; set; }

        public List<string> Subjects { get; set; } = new();

        public List<string> Languages { get; set; } = new();
    }

    public class TeacherProfileResponseDto
    {
        public int TeacherId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Qualifications { get; set; }
        public decimal HourlyRateQAR { get; set; }
        public List<string> Subjects { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public double Rating { get; set; }
        public int YearsOfExperience { get; set; }
        public int CompletedSessions { get; set; }
    }

    // --------------------------- Availability --------------------------

    public class CreateAvailabilityDto
    {
        /// <summary>true = repeats weekly on DayOfWeek; false = one-off on Date.</summary>
        public bool IsRecurring { get; set; }

        /// <summary>Required when IsRecurring = true (0 = Sunday ... 6 = Saturday).</summary>
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>Required when IsRecurring = false.</summary>
        public DateTime? Date { get; set; }

        /// <summary>HH:mm, e.g. "16:00".</summary>
        [Required]
        public string StartTime { get; set; } = string.Empty;

        /// <summary>HH:mm, e.g. "18:00".</summary>
        [Required]
        public string EndTime { get; set; } = string.Empty;
    }

    public class AvailabilityResponseDto
    {
        public int Id { get; set; }
        public bool IsRecurring { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public DateTime? Date { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsBooked { get; set; }
    }

    // ----------------------------- Bookings ----------------------------

    public class UpdateBookingStatusDto
    {
        /// <summary>true = accept (Confirmed), false = reject (Cancelled).</summary>
        public bool Accept { get; set; }
    }
}