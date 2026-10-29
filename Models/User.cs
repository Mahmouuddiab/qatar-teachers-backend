using System.ComponentModel.DataAnnotations;

namespace QatarLearningBackend.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\+974[34567789]\d{7}$", ErrorMessage = "Phone number must be a valid Qatar number starting with +974")]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty; // e.g., +974XXXXXXXX

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "Student"; // "Student" or "Teacher"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
