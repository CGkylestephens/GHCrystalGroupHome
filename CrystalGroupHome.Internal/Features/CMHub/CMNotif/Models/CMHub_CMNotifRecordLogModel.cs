using CrystalGroupHome.SharedRCL.Data.Labor;

namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Models
{
    public class CMHub_CMNotifRecordLogModel
    {
        public int Id { get; set; }

        public bool Deleted { get; set; } = false;

        // Relations for the log
        public int RecordId { get; set; }

        // This field is optional. If it has no value the log is associated with the parent record.
        // If it has a value the log is associated with one of the CMHub_CMNotifRecordPartModels by PartNum.
        public string? LogAssociatedWithPartNum { get; set; } = null;

        // Details of the Log
        public string? LogMessage { get; set; } = null;
        // Things to log in the LogMessage (plain text)
        //
        // When sent:
        //  - Customer Contact Name (sent to)
        //  - Customer Contact Email (sent to)
        //  - PM Name (sender)
        //  - PM Email (sender)
        // 
        // When status changes?
        // 
        // When new Parts are added to a record?

        // The LogFileLocation is separate from the LogMessage so that we can make it a link to the file.
        // This would only be for log entries where a notification was sent so we can include the actual pdf.
        public string? LogFileLocation { get; set; } = null;

        public DateTime LogDate { get; set; } = DateTime.UtcNow;

        public string LoggedByEmpId { get; set; } = string.Empty;

        public bool IsManualLogEntry { get; set; } = false;

        // Below are not part of the table data. Retrieved during load.
        public ADUserDTO_Base? LoggedByEmployee { get; set; } = null;
    }
}