using CrystalGroupHome.SharedRCL.Data;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Data
{
    public class FirstTimeYield_AreaFailureReasonDTO
    {
        [TableColumn("FR", "Id")]
        public int FailureId { get; set; }
        [TableColumn("FR")]
        public string FailureDescription { get; set; } = string.Empty;
        [TableColumn("FR", "Deleted")]
        public bool FailureDeleted { get; set; }
        [TableColumn("FR")]
        public string EntryUser { get; set; } = string.Empty;
        [TableColumn("FR")]
        public DateTime EntryDate { get; set; }
        [TableColumn("FR")]
        public string LastModifiedUser { get; set; } = string.Empty;
        [TableColumn("FR")]
        public DateTime LastModifiedDate { get; set; }
        [TableColumn("AFR")]
        public int AreaId { get; set; }
        [TableColumn("A")]
        public string AreaDescription { get; set; } = string.Empty;
        [TableColumn("A", "Deleted")]
        public bool AreaDeleted { get; set; }
    }
}
