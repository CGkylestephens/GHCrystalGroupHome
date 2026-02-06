using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Models
{
    // Model for combined display data
    public class RMAFileAttachmentModel
    {
        public RMAFileAttachmentDTO FileAttachment { get; set; } = default!;
        public List<RMAFileAttachmentLogDTO> Logs { get; set; } = new();
    }

    // RMA history view
    public class RMAFileHistoryModel
    {
        public int LogId { get; set; }
        public int FileAttachmentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string CategoryDisplayValue { get; set; } = string.Empty;
        
        public string? SerialNumber { get; set; }
        public int? RMALineNumber { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? ActionDetails { get; set; }
        public string ActionByUsername { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
        public bool IsSystemAction { get; set; }
        public long FileSize { get; set; }
    }
}