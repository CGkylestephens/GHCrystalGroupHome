using CrystalGroupHome.SharedRCL.Data.Labor;

namespace CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms
{
    public class CMHub_VendorCommsTrackerLogModel
    {
        public int Id { get; set; }

        public int TrackerId { get; set; }

        public string? LogMessage { get; set; }

        public DateTime LogDate { get; set; }

        public string LoggedByUser { get; set; } = string.Empty;

        public bool Deleted { get; set; } = false;

        public bool ManualLogEntry { get; set; } = false;

        // Below are not part of the table data. Retrieved during load.
        public ADUserDTO_Base? Employee { get; set; } = null;
    }
}