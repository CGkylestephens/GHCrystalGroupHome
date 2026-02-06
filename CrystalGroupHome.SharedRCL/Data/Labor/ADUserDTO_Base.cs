using CrystalGroupHome.SharedRCL.Data.Employees;

namespace CrystalGroupHome.SharedRCL.Data.Labor
{
    public class ADUserDTO_Base
    {
        public string EmployeeNumber { get; set; } = string.Empty;
        public string GivenName { get; set; } = string.Empty;
        public string Sn { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string TelephoneNumber { get; set; } = string.Empty;
        public string SAMAccountName { get; set; } = string.Empty;

        public static explicit operator EmpBasicDTO_Base(ADUserDTO_Base aDUserDTO) => new()
        {
            EmpID = aDUserDTO.EmployeeNumber,
            FirstName = aDUserDTO.GivenName,
            LastName = aDUserDTO.Sn,
            Name = aDUserDTO.DisplayName,
            EMailAddress = aDUserDTO.Mail,
            Phone = aDUserDTO.TelephoneNumber,
            DcdUserID = aDUserDTO.SAMAccountName
        };
    } 
}