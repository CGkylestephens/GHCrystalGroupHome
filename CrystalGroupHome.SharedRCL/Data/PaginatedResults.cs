namespace CrystalGroupHome.SharedRCL.Data
{
    public class PaginatedResult<T>
    {
        public List<T>? Items { get; set; }
        public int TotalRecords { get; set; }
    }
}
