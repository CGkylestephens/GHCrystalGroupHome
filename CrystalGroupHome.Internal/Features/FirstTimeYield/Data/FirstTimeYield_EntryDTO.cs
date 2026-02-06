namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Data
{
    public class FirstTimeYield_EntryDTO
    {
        public int Id { get; set; }
        public string JobNum { get; set; } = string.Empty;
        public string OpCode { get; set; } = string.Empty;
        public string OpCodeOperator { get; set; } = string.Empty; // Saved as the user's EmpID from EmpBasic table
        public int AreaId { get; set; }
        public int QtyTested { get; set; }
        public int QtyPassed { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string EntryUser { get; set; } = string.Empty; // Saved as the user's EmpID from EmpBasic table
        public DateTime EntryDate { get; set; }
        public string LastModifiedUser { get; set; } = string.Empty; // Saved as the user's EmpID from EmpBasic table
        public DateTime LastModifiedDate { get; set; }
        public bool Deleted { get; set; } = false;
    }
}
