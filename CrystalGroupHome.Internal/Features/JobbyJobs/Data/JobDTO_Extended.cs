using CrystalGroupHome.Internal.Common.Data.Jobs;

namespace CrystalGroupHome.Internal.Features.JobbyJobs.Data
{
    public class JobDTO_Extended : JobHeadDTO_Base
    {
        public string RevisionNum { get; set; } = string.Empty;
        public string DrawNum { get; set; } = string.Empty;
        public string PartDescription { get; set; } = string.Empty;
    }
}
