using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace CrystalGroupHome.Internal.Common.Data.Labor
{
    public interface ILaborService
    {
        Task<List<T>> GetLaborEmployeesByJobNum<T>(string jobNum);
        Task<List<T>> GetLaborEmployeesByJobNumAndOpCode<T>(string jobNum, string opCode);
        Task<List<T>> GetEmployeesByEmpIDsAsync<T>(IEnumerable<string> empIDs);
        Task<T?> GetEmployeeByEmpIDAsync<T>(string empID);
        Task<T?> GetEmployeeByDcdUserIDAsync<T>(string dcdUserID);
    }

    public class LaborService : ILaborService
    {
        private readonly string _connectionString;
        private readonly ILogger<LaborService> _logger;
        private readonly DebugModeService _debugModeService;

        // Table Stuff
        private const string LaborDtlTable = "dbo.LaborDtl";
        private const string EmpBasicTable = "Erp.EmpBasic";

        // Company condition
        private const string CompanyEqualsCG = "Company = 'CG'";

        public LaborService(IOptions<DatabaseOptions> dbOptions, ILogger<LaborService> logger, DebugModeService debugModeService)
        {
            _connectionString = dbOptions.Value.KineticErpConnection;
            _logger = logger;
            _debugModeService = debugModeService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<List<T>> GetLaborEmployeesByJobNum<T>(string jobNum)
        {
            return await GetLaborEmployeesByJobNumAndOpCode<T>(jobNum);
        }

        public async Task<List<T>> GetLaborEmployeesByJobNumAndOpCode<T>(string jobNum, string opCode = "")
        {
            var DTOColumnsPrefixed = DataHelpers.DTOPropertiesToSQLColumnsString<T>(EmpBasicTable);
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var andOpCode = string.IsNullOrEmpty(opCode) ? "" : $"AND {LaborDtlTable}.OpCode = '{opCode}'";

            var sql = $@"
                WITH CTE AS (
                    SELECT {DTOColumnsPrefixed},
                        ROW_NUMBER() OVER (PARTITION BY {EmpBasicTable}.EmpID ORDER BY {EmpBasicTable}.FirstName) AS RowNum
                    FROM {LaborDtlTable}
                    INNER JOIN {EmpBasicTable}
                        ON {LaborDtlTable}.EmployeeNum = {EmpBasicTable}.EmpID
                    WHERE {LaborDtlTable}.{CompanyEqualsCG}
                        AND {LaborDtlTable}.JobNum = '{jobNum}'
                        {andOpCode}
                )
                SELECT {DTOColumns}
                FROM CTE
                WHERE RowNum = 1
                ;";

            try
            {
                using var conn = Connection;
                var result = await Connection.QueryAsync<T>(new CommandDefinition(sql)).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employees with columns: {Columns}", DTOColumns);
                throw;
            }
        }

        public async Task<List<T>> GetEmployeesByEmpIDsAsync<T>(IEnumerable<string> empIDs)
        {
            if (empIDs == null || !empIDs.Any())
            {
                return [];
            }

            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {EmpBasicTable}
                WHERE {CompanyEqualsCG}
                  AND EmpID IN @EmpIDs;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(new CommandDefinition(sql, new { EmpIDs = empIDs })).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employees with IDs: {EmpIDs}", string.Join(", ", empIDs));
                throw;
            }
        }

        public async Task<T?> GetEmployeeByEmpIDAsync<T>(string empID)
        {
            var employees = await GetEmployeesByEmpIDsAsync<T>(new[] { empID }).ConfigureAwait(false);
            return employees.FirstOrDefault();
        }

        public async Task<T?> GetEmployeeByDcdUserIDAsync<T>(string dcdUserID)
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {EmpBasicTable}
                WHERE {CompanyEqualsCG}
                  AND DcdUserID = @DcdUserID;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, new { DcdUserID = dcdUserID.Trim() })).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee with DcdUserID: {DcdUserID}", dcdUserID);
                throw;
            }
        }
    }
}
