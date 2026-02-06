namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Data
{
    public class FirstTimeYield_FailureReasonDTO
    {
        public int Id { get; set; }
        public string FailureDescription { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string EntryUser { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string LastModifiedUser { get; set; }
        public bool Deleted { get; set; }

        public FirstTimeYield_FailureReasonDTO()
        {
            Id = 11; // Default to "Other"
            FailureDescription = "Other";
            EntryDate = DateTime.Now;
            EntryUser = "N/A";
            LastModifiedDate = DateTime.Now;
            LastModifiedUser = "N/A";
            Deleted = false;
        }

        public FirstTimeYield_FailureReasonDTO(string empId)
        {
            Id = 11; // Default to "Other"
            FailureDescription = "Other";
            EntryDate = DateTime.Now;
            EntryUser = empId;
            LastModifiedDate = DateTime.Now;
            LastModifiedUser = empId;
            Deleted = false;
        }
    }
}