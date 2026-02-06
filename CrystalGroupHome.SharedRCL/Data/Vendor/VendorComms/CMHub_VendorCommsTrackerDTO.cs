namespace CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms
{
    public class CMHub_VendorCommsTrackerDTO
    {
        [TableColumn("T")]
        public int Id { get; set; }

        [TableColumn("T")]
        public string PartNum { get; set; } = string.Empty;

        [TableColumn("T")]
        public int VendorNum { get; set; }

        [TableColumn("T")]
        public bool Deleted { get; set; }

        [TableColumn("T")]
        public string? ext_VendorName { get; set; }

        [TableColumn("T")]
        public string? ext_VendorPartNum { get; set; }

        [TableColumn("T")]
        public string? ext_PartDesc { get; set; }
    }
}