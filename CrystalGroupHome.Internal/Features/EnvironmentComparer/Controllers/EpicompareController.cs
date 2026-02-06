using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Dapper;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.Internal.Features.EnvironmentComparer.Data;
using System.Data;

namespace CrystalGroupHome.Internal.Common.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EpicompareController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<EpicompareController> _logger;

        public EpicompareController(IOptions<DatabaseOptions> dbOptions, ILogger<EpicompareController> logger)
        {
            _connectionString = dbOptions.Value.KineticErpConnection;
            _logger = logger;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        [HttpGet("ud-columns")]
        public async Task<IActionResult> GetEnvironmentUDColumns()
        {
            const string sql = @"
                SELECT
                  t.name AS TableName,
                  c.name AS ColumnName
                FROM sys.tables AS t
                INNER JOIN sys.columns AS c
                  ON t.object_id = c.object_id
                WHERE
                  c.name LIKE '%_c'
                  AND t.name LIKE '%_UD'
                ORDER BY
                  TableName,
                  ColumnName;";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<UDColumnDTO>(sql);
                
                _logger.LogInformation("Successfully retrieved {Count} UD columns", result.Count());
                
                return Ok(result.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving UD columns from database");
                return StatusCode(500, "An error occurred while retrieving UD columns");
            }
        }
    }
}
