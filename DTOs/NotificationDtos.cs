namespace QatarLearningBackend.DTOs
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        /// <summary>BookingRequested, BookingCancelled, BookingConfirmed, BookingRejected, SessionCompleted.</summary>
        public string Type { get; set; } = string.Empty;

        public int? BookingId { get; set; }
        public bool IsRead { get; set; }

        /// <summary>Qatar local time.</summary>
        public DateTime CreatedAt { get; set; }
    }
}