namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Data
{
    public class FirstTimeYield_AreaDTO
    {
        public int Id { get; set; }
        public string AreaDescription { get; set; } = string.Empty;
        public bool Deleted { get; set; }

        public FirstTimeYield_AreaDTO() {
            Id = 12; // Default to "N/A"
            AreaDescription = "N/A";
            Deleted = false;
        }
    }
}
