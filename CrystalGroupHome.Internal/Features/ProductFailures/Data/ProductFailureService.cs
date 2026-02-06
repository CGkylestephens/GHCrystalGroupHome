using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using CrystalGroupHome.SharedRCL.Data;
using Microsoft.Extensions.Options;

namespace CrystalGroupHome.Internal.Features.ProductFailures.Data
{
    public interface IProductFailureService
    {
        Task<int> InsertAsync(ProductFailureDTO entry);
        Task UpdateAsync(ProductFailureDTO entry);
        Task DeleteAsync(int id);
        Task<List<ProductFailureDTO>> GetAllAsync();
        Task<ProductFailureDTO?> GetByIdAsync(int id);
    }

    public class ProductFailureService : IProductFailureService
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductFailureService> _logger;

        public ProductFailureService(IOptions<DatabaseOptions> dbOptions, ILogger<ProductFailureService> logger)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<int> InsertAsync(ProductFailureDTO entry)
        {
            var sql = "EXEC [dbo].[TEST_ProductFailureLog_Insert] @ProductId, @Failures, @TotalTested, @EnteredBy";
            using var conn = Connection;
            var result = await conn.QuerySingleAsync<int>(sql, new
            {
                entry.ProductId,
                entry.Failures,
                entry.TotalTested,
                entry.EnteredBy
            });
            return result;
        }

        public async Task UpdateAsync(ProductFailureDTO entry)
        {
            var sql = "EXEC [dbo].[TEST_ProductFailureLog_Update] @Id, @ProductId, @Failures, @TotalTested, @EnteredBy";
            using var conn = Connection;
            await conn.ExecuteAsync(sql, new
            {
                entry.Id,
                entry.ProductId,
                entry.Failures,
                entry.TotalTested,
                entry.EnteredBy
            });
        }

        public async Task DeleteAsync(int id)
        {
            var sql = "EXEC [dbo].[TEST_ProductFailureLog_Delete] @Id";
            using var conn = Connection;
            await conn.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<List<ProductFailureDTO>> GetAllAsync()
        {
            var sql = "EXEC [dbo].[TEST_ProductFailureLog_Select]";
            using var conn = Connection;
            var result = await conn.QueryAsync<ProductFailureDTO>(sql);
            return result.ToList();
        }

        public async Task<ProductFailureDTO?> GetByIdAsync(int id)
        {
            var sql = "EXEC [dbo].[TEST_ProductFailureLog_Select] @Id";
            using var conn = Connection;
            return await conn.QueryFirstOrDefaultAsync<ProductFailureDTO>(sql, new { Id = id });
        }
    }
}
