using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Data
{
    public interface IRMAValidationService
    {
        Task<bool> ValidateRMAExistsAsync(int rmaNumber);
        Task<bool> ValidateRMALineExistsAsync(int rmaNumber, int lineNumber);
        Task<RMAValidationResult> ValidateRMAAndLineAsync(int rmaNumber, int? lineNumber = null);
        Task<RMASummaryResponse> GetRMASummariesAsync(RMASummaryQuery query);
        Task<RMASummaryModel?> GetRMASummaryAsync(int rmaNumber);
    }

    public class RMAValidationService : IRMAValidationService
    {
        private readonly string _connectionString;
        private readonly ILogger<RMAValidationService> _logger;
        private readonly DebugModeService _debugModeService;
        private readonly IRMAFileDataService _dataService;

        public RMAValidationService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<RMAValidationService> logger,
            DebugModeService debugModeService,
            IRMAFileDataService dataService)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _debugModeService = debugModeService;
            _dataService = dataService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<bool> ValidateRMAExistsAsync(int rmaNumber)
        {
            try
            {
                using var conn = Connection;
                
                // Check both Epicor and Legacy RMAs
                var epicorSql = "SELECT COUNT(1) FROM vw_EpicorRMAs WHERE RMANum = @RMANumber";
                var legacySql = "SELECT COUNT(1) FROM vw_LegacyIRMAs WHERE RMANum = @RMANumber";
                
                var epicorCount = await conn.QuerySingleAsync<int>(epicorSql, new { RMANumber = rmaNumber });
                if (epicorCount > 0) return true;
                
                var legacyCount = await conn.QuerySingleAsync<int>(legacySql, new { RMANumber = rmaNumber });
                return legacyCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating RMA {RMANumber}", rmaNumber);
                return false;
            }
        }

        public async Task<bool> ValidateRMALineExistsAsync(int rmaNumber, int lineNumber)
        {
            try
            {
                var lines = await GetRmaLinesAsync(rmaNumber);
                return lines.Any(l => l.LineNumber == lineNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating RMA {RMANumber} Line {LineNumber}", rmaNumber, lineNumber);
                return false;
            }
        }

        public async Task<RMAValidationResult> ValidateRMAAndLineAsync(int rmaNumber, int? lineNumber = null)
        {
            var result = new RMAValidationResult
            {
                RMANumber = rmaNumber,
                LineNumber = lineNumber
            };

            try
            {
                // Check if RMA exists in either system
                using var conn = Connection;
                
                var epicorSql = "SELECT COUNT(1) FROM vw_EpicorRMAs WHERE RMANum = @RMANumber";
                var legacySql = "SELECT COUNT(1) FROM vw_LegacyIRMAs WHERE RMANum = @RMANumber";
                
                var epicorCount = await conn.QuerySingleAsync<int>(epicorSql, new { RMANumber = rmaNumber });
                var legacyCount = await conn.QuerySingleAsync<int>(legacySql, new { RMANumber = rmaNumber });
                
                result.RMAExists = epicorCount > 0 || legacyCount > 0;
                result.IsLegacyRMA = legacyCount > 0 && epicorCount == 0;
                
                if (!result.RMAExists)
                {
                    result.ErrorMessage = $"RMA {rmaNumber} does not exist in either Epicor or Legacy systems.";
                    return result;
                }

                if (lineNumber.HasValue)
                {
                    var lines = await GetRmaLinesAsync(rmaNumber);
                    result.AvailableLines = lines.Select(l => l.LineNumber).ToList();
                    result.LineExists = lines.Any(l => l.LineNumber == lineNumber.Value);
                    
                    if (!result.LineExists)
                    {
                        if (result.AvailableLines.Any())
                        {
                            result.ErrorMessage = $"Line {lineNumber} does not exist for RMA {rmaNumber}. Available lines: {string.Join(", ", result.AvailableLines)}";
                        }
                        else
                        {
                            result.ErrorMessage = $"RMA {rmaNumber} has no line items. Use header-level (no line number) instead.";
                        }
                    }
                }
                else
                {
                    result.LineExists = true;
                    var lines = await GetRmaLinesAsync(rmaNumber);
                    result.AvailableLines = lines.Select(l => l.LineNumber).ToList();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating RMA {RMANumber} and Line {LineNumber}", rmaNumber, lineNumber);
                result.ErrorMessage = $"Error validating RMA and line: {ex.Message}";
                return result;
            }
        }

        public async Task<RMASummaryResponse> GetRMASummariesAsync(RMASummaryQuery query)
        {
            var whereConditions = new List<string>();
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(query.RMANumberFilter))
            {
                whereConditions.Add("R.RMANum LIKE @RMANumberFilter");
                parameters.Add("@RMANumberFilter", $"%{query.RMANumberFilter}%");
            }

            if (query.OpenRMAFilter.HasValue)
            {
                whereConditions.Add("R.OpenRMA = @OpenRMAFilter");
                parameters.Add("@OpenRMAFilter", query.OpenRMAFilter.Value);
            }

            if (query.HDCaseNumFilter.HasValue)
            {
                whereConditions.Add("R.HDCaseNum = @HDCaseNumFilter");
                parameters.Add("@HDCaseNumFilter", query.HDCaseNumFilter.Value);
            }

            if (query.RMADateFrom.HasValue)
            {
                whereConditions.Add("R.RMADate >= @RMADateFrom");
                parameters.Add("@RMADateFrom", query.RMADateFrom.Value);
            }

            if (query.RMADateTo.HasValue)
            {
                whereConditions.Add("R.RMADate <= @RMADateTo");
                parameters.Add("@RMADateTo", query.RMADateTo.Value);
            }

            if (query.HasFilesFilter.HasValue)
            {
                if (query.HasFilesFilter.Value)
                {
                    whereConditions.Add("ISNULL(R.FileCount, 0) > 0");
                }
                else
                {
                    whereConditions.Add("ISNULL(R.FileCount, 0) = 0");
                }
            }

            if (!string.IsNullOrEmpty(query.SerialNumberFilter))
            {
                whereConditions.Add("R.SerialNumbers LIKE @SerialNumberFilter");
                parameters.Add("@SerialNumberFilter", $"%{query.SerialNumberFilter}%");
            }

            if (!string.IsNullOrEmpty(query.NotesFilter))
            {
                whereConditions.Add("R.InternalNotes_c LIKE @NotesFilter");
                parameters.Add("@NotesFilter", $"%{query.NotesFilter}%");
            }

            var whereClause = whereConditions.Any()
                ? "WHERE " + string.Join(" AND ", whereConditions)
                : string.Empty;

            var validSortColumns = new Dictionary<string, string>
            {
                { "RMANum", "R.RMANum" },
                { "RMADate", "R.RMADate" },
                { "RMAStatus", "R.OpenRMA" },
                { "HDCaseNum", "R.HDCaseNum" },
                { "FileCount", "R.FileCount" },
                { "SerialNumbers", "R.SerialNumbers" },
                { "IsLegacyRMA", "R.IsLegacyRMA" }
            };

            var sortColumn = validSortColumns.ContainsKey(query.SortBy ?? string.Empty)
                ? validSortColumns[query.SortBy!]
                : "R.RMANum";

            var sortDirection = query.SortDirection?.ToLower() == "asc" ? "ASC" : "DESC";
            var orderByClause = $"ORDER BY {sortColumn} {sortDirection}";

            // FIXED: Determine which view(s) to query based on LegacyRMAFilter
            string unionSql;
            
            if (query.LegacyRMAFilter == null)
            {
                // Default: Epicor only
                unionSql = @"
                    SELECT R.RMANum, R.OpenRMA, R.RMAStatus, R.InternalNotes_c, R.RMADate,
                           R.HDCaseNum, R.CaseDescription, R.FileCount, R.SerialNumbers,
                           0 as IsLegacyRMA
                    FROM dbo.vw_EpicorRMAs R";
            }
            else if (query.LegacyRMAFilter == true)
            {
                // Legacy only
                unionSql = @"
                    SELECT L.RMANum, L.OpenRMA, L.RMAStatus, L.InternalNotes_c, L.RMADate,
                           L.HDCaseNum, L.CaseDescription, L.FileCount, L.SerialNumbers,
                           1 as IsLegacyRMA
                    FROM dbo.vw_LegacyIRMAs L";
            }
            else
            {
                // Both (LegacyRMAFilter == false means show both)
                unionSql = @"
                    SELECT R.RMANum, R.OpenRMA, R.RMAStatus, R.InternalNotes_c, R.RMADate,
                           R.HDCaseNum, R.CaseDescription, R.FileCount, R.SerialNumbers,
                           0 as IsLegacyRMA
                    FROM dbo.vw_EpicorRMAs R
                    UNION ALL
                    SELECT L.RMANum, L.OpenRMA, L.RMAStatus, L.InternalNotes_c, L.RMADate,
                           L.HDCaseNum, L.CaseDescription, L.FileCount, L.SerialNumbers,
                           1 as IsLegacyRMA
                    FROM dbo.vw_LegacyIRMAs L";
            }

            var countSql = $@"
                SELECT COUNT(*)
                FROM ({unionSql}) R
                {whereClause}";

            var dataSql = $@"
                SELECT R.RMANum, R.OpenRMA, R.RMAStatus, R.InternalNotes_c, R.RMADate,
                       R.HDCaseNum, R.CaseDescription, R.FileCount, R.SerialNumbers, R.IsLegacyRMA
                FROM ({unionSql}) R
                {whereClause}
                {orderByClause}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            parameters.Add("@Offset", (query.Page - 1) * query.PageSize);
            parameters.Add("@PageSize", query.PageSize);

            try
            {
                using var conn = Connection;

                var totalCount = await conn.QuerySingleAsync<int>(countSql, parameters);
                var rmas = await conn.QueryAsync<RMASummaryModel>(dataSql, parameters);

                await _debugModeService.SqlQueryDebugMessage(dataSql, rmas);

                return new RMASummaryResponse
                {
                    RMAs = rmas.ToList(),
                    TotalCount = totalCount,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RMA summaries");
                throw;
            }
        }

        public async Task<RMASummaryModel?> GetRMASummaryAsync(int rmaNumber)
        {
            try
            {
                using var conn = Connection;
                
                // Try Epicor first
                var epicorSql = @"
                    SELECT R.RMANum, R.OpenRMA, R.RMAStatus, R.InternalNotes_c, R.RMADate,
                           R.HDCaseNum, R.CaseDescription, R.FileCount, R.SerialNumbers,
                           0 as IsLegacyRMA
                    FROM dbo.vw_EpicorRMAs R
                    WHERE R.RMANum = @RMANumber";

                var result = await conn.QueryFirstOrDefaultAsync<RMASummaryModel>(epicorSql, new { RMANumber = rmaNumber });
                
                if (result != null)
                {
                    await _debugModeService.SqlQueryDebugMessage(epicorSql, result);
                    return result;
                }

                // Try Legacy if not found in Epicor
                var legacySql = @"
                    SELECT L.RMANum, L.OpenRMA, L.RMAStatus, L.InternalNotes_c, L.RMADate,
                           L.HDCaseNum, L.CaseDescription, L.FileCount, L.SerialNumbers,
                           1 as IsLegacyRMA
                    FROM dbo.vw_LegacyIRMAs L
                    WHERE L.RMANum = @RMANumber";

                result = await conn.QueryFirstOrDefaultAsync<RMASummaryModel>(legacySql, new { RMANumber = rmaNumber });
                await _debugModeService.SqlQueryDebugMessage(legacySql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RMA summary for RMA {RMANumber}", rmaNumber);
                throw;
            }
        }

        // Helper method to get RMA lines from appropriate view
        private async Task<List<RmaLineSummary>> GetRmaLinesAsync(int rmaNumber)
        {
            try
            {
                using var conn = Connection;
                
                // Check if it's a legacy RMA first
                var legacyCheckSql = "SELECT COUNT(1) FROM vw_LegacyIRMAs WHERE RMANum = @RMANumber";
                var isLegacy = await conn.QuerySingleAsync<int>(legacyCheckSql, new { RMANumber = rmaNumber }) > 0;
                
                string sql;
                if (isLegacy)
                {
                    sql = @"
                        SELECT 
                            RD.LineNumber    AS LineNumber,
                            RD.PartNum       AS PartNum,
                            RD.ReturnQty     AS ReturnQty,
                            RD.SerialNumber  AS SerialNumber,
                            RD.MaxTranNum    AS MaxTranNum
                        FROM dbo.vw_LegacyIRMALinesAndSerials RD
                        WHERE RD.RMANumber = @RMANumber
                        ORDER BY RD.LineNumber, RD.SerialNumber";
                }
                else
                {
                    sql = @"
                        SELECT 
                            RD.RMALine      AS LineNumber,
                            RD.PartNum      AS PartNum,
                            RD.ReturnQty    AS ReturnQty,
                            RD.SerialNumber AS SerialNumber,
                            RD.MaxTranNum   AS MaxTranNum
                        FROM dbo.vw_EpicorRMALinesAndSerials RD
                        WHERE RD.RMANum = @RMANumber
                        ORDER BY RD.RMALine, RD.MaxTranNum DESC, RD.SerialNumber";
                }

                var rows = await conn.QueryAsync<(int LineNumber, string PartNum, decimal ReturnQty, string? SerialNumber, int? MaxTranNum)>(
                    sql, new { RMANumber = rmaNumber });

                var grouped = rows
                    .GroupBy(r => new { r.LineNumber, r.PartNum, r.ReturnQty })
                    .Select(g => new RmaLineSummary
                    {
                        LineNumber = g.Key.LineNumber,
                        PartNum = g.Key.PartNum,
                        ReturnQty = g.Key.ReturnQty,
                        Serials = g.Where(x => !string.IsNullOrWhiteSpace(x.SerialNumber))
                                   .Select(x => x.SerialNumber!)
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .OrderBy(s => s)
                                   .ToList()
                    })
                    .OrderBy(s => s.LineNumber)
                    .ToList();

                await _debugModeService.SqlQueryDebugMessage(sql, grouped);
                return grouped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RMA lines for RMA {RMANumber}", rmaNumber);
                return new List<RmaLineSummary>();
            }
        }
    }
}