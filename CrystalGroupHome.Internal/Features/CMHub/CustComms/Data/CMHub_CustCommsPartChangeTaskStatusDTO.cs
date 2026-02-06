using CrystalGroupHome.SharedRCL.Data;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Data
{
    public class CMHub_CustCommsPartChangeTaskStatusDTO
    {
        public int Id { get; set; }

        [TableColumn("Desc")]
        public string Description { get; set; } = string.Empty;

        public bool Deleted { get; set; }

        public string Code { get; set; } = string.Empty;

        public int Sequence { get; set; }
    }
}
