namespace CrystalGroupHome.SharedRCL.Data.Parts
{
    public class PartActivityDTO
    {
        public string PartNum { get; set; } = string.Empty;
        public DateTime? LastQuoted { get; set; }
        public DateTime? LastSold { get; set; }
    }
}
