using Microsoft.AspNetCore.Authorization;

namespace CrystalGroupHome.Internal.Authorization.Requirements
{
    // Base requirement for IT access
    public class ITAccessRequirement : IAuthorizationRequirement { }

    // RMA Processing requirements
    public class RMAProcessingAccessRequirement : IAuthorizationRequirement { }

    // First Time Yield requirements
    public class FirstTimeYieldAdminRequirement : IAuthorizationRequirement { }

    // CM Hub requirements
    public class CMHubAdminRequirement : IAuthorizationRequirement { }
    public class CMHubVendorCommsEditRequirement : IAuthorizationRequirement { }
    public class CMHubCustCommsEditRequirement : IAuthorizationRequirement { }
    public class CMHubCustCommsTaskStatusEditRequirement : IAuthorizationRequirement { }
    public class CMHubCMDexEditRequirement : IAuthorizationRequirement { }
    public class CMHubCMNotifDocumentEditRequirement : IAuthorizationRequirement { }
    public class CMHubCMNotifCreateLogRequirement : IAuthorizationRequirement { }
    public class CMHubTechServicesEditRequirement : IAuthorizationRequirement { }
}