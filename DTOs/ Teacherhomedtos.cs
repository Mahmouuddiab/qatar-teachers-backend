namespace QatarLearningBackend.DTOs
{
    /// <summary>Everything the teacher home screen (الرئيسية) needs in one call.</summary>
    public class TeacherHomeDto
    {
        public int TeacherId { get; set; }

        /// <summary>"أهلاً بك أستاذ {TeacherName}"</summary>
        public string TeacherName { get; set; } = string.Empty;

        /// <summary>Banner: "لديك N طلبات حجز جديدة" (only requests that can still be accepted).</summary>
        public int PendingRequests { get; set; }

        /// <summary>إجمالي الأرباح</summary>
        public decimal TotalEarningsQAR { get; set; }

        /// <summary>حصة مكتملة</summary>
        public int CompletedClasses { get; set; }

        /// <summary>متوسط التقييم</summary>
        public double AverageRating { get; set; }

        /// <summary>حصص قادمة</summary>
        public int UpcomingClasses { get; set; }

        /// <summary>Preview list under "الحصص القادمة" (next few confirmed sessions).</summary>
        public List<UpcomingSessionDto> UpcomingSessions { get; set; } = new();
    }

    public class UpcomingSessionDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string? StudentName { get; set; }

        /// <summary>Course title, e.g. "رياضيات".</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>"Online" (أونلاين) or "InPerson" (حضوري). Null for old bookings.</summary>
        public string? Mode { get; set; }

        /// <summary>Qatar local time. The app shows "اليوم" / "غداً" and the time from this.</summary>
        public DateTime SessionDate { get; set; }
    }

    /// <summary>One row in the bookings tab (الحجوزات).</summary>
    public class TeacherBookingItemDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string? StudentName { get; set; }
        public int? CourseId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Mode { get; set; }
        public DateTime SessionDate { get; set; }
        public decimal TotalPriceQAR { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>Badge counts for the status tabs in the bookings screen.</summary>
    public class TeacherBookingCountsDto
    {
        public int Pending { get; set; }
        public int Confirmed { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
    }
}