using System.Data;
using CrystalGroupHome.SharedRCL.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CrystalGroupHome.Internal.Common.Data.Jobs
{
    public interface IJobService
    {
        // JobHeads
        Task<List<T>> GetJobHeadsAsync<T>();
        Task<PaginatedResult<T>> GetJobHeadsPaginatedAsync<T>(int pageNumber, int pageSize);
        Task<T?> GetJobHeadByJobNumAsync<T>(string jobNum);

        // JobOpers
        Task<List<T>> GetJobOpersByJobNumAsync<T>(string jobNum);
        Task<List<T>> GetUniqueRecordedOpCodesByJobNumAsync<T>(string jobNum);
    }

    public class JobService : IJobService
    {
        private readonly string _connectionString;
        private readonly ILogger<JobService> _logger;

        // Tables
        private const string JobHeadTable = "JobHead";
        private const string JobOperTable = "JobOper";
        private const string LaborDtlTable = "LaborDtl";

        // JobDTO Stuff
        private const string JobOrderBy = "CreateDate";

        // JobOperDTO Stuff
        private const string JobOperOrderBy = "OprSeq";

        // Company Stuff
        private const string CompanyEqualsCG = "Company = 'CG'";

        public JobService(IOptions<DatabaseOptions> dbOptions, ILogger<JobService> logger)
        {
            _connectionString = dbOptions.Value.KineticErpConnection;
            _logger = logger;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<List<T>> GetJobHeadsAsync<T>()
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {JobHeadTable}
                ORDER BY {JobOrderBy}
                WHERE {CompanyEqualsCG};";

            try
            {
                using var conn = Connection;
                var result = await Connection.QueryAsync<T>(new CommandDefinition(sql)).ConfigureAwait(false);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching jobs with columns: {Columns}", DTOColumns);
                throw;
            }
        }

        public async Task<PaginatedResult<T>> GetJobHeadsPaginatedAsync<T>(int pageNumber, int pageSize)
        {
            var offset = (pageNumber - 1) * pageSize;
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {JobHeadTable}
                WHERE {CompanyEqualsCG}
                ORDER BY {JobOrderBy}
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(*) FROM {JobHeadTable};
                ";

            try
            {
                using var conn = await Connection.QueryMultipleAsync(sql, new { Offset = offset, PageSize = pageSize });
                var items = await conn.ReadAsync<T>();
                var totalRecords = await conn.ReadFirstAsync<int>();

                return new PaginatedResult<T>
                {
                    Items = items.ToList(),
                    TotalRecords = totalRecords
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated jobs with columns: {Columns}", DTOColumns);
                throw;
            }
        }

        public async Task<T?> GetJobHeadByJobNumAsync<T>(string jobNum)
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {JobHeadTable}
                WHERE {CompanyEqualsCG}
                    AND JobNum = @JobNum
                ORDER BY {JobOrderBy};";

            try
            {
                using var conn = Connection;
                return await conn.QueryFirstOrDefaultAsync<T>(sql, new { JobNum = jobNum });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching job head with JobNum: {JobNum}, columns: {Columns}", jobNum, DTOColumns);
                throw;
            }
        }

        public async Task<List<T>> GetJobOpersByJobNumAsync<T>(string jobNum)
        {
            var DTOColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();

            var sql = $@"
                SELECT {DTOColumns}
                FROM {JobOperTable}
                WHERE {CompanyEqualsCG}
                    AND JobNum = @JobNum
                ORDER BY {JobOperOrderBy};";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql, new { JobNum = jobNum });
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching job operations with columns: {Columns}", DTOColumns);
                throw;
            }
        }

        public async Task<List<T>> GetUniqueRecordedOpCodesByJobNumAsync<T>(string jobNum)
        {
            var sql = $@"
                SELECT DISTINCT
                    JobNum,
                    OprSeq,
                    OpCode
                FROM {LaborDtlTable}
                WHERE {CompanyEqualsCG}
                    AND JobNum = @JobNum
                ORDER BY OprSeq;";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql, new { JobNum = jobNum });
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching job operations.");
                throw;
            }
        }
    }
}