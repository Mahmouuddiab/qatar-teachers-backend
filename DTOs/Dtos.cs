namespace QatarLearningBackend.DTOs
{
    public record RegisterDto(
        string FullName,
        string Email,
        string PhoneNumber,
        string Password,
        string Role // "Student" or "Teacher"
    );

    public record LoginDto(
        string Email,
        string Password
    );

    public record AuthResponseDto(
        int Id,
        string FullName,
        string Email,
        string PhoneNumber,
        string Role,
        string Token
    );

    public record CreateBookingDto(
        int TeacherId,
        string Subject,
        DateTime SessionDate,
        decimal TotalPriceQAR
    );

    // NEW DTOs for Profile & Security
    public record UpdateProfileDto(
        string FullName,
        string PhoneNumber,
        string? Bio,
        string? Subjects,
        decimal? HourlyRateQAR
    );

    public record ChangePasswordDto(
        string CurrentPassword,
        string NewPassword
    );
}