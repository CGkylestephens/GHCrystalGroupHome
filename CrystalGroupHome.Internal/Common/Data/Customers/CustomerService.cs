using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace CrystalGroupHome.Internal.Common.Data.Customers
{
    public interface ICustomerService
    {
        Task<List<T>> GetCustomerContactsAsync<T>();
        Task<T?> GetCustomerContactByCustNumAsync<T>(int custNum);
        Task<T?> GetCustomerContactByCompoundKeyAsync<T>(int custNum, int conNum, int perConID);
    }

    public class CustomerService : ICustomerService
    {
        private readonly string _connectionString;
        private readonly ILogger<CustomerService> _logger;
        private readonly DebugModeService _debugModeService;

        // Table definitions
        private const string CustomerTable = "dbo.Customer";
        private const string CustCntTable = "Erp.CustCnt";

        // Company condition
        private const string CompanyEqualsCG = "Company = 'CG'";

        public CustomerService(IOptions<DatabaseOptions> dbOptions, ILogger<CustomerService> logger, DebugModeService debugModeService)
        {
            _connectionString = dbOptions.Value.KineticErpConnection;
            _logger = logger;
            _debugModeService = debugModeService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<List<T>> GetCustomerContactsAsync<T>()
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {CustomerTable} AS C
                INNER JOIN {CustCntTable} AS CC
                    ON CC.CustNum = C.CustNum
                WHERE C.{CompanyEqualsCG}
                    AND CC.ShipToNum = ''
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(new CommandDefinition(sql)).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer contacts");
                throw;
            }
        }

        public async Task<T?> GetCustomerContactByCustNumAsync<T>(int custNum)
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {CustomerTable} AS C
                INNER JOIN {CustCntTable} AS CC
                    ON CC.CustNum = C.CustNum
                WHERE C.{CompanyEqualsCG}
                    AND CC.ShipToNum = ''
                    AND C.CustNum = @CustNum
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, new { CustNum = custNum })).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer contact with CustNum: {CustNum}", custNum);
                throw;
            }
        }

        public async Task<T?> GetCustomerContactByCompoundKeyAsync<T>(int custNum, int conNum, int perConID)
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {CustomerTable} AS C
                INNER JOIN {CustCntTable} AS CC
                    ON CC.CustNum = C.CustNum
                WHERE C.{CompanyEqualsCG}
                    AND CC.ShipToNum = ''
                    AND C.CustNum = @CustNum
                    AND CC.ConNum = @ConNum
                    AND CC.PerConID = @PerConID
            ";

            try
            {
                using var conn = Connection;
                var results = await conn.QueryAsync<T>(new CommandDefinition(
                    sql,
                    new
                    {
                        CustNum = custNum,
                        ConNum = conNum,
                        PerConID = perConID
                    })).ConfigureAwait(false);

                var resultsList = results.ToList();
                await _debugModeService.SqlQueryDebugMessage(sql, resultsList);

                // Validate that only a single record is returned
                if (resultsList.Count > 1)
                {
                    _logger.LogWarning("Multiple records found for CustNum: {CustNum}, ConNum: {ConNum}, PerConID: {PerConID}",
                        custNum, conNum, perConID);
                    throw new InvalidOperationException($"Multiple customer contacts found with the provided identifiers. Expected a single record.");
                }

                return resultsList.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer contact with CustNum: {CustNum}, ConNum: {ConNum}, PerConID: {PerConID}",
                    custNum, conNum, perConID);
                throw;
            }
        }
    }
}