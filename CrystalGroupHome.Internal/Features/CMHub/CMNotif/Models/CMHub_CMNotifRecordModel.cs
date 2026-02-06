namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Models
{
    public class CMHub_CMNotifRecordModel
    {
        public int Id { get; set; } = 0;

        public string ECNNumber { get; set; } = string.Empty;

        public bool Deleted { get; set; } = false;

        public bool Completed { get; set; } = false;

        // Below are not part of the table data. UI related, calculated, or retrieved during load.

        /// <summary>
        /// Returns the date of the most recent log.
        /// </summary>
        public DateTime? LastUpdated
        {
            get
            {
                if (RecordedLogs != null && RecordedLogs.Count > 0)
                {
                    return RecordedLogs
                        .Where(log => !log.Deleted)
                        .OrderByDescending(log => log.LogDate)
                        .FirstOrDefault()?.LogDate ?? DateTime.MinValue;
                }
                return null;
            }
        }

        // Associated Models
        public List<CMHub_CMNotifRecordLogModel> RecordedLogs = [];
        public List<CMHub_CMNotifRecordPartModel> RecordedParts = [];
    }
}