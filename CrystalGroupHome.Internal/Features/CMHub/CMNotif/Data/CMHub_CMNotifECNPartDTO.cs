using CrystalGroupHome.SharedRCL.Data;

namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Data
{
    public class CMHub_CMNotifECNPartDTO
    {
        [TableColumn("EAA")]
        public int ECNHeaderId { get; set; }

        [TableColumn("EH")]
        public string ECNNumber { get; set; } = string.Empty;

        [TableColumn("EH")]
        public string ReasonForChange { get; set; } = string.Empty;

        [TableColumn("EH")]
        public int ECNStatusId { get; set; }

        [TableColumn("P")]
        public string PartNum { get; set; } = string.Empty;

        [TableColumn("P")]
        public string PartDescription { get; set; } = string.Empty;

        [TableColumn("FECPR", "RevisionNum")]
        public string CurrentRev { get; set; } = string.Empty;

        [TableColumn("EAA", "ParentRevisionNumber")]
        public string NewRev { get; set; } = string.Empty;

        [TableColumn("EB", "Name")]
        public string PrimaryPMName { get; set; } = string.Empty;
    }
}