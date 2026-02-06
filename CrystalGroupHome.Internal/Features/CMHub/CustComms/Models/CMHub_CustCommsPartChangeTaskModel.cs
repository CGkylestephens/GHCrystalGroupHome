using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.SharedRCL.Data.Parts;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Models
{
    public class CMHub_CustCommsPartChangeTaskModel
    {
        public int Id { get; set; } = 0;
        public int TrackerId { get; set; } = 0;
        public string TrackerPartNum { get; set; } = string.Empty;
        public string ImpactedPartNum { get; set; } = string.Empty;
        public string ImpactedPartDesc { get; set; } = string.Empty;
        public string ImpactedPartRev { get; set; } = string.Empty;
        public int? StatusId { get; set; }
        public bool Completed { get; set; } = false;
        public bool Deleted { get; set; } = false;
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        public List<CMHub_CustCommsPartChangeTaskLogModel> Logs = [];
        public string ECNNumber { get; set; } = string.Empty;
        public int Type { get; set; } = 0; // 1 = CM Part Task, 2 = Where Used Part Task, 3 = Tech Services Task

        // Additional Part info
        public int Lvl { get; set; }
        public decimal QtyPer { get; set; }
        public string Sort { get; set; } = string.Empty;
        public List<PartIndentedWhereUsedDTO> LinkedHigherLevelParts = [];

        // CM Dex info (Only if this is a part change task for a CM part)
        public CMHub_CMDexPartModel? CMDexPart { get; set; }
        public string PrimaryPMName => CMDexPart?.PrimaryPMName ?? "";
        public string StatusDescription { get; set; } = "";
        public string StatusCode { get; set; } = "";

        /// <summary>
        /// Determines if this task is considered complete.
        /// For Tech Services tasks (Type 3), completion is determined by TS_CONFIRMED status.
        /// For other tasks, the Completed property is used.
        /// </summary>
        public bool IsTaskComplete => Type == 3 
            ? StatusCode.Equals("TS_CONFIRMED", StringComparison.OrdinalIgnoreCase)
            : Completed;
    }
}