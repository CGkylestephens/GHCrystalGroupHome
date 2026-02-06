namespace CrystalGroupHome.External.Features.VendorSurvey.Models
{
    public class CMHub_VendorCommsSurveyResponseModel
    {
        public int Id { get; set; }
        public int PartTrackerId { get; set; }
        public int QuestionId { get; set; }
        public string ResponseValue { get; set; } = string.Empty;
    }
}