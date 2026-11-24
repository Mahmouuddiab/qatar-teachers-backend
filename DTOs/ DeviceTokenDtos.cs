using System.ComponentModel.DataAnnotations;

namespace QatarLearningBackend.DTOs
{
    public class RegisterDeviceTokenDto
    {
        /// <summary>The FCM registration token of this device.</summary>
        [Required, MaxLength(512)]
        public string Token { get; set; } = string.Empty;

        /// <summary>"android" or "ios".</summary>
        [Required, MaxLength(20)]
        public string Platform { get; set; } = string.Empty;
    }
}