using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.SharedRCL.Data
{
    public class DatabaseOptions
    {
        [Required]
        public string CgiConnection { get; set; } = string.Empty;
        [Required]
        public string CGIExtConnection { get; set; } = string.Empty;
        [Required]
        public string KineticErpConnection { get; set; } = string.Empty;
    }
}