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
    public interface IRMAFileDataService
    {
        Task<List<RMAFileAttachmentDTO>> GetTrackedFilesAsync(RMAFileQuery query);
        Task<List<RMAFileAttachmentLogDTO>> GetFileLogsAsync(int fileAttachmentId);
        Task<RMAFileAttachmentDTO?> GetFileAttachmentAsync(int id);
        Task<RMAFileAttachmentDTO?> GetFileAttachmentByPathAsync(string filePath);
        Task<int> CreateFileAttachmentAsync(RMAFileAttachmentDTO fileAttachment);
        Task<int> CreateFileLogAsync(RMAFileAttachmentLogDTO logEntry);
        Task<List<RMAFileHistoryModel>> GetRMAFileHistoryAsync(RMAFileQuery query);
        Task MarkFileAsDeletedAsync(int id, string deletedByUsername);
        Task UpdateFileAttachmentForOverwriteAsync(int id, long newFileSize, string overwrittenByUsername);
        Task<List<RmaLineSummary>> GetRmaLinesAsync(string rmaNumber);
    }

    public class RMAFileDataService : IRMAFileDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<RMAFileDataService> _logger;
        private readonly DebugModeService _debugModeService;

        // Table Names
        private const string FileAttachmentsTable = "dbo.RMA_FileAttachments";
        private const string FileAttachmentLogsTable = "dbo.RMA_FileAttachmentLogs";
        private const string FileCategoriesTable = "dbo.RMA_FileCategories";

        public RMAFileDataService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<RMAFileDataService> logger,
            DebugModeService debugModeService)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _debugModeService = debugModeService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<List<RMAFileAttachmentDTO>> GetTrackedFilesAsync(RMAFileQuery query)
        {
            var sql = $@"
                SELECT 
                    f.Id, f.RMANumber, f.RMALineNumber, f.FileName, f.FilePath, 
                    f.FileSize, f.CategoryId, f.UploadedByUsername, f.UploadedDate, 
                    f.Deleted, f.DeletedByUsername, f.DeletedDate,
                    c.Id, c.ShortName, c.DisplayValue, c.AcceptedFileTypes, c.IsDetailLevel,
                    c.IsActive, c.CreatedByUsername, c.CreatedDate, c.ModifiedByUsername, c.ModifiedDate
                FROM {FileAttachmentsTable} f
                INNER JOIN {FileCategoriesTable} c ON f.CategoryId = c.Id
                WHERE f.RMANumber = @RMANumber
                  AND f.Deleted = 0
                  AND c.IsActive = 1";

            var parameters = new DynamicParameters();
            parameters.Add("@RMANumber", query.RmaNumber);

            // Apply line number filter if specified
            if (!string.IsNullOrEmpty(query.RmaLineNumber) && int.TryParse(query.RmaLineNumber, out int lineNumber))
            {
                sql += " AND f.RMALineNumber = @RMALineNumber";
                parameters.Add("@RMALineNumber", lineNumber);
            }
            
            // Apply category filter if specified
            if (!string.IsNullOrEmpty(query.CategoryShortName))
            {
                sql += " AND c.ShortName = @CategoryShortName";
                parameters.Add("@CategoryShortName", query.CategoryShortName);
            }
            
            sql += " ORDER BY f.UploadedDate DESC";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<RMAFileAttachmentDTO, RMAFileCategoryDTO, RMAFileAttachmentDTO>(
                    sql, 
                    (file, category) => 
                    {
                        file.Category = category;
                        return file;
                    },
                    parameters,
                    splitOn: "Id");
    
                var files = result.ToList();
                
                // Populate serial numbers from view lookup
                await PopulateSerialNumbersAsync(files);
                
                await _debugModeService.SqlQueryDebugMessage(sql, files);

                _logger.LogInformation("GetTrackedFilesAsync: Returning {FileCount} files with categories and serials populated", files.Count);

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tracked files for RMA {RmaNumber}", query.RmaNumber);
                throw;
            }
        }

        public async Task<List<RMAFileAttachmentLogDTO>> GetFileLogsAsync(int fileAttachmentId)
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<RMAFileAttachmentLogDTO>();
            var sql = $@"
                SELECT {dtoColumns}
                FROM {FileAttachmentLogsTable}
                WHERE FileAttachmentId = @FileAttachmentId
                ORDER BY ActionDate DESC";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<RMAFileAttachmentLogDTO>(sql, new { FileAttachmentId = fileAttachmentId });
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file logs for attachment {FileAttachmentId}", fileAttachmentId);
                throw;
            }
        }

        public async Task<RMAFileAttachmentDTO?> GetFileAttachmentAsync(int id)
        {
            var sql = $@"
                SELECT 
                    f.Id, f.RMANumber, f.RMALineNumber, f.FileName, f.FilePath,
                    f.FileSize, f.CategoryId, f.UploadedByUsername, f.UploadedDate,
                    f.Deleted, f.DeletedByUsername, f.DeletedDate,
                    c.Id as CategoryId, c.ShortName as CategoryShortName, c.DisplayValue as CategoryDisplayValue,
                    c.AcceptedFileTypes as CategoryAcceptedFileTypes, c.IsDetailLevel as CategoryIsDetailLevel
                FROM {FileAttachmentsTable} f
                INNER JOIN {FileCategoriesTable} c ON f.CategoryId = c.Id
                WHERE f.Id = @Id";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<RMAFileAttachmentDTO, RMAFileCategoryDTO, RMAFileAttachmentDTO>(
                    sql,
                    (file, category) =>
                    {
                        file.Category = category;
                        return file;
                    },
                    new { Id = id },
                    splitOn: "CategoryId");
                    
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file attachment {Id}", id);
                throw;
            }
        }

        public async Task<RMAFileAttachmentDTO?> GetFileAttachmentByPathAsync(string filePath)
        {
            var sql = $@"
                SELECT 
                    f.Id, f.RMANumber, f.RMALineNumber, f.FileName, f.FilePath,
                    f.FileSize, f.CategoryId, f.UploadedByUsername, f.UploadedDate,
                    f.Deleted, f.DeletedByUsername, f.DeletedDate,
                    c.Id as CategoryId, c.ShortName as CategoryShortName, c.DisplayValue as CategoryDisplayValue,
                    c.AcceptedFileTypes as CategoryAcceptedFileTypes, c.IsDetailLevel as CategoryIsDetailLevel
                FROM {FileAttachmentsTable} f
                INNER JOIN {FileCategoriesTable} c ON f.CategoryId = c.Id
                WHERE f.FilePath = @FilePath AND f.Deleted = 0";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<RMAFileAttachmentDTO, RMAFileCategoryDTO, RMAFileAttachmentDTO>(
                    sql,
                    (file, category) =>
                    {
                        file.Category = category;
                        return file;
                    },
                    new { FilePath = filePath },
                    splitOn: "CategoryId");
                    
                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file attachment by path {FilePath}", filePath);
                return null;
            }
        }

        public async Task<int> CreateFileAttachmentAsync(RMAFileAttachmentDTO fileAttachment)
        {
            var sql = $@"
                INSERT INTO {FileAttachmentsTable} 
                (RMANumber, RMALineNumber, FileName, FilePath, FileSize, 
                 CategoryId, UploadedByUsername, UploadedDate)
                OUTPUT INSERTED.Id
                VALUES (@RMANumber, @RMALineNumber, @FileName, @FilePath, @FileSize,
                        @CategoryId, @UploadedByUsername, @UploadedDate)";

            try
            {
                using var conn = Connection;
                var newId = await conn.ExecuteScalarAsync<int>(sql, fileAttachment);
                await _debugModeService.SqlQueryDebugMessage(sql, newId);
                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating file attachment for {FileName}", fileAttachment.FileName);
                throw;
            }
        }

        public async Task<int> CreateFileLogAsync(RMAFileAttachmentLogDTO logEntry)
        {
            var sql = $@"
                INSERT INTO {FileAttachmentLogsTable} 
                (FileAttachmentId, Action, ActionDetails, ActionByUsername, ActionDate, IsSystemAction)
                OUTPUT INSERTED.Id
                VALUES (@FileAttachmentId, @Action, @ActionDetails, @ActionByUsername, @ActionDate, @IsSystemAction)";

            try
            {
                using var conn = Connection;
                var newId = await conn.ExecuteScalarAsync<int>(sql, logEntry);
                await _debugModeService.SqlQueryDebugMessage(sql, newId);
                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating file log for attachment {FileAttachmentId}", logEntry.FileAttachmentId);
                throw;
            }
        }

        public async Task<List<RMAFileHistoryModel>> GetRMAFileHistoryAsync(RMAFileQuery query)
        {
            if (!int.TryParse(query.RmaNumber, out int rmaNumber))
                return new List<RMAFileHistoryModel>();

            var sql = $@"
                SELECT 
                    l.Id as LogId,
                    l.FileAttachmentId,
                    f.FileName,
                    c.DisplayValue as CategoryDisplayValue,
                    f.RMALineNumber,
                    l.Action,
                    l.ActionDetails,
                    l.ActionByUsername,
                    l.ActionDate,
                    l.IsSystemAction,
                    f.FileSize
                FROM {FileAttachmentLogsTable} l
                INNER JOIN {FileAttachmentsTable} f ON l.FileAttachmentId = f.Id
                INNER JOIN {FileCategoriesTable} c ON f.CategoryId = c.Id
                WHERE f.RMANumber = @RMANumber";

            var parameters = new DynamicParameters();
            parameters.Add("@RMANumber", rmaNumber);

            if (!string.IsNullOrEmpty(query.RmaLineNumber) && int.TryParse(query.RmaLineNumber, out int lineNumber))
            {
                sql += " AND f.RMALineNumber = @RMALineNumber";
                parameters.Add("@RMALineNumber", lineNumber);
            }

            if (!string.IsNullOrEmpty(query.CategoryShortName))
            {
                sql += " AND c.ShortName = @CategoryShortName";
                parameters.Add("@CategoryShortName", query.CategoryShortName);
            }

            sql += " ORDER BY l.ActionDate DESC";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<RMAFileHistoryModel>(sql, parameters);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RMA file history for RMA {RmaNumber}", query.RmaNumber);
                throw;
            }
        }

        public async Task MarkFileAsDeletedAsync(int id, string deletedByUsername)
        {
            var sql = $@"
                UPDATE {FileAttachmentsTable}
                SET Deleted = 1, DeletedByUsername = @DeletedByUsername, DeletedDate = @DeletedDate
                WHERE Id = @Id";

            try
            {
                using var conn = Connection;
                await conn.ExecuteAsync(sql, new 
                { 
                    Id = id, 
                    DeletedByUsername = deletedByUsername, 
                    DeletedDate = DateTime.UtcNow 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking file attachment {Id} as deleted", id);
                throw;
            }
        }

        public async Task UpdateFileAttachmentForOverwriteAsync(int id, long newFileSize, string overwrittenByUsername)
        {
            var sql = $@"
                UPDATE {FileAttachmentsTable}
                SET FileSize = @FileSize,
                    UploadedByUsername = @UploadedByUsername,
                    UploadedDate = @UploadedDate
                WHERE Id = @Id";

            try
            {
                using var conn = Connection;
                await conn.ExecuteAsync(sql, new 
                { 
                    Id = id,
                    FileSize = newFileSize,
                    UploadedByUsername = overwrittenByUsername,
                    UploadedDate = DateTime.UtcNow
                });
                
                _logger.LogInformation("Updated file attachment {Id} for overwrite by {Username}", id, overwrittenByUsername);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file attachment {Id} for overwrite", id);
                throw;
            }
        }

        public async Task<List<RmaLineSummary>> GetRmaLinesAsync(string rmaNumber)
        {
            if (!int.TryParse(rmaNumber, out var rmaNumInt))
                return new List<RmaLineSummary>();

            try
            {
                using var conn = Connection;
                
                // Check if it's a legacy RMA first
                var legacyCheckSql = "SELECT COUNT(1) FROM vw_LegacyIRMAs WHERE RMANum = @RMANumber";
                var isLegacy = await conn.QuerySingleAsync<int>(legacyCheckSql, new { RMANumber = rmaNumInt }) > 0;
                
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
                        WHERE RD.RMANumber = @RMANum
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
                        WHERE RD.RMANum = @RMANum
                        ORDER BY RD.RMALine, RD.MaxTranNum DESC, RD.SerialNumber";
                }

                var rows = await conn.QueryAsync<(int LineNumber, string PartNum, decimal ReturnQty, string? SerialNumber, int? MaxTranNum)>(
                    sql, new { RMANum = rmaNumInt });

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
                _logger.LogError(ex, "Error fetching RMA lines for RMA {RmaNumber}", rmaNumber);
                return new List<RmaLineSummary>();
            }
        }

        // Method to populate serial numbers from SQL view
        private async Task PopulateSerialNumbersAsync(List<RMAFileAttachmentDTO> files)
        {
            if (!files.Any()) return;

            try
            {
                // Group files by RMA + Line to minimize view queries
                var rmaLineGroups = files
                    .GroupBy(f => new { f.RMANumber, f.RMALineNumber })
                    .ToList();

                foreach (var group in rmaLineGroups)
                {
                    var serials = new List<string>();
                    
                    if (group.Key.RMALineNumber.HasValue)
                    {
                        // Get serials for specific line from appropriate view (Epicor or Legacy)
                        var rmaLines = await GetRmaLinesAsync(group.Key.RMANumber.ToString());
                        var lineInfo = rmaLines.FirstOrDefault(l => l.LineNumber == group.Key.RMALineNumber.Value);
                        if (lineInfo != null)
                        {
                            serials = lineInfo.Serials;
                        }
                    }
                    // Header-level files (no line number) don't have associated serials
                    
                    // Apply serials to all files in this RMA + Line group
                    foreach (var file in group)
                    {
                        file.SerialNumbers = new List<string>(serials);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating serial numbers for files");
                // Don't throw - files can still be displayed without serials
            }
        }
    }
}