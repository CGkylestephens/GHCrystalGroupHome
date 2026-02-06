using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms
{
    public class CMHub_VendorCommsSurveyDTOs
    {
        public class CMHub_SurveyTemplateDTO
        {
            public int Id { get; set; }
            [Required]
            [MaxLength(255)]
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        public class CMHub_SurveyTemplateVersionDTO
        {
            public int Id { get; set; }
            public int SurveyTemplateID { get; set; }
            public int VersionNumber { get; set; }
            public DateTime CreatedDate { get; set; }
            public bool IsActive { get; set; }
        }

        public class CMHub_SurveyQuestionDTO
        {
            public int Id { get; set; }
            public int SurveyTemplateVersionID { get; set; }
            [Required]
            public string QuestionText { get; set; } = string.Empty;
            [Required]
            [MaxLength(50)]
            public string QuestionType { get; set; } = string.Empty;
            public bool IsRequired { get; set; }
            public int DisplayOrder { get; set; }
            [MaxLength(100)]
            public string? MapsToField { get; set; }
            [MaxLength(50)]
            public string? FieldDataType { get; set; }
            public bool AutoUpdateOnResponse { get; set; }
        }

        public class CMHub_SurveyBatchDTO
        {
            public int Id { get; set; }
            public int VendorNum { get; set; }
            public int SurveyTemplateVersionID { get; set; }
            [MaxLength(50)]
            public string Status { get; set; } = "Draft"; // Draft, Sent, Closed
            public DateTime? SentDate { get; set; }
            public DateTime? ResponseDueDate { get; set; }
            public DateTime CreatedDate { get; set; }
            /// <summary>
            /// 6-digit confirmation code required for vendor to access the survey.
            /// Sent in the survey email for verification.
            /// </summary>
            [MaxLength(6)]
            public string? ConfirmationCode { get; set; }
        }

        public class CMHub_SurveyBatchPartDTO
        {
            public int Id { get; set; }
            public int SurveyBatchID { get; set; }
            public int PartStatusTrackerID { get; set; }
            public string SubmissionStatus { get; set; } = "Draft"; // Draft, Submitted
        }

        public class CMHub_SurveyResponseDTO
        {
            public int Id { get; set; }
            public int SurveyBatchPartID { get; set; }
            public int QuestionID { get; set; }
            [Required]
            public string ResponseValue { get; set; } = string.Empty;
            public DateTime ResponseReceivedDate { get; set; }
        }
    }
}