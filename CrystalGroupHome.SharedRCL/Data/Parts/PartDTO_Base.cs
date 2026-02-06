namespace CrystalGroupHome.SharedRCL.Data.Parts
{
    public class PartDTO_Base
    {
        [TableColumn("P")]
        public string PartNum { get; set; } = string.Empty;

        [TableColumn("P")]
        public string PartDescription { get; set; } = string.Empty;

        // RevisionNum comes from dbo.PartRev, so we use a different alias.
        [TableColumn("PR")]
        public string RevisionNum { get; set; } = string.Empty;

        [TableColumn("P")]
        public bool InActive { get; set; }

        // UD Fields
        [TableColumn("P")]
        public bool Deprecated_c { get; set; }

        [TableColumn("P", "CM_CMManaged_c")]
        public bool CMManaged_c { get; set; } = false;

        [TableColumn("P", "CM_CMOriginationDate_c")]
        public DateTime? CMOrignationDate_c { get; set; } = null;
    }
}
