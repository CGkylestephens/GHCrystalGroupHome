using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace CrystalGroupHome.Internal.Common.Data.Labor
{
    public interface IADUserService
    {
        Task<T?> GetADUserBySAMAccountNameAsync<T>(string sAMAccountName);
        Task<List<T>> GetADUsersBySAMAccountNamesAsync<T>(IEnumerable<string> sAMAccountNames);
        Task<T?> GetADUserByEmployeeNumberAsync<T>(string employeeNumber);
        Task<List<T>> GetADUsersByEmployeeNumbersAsync<T>(IEnumerable<string> employeeNumbers);
    }

    public class ADUserService : IADUserService
    {
        private readonly string _connectionString;
        private readonly ILogger<LaborService> _logger;
        private readonly DebugModeService _debugModeService;

        // Table Stuff
        private const string VwAdUsersTable = "dbo.vw_AD_Users";

        public ADUserService(IOptions<DatabaseOptions> dbOptions, ILogger<LaborService> logger, DebugModeService debugModeService)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _debugModeService = debugModeService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<T?> GetADUserBySAMAccountNameAsync<T>(string sAMAccountName)
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {VwAdUsersTable}
                WHERE sAMAccountName = @SAMAccountName;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<T>(
                    new CommandDefinition(sql, new { SAMAccountName = sAMAccountName })
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user with sAMAccountName: {sAMAccountName}", sAMAccountName);
                throw;
            }
        }

        public async Task<List<T>> GetADUsersBySAMAccountNamesAsync<T>(IEnumerable<string> sAMAccountNames)
        {
            if (!sAMAccountNames.Any()) return [];

            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            // Create a parameterized IN clause for multiple SAM account names
            var parameters = new DynamicParameters();
            var parameterNames = new List<string>();

            int i = 0;
            foreach (var accountName in sAMAccountNames)
            {
                var paramName = $"@SAMAccountName{i}";
                parameters.Add(paramName, accountName);
                parameterNames.Add(paramName);
                i++;
            }

            var parameterizedList = string.Join(",", parameterNames);

            var sql = $@"
                SELECT {DTOColumns}
                FROM {VwAdUsersTable}
                WHERE sAMAccountName IN ({parameterizedList});
            ";

            try
            {
                using var conn = Connection;
                var results = await conn.QueryAsync<T>(
                    new CommandDefinition(sql, parameters)
                ).ConfigureAwait(false);

                await _debugModeService.SqlQueryDebugMessage(sql, results);

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users with multiple sAMAccountNames");
                throw;
            }
        }

        public async Task<T?> GetADUserByEmployeeNumberAsync<T>(string employeeNumber)
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {VwAdUsersTable}
                WHERE employeeNumber = @EmployeeNumber;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<T>(
                    new CommandDefinition(sql, new { EmployeeNumber = employeeNumber })
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user with employeeNumber: {employeeNumber}", employeeNumber);
                throw;
            }
        }

        public async Task<List<T>> GetADUsersByEmployeeNumbersAsync<T>(IEnumerable<string> employeeNumbers)
        {
            if (employeeNumbers == null || !employeeNumbers.Any())
            {
                return [];
            }

            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {VwAdUsersTable}
                WHERE employeeNumber IN @EmployeeNumbers;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(
                    new CommandDefinition(sql, new { EmployeeNumbers = employeeNumbers })
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users with employeeNumbers: {EmployeeNumbers}", string.Join(", ", employeeNumbers));
                throw;
            }
        }
    }
}
