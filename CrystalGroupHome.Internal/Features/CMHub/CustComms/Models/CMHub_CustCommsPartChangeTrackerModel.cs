using CrystalGroupHome.SharedRCL.Data.Parts;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Models
{
    public class CMHub_CustCommsPartChangeTrackerModel
    {
        public int Id { get; set; } = 0;
        public string PartNum { get; set; } = string.Empty;
        public string PartDesc { get; set; } = string.Empty;
        public string PartRev { get; set; } = string.Empty;
        public bool Deleted { get; set; } = false;
        public bool IsSelected = false;

        public List<CMHub_CustCommsPartChangeTaskLogModel> TrackerLogs { get; set; } = [];

        public CMHub_CustCommsPartChangeTaskModel? TechServicesTask { get; set; }
        public int? TechServicesLTBQuantity { get; set; }

        // Compute LastUpdated based on tasks.
        public DateTime LastUpdated
        {
            get
            {
                var taskDates = CMPartTasks
                    .Concat(WhereUsedPartTasks)
                    .Select(task => task.LastUpdated);

                if (TechServicesTask != null)
                {
                    taskDates = taskDates.Append(TechServicesTask.LastUpdated);
                }

                var logDates = TrackerLogs
                    .Where(log => !log.Deleted)
                    .Select(log => log.LogDate);

                return taskDates
                    .Concat(logDates)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();
            }
        }

        public PartEoltDTO PartEolt { get; set; } = new();
        public DateTime? LastTimeBuyDate => PartEolt.LastTimeBuyDate;
        public List<CMHub_CustCommsPartChangeTaskModel> CMPartTasks { get; set; } = [];
        public List<CMHub_CustCommsPartChangeTaskModel> WhereUsedPartTasks { get; set; } = [];

        /// <summary>
        /// Total number of tasks including CM Part Tasks, Where Used Tasks (excluding hidden status), 
        /// and Tech Services Task (if exists and not deleted).
        /// </summary>
        public int TotalTasks
        {
            get
            {
                var count = CMPartTasks.Count + WhereUsedPartTasks.Where(_ => _.StatusId != 7).Count();
                
                // Include Tech Services task if it exists and is not deleted
                if (TechServicesTask != null && !TechServicesTask.Deleted)
                {
                    count++;
                }
                
                return count;
            }
        }

        /// <summary>
        /// Number of completed tasks including CM Part Tasks, Where Used Tasks, 
        /// and Tech Services Task (completed when status is TS_CONFIRMED).
        /// </summary>
        public int CompletedTasks
        {
            get
            {
                var count = CMPartTasks.Where(_ => _.Completed).Count() 
                          + WhereUsedPartTasks.Where(_ => _.Completed).Count();
                
                // Include Tech Services task if it's complete (TS_CONFIRMED status)
                if (TechServicesTask != null && !TechServicesTask.Deleted && TechServicesTask.IsTaskComplete)
                {
                    count++;
                }
                
                return count;
            }
        }
    }
}
