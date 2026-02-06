using CrystalGroupHome.SharedRCL.Data;

namespace CrystalGroupHome.Internal.Common.Data.Customers
{
    public class CustomerContactDTO_Base
    {
        [TableColumn("C")]
        public string CustID { get; set; } = string.Empty;

        [TableColumn("C", "Name")]
        public string CustName { get; set; } = string.Empty;

        [TableColumn("C")]
        public string City { get; set; } = string.Empty;

        [TableColumn("C")]
        public string State { get; set; } = string.Empty;

        [TableColumn("C")]
        public string Country { get; set; } = string.Empty;

        [TableColumn("C")]
        public int CustNum { get; set; }

        [TableColumn("CC")]
        public int ConNum { get; set; }

        [TableColumn("CC")]
        public int PerConID { get; set; }

        [TableColumn("CC", "Name")]
        public string ConName { get; set; } = string.Empty;

        [TableColumn("CC")]
        public string PhoneNum { get; set; } = string.Empty;

        [TableColumn("CC")]
        public string EMailAddress { get; set; } = string.Empty;
    }
}
