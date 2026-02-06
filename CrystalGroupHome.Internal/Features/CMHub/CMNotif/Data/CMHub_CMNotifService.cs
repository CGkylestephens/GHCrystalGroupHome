using CrystalGroupHome.Internal.Features.CMHub.CMNotif.Models;
using CrystalGroupHome.SharedRCL.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Dapper;
using System.Data;

namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Data
{
    public interface ICMHub_CMNotifService
    {
        Task<List<CMHub_CMNotifRecordModel>> GetAllRecordsAsync(bool includeDeleted = false);
        Task<CMHub_CMNotifRecordModel?> GetRecordByIdAsync(int recordId, bool includeDeleted = false);
        Task<List<CMHub_CMNotifRecordPartModel>> GetPartsByRecordIdAsync(int recordId, bool includeDeleted = false);
        Task<List<CMHub_CMNotifRecordLogModel>> GetLogsByRecordIdAsync(int recordId, bool includeDeleted = false);
        Task<List<CMHub_CMNotifRecordModel>> GetRecordsByECNNumbersAsync(List<string>? ecnNumbers = null, bool includeDeleted = false, bool getAll = false);

        Task<List<CMHub_CMNotifECNMatchedRecordModel>> GetHeldECNPartsAsync(List<string>? ecnNumbers = null, bool getAll = false);

        Task<CMHub_CMNotifRecordModel> CreateRecordByECNNumberAsync(string ecnNumber);
        Task<int> CreateRecordLogAsync(CMHub_CMNotifRecordLogModel log);
        Task<int> CreatePartAssociatedWithRecordAsync(CMHub_CMNotifRecordPartModel part);

        Task<bool> UpdatePartByPartIdAsync(CMHub_CMNotifRecordPartModel part);
        Task<bool> UpdateRecordLogNotifLocationAsync(string notifLogLocation, int logId);
    }

    public class CMHub_CMNotifService : ICMHub_CMNotifService
    {
        private readonly string _connectionString;
        private readonly ILogger<CMHub_CMNotifService> _logger;

        private const string RecordTable = "[dbo].[CMNotif_Record]";
        private const string PartTable = "[dbo].[CMNotif_RecordPart]";
        private const string LogTable = "[dbo].[CMNotif_RecordLog]";
        private const string EcnHeaderTable = "[QECNMANAGER].[dbo].[EcnHeaders]";
        private const string EcnAffectedAssembliesTable = "[QECNMANAGER].[dbo].[EcnAffectedAssemblies]";
        private const string EcnAdditionalFieldsTable = "[QECNMANAGER].[dbo].[EcnAdditionalFields]";
        private const string KineticPartTable = "[KineticERP].[dbo].[Part]";
        private const string EpicorCurrentPartRevsFunc = "[dbo].[f_EpicorCurrentPartRevs]";
        private const string CMDexPartEmployeeTable = "[dbo].[CMDex_PartEmployee]";
        private const string KineticEmpBasicTable = "[KineticERP].[Erp].[EmpBasic]";

        public CMHub_CMNotifService(IOptions<DatabaseOptions> dbOptions, ILogger<CMHub_CMNotifService> logger)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
        }

        public async Task<List<CMHub_CMNotifRecordModel>> GetAllRecordsAsync(bool includeDeleted = false)
        {
            using var conn = new SqlConnection(_connectionString);
            string query = $@"
                SELECT Id, ECNNumber, Deleted, Completed
                FROM {RecordTable}
                WHERE (@IncludeDeleted = 1 OR Deleted = 0);";

            try
            {
                var records = (await conn.QueryAsync<CMHub_CMNotifRecordModel>(
                    query, new { IncludeDeleted = includeDeleted })).ToList();

                foreach (var record in records)
                {
                    record.RecordedParts = await GetPartsByRecordIdAsync(record.Id, includeDeleted);
                    record.RecordedLogs = await GetLogsByRecordIdAsync(record.Id, includeDeleted);
                }

                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all CMNotif Records.");
                throw;
            }
        }

        public async Task<CMHub_CMNotifRecordModel?> GetRecordByIdAsync(int recordId, bool includeDeleted = false)
        {
            using var conn = new SqlConnection(_connectionString);
            string query = $@"
                SELECT Id, ECNNumber, Deleted, Completed
                FROM {RecordTable}
                WHERE Id = @RecordId AND (@IncludeDeleted = 1 OR Deleted = 0);";

            try
            {
                var record = await conn.QuerySingleOrDefaultAsync<CMHub_CMNotifRecordModel>(
                    query, new { RecordId = recordId, IncludeDeleted = includeDeleted });

                if (record != null)
                {
                    record.RecordedParts = await GetPartsByRecordIdAsync(record.Id, includeDeleted);
                    record.RecordedLogs = await GetLogsByRecordIdAsync(record.Id, includeDeleted);
                }

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching CMNotif Record with ID {recordId}.");
                throw;
            }
        }

        public async Task<List<CMHub_CMNotifRecordPartModel>> GetPartsByRecordIdAsync(int recordId, bool includeDeleted = false)
        {
            using var conn = new SqlConnection(_connectionString);
            string query = $@"
                SELECT Id, RecordId, PartNum, Deleted, DateCreated, AffectedPartNum, ReplacementPartNum, ECNChangeDetail,
                       EffectiveDate, PriceEffect, IsApproved, ApprovedByEmpId, IsNotifSent, DateNotifSent, HasCustAcceptance,
                       HasCustAcceptanceOverride, DateCustAccepted, Notes, IsConfirmSent, DateConfirmSent
                FROM {PartTable}
                WHERE RecordId = @RecordId AND (@IncludeDeleted = 1 OR Deleted = 0);";

            try
            {
                return (await conn.QueryAsync<CMHub_CMNotifRecordPartModel>(
                    query, new { RecordId = recordId, IncludeDeleted = includeDeleted })).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Parts for Record ID {recordId}.");
                throw;
            }
        }

        public async Task<List<CMHub_CMNotifRecordLogModel>> GetLogsByRecordIdAsync(int recordId, bool includeDeleted = false)
        {
            using var conn = new SqlConnection(_connectionString);
            string query = $@"
                SELECT Id, RecordId, LogAssociatedWithPartNum, LogMessage, LogFileLocation, LogDate,
                       LoggedByEmpId AS LoggedByEmployeeId, IsManualLogEntry
                FROM {LogTable}
                WHERE RecordId = @RecordId AND (@IncludeDeleted = 1 OR Deleted = 0);";

            try
            {
                return (await conn.QueryAsync<CMHub_CMNotifRecordLogModel>(
                    query, new { RecordId = recordId, IncludeDeleted = includeDeleted })).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Logs for Record ID {recordId}.");
                throw;
            }
        }

        public async Task<List<CMHub_CMNotifRecordModel>> GetRecordsByECNNumbersAsync(
            List<string>? ecnNumbers = null,
            bool includeDeleted = false,
            bool getAll = false)
        {
            if (!getAll && (ecnNumbers == null || ecnNumbers.Count == 0))
                return new List<CMHub_CMNotifRecordModel>(); // Return empty if no ECNs and not fetching all

            using var conn = new SqlConnection(_connectionString);

            string recordQuery = $@"
                SELECT Id, ECNNumber, Deleted, Completed
                FROM {RecordTable}
                WHERE (@IncludeDeleted = 1 OR Deleted = 0)
                  {(getAll ? "" : "AND ECNNumber IN @ECNNumbers")};";

            string partQuery = $@"
                SELECT Id, RecordId, PartNum, Deleted, DateCreated, AffectedPartNum, ReplacementPartNum, ECNChangeDetail,
                       EffectiveDate, PriceEffect, IsApproved, ApprovedByEmpId, IsNotifSent, DateNotifSent, HasCustAcceptance,
                       HasCustAcceptanceOverride, DateCustAccepted, Notes, IsConfirmSent, DateConfirmSent
                FROM {PartTable}
                WHERE (@IncludeDeleted = 1 OR Deleted = 0);";

            string logQuery = $@"
                SELECT Id, RecordId, LogAssociatedWithPartNum, LogMessage, LogFileLocation, LogDate,
                       LoggedByEmpId, IsManualLogEntry
                FROM {LogTable}
                WHERE (@IncludeDeleted = 1 OR Deleted = 0);";

            try
            {
                await conn.OpenAsync();

                var parameters = new
                {
                    IncludeDeleted = includeDeleted,
                    ECNNumbers = ecnNumbers ?? new List<string>()
                };

                using var multi = await conn.QueryMultipleAsync($"{recordQuery} {partQuery} {logQuery}", parameters);

                var records = (await multi.ReadAsync<CMHub_CMNotifRecordModel>()).ToList();
                var parts = (await multi.ReadAsync<CMHub_CMNotifRecordPartModel>()).ToList();
                var logs = (await multi.ReadAsync<CMHub_CMNotifRecordLogModel>()).ToList();

                var partsLookup = parts.ToLookup(p => p.RecordId);
                var logsLookup = logs.ToLookup(l => l.RecordId);

                foreach (var record in records)
                {
                    record.RecordedParts = partsLookup[record.Id].ToList();
                    record.RecordedLogs = logsLookup[record.Id].ToList();
                }

                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching CMNotif Records by ECNNumbers.");
                throw;
            }
        }

        public async Task<CMHub_CMNotifRecordModel> CreateRecordByECNNumberAsync(string ecnNumber)
        {
            if (string.IsNullOrWhiteSpace(ecnNumber))
                throw new ArgumentException("ECN number must be provided.", nameof(ecnNumber));

            using var conn = new SqlConnection(_connectionString);

            try
            {
                // Check if record already exists
                string selectQuery = $@"
                    SELECT Id, ECNNumber, Deleted, Completed
                    FROM {RecordTable}
                    WHERE ECNNumber = @ECNNumber AND Deleted = 0;";

                var existing = await conn.QuerySingleOrDefaultAsync<CMHub_CMNotifRecordModel>(
                    selectQuery, new { ECNNumber = ecnNumber });

                if (existing != null)
                {
                    existing.RecordedParts = await GetPartsByRecordIdAsync(existing.Id);
                    existing.RecordedLogs = await GetLogsByRecordIdAsync(existing.Id);
                    return existing;
                }

                // Insert new record
                string insertQuery = $@"
                    INSERT INTO {RecordTable} (ECNNumber, Deleted, Completed)
                    VALUES (@ECNNumber, 0, 0);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                int newId = await conn.ExecuteScalarAsync<int>(insertQuery, new { ECNNumber = ecnNumber });

                var newRecord = new CMHub_CMNotifRecordModel
                {
                    Id = newId,
                    ECNNumber = ecnNumber,
                    Deleted = false,
                    Completed = false,
                    RecordedParts = [],
                    RecordedLogs = []
                };

                return newRecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating CMNotif Record for ECN {ecnNumber}.");
                throw;
            }
        }


        public async Task<List<CMHub_CMNotifECNMatchedRecordModel>> GetHeldECNPartsAsync(
            List<string>? ecnNumbers = null,
            bool getAll = false)
        {
            using var conn = new SqlConnection(_connectionString);

            var dtoColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_CMNotifECNPartDTO>();
            var query = $@"
                SELECT {dtoColumns}
                FROM {EcnHeaderTable} AS EH
                INNER JOIN {EcnAffectedAssembliesTable} AS EAA 
                    ON EH.Id = EAA.EcnHeaderId
                INNER JOIN {EcnAdditionalFieldsTable} AS EAF 
                    ON EAF.EcxHeaderId = EH.Id -- this currently is at header level - we'll need a per part addtl field
                INNER JOIN {KineticPartTable} AS P 
                    ON EAA.ParentPartNumber = P.PartNum
                LEFT JOIN {EpicorCurrentPartRevsFunc}() AS FECPR 
                    ON P.PartNum = FECPR.PartNum
                LEFT JOIN {CMDexPartEmployeeTable} AS CMPE
	                ON P.PartNum = CMPE.PartNum
                        AND CMPE.IsPrimary = 1
                LEFT JOIN {KineticEmpBasicTable} AS EB
	                ON CMPE.EmpID = EB.EmpID
                WHERE  
                    P.CM_CMManaged_c = 1 -- + Is CM Managed
                    AND EH.EcnStatusId IN ('1') -- we really want to know when it's ""HELD"" for CMNS - TBD
                    {(getAll ? "" : "AND EH.ECNNumber IN @ECNNumbers")}
                    -- AND EAF.IsFunctionChange = 'Yes' -- FFF = Yes on the ECN means go to CMNS, but we don't have this so ignore
                    ;
            ";

            try
            {
                var parameters = new
                {
                    ECNNumbers = ecnNumbers ?? new List<string>()
                };

                var ecnParts = await conn.QueryAsync<CMHub_CMNotifECNPartDTO>(query, parameters);

                List<string> uniqueECNNumbers = ecnParts
                    .Select(p => p.ECNNumber)
                    .Distinct()
                    .ToList();

                var records = await GetRecordsByECNNumbersAsync(uniqueECNNumbers);

                List<CMHub_CMNotifECNMatchedRecordModel> ecnMatchedRecords = new();
                foreach(var ecn in ecnParts)
                {
                    var matchedRecord = records.FirstOrDefault(r => r.ECNNumber == ecn.ECNNumber);

                    CMHub_CMNotifECNMatchedRecordModel emr = new();
                    emr.ECNNumber = ecn.ECNNumber;
                    emr.ECNId = ecn.ECNHeaderId;
                    emr.ReasonForChange = ecn.ReasonForChange;
                    emr.PrimaryPMName = ecn.PrimaryPMName;
                    if (matchedRecord != null)
                    {
                        emr.Record = matchedRecord;
                        emr.ECNParts = ecnParts.Where(p => p.ECNNumber == ecn.ECNNumber).ToList();
                    }
                    else
                    {
                        emr.TempRecord = new CMHub_CMNotifRecordModel()
                        {
                            ECNNumber = ecn.ECNNumber
                        };
                        emr.ECNParts = ecnParts.Where(p => p.ECNNumber == ecn.ECNNumber).ToList();
                    }

                    ecnMatchedRecords.Add(emr);
                }

                return ecnMatchedRecords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Held CMNS ECN Notification Records.");
                throw;
            }
        }

        public async Task<int> CreateRecordLogAsync(CMHub_CMNotifRecordLogModel log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            const string insertQuery = $@"
                INSERT INTO {LogTable}
                (
                    RecordId,
                    LogAssociatedWithPartNum,
                    LogMessage,
                    LogFileLocation,
                    LogDate,
                    LoggedByEmpId,
                    IsManualLogEntry,
                    Deleted
                )
                VALUES
                (
                    @RecordId,
                    @LogAssociatedWithPartNum,
                    @LogMessage,
                    @LogFileLocation,
                    @LogDate,
                    @LoggedByEmpId,
                    @IsManualLogEntry,
                    @Deleted
                );
                SELECT CAST(SCOPE_IDENTITY() as int);";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                int newId = await conn.ExecuteScalarAsync<int>(insertQuery, new
                {
                    log.RecordId,
                    log.LogAssociatedWithPartNum,
                    log.LogMessage,
                    log.LogFileLocation,
                    log.LogDate,
                    log.LoggedByEmpId,
                    log.IsManualLogEntry,
                    log.Deleted
                });

                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating CMNotif Record Log.");
                throw;
            }
        }

        public async Task<int> CreatePartAssociatedWithRecordAsync(CMHub_CMNotifRecordPartModel part)
        {
            if (part == null)
                throw new ArgumentNullException(nameof(part));

            const string insertQuery = $@"
                INSERT INTO {PartTable}
                (
                    RecordId,
                    PartNum,
                    Deleted,
                    DateCreated,
                    AffectedPartNum,
                    ReplacementPartNum,
                    ECNChangeDetail,
                    EffectiveDate,
                    PriceEffect,
                    IsApproved,
                    ApprovedByEmpId,
                    IsNotifSent,
                    DateNotifSent,
                    IsConfirmSent,
                    DateConfirmSent,
                    HasCustAcceptance,
                    HasCustAcceptanceOverride,
                    DateCustAccepted,
                    Notes
                )
                VALUES
                (
                    @RecordId,
                    @PartNum,
                    @Deleted,
                    @DateCreated,
                    @AffectedPartNum,
                    @ReplacementPartNum,
                    @ECNChangeDetail,
                    @EffectiveDate,
                    @PriceEffect,
                    @IsApproved,
                    @ApprovedByEmpId,
                    @IsNotifSent,
                    @DateNotifSent,
                    @IsConfirmSent,
                    @DateConfirmSent,
                    @HasCustAcceptance,
                    @HasCustAcceptanceOverride,
                    @DateCustAccepted,
                    @Notes
                );
                SELECT CAST(SCOPE_IDENTITY() AS int);";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                int newId = await conn.ExecuteScalarAsync<int>(insertQuery, new
                {
                    part.RecordId,
                    part.PartNum,
                    part.Deleted,
                    part.DateCreated,
                    part.AffectedPartNum,
                    part.ReplacementPartNum,
                    part.ECNChangeDetail,
                    part.EffectiveDate,
                    part.PriceEffect,
                    part.IsApproved,
                    part.ApprovedByEmpId,
                    part.IsNotifSent,
                    part.DateNotifSent,
                    part.IsConfirmSent,
                    part.DateConfirmSent,
                    part.HasCustAcceptance,
                    part.HasCustAcceptanceOverride,
                    part.DateCustAccepted,
                    part.Notes
                });

                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating Part for Record ID {part.RecordId}.");
                throw;
            }
        }

        public async Task<bool> UpdatePartByPartIdAsync(CMHub_CMNotifRecordPartModel part)
        {
            ArgumentNullException.ThrowIfNull(part);

            const string updatePartQuery = $@"
                UPDATE {PartTable}
                SET
                    RecordId = @RecordId,
                    PartNum = @PartNum,
                    Deleted = @Deleted,
                    AffectedPartNum = @AffectedPartNum,
                    ReplacementPartNum = @ReplacementPartNum,
                    ECNChangeDetail = @ECNChangeDetail,
                    EffectiveDate = @EffectiveDate,
                    PriceEffect = @PriceEffect,
                    IsApproved = @IsApproved,
                    ApprovedByEmpId = @ApprovedByEmpId,
                    IsNotifSent = @IsNotifSent,
                    DateNotifSent = @DateNotifSent,
                    IsConfirmSent = @IsConfirmSent,
                    DateConfirmSent = @DateConfirmSent,
                    HasCustAcceptance = @HasCustAcceptance,
                    HasCustAcceptanceOverride = @HasCustAcceptanceOverride,
                    DateCustAccepted = @DateCustAccepted,
                    Notes = @Notes
                WHERE Id = @Id;";

            const string checkConfirmStatusQuery = $@"
                SELECT 
                    CASE 
                        WHEN SUM(CASE WHEN IsConfirmSent = 0 THEN 1 ELSE 0 END) > 0 
                        THEN 0 ELSE 1 
                    END AS AllConfirmed
                FROM {PartTable}
                WHERE RecordId = @RecordId AND Deleted = 0;";

            const string updateRecordQuery = $@"
                UPDATE {RecordTable}
                SET Completed = @Completed
                WHERE Id = @RecordId;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var tran = conn.BeginTransaction();

                // 1. Update the part
                int rowsAffected = await conn.ExecuteAsync(updatePartQuery, new
                {
                    part.Id,
                    part.RecordId,
                    part.PartNum,
                    part.Deleted,
                    part.AffectedPartNum,
                    part.ReplacementPartNum,
                    part.ECNChangeDetail,
                    part.EffectiveDate,
                    part.PriceEffect,
                    part.IsApproved,
                    part.ApprovedByEmpId,
                    part.IsNotifSent,
                    part.DateNotifSent,
                    part.IsConfirmSent,
                    part.DateConfirmSent,
                    part.HasCustAcceptance,
                    part.HasCustAcceptanceOverride,
                    part.DateCustAccepted,
                    part.Notes
                }, tran);

                // 2. Check confirm status across all parts of the record
                bool allConfirmed = await conn.ExecuteScalarAsync<bool>(checkConfirmStatusQuery,
                    new { part.RecordId }, tran);

                // 3. Update the parent record's Completed field
                await conn.ExecuteAsync(updateRecordQuery, new
                {
                    RecordId = part.RecordId,
                    Completed = allConfirmed
                }, tran);

                tran.Commit();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Part with ID {part.Id} and parent Record completion status.");
                throw;
            }
        }

        public async Task<bool> UpdateRecordLogNotifLocationAsync(string logFileLocation, int logId)
        {
            if (string.IsNullOrEmpty(logFileLocation))
                return false;

            const string updateQuery = $@"
                UPDATE {LogTable}
                SET
                    LogFileLocation = @LogFileLocation
                WHERE Id = @Id;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                int rowsAffected = await conn.ExecuteAsync(updateQuery, new
                {
                    Id = logId,
                    LogFileLocation = logFileLocation
                });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Log with ID {logId}.");
                throw;
            }
        }
    }
}
