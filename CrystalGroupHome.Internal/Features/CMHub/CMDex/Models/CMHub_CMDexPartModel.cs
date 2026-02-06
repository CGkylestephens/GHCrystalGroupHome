using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Parts;

namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Models
{
    public class CMHub_PartCustomerContactModel
    {
        public CMHub_PartCustomerContactDTO PartCustomerContact { get; set; } = default!;
        public CustomerContactDTO_Base DisplayContact { get; set; } = default!;
    }

    public class CMHub_CMDexPartModel
    {
        public PartDTO_Base Part { get; set; } = new PartDTO_Base();
        public PartActivityDTO? PartActivity { get; set; } = null;
        public List<ADUserDTO_Base> ADEmployees { get; set; } = [];
        public List<CMHub_PartEmployeeDTO> PartEmployees { get; set; } = [];
        public int PMEmployeesCount => PartEmployees?.Where(_ => _.Type == (int)PartEmployeeType.PM).Count() ?? 0;
        public int SAEmployeesCount => PartEmployees?.Where(_ => _.Type == (int)PartEmployeeType.SA).Count() ?? 0;
        public string MissingData => BuildMissingDataString();
        public List<CMHub_PartCustomerContactDTO> PartCustomerContacts { get; set; } = [];
        public int PartCustomerContactsCount => PartCustomerContacts?.Count ?? 0;
        public List<CustomerContactDTO_Base> PartCustomerContactsForDisplay { get; set; } = [];
        public List<CMHub_PartCustomerContactModel> PartCustomerContactModels { get; set; } = [];
        public bool IsDetailExpanded = false;

        public bool HasPrimary => HasPrimaryPM();
        public CMHub_PartEmployeeDTO? PrimaryPM => PartEmployees.FirstOrDefault(pe => pe.Type == (int)PartEmployeeType.PM && pe.IsPrimary == true);
        public ADUserDTO_Base? PrimaryPMEmployee => GetADUserForEmployee(PartEmployees?.FirstOrDefault(pe => pe.Type == 1 && pe.IsPrimary == true) ?? new());
        public string PrimaryPMName => PrimaryPMEmployee?.DisplayName ?? "";
        public string PrimaryPMEmail => PrimaryPMEmployee?.Mail ?? "";
        public string PrimaryPMPhone => PrimaryPMEmployee?.TelephoneNumber ?? "";
        public bool HasOwner => HasOwnerCustCon();
        public List<CMHub_PartCustomerContactDTO> CustConsAssociatedWithPrimaryPM => PartCustomerContacts.Where(pc => pc.PartEmployeeId == PrimaryPM?.Id).ToList();
        public string OwnerCustName => GetOwnerCustomerContact()?.DisplayContact.CustName ?? string.Empty;
        public string OwnerConName => GetOwnerCustomerContact()?.DisplayContact.ConName ?? string.Empty;
        public string OwnerConEmail => GetOwnerCustomerContact()?.DisplayContact.EMailAddress ?? string.Empty;

        // Dictionary for quick lookup of AD users by employee ID
        private Dictionary<string, ADUserDTO_Base>? _adUserLookup;

        // Method to get an AD user for a specific employee
        public ADUserDTO_Base? GetADUserForEmployee(CMHub_PartEmployeeDTO employee)
        {
            // Initialize the lookup dictionary if it hasn't been created yet
            _adUserLookup ??= ADEmployees.ToDictionary(ad => ad.EmployeeNumber);

            // Return the matching AD user if found, otherwise null
            return _adUserLookup.TryGetValue(employee.EmpID, out var adUser) ? adUser : null;
        }

        private bool HasPrimaryPM()
        {
            var primaryPm = PartEmployees.FirstOrDefault(pe => pe.Type == (int)PartEmployeeType.PM && pe.IsPrimary == true);
            return primaryPm != null;
        }

        private bool HasOwnerCustCon()
        {
            var primaryPm = PartEmployees.FirstOrDefault(pe => pe.Type == (int)PartEmployeeType.PM && pe.IsPrimary == true);
            if (primaryPm == null) return false;

            return PartCustomerContacts.Any(pc => pc.PartEmployeeId == primaryPm.Id && (pc.IsOwner ?? false));
        }

        private CMHub_PartCustomerContactModel? GetOwnerCustomerContact()
        {
            var primaryPm = PartEmployees.FirstOrDefault(pe => pe.Type == (int)PartEmployeeType.PM && pe.IsPrimary == true);
            if (primaryPm == null) return null;

            var ownerCustCon = PartCustomerContacts
                .Where(pc => pc.PartEmployeeId == primaryPm.Id && (pc.IsOwner ?? false))
                .FirstOrDefault();

            if (ownerCustCon == null) return null;

            int index = PartCustomerContacts.IndexOf(ownerCustCon);
            if (index >= 0 && index < PartCustomerContactModels.Count)
            {
                return PartCustomerContactModels[index];
            }

            return null;
        }

        public CMHub_PartCustomerContactModel? GetCustomerContactFor(CMHub_PartCustomerContactDTO contact)
        {
            if (contact == null)
                return null;

            int index = PartCustomerContacts.IndexOf(contact);
            if (index >= 0 && index < PartCustomerContactModels.Count)
            {
                return PartCustomerContactModels[index];
            }

            return null;
        }

        private string BuildMissingDataString()
        {
            bool missingPrimaryPM = PartEmployees?.Where(_ => _.IsPrimary == true).Count() <= 0;
            bool missingOwnerCC = PartCustomerContacts?.Where(_ => _.IsOwner == true).Count() <= 0;

            string missingText = "";
            if (missingPrimaryPM && missingOwnerCC)
            {
                missingText += "Primary PM & Owner CC";
            }
            else if (missingPrimaryPM)
            {
                missingText += "Primary PM";
            }
            else if (missingOwnerCC)
            {
                missingText += "Owner CC";
            }
            else
            {
                missingText += "✓ No Missing Data";
            }

            return missingText;
        }
    }
}
