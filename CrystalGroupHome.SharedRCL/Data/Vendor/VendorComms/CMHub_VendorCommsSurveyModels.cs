using static CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms.CMHub_VendorCommsSurveyDTOs;

namespace CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms
{
    public class CMHub_VendorCommsSurveyModels
    {
        public class CMHub_SurveyBatchCreateModel
        {
            public int VendorNum { get; set; }
            public string VendorName { get; set; } = string.Empty;
            public List<int> SelectedTrackerIds { get; set; } = new();
            public int? TemplateVersionId { get; set; }
            public DateTime? ResponseDueDate { get; set; }
        }

        public class CMHub_SurveyBatchViewModel
        {
            public CMHub_SurveyBatchDTO Batch { get; set; } = new();
            public CMHub_SurveyTemplateVersionDTO? TemplateVersion { get; set; }
            public CMHub_SurveyTemplateDTO? Template { get; set; }
            public List<CMHub_VendorCommsTrackerModel> Parts { get; set; } = new();
            public VendorDTO_Base? Vendor { get; set; }
            public string? SurveyLink { get; set; }
        }
    }
}
