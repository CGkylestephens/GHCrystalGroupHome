namespace CrystalGroupHome.SharedRCL.Data;

/// <summary>
/// Central location for all Active Directory group role names used throughout the application.
/// These constants should be used everywhere AD group names are referenced to ensure consistency.
/// </summary>
public static class ADGroupRoles
{
    /// <summary>
    /// All employees in the Crystal Group organization
    /// </summary>
    public const string AllEmployees = "Crystal Group";
    // =====================================================
    // IT / Administrative
    // =====================================================

    /// <summary>
    /// IT team members - typically has elevated access across all features
    /// </summary>
    public const string IT = "Crystal IT SG";

    // =====================================================
    // CM Hub Roles
    // =====================================================
    
    /// <summary>
    /// Full CM Hub administrator with all CM Hub permissions
    /// </summary>
    public const string CMHubAdmin = "Crystal CMHub Admin";

    /// <summary>
    /// Customer Communications administrator - can manage trackers and task statuses
    /// </summary>
    public const string CustCommsAdmin = "CG EOLT CustComms Admin";

    /// <summary>
    /// Vendor Communications administrator
    /// </summary>
    public const string VendorCommsAdmin = "CG EOLT VendorComms Admin";

    /// <summary>
    /// Sales Support team - can edit CM Dex and CM Notifications
    /// </summary>
    public const string SalesSupportManager = "Crystal Sales Support SG";

    /// <summary>
    /// Purchasing team - can edit Customer Comms trackers
    /// </summary>
    public const string Purchasing = "Crystal Purchasing SG";

    /// <summary>
    /// Tech Services team - can edit Tech Services sections in Customer Comms
    /// </summary>
    public const string TechServices = "Crystal Tech Services SG";

    // =====================================================
    // RMA Processing Roles
    // =====================================================
    
    /// <summary>
    /// Technical Services Coordinator - admin access to RMA processing
    /// </summary>
    public const string TechServicesCoordinator = "Crystal Technical Services Coordinators";

    /// <summary>
    /// IRMA Technical Services - can process RMAs (matching Epicor's menu maintenance mapping for RMA Processing)
    /// </summary>
    public const string IRMATechServices = "FS_RW";

    // =====================================================
    // First Time Yield Roles
    // =====================================================

    /// <summary>
    /// First Time Yield administrator
    /// </summary>
    public const string FTYAdmin = "Crystal FTY Admin";
}
