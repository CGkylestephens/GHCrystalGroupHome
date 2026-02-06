namespace CrystalGroupHome.SharedRCL.Data.Vendor
{
    public class VendorDTO_Base
    {
        public int VendorNum { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string? EmailAddress { get; set; }
        public bool InActive { get; set; }
    }
}