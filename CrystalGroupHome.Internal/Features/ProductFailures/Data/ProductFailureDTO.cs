namespace CrystalGroupHome.Internal.Features.ProductFailures.Data
{
    public class ProductFailureDTO
    {
        public int Id { get; set; }
        public string? ProductId { get; set; }
        public int Failures { get; set; }
        public int TotalTested { get; set; }
        public string? EnteredBy { get; set; }
        public DateTime EnteredDate { get; set; }
    }
}
