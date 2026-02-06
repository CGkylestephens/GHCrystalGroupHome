namespace CrystalGroupHome.Internal.Common.Data.Jobs
{
    // This DTO carries the most common, bare bones Job-related data
    // which is all found in the JobHead table. If you need more data
    // associated with a Job, create a separate DTO that inherits from
    // this within the Blazor Component where it is used.
    public class JobHeadDTO_Base
    {
        public DateTime CreateDate { get; set; }
        public string JobNum { get; set; } = string.Empty;
        public string PartNum { get; set; } = string.Empty;
        public decimal ProdQty { get; set; }
    }
}
