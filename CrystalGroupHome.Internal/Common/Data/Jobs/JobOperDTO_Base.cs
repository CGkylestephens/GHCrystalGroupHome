namespace CrystalGroupHome.Internal.Common.Data.Jobs
{
    public class JobOperDTO_Base
    {
        public string JobNum { get; set; } = string.Empty;
        public bool JobComplete { get; set; } = false;
        public bool OpComplete { get; set; } = false;
        public int OprSeq { get; set; }
        public string OpCode { get; set; } = string.Empty;

        public decimal QtyCompleted;
        public decimal RunQty;
        public decimal QtyPer;
    }
}
