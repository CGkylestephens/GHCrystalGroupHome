namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Data
{
    public class FirstTimeYield_FailureDTO
    {
        public int Id { get; set; }
        public int EntryId { get; set; }
        public int ReasonID { get; set; }
        public string ReasonDescriptionOther { get; set; } = string.Empty; // TODO: The intent of this was to allow the user to enter a custom Reason when the selectable options didn't fit the failure, but the UI doesn't support it yet.
        public int Qty { get; set; }
        public int AreaIdToBlame { get; set; }
        public string JobNumToBlame { get; set; } = string.Empty;
        public string OpCodeToBlame { get; set; } = string.Empty;
        public string OpCodeOperatorToBlame { get; set; } = string.Empty; // Saved as the user's EmpID from EmpBasic table
        public string EntryUser { get; set; } = string.Empty; // Saved as just their name (since it's custom editable). TODO: But maybe we need to change this so it's more reliably trackable with reporting?
        public DateTime EntryDate { get; set; }
        public bool Deleted { get; set; } = false;
    }
}
