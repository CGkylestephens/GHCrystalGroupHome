using System.Data;
using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Helpers;
using Dapper;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Data
{
    public interface ICMHub_CMDexService
    {
        Task<CMHub_CMDexPartModel?> GetCMDexPartAsync(string partNum);
        Task<List<CMHub_CMDexPartModel>> GetAllCMDexPartsAsync(bool includeInactive);
        Task<List<CMHub_CMDexPartModel>> GetCMDexPartsByPartNumbersAsync(IEnumerable<string> partNumbers, bool includeInactive = false);
        Task<PaginatedResult<CMHub_CMDexPartModel>> GetCMDexPartsPaginatedAsync(int pageNumber, int pageSize);
        Task<int> CreatePartEmployeeOnPartAsync(string partNum, string empID, PartEmployeeType type);
        Task<int> CreatePartCustomerContactOnPartAsync(string partNum, int partEmpId, int custNum, int conNum, int perConID);
        Task<int> UpdatePartEmployeeAsync(CMHub_PartEmployeeDTO partEmployee);
        Task<int> UpdatePartCustomerContactAsync(CMHub_PartCustomerContactDTO partCustomerContact);
        Task<int> DeletePartEmployeeAsync(int id);
        Task<int> DeletePartCustomerContactAsync(int id);

        Task OpenCMDexPartInNewTabAsync(string partNum);

        Task FillCMPartDisplayData(CMHub_CMDexPartModel cmDexPart);
    }

    public class CMHub_CMDexService : ICMHub_CMDexService
    {
        private readonly string _connectionString;
        private readonly ILogger<CMHub_CMDexService> _logger;
        private readonly DebugModeService _debugModeService;
        private readonly IPartService _partService;
        private readonly IADUserService _adUserService;
        private readonly ICustomerService _customerService;
        private readonly NavigationManager _navigationManager;
        private readonly IJSRuntime _js;

        // Table Names
        private const string PartEmployeeTable = "dbo.CMDex_PartEmployee";
        private const string PartCustomerTable = "dbo.CMDex_PartCustomerContact";

        public CMHub_CMDexService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<CMHub_CMDexService> logger,
            DebugModeService debugModeService,
            IPartService partService,
            IADUserService adUserService,
            ICustomerService customerService,
            NavigationManager navigationManager,
            IJSRuntime jSRuntime
            )
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _debugModeService = debugModeService;
            _partService = partService;
            _adUserService = adUserService;
            _customerService = customerService;
            _navigationManager = navigationManager;
            _js = jSRuntime;
        }

        /// <summary>
        /// Retrieves a combined model for a given part number by combining the part data
        /// (from Part and PartRev) with associated CMDex employee and customer data.
        /// </summary>
        public async Task<CMHub_CMDexPartModel?> GetCMDexPartAsync(string partNum)
        {
            // First, retrieve the part data from the PartService.
            var foundParts = await _partService.GetPartsByPartNumbersAsync<PartDTO_Base>([partNum], includeInactive: true);
            var part = foundParts.FirstOrDefault();
            if (part == null)
            {
                _logger.LogWarning("Part with PartNum {PartNum} not found.", partNum);
                return null;
            }

            var combinedModel = new CMHub_CMDexPartModel { Part = part };

            // Get employees for the part.
            var employeeColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_PartEmployeeDTO>();
            var employeeQuery = @$"
                SELECT {employeeColumns}
                FROM {PartEmployeeTable}
                WHERE PartNum = @PartNum
                ;";

            IEnumerable<CMHub_PartEmployeeDTO> employees;
            try
            {
                using var cmdexConn = new SqlConnection(_connectionString);
                employees = await cmdexConn.QueryAsync<CMHub_PartEmployeeDTO>(employeeQuery, new { PartNum = partNum });
                await _debugModeService.SqlQueryDebugMessage(employeeQuery.Replace("@PartNum", partNum), employees);
                combinedModel.PartEmployees = employees.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching CMDex_PartEmployee data for PartNum {PartNum}", partNum);
                throw;
            }

            // If we found employees, get their associated customers.
            var employeeIds = combinedModel.PartEmployees.Select(e => e.Id).ToList();
            if (employeeIds.Count != 0)
            {
                var customerColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_PartCustomerContactDTO>();
                var customerQuery = @$"
                    SELECT {customerColumns}
                    FROM {PartCustomerTable}
                    WHERE PartEmployeeId IN @EmployeeIds
                    ;";

                try
                {
                    using var cmdexConn = new SqlConnection(_connectionString);
                    var customers = await cmdexConn.QueryAsync<CMHub_PartCustomerContactDTO>(customerQuery, new { EmployeeIds = employeeIds });
                    await _debugModeService.SqlQueryDebugMessage(customerQuery.Replace("@EmployeeIds", "(" + string.Join(",", employeeIds) + ")"), customers);
                    combinedModel.PartCustomerContacts = customers.ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching CMDex_PartCustomer data for EmployeeIds {@EmployeeIds}", employeeIds);
                    throw;
                }
            }

            await FillCMPartDisplayData(combinedModel);

            return combinedModel;
        }

        /// <summary>
        /// Retrieves all parts with all associated CMDex employee and customer data in a single set.
        /// Uses three queries: one for parts, one for employees, and one for customers.
        /// </summary>
        public async Task<List<CMHub_CMDexPartModel>> GetAllCMDexPartsAsync(bool includeInactive)
        {
            // 1. Get all parts using PartService.
            var parts = await _partService.GetPartsAsync<PartDTO_Base>(includeInactive: includeInactive, cmOnly: true);
            if (parts == null || parts.Count == 0)
                return [];

            // Build a dictionary keyed by PartNum.
            var partsDictionary = parts.ToDictionary(
                p => p.PartNum,
                p => new CMHub_CMDexPartModel
                {
                    Part = p,
                    PartEmployees = [],
                    PartCustomerContacts = []
                }
            );
            var partNums = partsDictionary.Keys.ToList();

            // 2. Retrieve all employees for these parts using batch processing.
            var employeeColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_PartEmployeeDTO>();
            var allEmployees = new List<CMHub_PartEmployeeDTO>();

            // Process in batches of 2000 parameters to avoid SQL Server's 2100 parameter limit
            const int batchSize = 2000;
            for (int i = 0; i < partNums.Count; i += batchSize)
            {
                var batchPartNums = partNums.Skip(i).Take(batchSize).ToList();
                var employeeQuery = @$"
                    SELECT {employeeColumns}
                    FROM {PartEmployeeTable}
                    WHERE PartNum IN @PartNums
                    ;";

                try
                {
                    using var conn = new SqlConnection(_connectionString);
                    var batchEmployees = await conn.QueryAsync<CMHub_PartEmployeeDTO>(employeeQuery, new { PartNums = batchPartNums });
                    allEmployees.AddRange(batchEmployees);

                    await _debugModeService.SqlQueryDebugMessage(
                        employeeQuery.Replace("@PartNums", "(" + string.Join(",", batchPartNums) + ")"),
                        batchEmployees);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching CMDexPartEmployee data for batch of PartNums {@PartNums}", batchPartNums);
                    throw;
                }
            }

            // Map employees to their corresponding part.
            var employeeIdToPartNum = new Dictionary<int, string>();
            foreach (var emp in allEmployees)
            {
                if (partsDictionary.TryGetValue(emp.PartNum, out var model))
                {
                    model.PartEmployees.Add(emp);
                    employeeIdToPartNum[emp.Id] = emp.PartNum;
                }
            }

            // 3. Retrieve all customers associated with the employees, also in batches.
            if (employeeIdToPartNum.Count != 0)
            {
                var employeeIds = employeeIdToPartNum.Keys.ToList();
                var customerColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_PartCustomerContactDTO>();
                var allCustomers = new List<CMHub_PartCustomerContactDTO>();

                // Process employee IDs in batches as well
                for (int i = 0; i < employeeIds.Count; i += batchSize)
                {
                    var batchEmployeeIds = employeeIds.Skip(i).Take(batchSize).ToList();
                    var customerQuery = @$"
                        SELECT {customerColumns}
                        FROM {PartCustomerTable}
                        WHERE PartEmployeeId IN @EmployeeIds
                        ;";

                    try
                    {
                        using var conn = new SqlConnection(_connectionString);
                        var batchCustomers = await conn.QueryAsync<CMHub_PartCustomerContactDTO>(
                            customerQuery, new { EmployeeIds = batchEmployeeIds });
                        allCustomers.AddRange(batchCustomers);

                        await _debugModeService.SqlQueryDebugMessage(
                            customerQuery.Replace("@EmployeeIds", "(" + string.Join(",", batchEmployeeIds) + ")"),
                            batchCustomers);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching CMDexPartCustomer data for batch of EmployeeIds {@EmployeeIds}", batchEmployeeIds);
                        throw;
                    }
                }

                // Assign customers to the appropriate part based on employee mapping.
                foreach (var cust in allCustomers)
                {
                    if (employeeIdToPartNum.TryGetValue(cust.PartEmployeeId, out var partNum))
                    {
                        if (partsDictionary.TryGetValue(partNum, out var model))
                        {
                            model.PartCustomerContacts.Add(cust);
                        }
                    }
                }
            }

            return partsDictionary.Values.ToList();
        }

        public async Task<List<CMHub_CMDexPartModel>> GetCMDexPartsByPartNumbersAsync(IEnumerable<string> partNumbers, bool includeInactive)
        {
            if (partNumbers == null || !partNumbers.Any())
            {
                return new List<CMHub_CMDexPartModel>();
            }

            var uniquePartNumbers = partNumbers.Distinct().ToList();

            // 1. Get Base Part Info using PartService (assuming it can handle batches)
            var parts = await _partService.GetPartsByPartNumbersAsync<PartDTO_Base>(uniquePartNumbers, includeInactive);
            if (parts == null || !parts.Any())
            {
                return new List<CMHub_CMDexPartModel>();
            }

            // Initialize the result dictionary
            var results = parts.ToDictionary(
                p => p.PartNum,
                p => new CMHub_CMDexPartModel
                {
                    Part = p,
                    PartEmployees = new List<CMHub_PartEmployeeDTO>(),
                    PartCustomerContacts = new List<CMHub_PartCustomerContactDTO>(),
                    ADEmployees = new List<ADUserDTO_Base>()
                }
            );

            // 2. Fetch Employees and Contacts in a single query using JOINs
            // Dapper can map nested objects from JOINs if the query aliases match property names or using multi-mapping.
            // We fetch all related data and let Dapper/C# handle the hierarchy construction.
            var query = $@"
                SELECT
                    pe.Id AS PartEmployee_Id, pe.PartNum AS PartEmployee_PartNum, pe.EmpID AS PartEmployee_EmpID, pe.Type AS PartEmployee_Type, pe.IsPrimary AS PartEmployee_IsPrimary, pe.DateAdded AS PartEmployee_DateAdded, -- Employee Columns (prefixed for clarity)
                    pcc.Id AS Contact_Id, pcc.PartEmployeeId AS Contact_PartEmployeeId, pcc.CustNum AS Contact_CustNum, pcc.ConNum AS Contact_ConNum, pcc.PerConID AS Contact_PerConID, pcc.IsOwner AS Contact_IsOwner, pcc.ECNChangeNotice AS Contact_ECNChangeNotice, pcc.ECNImplementationNotice AS Contact_ECNImplementationNotice, pcc.ECNAlwaysNotify AS Contact_ECNAlwaysNotify, pcc.DateAdded AS Contact_DateAdded -- Contact Columns (prefixed for clarity)
                FROM
                    {PartEmployeeTable} pe
                LEFT JOIN
                    {PartCustomerTable} pcc ON pe.Id = pcc.PartEmployeeId
                WHERE
                    pe.PartNum IN @PartNumbers
                ORDER BY
                    pe.PartNum, pe.Id;
            ";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var flatResults = await conn.QueryAsync<dynamic>(query, new { PartNumbers = uniquePartNumbers });

                // Process the flat results to build the hierarchy
                CMHub_PartEmployeeDTO? currentEmployee = null;
                int currentEmployeeId = -1;

                foreach (var row in flatResults)
                {
                    string partNum = row.PartEmployee_PartNum;
                    if (results.TryGetValue(partNum, out var partModel))
                    {
                        int employeeId = row.PartEmployee_Id;

                        // Check if this is a new employee for the current part
                        if (employeeId != currentEmployeeId)
                        {
                            currentEmployeeId = employeeId;
                            currentEmployee = new CMHub_PartEmployeeDTO
                            {
                                Id = employeeId,
                                PartNum = partNum,
                                EmpID = row.PartEmployee_EmpID,
                                Type = row.PartEmployee_Type,
                                IsPrimary = row.PartEmployee_IsPrimary,
                                DateAdded = row.PartEmployee_DateAdded
                            };
                            partModel.PartEmployees.Add(currentEmployee);
                        }

                        // Check if there's contact data in this row
                        if (currentEmployee != null && row.Contact_Id != null)
                        {
                            var contact = new CMHub_PartCustomerContactDTO
                            {
                                Id = row.Contact_Id,
                                PartEmployeeId = row.Contact_PartEmployeeId,
                                CustNum = row.Contact_CustNum,
                                ConNum = row.Contact_ConNum,
                                PerConID = row.Contact_PerConID,
                                IsOwner = row.Contact_IsOwner,
                                ECNChangeNotice = row.Contact_ECNChangeNotice,
                                ECNImplementationNotice = row.Contact_ECNImplementationNotice,
                                ECNAlwaysNotify = row.Contact_ECNAlwaysNotify,
                                DateAdded = row.Contact_DateAdded
                            };
                            // Avoid adding duplicate contacts if the JOIN produces multiple rows for the same contact (shouldn't happen with LEFT JOIN from Employee)
                            if (!partModel.PartCustomerContacts.Any(c => c.Id == contact.Id))
                            {
                                partModel.PartCustomerContacts.Add(contact);
                            }
                        }
                    }
                    // Reset current employee when part number changes (relies on ORDER BY)
                    // This simple approach works if results are ordered by PartNum, EmployeeId
                    // More robust grouping might be needed if ordering isn't guaranteed or complex.
                    currentEmployeeId = row.PartEmployee_Id; // Update ID regardless
                }

                await _debugModeService.SqlQueryDebugMessage(query.Replace("@PartNumbers", $"('{string.Join("','", uniquePartNumbers)}')"), flatResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching batch CMDex Employee/Customer data for PartNums {@PartNums}", uniquePartNumbers);
                throw;
            }

            return results.Values.ToList();
        }

        /// <summary>
        /// Retrieves a paginated list of parts with associated CMDex employee and customer data.
        /// Uses the PartService's paginated method and then performs CMDex queries filtered to the page's parts.
        /// </summary>
        public async Task<PaginatedResult<CMHub_CMDexPartModel>> GetCMDexPartsPaginatedAsync(int pageNumber, int pageSize)
        {
            // 1. Get paginated parts using PartService.
            var paginatedParts = await _partService.GetPartsPaginatedAsync<PartDTO_Base>(pageNumber, pageSize);
            var parts = paginatedParts.Items;
            if (parts == null || parts.Count == 0)
            {
                return new PaginatedResult<CMHub_CMDexPartModel> { Items = new List<CMHub_CMDexPartModel>(), TotalRecords = 0 };
            }

            // Build a dictionary keyed by PartNum.
            var partsDictionary = parts.ToDictionary(
                p => p.PartNum,
                p => new CMHub_CMDexPartModel
                {
                    Part = p,
                    PartEmployees = [],
                    PartCustomerContacts = []
                }
            );
            var partNums = partsDictionary.Keys.ToList();

            // 2. Query for employees on this page.
            var employeeColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_PartEmployeeDTO>();
            var employeeQuery = @$"
                SELECT {employeeColumns}
                FROM {PartEmployeeTable}
                WHERE PartNum IN @PartNums
                ;";

            IEnumerable<CMHub_PartEmployeeDTO> employees;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                employees = await conn.QueryAsync<CMHub_PartEmployeeDTO>(employeeQuery, new { PartNums = partNums });
                await _debugModeService.SqlQueryDebugMessage(employeeQuery.Replace("@PartNums", "(" + string.Join(",", partNums) + ")"), employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching CMDex_PartEmployee data for PartNums {@PartNums}", partNums);
                throw;
            }

            // Map employees to parts.
            var employeeIdToPartNum = new Dictionary<int, string>();
            foreach (var emp in employees)
            {
                if (partsDictionary.TryGetValue(emp.PartNum, out var model))
                {
                    model.PartEmployees.Add(emp);
                    employeeIdToPartNum[emp.Id] = emp.PartNum;
                }
            }

            // 3. Query for customers associated with these employees.
            if (employeeIdToPartNum.Count != 0)
            {
                var employeeIds = employeeIdToPartNum.Keys.ToList();
                var customerColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_PartCustomerContactDTO>();
                var customerQuery = @$"
                    SELECT {customerColumns}
                    FROM {PartCustomerTable}
                    WHERE PartEmployeeId IN @EmployeeIds
                    ;";

                IEnumerable<CMHub_PartCustomerContactDTO> customers;
                try
                {
                    using var conn = new SqlConnection(_connectionString);
                    customers = await conn.QueryAsync<CMHub_PartCustomerContactDTO>(customerQuery, new { EmployeeIds = employeeIds });
                    await _debugModeService.SqlQueryDebugMessage(employeeQuery.Replace("@EmployeeIds", "(" + string.Join(",", employeeIds) + ")"), customers);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching CMDex_PartCustomer data for EmployeeIds {@EmployeeIds}", employeeIds);
                    throw;
                }

                foreach (var cust in customers)
                {
                    if (employeeIdToPartNum.TryGetValue(cust.PartEmployeeId, out var partNum))
                    {
                        if (partsDictionary.TryGetValue(partNum, out var model))
                        {
                            model.PartCustomerContacts.Add(cust);
                        }
                    }
                }
            }

            return new PaginatedResult<CMHub_CMDexPartModel>
            {
                Items = partsDictionary.Values.ToList(),
                TotalRecords = paginatedParts.TotalRecords
            };
        }

        /// <summary>
        /// Adds an employee (PM or SA) to a part with the specified type.
        /// </summary>
        /// <param name="partNum">The part number to add the employee to</param>
        /// <param name="empID">The SAM account name of the employee to add</param>
        /// <param name="type">The type of employee (PM or SA)</param>
        /// <returns>ID of the newly added employee record</returns>
        public async Task<int> CreatePartEmployeeOnPartAsync(string partNum, string empID, PartEmployeeType type)
        {
            const string insertQuery = @$"
                INSERT INTO {PartEmployeeTable} (PartNum, EmpID, Type, DateAdded)
                OUTPUT INSERTED.Id
                VALUES (@PartNum, @EmpID, @Type, @DateAdded)
                ;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var parameters = new
                {
                    PartNum = partNum,
                    EmpID = empID,
                    Type = (int)type,
                    DateAdded = DateTime.UtcNow
                };

                var newId = await conn.ExecuteScalarAsync<int>(insertQuery, parameters);

                await _debugModeService.SqlQueryDebugMessage(
                    insertQuery
                        .Replace("@PartNum", $"'{partNum}'")
                        .Replace("@EmpID", $"'{empID}'")
                        .Replace("@Type", $"'{type}'")
                        .Replace("@DateAdded", $"'{DateTime.UtcNow}'"),
                    newId);

                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding employee {EmpID} as {Type} to part {PartNum}",
                    empID, type, partNum);
                throw;
            }
        }

        /// <summary>
        /// Adds a customer contact to an existing part employee.
        /// </summary>
        /// <param name="partNum">The part number</param>
        /// <param name="partEmpId">The ID of the part employee to associate this contact with</param>
        /// <param name="custNum">The customer number</param>
        /// <param name="conNum">The contact number</param>
        /// <param name="perConID">The person contact ID</param>
        /// <returns>ID of the newly added customer contact record</returns>
        public async Task<int> CreatePartCustomerContactOnPartAsync(string partNum, int partEmpId, int custNum, int conNum, int perConID)
        {
            const string insertQuery = @$"
                INSERT INTO {PartCustomerTable} (PartEmployeeId, CustNum, ConNum, PerConID, DateAdded)
                OUTPUT INSERTED.Id
                VALUES (@PartEmployeeId, @CustNum, @ConNum, @PerConID, @DateAdded)
                ;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var parameters = new
                {
                    PartEmployeeId = partEmpId,
                    CustNum = custNum,
                    ConNum = conNum,
                    PerConID = perConID,
                    DateAdded = DateTime.UtcNow
                };

                var newId = await conn.ExecuteScalarAsync<int>(insertQuery, parameters);

                await _debugModeService.SqlQueryDebugMessage(
                    insertQuery
                        .Replace("@PartEmployeeId", $"{partEmpId}")
                        .Replace("@CustNum", $"{custNum}")
                        .Replace("@ConNum", $"{conNum}")
                        .Replace("@PerConID", $"'{perConID}'")
                        .Replace("@DateAdded", $"'{DateTime.UtcNow}'"),
                    newId);

                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding customer contact (CustNum: {CustNum}, ConNum: {ConNum}) to part {PartNum}",
                    custNum, conNum, partNum);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing part employee record.
        /// </summary>
        /// <param name="partEmployee">The part employee data to update</param>
        /// <returns>Number of rows affected (should be 1 if successful)</returns>
        public async Task<int> UpdatePartEmployeeAsync(CMHub_PartEmployeeDTO partEmployee)
        {
            const string updateQuery = @$"
                UPDATE {PartEmployeeTable}
                SET EmpID = @EmpID,
                    Type = @Type,
                    IsPrimary = @IsPrimary
                WHERE Id = @Id
                ;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var parameters = new
                {
                    partEmployee.Id,
                    partEmployee.EmpID,
                    partEmployee.Type,
                    partEmployee.IsPrimary
                };

                var rowsAffected = await conn.ExecuteAsync(updateQuery, parameters);

                await _debugModeService.SqlQueryDebugMessage(
                    updateQuery
                        .Replace("@Id", $"{partEmployee.Id}")
                        .Replace("@EmpID", $"'{partEmployee.EmpID}'")
                        .Replace("@Type", $"{partEmployee.Type}")
                        .Replace("@IsPrimary", $"{partEmployee.IsPrimary}"),
                    rowsAffected);

                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating part employee {Id} for part {PartNum}",
                    partEmployee.Id, partEmployee.PartNum);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing part customer contact record.
        /// </summary>
        /// <param name="partCustomerContact">The part customer contact data to update</param>
        /// <returns>Number of rows affected (should be 1 if successful)</returns>
        public async Task<int> UpdatePartCustomerContactAsync(CMHub_PartCustomerContactDTO partCustomerContact)
        {
            const string updateQuery = @$"
                UPDATE {PartCustomerTable}
                SET CustNum = @CustNum,
                    ConNum = @ConNum,
                    PerConID = @PerConID,
                    IsOwner = @IsOwner,
                    ECNChangeNotice = @ECNChangeNotice,
                    ECNImplementationNotice = @ECNImplementationNotice,
                    ECNAlwaysNotify = @ECNAlwaysNotify
                WHERE Id = @Id
                ;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var parameters = new
                {
                    partCustomerContact.Id,
                    partCustomerContact.CustNum,
                    partCustomerContact.ConNum,
                    partCustomerContact.PerConID,
                    partCustomerContact.IsOwner,
                    partCustomerContact.ECNChangeNotice,
                    partCustomerContact.ECNImplementationNotice,
                    partCustomerContact.ECNAlwaysNotify
                };

                var rowsAffected = await conn.ExecuteAsync(updateQuery, parameters);

                await _debugModeService.SqlQueryDebugMessage(
                    updateQuery
                        .Replace("@Id", $"{partCustomerContact.Id}")
                        .Replace("@CustNum", $"{partCustomerContact.CustNum}")
                        .Replace("@ConNum", $"{partCustomerContact.ConNum}")
                        .Replace("@PerConID", $"{partCustomerContact.PerConID}")
                        .Replace("@IsOwner", $"{partCustomerContact.IsOwner}")
                        .Replace("@ECNChangeNotice", $"{partCustomerContact.ECNChangeNotice}")
                        .Replace("@ECNImplementationNotice", $"{partCustomerContact.ECNImplementationNotice}")
                        .Replace("@ECNAlwaysNotify", $"{partCustomerContact.ECNAlwaysNotify}"),
                    rowsAffected);

                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating part customer contact {Id} for part employee {PartEmployeeId}",
                    partCustomerContact.Id, partCustomerContact.PartEmployeeId);
                throw;
            }
        }

        /// <summary>
        /// Deletes a part employee record by ID and all associated customer contacts.
        /// </summary>
        /// <param name="id">The ID of the part employee to delete</param>
        /// <returns>Number of rows affected (combined from both delete operations)</returns>
        public async Task<int> DeletePartEmployeeAsync(int id)
        {
            // First delete query - remove associated customer contacts
            const string deleteCustomerContactsQuery = @$"
                DELETE FROM {PartCustomerTable}
                WHERE PartEmployeeId = @Id
                ;";

            // Second delete query - remove the employee record
            const string deleteEmployeeQuery = @$"
                DELETE FROM {PartEmployeeTable}
                WHERE Id = @Id
                ;";

            try
            {
                int totalRowsAffected = 0;
                using var conn = new SqlConnection(_connectionString);

                // Ensure the connection is open before beginning a transaction
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                // Begin transaction to ensure both operations succeed or fail together
                using var transaction = conn.BeginTransaction();

                try
                {
                    // Step 1: Delete all associated customer contacts
                    var customerRowsAffected = await conn.ExecuteAsync(
                        deleteCustomerContactsQuery,
                        new { Id = id },
                        transaction);

                    await _debugModeService.SqlQueryDebugMessage(
                        deleteCustomerContactsQuery.Replace("@Id", $"{id}"),
                        customerRowsAffected);

                    // Step 2: Delete the employee record
                    var employeeRowsAffected = await conn.ExecuteAsync(
                        deleteEmployeeQuery,
                        new { Id = id },
                        transaction);

                    await _debugModeService.SqlQueryDebugMessage(
                        deleteEmployeeQuery.Replace("@Id", $"{id}"),
                        employeeRowsAffected);

                    // Commit the transaction once both operations complete successfully
                    transaction.Commit();

                    totalRowsAffected = customerRowsAffected + employeeRowsAffected;

                    _logger.LogInformation(
                        "Deleted part employee {Id} and {CustomerCount} associated customer contacts",
                        id,
                        customerRowsAffected);
                }
                catch
                {
                    // If any error occurs, roll back the transaction
                    transaction.Rollback();
                    throw;
                }

                return totalRowsAffected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting part employee with ID {Id} and its associated customer contacts", id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a part customer contact record by ID.
        /// </summary>
        /// <param name="id">The ID of the part customer contact to delete</param>
        /// <returns>Number of rows affected (should be 1 if successful)</returns>
        public async Task<int> DeletePartCustomerContactAsync(int id)
        {
            const string deleteQuery = @$"
                DELETE FROM {PartCustomerTable}
                WHERE Id = @Id
                ;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var rowsAffected = await conn.ExecuteAsync(deleteQuery, new { Id = id });

                await _debugModeService.SqlQueryDebugMessage(
                    deleteQuery.Replace("@Id", $"{id}"),
                    rowsAffected);

                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting part customer contact with ID {Id}", id);
                throw;
            }
        }

        public async Task OpenCMDexPartInNewTabAsync(string partNum)
        {
            if (string.IsNullOrWhiteSpace(partNum)) return; // Prevent opening blank tabs

            // build the relative URL
            var relativeUrl = $"{NavHelpers.CMHub_CMDexPartDetails}/{partNum}";
            // turn it into an absolute URI
            var absoluteUrl = _navigationManager.ToAbsoluteUri(relativeUrl).ToString();
            // open in a new tab
            try
            {
                await _js.InvokeVoidAsync("open", absoluteUrl, "_blank");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to open new tab: {ex.Message}");
            }
        }

        public async Task FillCMPartDisplayData(CMHub_CMDexPartModel cmDexPart)
        {
            // Fill the display data for employees and customers
            if (cmDexPart.PartEmployees != null)
            {
                // Sort PartEmployees so that primary employees come first
                cmDexPart.PartEmployees = cmDexPart.PartEmployees
                .OrderByDescending(pe => pe.IsPrimary)
                .ToList();

                var empIds = cmDexPart.PartEmployees.Select(pe => pe.EmpID).ToList();
                cmDexPart.ADEmployees = await _adUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(empIds);
            }
            if (cmDexPart.PartCustomerContacts != null)
            {
                var contactViewModels = new List<CMHub_PartCustomerContactModel>();

                foreach (var custCon in cmDexPart.PartCustomerContacts)
                {
                    var displayContact = await _customerService.GetCustomerContactByCompoundKeyAsync<CustomerContactDTO_Base>(
                        custCon.CustNum, custCon.ConNum, custCon.PerConID);

                    contactViewModels.Add(new CMHub_PartCustomerContactModel
                    {
                        PartCustomerContact = custCon,
                        DisplayContact = displayContact ?? new CustomerContactDTO_Base
                        {
                            // Create a placeholder with minimal information
                            ConName = "Unknown Contact"
                        }
                    });
                }

                cmDexPart.PartCustomerContactModels = contactViewModels;
            }
        }
    }
}
