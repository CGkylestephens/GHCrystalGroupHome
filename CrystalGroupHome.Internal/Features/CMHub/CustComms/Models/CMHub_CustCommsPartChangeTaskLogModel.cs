using CrystalGroupHome.SharedRCL.Data.Labor;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Models
{
    public class CMHub_CustCommsPartChangeTaskLogModel
    {
        public int Id { get; set; }

        public int? TaskId { get; set; }

        public string? LogMessage { get; set; }

        public DateTime LogDate { get; set; }

        public string LoggedByUser { get; set; } = string.Empty;

        public bool Deleted { get; set; } = false;

        public bool ManualLogEntry { get; set; } = false;

        public int TrackerId { get; set; }

        // Below are not part of the table data. Retrieved during load.
        public ADUserDTO_Base? Employee { get; set; } = null;

        public string? ImpactedPartNum { get; set; }

        public string? ImpactedPartRev { get; set; }
    }
}