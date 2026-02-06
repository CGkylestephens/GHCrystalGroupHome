namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Data
{
    public class CMHub_PartEmployeeDTO
    {
        public int Id { get; set; }
        public string PartNum { get; set; } = string.Empty;
        public string EmpID { get; set; } = string.Empty;
        public int Type { get; set; }
        public bool? IsPrimary { get; set; }
        public DateTime DateAdded { get; set; }
    }
}
