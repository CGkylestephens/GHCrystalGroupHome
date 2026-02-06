namespace CrystalGroupHome.SharedRCL.Models;

/// <summary>
/// Represents a predefined user persona for impersonation testing.
/// Each persona defines a set of roles that simulate a specific type of user.
/// </summary>
public class ImpersonationPersona
{
    /// <summary>
    /// Unique identifier for this persona
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display name for this persona
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Description of what this persona represents
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The AD group roles this persona simulates having
    /// </summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Category for grouping personas in the UI
    /// </summary>
    public string Category { get; }

    public ImpersonationPersona(string id, string name, string description, IReadOnlyList<string> roles, string category = "General")
    {
        Id = id;
        Name = name;
        Description = description;
        Roles = roles;
        Category = category;
    }

    // AD Group name constants (copied from various base classes for reference)
    private const string ITRole = "Crystal IT SG";
    private const string CMHubAdminRole = "Crystal CMHub Admin";
    private const string CustCommsAdminRole = "CG EOLT CustComms Admin";
    private const string VendorCommsAdminRole = "CG EOLT VendorComms Admin";
    private const string SalesSupportManagerRole = "Crystal Sales Support SG";
    private const string PurchasingRole = "Crystal Purchasing SG";
    private const string TechServicesRole = "Crystal Tech Services SG";
    private const string TechServicesAdminRole = "Crystal Technical Services Coordinators";
    private const string IRMATechServicesRole = "Crystal IRMA TS";

    /// <summary>
    /// Gets all predefined personas available for impersonation
    /// </summary>
    public static IReadOnlyList<ImpersonationPersona> GetAvailablePersonas()
    {
        return
        [
            // === No Permissions (baseline) ===
            new ImpersonationPersona(
                id: "no-access",
                name: "No Permissions",
                description: "A standard user with no special permissions - baseline view",
                roles: [],
                category: "Baseline"
            ),

            // === CM Hub Personas ===
            new ImpersonationPersona(
                id: "cmhub-admin",
                name: "CM Hub Admin",
                description: "Full CM Hub administrator with all CM Hub permissions",
                roles: [CMHubAdminRole],
                category: "CM Hub"
            ),

            new ImpersonationPersona(
                id: "tech-services",
                name: "Tech Services User",
                description: "Tech Services team member - can edit Tech Services sections in Customer Comms",
                roles: [TechServicesRole],
                category: "CM Hub"
            ),

            new ImpersonationPersona(
                id: "cust-comms-admin",
                name: "Customer Comms Admin",
                description: "Customer Communications administrator - can manage trackers and task statuses",
                roles: [CustCommsAdminRole],
                category: "CM Hub"
            ),

            new ImpersonationPersona(
                id: "purchasing",
                name: "Purchasing User",
                description: "Purchasing team member - can edit Customer Comms trackers",
                roles: [PurchasingRole],
                category: "CM Hub"
            ),

            new ImpersonationPersona(
                id: "vendor-comms-admin",
                name: "Vendor Comms Admin",
                description: "Vendor Communications administrator",
                roles: [VendorCommsAdminRole],
                category: "CM Hub"
            ),

            new ImpersonationPersona(
                id: "sales-support",
                name: "Sales Support Manager",
                description: "Sales Support team - can edit CM Dex and CM Notifications",
                roles: [SalesSupportManagerRole],
                category: "CM Hub"
            ),

            // === Combined Personas (common real-world scenarios) ===
            new ImpersonationPersona(
                id: "cust-comms-with-purchasing",
                name: "Cust Comms Admin + Purchasing",
                description: "Customer Comms admin who is also in Purchasing - typical PM scenario",
                roles: [CustCommsAdminRole, PurchasingRole],
                category: "Combined"
            ),

            new ImpersonationPersona(
                id: "tech-services-plus-cust-comms",
                name: "Tech Services + Cust Comms",
                description: "User with both Tech Services and Customer Comms admin access",
                roles: [TechServicesRole, CustCommsAdminRole],
                category: "Combined"
            ),

            // === RMA Processing Personas ===
            new ImpersonationPersona(
                id: "irma-tech-services",
                name: "IRMA Tech Services",
                description: "IRMA Technical Services - can process RMAs",
                roles: [IRMATechServicesRole],
                category: "RMA Processing"
            ),

            new ImpersonationPersona(
                id: "tech-services-coordinator",
                name: "Tech Services Coordinator",
                description: "Technical Services Coordinator - admin access to RMA processing",
                roles: [TechServicesAdminRole],
                category: "RMA Processing"
            ),

            // === IT (full access) ===
            new ImpersonationPersona(
                id: "it-user",
                name: "IT User",
                description: "IT team member - typically has elevated access across all features",
                roles: [ITRole],
                category: "IT"
            ),
        ];
    }
}
