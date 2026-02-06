using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Models;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Employees;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.SharedRCL.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Data
{
    public interface IFirstTimeYield_Service
    {
        Task<List<T>> GetAreasAsync<T>();
        Task<T?> GetAreaByIdAsync<T>(int areaId);

        Task<List<FirstTimeYield_EntryModel>> GetEntriesAsync();
        Task<FirstTimeYield_EntryModel?> GetEntryByIdAsync(int id);
        Task CreateEntryAsync(FirstTimeYield_EntryModel entry);
        Task UpdateEntryAsync(FirstTimeYield_EntryModel entry);
        Task DeleteEntryAsync(int entryId);

        Task<int> GetTestedQtyForEntriesByJobNum(string jobNum);

        Task<List<T>> GetFailureReasons<T>();
        Task<int> CreateFailureReasonAsync(FirstTimeYield_FailureReasonDTO failureReason);
        Task UpdateFailureReasonAsync(FirstTimeYield_FailureReasonDTO failureReason);
        Task DeleteFailureReasonAsync(int failureReasonId);

        Task<List<T>> GetAreaFailureReasons<T>();
        Task<List<T>> GetFailureReasonsByArea<T>(int areaId);
        Task AddAreaFailureReasonAsync(int areaId, int reasonId);
        Task DeleteAreaFailureReasonAsync(int areaId, int reasonId);
        Task<List<T>> GetFailureReasonsNotInAreaAsync<T>(int areaId);
    }

    public class FirstTimeYield_Service : IFirstTimeYield_Service
    {
        private readonly ILaborService _laborService;
        private readonly IADUserService _adUserService;
        private readonly string _connectionString;
        private readonly ILogger<FirstTimeYield_Service> _logger;
        private readonly DebugModeService _debugModeService;

        private const string EntriesTable = "FirstTimeYield_Entries";
        private const string FailuresTable = "FirstTimeYield_Failures";
        private const string AreasTable = "FirstTimeYield_Areas";
        private const string FailureReasonsTable = "FirstTimeYield_FailureReasons";
        private const string AreaFailureReasonsTable = "FirstTimeYield_AreaFailureReasons";

        private const string EntriesInsertSql = $@"
                INSERT INTO {EntriesTable}
                (
                    JobNum,
                    OpCode,
                    OpCodeOperator,
                    AreaId,
                    QtyTested,
                    QtyPassed,
                    Notes,
                    EntryUser,
                    EntryDate,
                    LastModifiedUser,
                    LastModifiedDate,
                    Deleted
                )
                OUTPUT INSERTED.Id
                VALUES
                (
                    @JobNum,
                    @OpCode,
                    @OpCodeOperator,
                    @AreaId,
                    @QtyTested,
                    @QtyPassed,
                    @Notes,
                    @EntryUser,
                    @EntryDate,
                    @LastModifiedUser,
                    @LastModifiedDate,
                    @Deleted
                );
            ";

        private const string EntryUpdateSql = $@"
                UPDATE {EntriesTable}
                SET
                    JobNum           = @JobNum,
                    OpCode           = @OpCode,
                    OpCodeOperator   = @OpCodeOperator,
                    AreaId           = @AreaId,
                    QtyTested        = @QtyTested,
                    QtyPassed        = @QtyPassed,
                    Notes            = @Notes,
                    EntryUser        = @EntryUser,
                    EntryDate        = @EntryDate,
                    LastModifiedUser = @LastModifiedUser,
                    LastModifiedDate = @LastModifiedDate
                WHERE Id = @Id;
            ";

        private const string FailureInsertSql = $@"
                INSERT INTO {FailuresTable}
                (
                    EntryId,
                    ReasonID,
                    ReasonDescriptionOther,
                    Qty,
                    AreaIdToBlame,
                    JobNumToBlame,
                    OpCodeToBlame,
                    OpCodeOperatorToBlame,
                    EntryUser,
                    EntryDate,
                    Deleted
                )
                VALUES
                (
                    @EntryId,
                    @ReasonID,
                    @ReasonDescriptionOther,
                    @Qty,
                    @AreaIdToBlame,
                    @JobNumToBlame,
                    @OpCodeToBlame,
                    @OpCodeOperatorToBlame,
                    @EntryUser,
                    @EntryDate,
                    @Deleted
                );
            ";

        private const string FailureDeleteSql = $@"
                DELETE FROM {FailuresTable}
                WHERE EntryId = @EntryId;
            ";

        public FirstTimeYield_Service(
            ILaborService laborService,
            IADUserService adUserService,
            IOptions<DatabaseOptions> dbOptions,
            ILogger<FirstTimeYield_Service> logger,
            DebugModeService debugModeService
        )
        {
            _laborService = laborService;
            _adUserService = adUserService;
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _debugModeService = debugModeService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        // TODO: Many of these methods/queries are inefficient due to multiple round trips to the DB.
        // We could cache FailureReasons and Areas since those tables rarely change.
        // We could also use larger queries with joins and then Dapper's multi-mapping to retrieve multiple DTO objects in one go.

        #region Areas Lookups

        public async Task<List<T>> GetAreasAsync<T>()
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var sql = $@"
                SELECT {dtoColumns}
                FROM {AreasTable};
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Areas with columns: {Columns}", dtoColumns);
                throw;
            }
        }

        public async Task<T?> GetAreaByIdAsync<T>(int areaId)
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var sql = $@"
                SELECT {dtoColumns}
                FROM {AreasTable}
                WHERE Id = {areaId};
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync<T>(sql).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Area with columns: {Columns} and ID: {AreaId}", dtoColumns, areaId);
                throw;
            }
        }

        #endregion

        #region Entry Queries

        /// <summary>
        /// Fetches all Entries from the last 6 months (and their Failures), mapping them to <see cref="FirstTimeYield_EntryModel"/>.
        /// </summary>
        public async Task<List<FirstTimeYield_EntryModel>> GetEntriesAsync()
        {
            var sqlEntries = $@"
                SELECT *
                FROM {EntriesTable}
                WHERE Deleted = 0
                    AND EntryDate >= DATEADD(MONTH, -6, GETDATE())
                ORDER BY Id DESC;
            ";

            var sqlFailures = $@"
                SELECT *
                FROM {FailuresTable}
                WHERE Deleted = 0;
            ";

            try
            {
                using var conn = Connection;

                // 1. Retrieve all entry DTOs.
                var entryDtos = (await conn.QueryAsync<FirstTimeYield_EntryDTO>(sqlEntries).ConfigureAwait(false)).ToList();
                await _debugModeService.SqlQueryDebugMessage(sqlEntries, entryDtos);

                // 2. Retrieve all failure DTOs.
                var failureDtos = (await conn.QueryAsync<FirstTimeYield_FailureDTO>(sqlFailures).ConfigureAwait(false)).ToList();
                await _debugModeService.SqlQueryDebugMessage(sqlFailures, failureDtos);

                // 3. Retrieve lookup data that rarely changes.
                var areaDtos = await GetAreasAsync<FirstTimeYield_AreaDTO>();
                var failureReasonDtos = await GetFailureReasons<FirstTimeYield_FailureReasonDTO>();

                // Build a dictionary for areas keyed by area ID.
                var areaDictionary = areaDtos.ToDictionary(a => a.Id);

                // 4. Bulk-retrieve AD users.
                var adUserNumbers = entryDtos
                    .SelectMany(e => new[] { e.OpCodeOperator, e.EntryUser, e.LastModifiedUser })
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();

                var adUsers = await _adUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(adUserNumbers)
                    .ConfigureAwait(false);
                var adUserDictionary = adUsers.ToDictionary(u => u.EmployeeNumber, u => u);

                // 5. Bulk-retrieve labor employees for failure operators.
                var failureOperatorNumbers = failureDtos
                    .Select(f => f.OpCodeOperatorToBlame)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();

                var laborEmployees = await _laborService.GetEmployeesByEmpIDsAsync<EmpBasicDTO_Base>(failureOperatorNumbers)
                    .ConfigureAwait(false);
                var laborEmployeeDictionary = laborEmployees.ToDictionary(e => e.EmpID, e => e);

                // 6. Process each entry.
                var results = new List<FirstTimeYield_EntryModel>();
                foreach (var eDto in entryDtos)
                {
                    // Lookup AD users from the bulk-fetched dictionary.
                    var opCodeOperator = adUserDictionary.TryGetValue(eDto.OpCodeOperator, out var opUser)
                        ? opUser
                        : new ADUserDTO_Base { EmployeeNumber = "UNKNOWN", DisplayName = "Unknown User" };

                    var entryUser = adUserDictionary.TryGetValue(eDto.EntryUser, out var enUser)
                        ? enUser
                        : new ADUserDTO_Base { EmployeeNumber = "UNKNOWN", DisplayName = "Unknown User" };

                    var lastModifiedUser = adUserDictionary.TryGetValue(eDto.LastModifiedUser, out var modUser)
                        ? modUser
                        : new ADUserDTO_Base { EmployeeNumber = "UNKNOWN", DisplayName = "Unknown User" };

                    // Lookup area info using our bulk-fetched dictionary.
                    var areaDto = areaDictionary.TryGetValue(eDto.AreaId, out var foundArea)
                        ? foundArea
                        : null;

                    // Filter failures for this entry.
                    var eFailures = failureDtos.Where(f => f.EntryId == eDto.Id).ToList();

                    // For each failure, look up the operator from the bulk-fetched labor employees.
                    var allFailureOperatorsDtos = new List<EmpBasicDTO_Base>();
                    foreach (var failure in eFailures)
                    {
                        if (laborEmployeeDictionary.TryGetValue(failure.OpCodeOperatorToBlame, out var laborEmp))
                        {
                            allFailureOperatorsDtos.Add(laborEmp);
                        }
                    }

                    // Map the DTO to the domain model.
                    var model = eDto.ToModel(
                        areaDto,
                        opCodeOperator,
                        eFailures,
                        failureReasonDtos,
                        entryUser,
                        lastModifiedUser,
                        areaDtos,
                        allFailureOperatorsDtos
                    );
                    results.Add(model);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all FTY Entries");
                throw;
            }
        }

        /// <summary>
        /// Fetches a single Entry (and its Failures) by ID.
        /// </summary>
        public async Task<FirstTimeYield_EntryModel?> GetEntryByIdAsync(int id)
        {
            var sqlEntry = $@"
                SELECT *
                FROM {EntriesTable}
                WHERE Id = {id}
                  AND Deleted = 0;
            ";

            var sqlFailures = $@"
                SELECT *
                FROM {FailuresTable}
                WHERE EntryId = {id}
                  AND Deleted = 0;
            ";

            try
            {
                using var conn = Connection;

                // Fetch the single EntryDTO.
                var entryDto = await conn.QueryFirstOrDefaultAsync<FirstTimeYield_EntryDTO>(sqlEntry).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sqlEntry, entryDto);

                if (entryDto == null)
                    return null;

                // Fetch the associated failures.
                var failureDTOs = (await conn.QueryAsync<FirstTimeYield_FailureDTO>(sqlFailures).ConfigureAwait(false)).ToList();
                await _debugModeService.SqlQueryDebugMessage(sqlFailures, failureDTOs);

                // Fetch reason dictionary, area, and operator info.
                var failureReasonDtos = await GetFailureReasons<FirstTimeYield_FailureReasonDTO>();
                var allAreaDtos = await GetAreasAsync<FirstTimeYield_AreaDTO>();
                var areaDto = allAreaDtos.FirstOrDefault(area => area.Id == entryDto.AreaId);

                var opCodeOperator = await _adUserService.GetADUserByEmployeeNumberAsync<ADUserDTO_Base>(entryDto.OpCodeOperator);
                var entryUser = await _adUserService.GetADUserByEmployeeNumberAsync<ADUserDTO_Base>(entryDto.EntryUser)
                                ?? new ADUserDTO_Base { EmployeeNumber = "UNKNOWN", DisplayName = "Unknown User" };
                var lastModifiedUser = await _adUserService.GetADUserByEmployeeNumberAsync<ADUserDTO_Base>(entryDto.LastModifiedUser)
                                ?? new ADUserDTO_Base { EmployeeNumber = "UNKNOWN", DisplayName = "Unknown User" };

                var allFailureOperatorsDtos = new List<EmpBasicDTO_Base>();
                foreach (var failure in failureDTOs)
                {
                    var oper = await _laborService.GetEmployeeByEmpIDAsync<EmpBasicDTO_Base>(failure.OpCodeOperatorToBlame);
                    if (oper != null)
                    {
                        allFailureOperatorsDtos.Add(oper);
                    }
                }

                var model = entryDto.ToModel(
                    areaDto,
                    opCodeOperator,
                    failureDTOs,
                    failureReasonDtos,
                    entryUser,
                    lastModifiedUser,
                    allAreaDtos,
                    allFailureOperatorsDtos
                );

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching single Entry by Id={Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Inserts a new Entry and its Failures.
        /// </summary>
        public async Task CreateEntryAsync(FirstTimeYield_EntryModel entry)
        {
            entry.EntryDate = DateTime.Now;
            entry.LastModifiedDate = DateTime.Now;
            var entryDto = entry.ToDto();
            var sqlInsertEntry = EntriesInsertSql;

            try
            {
                using var conn = Connection;
                var newId = await conn.ExecuteScalarAsync<int>(sqlInsertEntry, entryDto).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sqlInsertEntry, newId);
                entry.Id = newId;

                var failureDtos = entry.ToFailureDtos();
                foreach (var failDto in failureDtos)
                {
                    failDto.EntryId = newId;
                    await conn.ExecuteAsync(FailureInsertSql, failDto).ConfigureAwait(false);
                    await _debugModeService.SqlQueryDebugMessage(FailureInsertSql, "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting FTY Entry");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing Entry and replaces its Failures.
        /// </summary>
        public async Task UpdateEntryAsync(FirstTimeYield_EntryModel entry)
        {
            entry.LastModifiedDate = DateTime.Now;
            var entryDto = entry.ToDto();

            try
            {
                using var conn = Connection;
                conn.Open();
                using var tran = conn.BeginTransaction();

                await conn.ExecuteAsync(EntryUpdateSql, entryDto, transaction: tran).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(EntryUpdateSql, "");

                await conn.ExecuteAsync(FailureDeleteSql, new { EntryId = entry.Id }, transaction: tran).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(FailureDeleteSql, "");

                var newFailureDtos = entry.ToFailureDtos();
                foreach (var failDto in newFailureDtos)
                {
                    failDto.EntryId = entry.Id;
                    await conn.ExecuteAsync(FailureInsertSql, failDto, transaction: tran).ConfigureAwait(false);
                    await _debugModeService.SqlQueryDebugMessage(FailureInsertSql, "");
                }

                tran.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating FTY Entry with Id={Id}", entry.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes the Entry and its Failures.
        /// </summary>
        public async Task DeleteEntryAsync(int entryId)
        {
            var sqlSoftDeleteEntry = $@"
                 UPDATE {EntriesTable}
                 SET Deleted = 1
                 WHERE Id = '{entryId}';
             ";
            var sqlSoftDeleteFailures = $@"
                 UPDATE {FailuresTable}
                 SET Deleted = 1
                 WHERE EntryId = '{entryId}';
             ";

            try
            {
                using var conn = Connection;
                conn.Open();
                using var tran = conn.BeginTransaction();

                await conn.ExecuteAsync(sqlSoftDeleteFailures, transaction: tran);
                await _debugModeService.SqlQueryDebugMessage(sqlSoftDeleteFailures, "");

                await conn.ExecuteAsync(sqlSoftDeleteEntry, transaction: tran);
                await _debugModeService.SqlQueryDebugMessage(sqlSoftDeleteEntry, "");

                tran.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting FTY Entry with Id={Id}", entryId);
                throw;
            }
        }

        /// <summary>
        /// Fetches and sums all tested quantities for entries with the same JobNum.
        /// </summary>
        public async Task<int> GetTestedQtyForEntriesByJobNum(string jobNum)
        {
            var sqlEntry = $@"
                SELECT
                    SUM(QtyTested) as TotalTested
                FROM {EntriesTable}
                WHERE JobNum = @JobNum
                  AND Deleted = 0
                GROUP BY JobNum;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryFirstOrDefaultAsync(sqlEntry, new { JobNum = jobNum })
                       .ConfigureAwait(false);

                // If you know it’s effectively an int or int?, you can cast:
                int? totalTested = (int?)result?.TotalTested;

                // Then specify the type parameter
                await _debugModeService.SqlQueryDebugMessage<int?>(sqlEntry, totalTested);
                return totalTested ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching total tested quantity for JobNum={JobNum}", jobNum);
                throw;
            }
        }

        #endregion

        #region Failure Reasons Management

        public async Task<List<T>> GetFailureReasons<T>()
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var sql = $@"
                SELECT {dtoColumns}
                FROM {FailureReasonsTable};
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Failure Reasons with columns: {Columns}", dtoColumns);
                throw;
            }
        }

        public async Task<int> CreateFailureReasonAsync(FirstTimeYield_FailureReasonDTO failureReason)
        {
            var sql = $@"
                INSERT INTO {FailureReasonsTable}
                (
                    FailureDescription,
                    EntryUser,
                    EntryDate,
                    LastModifiedUser,
                    LastModifiedDate,
                    Deleted
                )
                OUTPUT INSERTED.Id
                VALUES
                (
                    @FailureDescription,
                    @EntryUser,
                    @EntryDate,
                    @LastModifiedUser,
                    @LastModifiedDate,
                    0
                );
            ";

            try
            {
                if (string.IsNullOrEmpty(failureReason.EntryUser))
                {
                    failureReason.EntryUser = failureReason.LastModifiedUser;
                }

                failureReason.EntryDate = DateTime.Now;
                failureReason.LastModifiedDate = DateTime.Now;

                using var conn = Connection;
                var newId = await conn.ExecuteScalarAsync<int>(sql, failureReason).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, newId);

                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating failure reason: {FailureDescription}", failureReason.FailureDescription);
                throw;
            }
        }

        public async Task UpdateFailureReasonAsync(FirstTimeYield_FailureReasonDTO failureReason)
        {
            var sql = $@"
                UPDATE {FailureReasonsTable}
                SET
                    Deleted = @Deleted,
                    FailureDescription = @FailureDescription,
                    LastModifiedUser = @LastModifiedUser,
                    LastModifiedDate = @LastModifiedDate
                WHERE Id = @Id;
            ";

            try
            {
                failureReason.LastModifiedDate = DateTime.Now;
                using var conn = Connection;
                await conn.ExecuteAsync(sql, failureReason).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating failure reason with Id={Id}", failureReason.Id);
                throw;
            }
        }

        public async Task DeleteFailureReasonAsync(int failureReasonId)
        {
            var usageCheckSql = $@"
                SELECT COUNT(*)
                FROM {FailuresTable} 
                WHERE ReasonID = @FailureReasonId
                  AND Deleted = 0;
            ";

            var hardDeleteSql = $@"
                DELETE FROM {FailureReasonsTable}
                WHERE Id = @FailureReasonId;
            ";

            var deleteAreaAssociationsSql = $@"
                DELETE FROM {AreaFailureReasonsTable}
                WHERE ReasonId = @FailureReasonId;
            ";

            try
            {
                using var conn = Connection;
                conn.Open();
                using var tran = conn.BeginTransaction();

                var usageCount = await conn.ExecuteScalarAsync<int>(
                    usageCheckSql,
                    new { FailureReasonId = failureReasonId },
                    transaction: tran
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(usageCheckSql, usageCount);

                if (usageCount > 0)
                {
                    tran.Rollback();
                    throw new InvalidOperationException($"Cannot delete failure reason with ID {failureReasonId} because it is used in {usageCount} entries");
                }

                await conn.ExecuteAsync(
                    deleteAreaAssociationsSql,
                    new { FailureReasonId = failureReasonId },
                    transaction: tran
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(deleteAreaAssociationsSql, "");

                await conn.ExecuteAsync(
                    hardDeleteSql,
                    new { FailureReasonId = failureReasonId },
                    transaction: tran
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(hardDeleteSql, "");

                tran.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting failure reason with Id={Id}", failureReasonId);
                throw;
            }
        }

        #endregion

        #region Area-Failure Reason Associations

        public async Task<List<T>> GetAreaFailureReasons<T>()
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var sql = $@"
                SELECT {dtoColumns}
                FROM {FailureReasonsTable} AS FR
                LEFT JOIN {AreaFailureReasonsTable} AS AFR 
                    ON AFR.ReasonId = FR.Id
                LEFT JOIN {AreasTable} AS A
                    ON A.Id = AFR.AreaId;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Area Failure Reasons with columns: {Columns}", dtoColumns);
                throw;
            }
        }

        public async Task<List<T>> GetFailureReasonsByArea<T>(int areaId)
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var sql = $@"
                SELECT {dtoColumns}, AFR.AreaId
                FROM {FailureReasonsTable} AS FR
                INNER JOIN {AreaFailureReasonsTable} AS AFR 
                    ON AFR.ReasonId = FR.Id
                WHERE AFR.AreaId = {areaId};
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Failure Reasons by area with columns: {Columns} and areaId: {Area}", dtoColumns, areaId);
                throw;
            }
        }

        public async Task AddAreaFailureReasonAsync(int areaId, int reasonId)
        {
            var checkSql = $@"
                SELECT COUNT(*)
                FROM {AreaFailureReasonsTable}
                WHERE AreaId = @AreaId AND ReasonId = @ReasonId;
            ";

            var insertSql = $@"
                INSERT INTO {AreaFailureReasonsTable}
                (
                    AreaId,
                    ReasonId
                )
                VALUES
                (
                    @AreaId,
                    @ReasonId
                );
            ";

            try
            {
                using var conn = Connection;
                var count = await conn.ExecuteScalarAsync<int>(
                    checkSql,
                    new { AreaId = areaId, ReasonId = reasonId }
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(checkSql, count);

                if (count == 0)
                {
                    await conn.ExecuteAsync(insertSql, new { AreaId = areaId, ReasonId = reasonId }).ConfigureAwait(false);
                    await _debugModeService.SqlQueryDebugMessage(insertSql, "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error associating failure reason {ReasonId} with area {AreaId}", reasonId, areaId);
                throw;
            }
        }

        public async Task DeleteAreaFailureReasonAsync(int areaId, int reasonId)
        {
            var usageCheckSql = $@"
                SELECT COUNT(*)
                FROM {EntriesTable} e
                JOIN {FailuresTable} f ON e.Id = f.EntryId
                WHERE e.AreaId = @AreaId 
                  AND f.ReasonID = @ReasonId
                  AND e.Deleted = 0
                  AND f.Deleted = 0;
            ";

            var deleteSql = $@"
                DELETE FROM {AreaFailureReasonsTable}
                WHERE AreaId = @AreaId AND ReasonId = @ReasonId;
            ";

            try
            {
                using var conn = Connection;
                var usageCount = await conn.ExecuteScalarAsync<int>(
                    usageCheckSql,
                    new { AreaId = areaId, ReasonId = reasonId }
                ).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(usageCheckSql, usageCount);

                if (usageCount > 0)
                {
                    _logger.LogWarning("Removing association between area {AreaId} and reason {ReasonId} that is used in {Count} entries", areaId, reasonId, usageCount);
                }

                await conn.ExecuteAsync(deleteSql, new { AreaId = areaId, ReasonId = reasonId }).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(deleteSql, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing association between area {AreaId} and reason {ReasonId}", areaId, reasonId);
                throw;
            }
        }

        public async Task<List<T>> GetFailureReasonsNotInAreaAsync<T>(int areaId)
        {
            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<T>();
            var sql = $@"
                SELECT {dtoColumns}
                FROM {FailureReasonsTable} FR
                WHERE FR.Deleted = 0
                  AND FR.Id NOT IN (
                    SELECT ReasonId 
                    FROM {AreaFailureReasonsTable}
                    WHERE AreaId = {areaId}
                  )
                ORDER BY FR.Description;
            ";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<T>(sql).ConfigureAwait(false);
                await _debugModeService.SqlQueryDebugMessage(sql, result);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching failure reasons not in area {AreaId} with columns: {Columns}", areaId, dtoColumns);
                throw;
            }
        }

        #endregion
    }
}
