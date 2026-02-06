using CrystalGroupHome.SharedRCL.Data.Labor;

namespace CrystalGroupHome.SharedRCL.Data.Employees
{
    public class EmpBasicDTO_Base
    {
        public string EmpID { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string EMailAddress { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string DcdUserID { get; set; } = string.Empty;

        public static explicit operator ADUserDTO_Base(EmpBasicDTO_Base empBasicDTO) => new()
        {
            EmployeeNumber = empBasicDTO.EmpID,
            GivenName = empBasicDTO.FirstName,
            Sn = empBasicDTO.LastName,
            DisplayName = empBasicDTO.Name,
            Mail = empBasicDTO.EMailAddress,
            TelephoneNumber = empBasicDTO.Phone,
            SAMAccountName = empBasicDTO.DcdUserID
        };
    }
}
