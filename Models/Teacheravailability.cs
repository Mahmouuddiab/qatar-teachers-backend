namespace QatarLearningBackend.Models;

/// <summary>
/// A teacher's open time slot. Either a one-off slot (Date set) or a recurring
/// weekly slot (IsRecurring = true, DayOfWeek set).
/// </summary>
public class TeacherAvailability
{
    public int Id { get; set; }

    public int TeacherId { get; set; }          // User.Id of the teacher

    public bool IsRecurring { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }   // recurring slots
    public DateTime? Date { get; set; }         // one-off slots (date part only)

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}