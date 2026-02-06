namespace CrystalGroupHome.SharedRCL.Data.Parts
{
    public class PartIndentedWhereUsedDTO : PartDTO_Base
    {
        [TableColumn("I", "lvl")]
        public int Lvl { get; set; }

        [TableColumn("I", "QtyPer")]
        public decimal QtyPer { get; set; }

        [TableColumn("I", "sort")]
        public string Sort { get; set; } = string.Empty;

        [ExcludeFromTableColumn]
        public bool CmEligible { get; set; } = false;
    }
}
