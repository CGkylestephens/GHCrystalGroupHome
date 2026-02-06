namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Data
{
    public class CMHub_PartCustomerContactDTO
    {
        public int Id { get; set; }  // Alias for CMDex_PartCustomer.Id
        public int PartEmployeeId { get; set; }  // References CMDex_PartEmployee.Id
        public int CustNum { get; set; }
        public int ConNum { get; set; }
        public int PerConID { get; set; }
        public bool? IsOwner { get; set; }  // Indicates if this customer is the owner
        public bool? ECNChangeNotice { get; set; }
        public bool? ECNImplementationNotice { get; set; }
        public bool? ECNAlwaysNotify { get; set; }
        public DateTime DateAdded { get; set; }
    }
}
