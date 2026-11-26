public class StudentBookingDetailsDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Mode { get; set; }
    public string Subject { get; set; } = string.Empty;

    public DateTime SessionDate { get; set; }
    public DateTime SessionEndDate { get; set; }
    public int DurationMinutes { get; set; }

    public decimal TotalPriceQAR { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool CanCancel { get; set; }

    public BookingTeacherDto Teacher { get; set; } = new();
    public BookingCourseDto? Course { get; set; }
}

public class BookingTeacherDto
{
    public int TeacherId { get; set; }
    public string? Name { get; set; }
    public string? Bio { get; set; }
    public string? Qualifications { get; set; }
    public List<string> Subjects { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public double Rating { get; set; }
    public int YearsOfExperience { get; set; }
}

public class BookingCourseDto
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Level { get; set; }
    public decimal PricePerSessionQAR { get; set; }
    public int DurationMinutes { get; set; }
    public string SessionMode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}