using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using CrystalGroupHome.SharedRCL.Data;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Data
{
    public interface ITempFileService
    {
        Task<string> CreateTempFileTokenAsync(string filePath, string originalFileName, TimeSpan? expiration = null);
        Task<(string? filePath, string? originalFileName)> GetTempFileByTokenAsync(string token);
        Task<bool> DeleteTempFileAsync(string token);
        Task<int> CleanupExpiredFilesAsync();
    }
    public class TempFileService : ITempFileService
    {
        private readonly string _connectionString;
        private readonly ILogger<TempFileService> _logger;
        private const string TempFilesTable = "dbo.TempFileTokens";

        public TempFileService(IOptions<DatabaseOptions> dbOptions, ILogger<TempFileService> logger)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
        }

        public async Task<string> CreateTempFileTokenAsync(string filePath, string originalFileName, TimeSpan? expiration = null)
        {
            var token = Guid.NewGuid().ToString("N"); // 32-character token without hyphens
            var expiryTime = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(2)); // Default 2 hour expiration

            var sql = $@"
                INSERT INTO {TempFilesTable} 
                (Token, FilePath, OriginalFileName, CreatedDate, ExpiryDate)
                VALUES (@Token, @FilePath, @OriginalFileName, @CreatedDate, @ExpiryDate)";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.ExecuteAsync(sql, new
                {
                    Token = token,
                    FilePath = filePath,
                    OriginalFileName = originalFileName,
                    CreatedDate = DateTime.UtcNow,
                    ExpiryDate = expiryTime
                });

                _logger.LogInformation("Created temp file token {Token} for file {FileName}, expires at {ExpiryDate}", 
                    token, originalFileName, expiryTime);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating temp file token for {FilePath}", filePath);
                throw;
            }
        }

        public async Task<(string? filePath, string? originalFileName)> GetTempFileByTokenAsync(string token)
        {
            var sql = $@"
                SELECT FilePath, OriginalFileName 
                FROM {TempFilesTable}
                WHERE Token = @Token 
                  AND ExpiryDate > @CurrentTime
                  AND Deleted = 0";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QueryFirstOrDefaultAsync<(string FilePath, string OriginalFileName)>(
                    sql, new { Token = token, CurrentTime = DateTime.UtcNow });

                return (result.FilePath, result.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving temp file for token {Token}", token);
                return (null, null);
            }
        }

        public async Task<bool> DeleteTempFileAsync(string token)
        {
            var sql = $@"
                UPDATE {TempFilesTable}
                SET Deleted = 1, DeletedDate = @DeletedDate
                WHERE Token = @Token";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // First get the file path to delete the physical file
                var (filePath, _) = await GetTempFileByTokenAsync(token);
                
                // Mark as deleted in database
                var rowsAffected = await connection.ExecuteAsync(sql, new 
                { 
                    Token = token, 
                    DeletedDate = DateTime.UtcNow 
                });

                // Delete physical file if it exists
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        _logger.LogInformation("Deleted temp file {FilePath} for token {Token}", filePath, token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete physical temp file {FilePath}", filePath);
                    }
                }

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting temp file for token {Token}", token);
                return false;
            }
        }

        public async Task<int> CleanupExpiredFilesAsync()
        {
            var sql = $@"
                SELECT Token, FilePath 
                FROM {TempFilesTable}
                WHERE (ExpiryDate < @CurrentTime OR Deleted = 1)
                  AND PhysicalFileDeleted = 0";

            var updateSql = $@"
                UPDATE {TempFilesTable}
                SET PhysicalFileDeleted = 1
                WHERE Token = @Token";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                var expiredFiles = await connection.QueryAsync<(string Token, string FilePath)>(
                    sql, new { CurrentTime = DateTime.UtcNow });

                var cleanupCount = 0;
                foreach (var (token, filePath) in expiredFiles)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            cleanupCount++;
                        }

                        // Mark as physically deleted
                        await connection.ExecuteAsync(updateSql, new { Token = token });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete expired temp file {FilePath}", filePath);
                    }
                }

                if (cleanupCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired temp files", cleanupCount);
                }

                return cleanupCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temp file cleanup");
                return 0;
            }
        }
    }
}