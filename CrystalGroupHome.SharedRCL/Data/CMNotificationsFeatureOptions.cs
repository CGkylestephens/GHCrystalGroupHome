using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.SharedRCL.Data
{
    /// <summary>
    /// Configuration options for controlling CM Notifications feature availability.
    /// These settings only affect Production - in non-production environments, 
    /// emails always go to the test inbox regardless of these settings.
    /// </summary>
    public class CMNotificationsFeatureOptions
    {
        /// <summary>
        /// Controls whether CM Notification emails can be sent to real customers in Production.
        /// When false, emails are redirected to the test inbox with a note about the original recipients.
        /// In non-production environments, emails always go to test inbox regardless of this setting.
        /// </summary>
        [Required]
        public bool EnableNotificationSending { get; set; } = false;

        /// <summary>
        /// Optional message to display in the UI when notification sending to real customers is disabled
        /// </summary>
        public string? DisabledMessage { get; set; }
    }
}
