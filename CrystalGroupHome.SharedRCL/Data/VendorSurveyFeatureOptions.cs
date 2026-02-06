using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.SharedRCL.Data
{
    /// <summary>
    /// Configuration options for controlling vendor survey feature availability
    /// </summary>
    public class VendorSurveyFeatureOptions
    {
        /// <summary>
        /// Controls whether vendor surveys can be created and sent in the current environment
        /// </summary>
        [Required]
        public bool EnableSurveySending { get; set; } = true;

        /// <summary>
        /// Optional message to display when survey sending is disabled
        /// </summary>
        public string? DisabledMessage { get; set; }
    }
}
