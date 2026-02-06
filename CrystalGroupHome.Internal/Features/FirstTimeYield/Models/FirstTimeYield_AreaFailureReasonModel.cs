using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Models
{
    public class FirstTimeYield_AreaFailureReasonModel
    {
        public FirstTimeYield_FailureReasonDTO FailureReason { get; set; }
        public List<FirstTimeYield_AreaDTO> Areas { get; set; } = [];
        public bool IsDetailExpanded { get; set; } = false;

        public FirstTimeYield_AreaFailureReasonModel()
        {
            FailureReason = new FirstTimeYield_FailureReasonDTO();
            Areas = [];
        }

        public FirstTimeYield_AreaFailureReasonModel(FirstTimeYield_FailureReasonDTO failureReason, List<FirstTimeYield_AreaDTO> areas)
        {
            FailureReason = failureReason;
            Areas = areas;
        }
    }
}