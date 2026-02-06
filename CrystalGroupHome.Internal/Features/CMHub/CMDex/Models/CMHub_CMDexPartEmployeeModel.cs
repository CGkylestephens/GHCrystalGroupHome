using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.SharedRCL.Data.Labor;

namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Models
{
    public enum PartEmployeeType
    {
        None,
        PM,
        SA,
        PM_SA
    }

    public class CMHub_CMDexPartEmployeeModel
    {
        public required CMHub_PartEmployeeDTO PartEmployeeDTO { get; set; }
        public ADUserDTO_Base? ADUserDTO { get; set; }

    }
}
