namespace QatarLearningBackend.Models
{
    /// <summary>How a session is delivered. A course can offer one mode or Both.</summary>
    public static class SessionModes
    {
        public const string Online = "Online";       // أونلاين
        public const string InPerson = "InPerson";   // حضوري
        public const string Both = "Both";           // course only: student chooses at booking time

        /// <summary>Parses a concrete booking mode (Online or InPerson), case-insensitive.</summary>
        public static bool TryParseBookingMode(string? value, out string mode)
        {
            if (string.Equals(value?.Trim(), Online, StringComparison.OrdinalIgnoreCase))
            {
                mode = Online;
                return true;
            }

            if (string.Equals(value?.Trim(), InPerson, StringComparison.OrdinalIgnoreCase))
            {
                mode = InPerson;
                return true;
            }

            mode = string.Empty;
            return false;
        }
    }
}