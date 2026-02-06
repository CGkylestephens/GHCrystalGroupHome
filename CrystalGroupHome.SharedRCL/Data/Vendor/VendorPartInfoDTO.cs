namespace CrystalGroupHome.SharedRCL.Data.Vendor
{
    public class VendorPartInfoDTO
    {
        public string Company { get; set; } = string.Empty;
        public string PartNum { get; set; } = string.Empty;
        public string? VenPartNum { get; set; }
        public int VendorNum { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string DefaultPurPoint { get; set; } = string.Empty;
        public string? EmailAddress { get; set; }
        public string EmailSource { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
    }
}