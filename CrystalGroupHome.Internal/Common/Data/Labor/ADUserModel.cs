using CrystalGroupHome.SharedRCL.Data.Labor;
using System.Security.Claims;

namespace CrystalGroupHome.Internal.Common.Data.Labor
{
    public class ADUserModel
    {
        public ADUserDTO_Base DBUser { get; set; } = new();
        public ClaimsPrincipal ADUser { get; set; } = new();
    }
}
