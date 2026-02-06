using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.SharedRCL.Data;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using CrystalGroupHome.SharedRCL.Helpers;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Data
{
    public interface IRMAFileCategoryService
    {
        Task<List<RMAFileCategoryDTO>> GetFileCategoriesAsync(bool? isDetailLevel = null);
        Task<RMAFileCategoryDTO?> GetFileCategoryAsync(int categoryId);
        Task<RMAFileCategoryDTO?> GetFileCategoryByShortNameAsync(string shortName, bool isDetailLevel);
        Task<int> CreateFileCategoryAsync(RMAFileCategoryDTO category);
        Task<bool> UpdateFileCategoryAsync(RMAFileCategoryDTO category);
        Task<bool> DeleteFileCategoryAsync(int categoryId);
        Task<List<FileCategory>> GetAvailableCategoriesAsync(bool isDetailLevel);
    }

    public class RMAFileCategoryService : IRMAFileCategoryService
    {
        private readonly string _connectionString;
        private readonly ILogger<RMAFileCategoryService> _logger;
        private readonly DebugModeService _debugModeService;

        private readonly IHttpContextAccessor _httpContextAccessor;

        // Table Names
        private const string FileCategoriesTable = "dbo.RMA_FileCategories";

        public RMAFileCategoryService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<RMAFileCategoryService> logger,
            DebugModeService debugModeService,
            IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _debugModeService = debugModeService;
            _httpContextAccessor = httpContextAccessor;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<List<RMAFileCategoryDTO>> GetFileCategoriesAsync(bool? isDetailLevel = null)
        {
            var sql = $@"
                SELECT Id, ShortName, DisplayValue, AcceptedFileTypes, IsDetailLevel, IsActive,
                       CreatedByUsername, CreatedDate, ModifiedByUsername, ModifiedDate
                FROM {FileCategoriesTable}
                WHERE IsActive = 1";
            
            var parameters = new DynamicParameters();
            
            if (isDetailLevel.HasValue)
            {
                sql += " AND IsDetailLevel = @IsDetailLevel";
                parameters.Add("@IsDetailLevel", isDetailLevel.Value);
            }
            
            sql += " ORDER BY DisplayValue";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<RMAFileCategoryDTO>(sql, parameters);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file categories");
                throw;
            }
        }

        public async Task<RMAFileCategoryDTO?> GetFileCategoryAsync(int categoryId)
        {
            var sql = $@"
                SELECT Id, ShortName, DisplayValue, AcceptedFileTypes, IsDetailLevel, IsActive,
                       CreatedByUsername, CreatedDate, ModifiedByUsername, ModifiedDate
                FROM {FileCategoriesTable}
                WHERE Id = @CategoryId AND IsActive = 1";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<RMAFileCategoryDTO>(sql, new { CategoryId = categoryId });
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file category {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<RMAFileCategoryDTO?> GetFileCategoryByShortNameAsync(string shortName, bool isDetailLevel)
        {
            var sql = $@"
                SELECT Id, ShortName, DisplayValue, AcceptedFileTypes, IsDetailLevel, IsActive,
                       CreatedByUsername, CreatedDate, ModifiedByUsername, ModifiedDate
                FROM {FileCategoriesTable}
                WHERE ShortName = @ShortName AND IsDetailLevel = @IsDetailLevel AND IsActive = 1";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<RMAFileCategoryDTO>(sql, new { ShortName = shortName, IsDetailLevel = isDetailLevel });
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file category by short name {ShortName}", shortName);
                throw;
            }
        }

        public async Task<int> CreateFileCategoryAsync(RMAFileCategoryDTO category)
        {
            var sql = $@"
                INSERT INTO {FileCategoriesTable} 
                (ShortName, DisplayValue, AcceptedFileTypes, IsDetailLevel, IsActive, CreatedByUsername, CreatedDate)
                OUTPUT INSERTED.Id
                VALUES (@ShortName, @DisplayValue, @AcceptedFileTypes, @IsDetailLevel, @IsActive, @CreatedByUsername, @CreatedDate)";

            try
            {
                using var conn = Connection;
                var newId = await conn.ExecuteScalarAsync<int>(sql, category);
                await _debugModeService.SqlQueryDebugMessage(sql, newId);
                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating file category {ShortName}", category.ShortName);
                throw;
            }
        }

        public async Task<bool> UpdateFileCategoryAsync(RMAFileCategoryDTO category)
        {
            var sql = $@"
                UPDATE {FileCategoriesTable}
                SET DisplayValue = @DisplayValue,
                    AcceptedFileTypes = @AcceptedFileTypes,
                    IsDetailLevel = @IsDetailLevel,
                    IsActive = @IsActive,
                    ModifiedByUsername = @ModifiedByUsername,
                    ModifiedDate = @ModifiedDate
                WHERE Id = @Id";

            try
            {
                using var conn = Connection;
                var rowsAffected = await conn.ExecuteAsync(sql, category);
                await _debugModeService.SqlQueryDebugMessage(sql, rowsAffected);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file category {Id}", category.Id);
                throw;
            }
        }

        public async Task<bool> DeleteFileCategoryAsync(int categoryId)
        {
            var sql = $@"
                UPDATE {FileCategoriesTable}
                SET IsActive = 0,
                    ModifiedByUsername = @ModifiedByUsername,
                    ModifiedDate = @ModifiedDate
                WHERE Id = @CategoryId";

            try
            {
                using var conn = Connection;
                var rowsAffected = await conn.ExecuteAsync(sql, new 
                { 
                    CategoryId = categoryId,
                    ModifiedByUsername = GetCurrentUsername(),
                    ModifiedDate = DateTime.UtcNow 
                });
                await _debugModeService.SqlQueryDebugMessage(sql, rowsAffected);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file category {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<List<FileCategory>> GetAvailableCategoriesAsync(bool isDetailLevel)
        {
            var dbCategories = await GetFileCategoriesAsync(isDetailLevel);
            
            return dbCategories.Select(c => new FileCategory
            {
                ShortName = c.ShortName,
                DisplayValue = c.DisplayValue,
                AcceptedFileTypes = c.AcceptedFileTypes,
                IsDetailLevel = c.IsDetailLevel  // ENSURE this is set from database
            }).ToList();
        }

        private string GetCurrentUsername()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var username = user.Identity.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    // Remove domain prefix if present (e.g., "DOMAIN\username" -> "username")
                    return username.Contains('\\') 
                        ? username.Split('\\').Last() 
                        : username;
                }
            }
            
            return "SYSTEM"; // Fallback for system operations
        }
    }
}