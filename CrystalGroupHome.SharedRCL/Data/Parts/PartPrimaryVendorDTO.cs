using CrystalGroupHome.SharedRCL.Data.Vendor;

namespace CrystalGroupHome.SharedRCL.Data.Parts
{
    public class PartPrimaryVendorDTO : PartDTO_Base
    {
        public VendorDTO_Base? PrimaryVendor { get; set; }

        // Vendor and Manufacturer Part Information
        public string? VendorPartNum { get; set; }
        public string? MfgPartNum { get; set; }
        public string? MfgName { get; set; }
    }
}
