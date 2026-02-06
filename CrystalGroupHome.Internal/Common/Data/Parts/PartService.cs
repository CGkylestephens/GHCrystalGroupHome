using CrystalGroupHome.Internal.Common.Data._Epicor;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text;

namespace CrystalGroupHome.Internal.Common.Data.Parts
{
    public interface IPartService
    {
        Task<List<T>> GetPartsAsync<T>(bool includeInactive, bool cmOnly);
        Task<PaginatedResult<T>> GetPartsPaginatedAsync<T>(int pageNumber, int pageSize);
        Task<List<T>> GetPartsByPartNumbersAsync<T>(IEnumerable<string> partNumbers, bool includeInactive = false, bool cmOnly = false);

        Task<List<T>> GetPartsIndentedWhereUsedByPartNumAsync<T>(string partNum, bool includeInactive, bool filterCmManaged, bool filterNotCmManaged = false);

        Task<PartActivityDTO?> GetPartActivityAsync(string partNum);

        Task<bool> UpdatePartAsync<T>(T partDto) where T : PartDTO_Base;
    }

    public class PartService : IPartService
    {
        private readonly string _connectionString;
        private readonly ILogger<PartService> _logger;
        private readonly DebugModeService _debugModeService;
        private readonly IEpicorPartService _epicorPartService;

        // Table Stuff
        private const string PartTable = "dbo.Part";
        private const string QuoteDtlTable = "Erp.QuoteDtl";
        private const string QuoteHedTable = "Erp.QuoteHed";
        private const string InvcDtlTable = "Erp.InvcDtl";
        private const string PartRevFunction = "CGI.dbo.f_EpicorCurrentPartRevs";
        private const string IndentedWhereUsedFunction = "CGI.dbo.f_IndentedWhereUsed_CTE";

        // Company Stuff
        private const string CompanyEqualsCG = "Company = 'CG'";

        public PartService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<PartService> logger,
            DebugModeService debugModeService,
            IEpicorPartService epicorPartService)
        {
            _connectionString = dbOptions.Value.KineticErpConnection;
            _logger = logger;
            _debugModeService = debugModeService;
            _epicorPartService = epicorPartService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        /// <summary>
        /// Constructs a SQL query for parts, optionally filtering by part number(s) and applying pagination.
        /// </summary>
        /// <typeparam name="T">The DTO type to map the results to.</typeparam>
        /// <param name="partNumbers">An optional collection of part numbers to filter by.</param>
        /// <param name="offset">The number of rows to skip (for pagination).</param>
        /// <param name="pageSize">The maximum number of rows to return (for pagination).</param>
        /// <param name="includeInactive">Whether to include inactive/deprecated parts.</param>
        /// <param name="filterCmEligible">Whether to filter for parts that are eligible for configuration management.</param>
        /// <param name="filterCmManaged">Whether to filter for parts that are flagged as CM managed.</param>
        /// <returns>A SQL query string. If pagination is used, it includes a second query for the total count, separated by a semicolon.</returns>
        private static string BuildPartSql<T>(
            IEnumerable<string>? partNumbers = null,
            int? offset = null,
            int? pageSize = null,
            bool includeInactive = false,
            bool filterCmEligible = false,
            bool filterCmManaged = false)
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var baseSqlBuilder = new StringBuilder();

            baseSqlBuilder.Append($@"
                FROM {PartTable} P
                LEFT JOIN {PartRevFunction}() PR ON P.PartNum = PR.PartNum
                WHERE P.{CompanyEqualsCG}");

            if (filterCmEligible)
            {
                baseSqlBuilder.Append(@" AND
                    (
                        P.CommodityCode_c IN ('C','CMS','TPL','CTL') -- Check specific commodity codes
                        OR (P.PartNum LIKE 'C1%' OR P.PartNum LIKE 'CMS-%' OR P.PartNum LIKE 'TPL-%' OR P.PartNum LIKE 'CTL-%') -- Check part number prefixes
                        OR (P.CM_CMManaged_c = 1) -- Check specific flag
                    )
                ");
            }

            if (filterCmManaged)
            {
                baseSqlBuilder.Append(@" AND P.CM_CMManaged_c = 1");
            }

            if (!includeInactive)
            {
                baseSqlBuilder.Append(" AND P.Deprecated_c <> 1 AND P.InActive <> 1 ");
            }

            if (partNumbers != null && partNumbers.Any())
            {
                baseSqlBuilder.Append($" AND P.PartNum IN @PartNumbers ");
            }

            var fromAndWhereClause = baseSqlBuilder.ToString();

            // --- Build the final SQL string(s) ---
            var finalSqlBuilder = new StringBuilder();

            // 1. Build the main data retrieval query
            finalSqlBuilder.Append($"SELECT {dtoColumns}");
            finalSqlBuilder.Append(fromAndWhereClause); // Add the FROM/WHERE clauses
            finalSqlBuilder.Append(" ORDER BY P.PartNum "); // Add ordering

            // 2. Add pagination and count query if needed
            if (offset.HasValue && pageSize.HasValue)
            {
                // Append pagination clauses (OFFSET/FETCH) to the main query
                finalSqlBuilder.Append($" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");

                // Append the total count query, separated by a semicolon for QueryMultiple
                finalSqlBuilder.Append("; ");
                finalSqlBuilder.Append($"SELECT COUNT(*)");
                finalSqlBuilder.Append(fromAndWhereClause);
            }

            // Terminate the SQL statement(s)
            finalSqlBuilder.Append(';');

            return finalSqlBuilder.ToString();
        }

        /// <summary>
        /// Retrieves a single part by its part number.
        /// </summary>
        public async Task<T?> GetPartByPartNumAsync<T>(string partNum)
        {
            if (string.IsNullOrWhiteSpace(partNum))
            {
                _logger.LogWarning("GetPartByPartNumAsync called with null or empty partNum.");
                return default;
            }

            var partNumList = new List<string> { partNum };
            var sql = BuildPartSql<T>(partNumbers: partNumList);

            try
            {
                using var conn = Connection;
                var parameters = new { PartNumbers = partNumList };
                var result = await conn.QueryFirstOrDefaultAsync<T>(sql, parameters);

                await _debugModeService.SqlQueryDebugMessage(sql.Replace("@PartNumbers", $"('{partNum}')"), result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching part with PartNum: {PartNum}. Query: {Query}", partNum, sql);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a list of all parts, optionally filtering.
        /// </summary>
        public async Task<List<T>> GetPartsAsync<T>(bool includeInactive, bool filterCmEligible)
        {
            var sql = BuildPartSql<T>(includeInactive: includeInactive, filterCmEligible: filterCmEligible);

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching parts. IncludeInactive: {IncludeInactive}, FilterCmEligible: {FilterCmEligible}. Query: {Query}", includeInactive, filterCmEligible, sql);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a paginated list of parts.
        /// </summary>
        public async Task<PaginatedResult<T>> GetPartsPaginatedAsync<T>(int pageNumber, int pageSize)
        {
            // Validate pagination parameters
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var offset = (pageNumber - 1) * pageSize;
            var sql = BuildPartSql<T>(offset: offset, pageSize: pageSize, includeInactive: false, filterCmEligible: false);

            try
            {
                using var conn = Connection;
                var parameters = new { Offset = offset, PageSize = pageSize };

                using var multi = await conn.QueryMultipleAsync(sql, parameters);

                var items = (await multi.ReadAsync<T>()).ToList();
                var totalRecords = await multi.ReadFirstAsync<int>();

                await _debugModeService.SqlQueryDebugMessage(sql, items);

                // Return the paginated result object
                return new PaginatedResult<T>
                {
                    Items = items,
                    TotalRecords = totalRecords
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated parts. PageNumber: {PageNumber}, PageSize: {PageSize}. Query: {Query}", pageNumber, pageSize, sql);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a list of parts based on a collection of part numbers.
        /// </summary>
        public async Task<List<T>> GetPartsByPartNumbersAsync<T>(IEnumerable<string> partNumbers, bool includeInactive = false, bool filterCmEligible = false)
        {
            if (partNumbers == null || !partNumbers.Any())
            {
                _logger.LogInformation("GetPartsByPartNumbersAsync called with empty or null list.");
                return new List<T>();
            }

            var distinctPartNumbers = partNumbers.Where(pn => !string.IsNullOrWhiteSpace(pn)).Distinct().ToList();
            if (distinctPartNumbers.Count == 0)
            {
                _logger.LogInformation("GetPartsByPartNumbersAsync called with list containing only null/empty strings.");
                return new List<T>();
            }

            // Handle large lists by batching to avoid SQL Server's 2100 parameter limit
            const int batchSize = 2000;
            var allResults = new List<T>();

            if (distinctPartNumbers.Count > batchSize)
            {
                // Process in batches
                for (int i = 0; i < distinctPartNumbers.Count; i += batchSize)
                {
                    var batchPartNumbers = distinctPartNumbers.Skip(i).Take(batchSize).ToList();
                    var sql = BuildPartSql<T>(partNumbers: batchPartNumbers, includeInactive: includeInactive, filterCmEligible: filterCmEligible);

                    try
                    {
                        using var conn = Connection;
                        var parameters = new { PartNumbers = batchPartNumbers };
                        var batchResult = await conn.QueryAsync<T>(sql, parameters);
                        allResults.AddRange(batchResult);

                        await _debugModeService.SqlQueryDebugMessage(
                            sql.Replace("@PartNumbers", $"('{string.Join("','", batchPartNumbers)}')"),
                            batchResult);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching parts by part numbers batch. Batch size: {BatchSize}, IncludeInactive: {IncludeInactive}, FilterCmEligible: {FilterCmEligible}. Query: {Query}",
                            batchPartNumbers.Count, includeInactive, filterCmEligible, sql);
                        throw;
                    }
                }
            }
            else
            {
                // Process as single batch (original logic)
                var sql = BuildPartSql<T>(partNumbers: distinctPartNumbers, includeInactive: includeInactive, filterCmEligible: filterCmEligible);

                try
                {
                    using var conn = Connection;
                    var parameters = new { PartNumbers = distinctPartNumbers };
                    var result = await conn.QueryAsync<T>(sql, parameters);
                    allResults.AddRange(result);

                    await _debugModeService.SqlQueryDebugMessage(
                        sql.Replace("@PartNumbers", $"('{string.Join("','", distinctPartNumbers)}')"),
                        result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching parts by part numbers. Count: {Count}, IncludeInactive: {IncludeInactive}, FilterCmEligible: {FilterCmEligible}. Query: {Query}",
                        distinctPartNumbers.Count, includeInactive, filterCmEligible, sql);
                    throw;
                }
            }

            return allResults;
        }

        /// <summary>
        /// Constructs a SQL query for parts, optionally filtering by part number(s) and applying pagination.
        /// </summary>
        /// <typeparam name="T">The DTO type to map the results to.</typeparam>
        /// <param name="partNum">The Part Number to search for.</param>
        /// <param name="includeInactive">Whether to include inactive/deprecated parts in the resulting Where Used.</param>
        /// <param name="filterCmManaged">Whether to filter for only parts that are eligible for configuration management.</param>
        /// <param name="filterNotCmManaged">Whether to filter for only parts that are NOT eligible for configuration management.</param>
        /// <param name="filterCmManaged">Whether to filter for parts that are flagged as CM managed.</param>
        /// <returns>A SQL query string.</returns>
        private static string BuildIndentedWhereUsedSql<T>(string partNum, bool includeInactive = false, bool filterCmManaged = false, bool filterNotCmManaged = false)
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var sqlBuilder = new StringBuilder();

            // Centralized CM group condition for reuse
            const string cmCondition = @"P.CM_CMManaged_c = 1";

            sqlBuilder.Append($@"
                FROM {IndentedWhereUsedFunction}('{partNum}', GETDATE(), 0) I
                LEFT JOIN {PartTable} P ON I.PartNum = P.PartNum
                LEFT JOIN {PartRevFunction}() PR ON P.PartNum = PR.PartNum
                WHERE P.{CompanyEqualsCG}
                    AND I.QtyPer > 0
            ");

            if (filterCmManaged)
            {
                sqlBuilder.Append($@" AND {cmCondition} ");
            }
            else if (filterNotCmManaged)
            {
                sqlBuilder.Append($@" AND NOT {cmCondition} ");
            }

            if (!includeInactive)
            {
                sqlBuilder.Append(" AND P.Deprecated_c <> 1 AND P.InActive <> 1 ");
            }

            var finalSqlBuilder = new StringBuilder();
            finalSqlBuilder.Append("SELECT ");
            finalSqlBuilder.Append(dtoColumns);

            finalSqlBuilder.Append($@",
                CASE 
                    WHEN {cmCondition} THEN 1
                    ELSE 0
                END AS CmEligible
            ");

            finalSqlBuilder.Append(sqlBuilder.ToString());

            return finalSqlBuilder.ToString();
        }


        /// <summary>
        /// Retrieves a list of parts where the searched part number is used.
        /// </summary>
        public async Task<List<T>> GetPartsIndentedWhereUsedByPartNumAsync<T>(string partNum, bool includeInactive = false, bool filterCmManaged = false, bool filterNotCmManaged = false)
        {
            var sql = BuildIndentedWhereUsedSql<T>(partNum, includeInactive, filterCmManaged, filterNotCmManaged);

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching indented where used parts. PartNum: {PartNum}, IncludeInactive: {IncludeInactive}, CmOnly: {CmOnly}. Query: {Query}", partNum, includeInactive, filterCmManaged, sql);
                throw;
            }
        }

        public async Task<PartActivityDTO?> GetPartActivityAsync(string partNum)
        {
            if (string.IsNullOrWhiteSpace(partNum))
            {
                _logger.LogWarning("GetPartActivityAsync called with null or empty partNum.");
                return null;
            }

            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append($@"
                SELECT 
                    Part.PartNum AS PartNum,
                    MAX(InvcDtl.ShipDate) AS LastSold,
                    MAX(QuoteHed.DateQuoted) AS LastQuoted
                FROM {PartTable} AS Part
                LEFT JOIN {QuoteDtlTable} AS QuoteDtl ON 
                    Part.Company = QuoteDtl.Company
                    AND Part.PartNum = QuoteDtl.PartNum
                LEFT JOIN {QuoteHedTable} AS QuoteHed ON 
                    QuoteDtl.Company = QuoteHed.Company
                    AND QuoteDtl.QuoteNum = QuoteHed.QuoteNum
                    AND QuoteHed.Quoted = 1
                LEFT JOIN {InvcDtlTable} AS InvcDtl ON 
                    Part.Company = InvcDtl.Company
                    AND Part.PartNum = InvcDtl.PartNum
                WHERE Part.{CompanyEqualsCG}
                  AND Part.PartNum = @PartNum
                GROUP BY Part.PartNum
            ");

            var sql = sqlBuilder.ToString();
            var parameters = new { PartNum = partNum };

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<PartActivityDTO>(sql, parameters);

                await _debugModeService.SqlQueryDebugMessage(
                    sql.Replace("@PartNum", $"'{partNum}'"), result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching part activity. PartNum: {PartNum}. Query: {Query}", partNum, sql);
                throw;
            }
        }

        /// <summary>
        /// Updates a part in the Epicor database using the Epicor API.
        /// </summary>
        /// <typeparam name="T">The PartDTO type to update.</typeparam>
        /// <param name="partDto">The PartDTO containing the updated values.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        public async Task<bool> UpdatePartAsync<T>(T partDto) where T : PartDTO_Base
        {
            if (partDto == null)
            {
                _logger.LogWarning("UpdatePartAsync called with null partDto.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(partDto.PartNum))
            {
                _logger.LogWarning("UpdatePartAsync called with null or empty PartNum.");
                return false;
            }

            try
            {
                // Use the generic Epicor update method that handles all properties automatically
                var success = await _epicorPartService.UpdatePartAsync(partDto);

                if (success)
                {
                    _logger.LogInformation("Successfully updated part {PartNum} in Epicor.", partDto.PartNum);
                }
                else
                {
                    _logger.LogWarning("Failed to update part {PartNum} in Epicor.", partDto.PartNum);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating part {PartNum} in Epicor.", partDto.PartNum);
                return false;
            }
        }
    }
}
