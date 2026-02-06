using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.SharedRCL.Data.Labor;
using static CrystalGroupHome.Internal.Features.CMHub.CMNotif.Components.CMHub_CMNotifPDFFormModal;

namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Models
{
    public class CMHub_CMNotifRecordPartModel
    {
        public int Id { get; set; }

        public bool Deleted { get; set; } = false;

        // Relations for the Part
        public int RecordId { get; set; }

        public string PartNum { get; set; } = string.Empty;

        public CMHub_CMDexPartModel? CMDexPart { get; set; } = null;

        // Details of the Change
        public DateTime? DateCreated { get; set; } = null;

        public string AffectedPartNum { get; set; } = string.Empty;

        public string ReplacementPartNum { get; set; } = string.Empty;

        public string ECNChangeDetail { get; set; } = string.Empty;

        public DateTime? EffectiveDate { get; set; } = null;

        public bool PriceEffect { get; set; } = false;

        public bool IsApproved { get; set; } = false;

        public string ApprovedByEmpId { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public PDFFormType PDFFormType { get; set; } = PDFFormType.ConfigurationManagementChangeNotification;

        // Details of the Change Notification
        public bool IsNotifSent { get; set; } = false;

        public DateTime? DateNotifSent { get; set; } = null;

        public bool IsConfirmSent { get; set; } = false;

        public DateTime? DateConfirmSent { get; set; } = null;

        public bool HasCustAcceptance { get; set; } = false;

        public bool HasCustAcceptanceOverride { get; set; } = false;

        public DateTime? DateCustAccepted { get; set; } = null;

        // Below are not part of the table data. Retrieved during load.
        public ADUserDTO_Base? ApprovedByEmployee { get; set; } = null;
        public bool MatchedToECNPart { get; set; } = false;
    }
}