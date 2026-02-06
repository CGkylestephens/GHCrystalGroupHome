namespace CrystalGroupHome.Internal.Authorization
{
    /// <summary>
    /// Centralized constants for authorization policy names.
    /// Use these constants when applying [Authorize] attributes to ensure consistency.
    /// </summary>
    public static class AuthorizationPolicies
    {
        // IT Access
        public const string ITAccess = "ITAccess";

        // RMA Processing
        public const string RMAProcessingAccess = "RMAProcessingAccess";

        // First Time Yield
        public const string FirstTimeYieldAdmin = "FirstTimeYieldAdmin";

        // CM Hub
        public const string CMHubAdmin = "CMHubAdmin";
        public const string CMHubVendorCommsEdit = "CMHubVendorCommsEdit";
        public const string CMHubCustCommsEdit = "CMHubCustCommsEdit";
        public const string CMHubCustCommsTaskStatusEdit = "CMHubCustCommsTaskStatusEdit";
        public const string CMHubCMDexEdit = "CMHubCMDexEdit";
        public const string CMHubCMNotifDocumentEdit = "CMHubCMNotifDocumentEdit";
        public const string CMHubCMNotifCreateLog = "CMHubCMNotifCreateLog";
        public const string CMHubTechServicesEdit = "CMHubTechServicesEdit";
    }
}