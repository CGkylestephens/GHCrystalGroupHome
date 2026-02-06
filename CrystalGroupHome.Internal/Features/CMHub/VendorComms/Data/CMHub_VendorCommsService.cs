using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.Internal.Common.Data.Vendors;

namespace CrystalGroupHome.Internal.Features.CMHub.VendorComms.Data
{
    public interface ICMHub_VendorCommsService
    {
        Task<List<CMHub_VendorCommsTrackerModel>> GetTrackersAsync(bool includeInactiveVendors = false, bool includeDeleted = false);
        Task<CMHub_VendorCommsTrackerModel?> GetTrackerByIdAsync(int trackerId);
        Task<CMHub_VendorCommsTrackerModel?> GetTrackerByPartNumAsync(string partNum);
        Task<int> CreateOrUpdateTrackerAsync(CMHub_VendorCommsTrackerModel tracker);
        Task<int> CreateOrUpdateTrackerWithChangeLoggingAsync(CMHub_VendorCommsTrackerModel currentTracker, CMHub_VendorCommsTrackerModel? originalTracker, string loggedByEmpId);
        Task<List<CMHub_VendorCommsTrackerLogModel>> GetTrackerLogsAsync(int trackerId, bool includeDeleted = false);
        Task CreateTrackerLogAsync(CMHub_VendorCommsTrackerLogModel log, string loggedByEmpId);
    }

    public class CMHub_VendorCommsService : ICMHub_VendorCommsService
    {
        private readonly string _connectionString;
        private readonly ILogger<CMHub_VendorCommsService> _logger;
        private readonly IVendorService _vendorService;
        private readonly IPartService _partService;
        private readonly ICMHub_VendorCommsSurveyService _surveyService;
        private readonly IADUserService _adUserService;
        private readonly EmailHelpers _emailHelpers;
        private readonly IWebHostEnvironment _environment;

        private const string TrackerTable = "[CGIExt].[dbo].[CMVendorComms_PartStatusTracker]";
        private const string LogTable = "[CGIExt].[dbo].[CMVendorComms_PartStatusTrackerLog]";
        private const string SurveyBatchPartTable = "[CGIExt].[dbo].[CMVendorComms_SurveyBatchPart]";

        public CMHub_VendorCommsService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<CMHub_VendorCommsService> logger,
            IVendorService vendorService,
            IPartService partService,
            ICMHub_VendorCommsSurveyService surveyService,
            IADUserService adUserService,
            EmailHelpers emailHelpers,
            IWebHostEnvironment environment)
        {
            _connectionString = dbOptions.Value.CGIExtConnection;
            _logger = logger;
            _vendorService = vendorService;
            _partService = partService;
            _surveyService = surveyService;
            _adUserService = adUserService;
            _emailHelpers = emailHelpers;
            _environment = environment;
        }

        public async Task<int> CreateOrUpdateTrackerAsync(CMHub_VendorCommsTrackerModel tracker)
        {
            int trackerId;

            // Populate the external columns from the related data
            tracker.Tracker.ext_VendorName = tracker.Vendor?.VendorName;
            tracker.Tracker.ext_VendorPartNum = tracker.PartEolt?.VendorPartNum;
            tracker.Tracker.ext_PartDesc = tracker.PartEolt?.PartDescription;

            if (tracker.Tracker.Id > 0)
            {
                // Update existing tracker
                var query = $@"
                    UPDATE {TrackerTable}
                    SET PartNum = @PartNum,
                        VendorNum = @VendorNum,
                        Deleted = @Deleted,
                        ext_VendorName = @ext_VendorName,
                        ext_VendorPartNum = @ext_VendorPartNum,
                        ext_PartDesc = @ext_PartDesc
                    WHERE Id = @Id;";
                using var conn = new SqlConnection(_connectionString);
                await conn.ExecuteAsync(query, new
                {
                    tracker.Tracker.Id,
                    tracker.Tracker.PartNum,
                    tracker.Tracker.VendorNum,
                    Deleted = tracker.Tracker.Deleted ? 1 : 0,
                    tracker.Tracker.ext_VendorName,
                    tracker.Tracker.ext_VendorPartNum,
                    tracker.Tracker.ext_PartDesc
                });
                trackerId = tracker.Tracker.Id;
            }
            else
            {
                // Create new tracker
                var query = $@"
                    INSERT INTO {TrackerTable} (PartNum, VendorNum, Deleted, ext_VendorName, ext_VendorPartNum, ext_PartDesc)
                    OUTPUT INSERTED.Id
                    VALUES (@PartNum, @VendorNum, @Deleted, @ext_VendorName, @ext_VendorPartNum, @ext_PartDesc);";
                using var conn = new SqlConnection(_connectionString);
                var newId = await conn.ExecuteScalarAsync<int>(query, new
                {
                    tracker.Tracker.PartNum,
                    tracker.Tracker.VendorNum,
                    Deleted = tracker.Tracker.Deleted ? 1 : 0,
                    tracker.Tracker.ext_VendorName,
                    tracker.Tracker.ext_VendorPartNum,
                    tracker.Tracker.ext_PartDesc
                });
                tracker.Tracker.Id = newId;
                trackerId = newId;
            }

            // Update the PartEoltDTO if it has data and PartNum is set
            if (!string.IsNullOrWhiteSpace(tracker.PartEolt?.PartNum))
            {
                try
                {
                    var updateSuccess = await _partService.UpdatePartAsync(tracker.PartEolt);
                    if (!updateSuccess)
                    {
                        _logger.LogWarning("Failed to update PartEoltDTO for PartNum: {PartNum}", tracker.PartEolt.PartNum);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating PartEoltDTO for PartNum: {PartNum}", tracker.PartEolt.PartNum);
                    // Don't throw here - we still want to return the tracker ID even if the part update fails
                }
            }

            return trackerId;
        }

        public async Task<int> CreateOrUpdateTrackerWithChangeLoggingAsync(CMHub_VendorCommsTrackerModel currentTracker, CMHub_VendorCommsTrackerModel? originalTracker, string loggedByEmpId)
        {
            // First, save the tracker
            int trackerId = await CreateOrUpdateTrackerAsync(currentTracker);

            // Generate change logs if we have an original tracker to compare against
            if (originalTracker != null && trackerId > 0)
            {
                var changes = GenerateChangeLog(originalTracker, currentTracker);
                
                if (!string.IsNullOrEmpty(changes))
                {
                    var changeLog = new CMHub_VendorCommsTrackerLogModel
                    {
                        TrackerId = trackerId,
                        LogMessage = changes,
                        ManualLogEntry = false // This is an automatic system log
                    };

                    await CreateTrackerLogAsync(changeLog, loggedByEmpId);
                }
            }

            return trackerId;
        }

        private string GenerateChangeLog(CMHub_VendorCommsTrackerModel original, CMHub_VendorCommsTrackerModel current)
        {
            var changes = new List<string>();

            // Check PartEolt changes
            if (original.PartEolt.ExcludeVendorComms != current.PartEolt.ExcludeVendorComms)
            {
                changes.Add($"Exclude from Vendor Communications: {original.PartEolt.ExcludeVendorComms} → {current.PartEolt.ExcludeVendorComms}");
            }

            if (original.PartEolt.NotifyIntervalDays != current.PartEolt.NotifyIntervalDays)
            {
                var originalValue = original.PartEolt.NotifyIntervalDays.ToString() ?? "null";
                var currentValue = current.PartEolt.NotifyIntervalDays.ToString() ?? "null";
                changes.Add($"Contact Interval (Days): {originalValue} → {currentValue}");
            }

            if (original.PartEolt.LastContactDate != current.PartEolt.LastContactDate)
            {
                var originalValue = original.PartEolt.LastContactDate?.ToString("yyyy-MM-dd") ?? "null";
                var currentValue = current.PartEolt.LastContactDate?.ToString("yyyy-MM-dd") ?? "null";
                changes.Add($"Last Contact Date: {originalValue} → {currentValue}");
            }

            if (original.PartEolt.LastProcessedSurveyResponseDate != current.PartEolt.LastProcessedSurveyResponseDate)
            {
                var originalValue = original.PartEolt.LastProcessedSurveyResponseDate?.ToString("yyyy-MM-dd") ?? "null";
                var currentValue = current.PartEolt.LastProcessedSurveyResponseDate?.ToString("yyyy-MM-dd") ?? "null";
                changes.Add($"Last Response Date: {originalValue} → {currentValue}");
            }

            if (original.PartEolt.EolDate != current.PartEolt.EolDate)
            {
                var originalValue = original.PartEolt.EolDate?.ToString("yyyy-MM-dd") ?? "null";
                var currentValue = current.PartEolt.EolDate?.ToString("yyyy-MM-dd") ?? "null";
                changes.Add($"EOL Date: {originalValue} → {currentValue}");
            }

            if (original.PartEolt.LastTimeBuyDate != current.PartEolt.LastTimeBuyDate)
            {
                var originalValue = original.PartEolt.LastTimeBuyDate?.ToString("yyyy-MM-dd") ?? "null";
                var currentValue = current.PartEolt.LastTimeBuyDate?.ToString("yyyy-MM-dd") ?? "null";
                changes.Add($"Last Time Buy Date: {originalValue} → {currentValue}");
            }

            if (original.PartEolt.LastTimeBuyDateConfirmed != current.PartEolt.LastTimeBuyDateConfirmed)
            {
                var originalValue = original.PartEolt.LastTimeBuyDateConfirmed ? "Confirmed" : "Projected";
                var currentValue = current.PartEolt.LastTimeBuyDateConfirmed ? "Confirmed" : "Projected";
                changes.Add($"Last Time Buy Date Status: {originalValue} → {currentValue}");
            }

            if (original.PartEolt.ReplacementPartNum != current.PartEolt.ReplacementPartNum)
            {
                var originalValue = string.IsNullOrEmpty(original.PartEolt.ReplacementPartNum) ? "null" : original.PartEolt.ReplacementPartNum;
                var currentValue = string.IsNullOrEmpty(current.PartEolt.ReplacementPartNum) ? "null" : current.PartEolt.ReplacementPartNum;
                changes.Add($"Replacement Part #: {originalValue} → {currentValue}");
            }

            if (original.PartEolt.TechNotes != current.PartEolt.TechNotes)
            {
                changes.Add($"Tech Notes: Modified");
            }

            return changes.Any() ? string.Join("\n", changes) : string.Empty;
        }

        public async Task<CMHub_VendorCommsTrackerModel?> GetTrackerByIdAsync(int trackerId)
        {
            var trackerColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_VendorCommsTrackerDTO>();
            var query = $"SELECT {trackerColumns} FROM {TrackerTable} T WHERE T.Id = @TrackerId;";

            using var conn = new SqlConnection(_connectionString);
            var trackerDto = await conn.QuerySingleOrDefaultAsync<CMHub_VendorCommsTrackerDTO>(query, new { TrackerId = trackerId });

            if (trackerDto != null)
            {
                var tracker = new CMHub_VendorCommsTrackerModel { Tracker = trackerDto };

                var partEoltTask = _partService.GetPartsByPartNumbersAsync<PartEoltDTO>(new[] { tracker.Tracker.PartNum });
                var vendorTask = _vendorService.GetVendorsByNumbersAsync(new[] { tracker.Tracker.VendorNum });
                var logsTask = GetTrackerLogsAsync(trackerId);

                await Task.WhenAll(partEoltTask, vendorTask, logsTask);

                tracker.PartEolt = partEoltTask.Result.FirstOrDefault() ?? new PartEoltDTO();
                tracker.Vendor = vendorTask.Result.FirstOrDefault();
                tracker.TrackerLogs = logsTask.Result;

                var partsWithVendorInfo = await _vendorService.GetPartsWithPrimaryVendorByPartNum(
                    [tracker.Tracker.PartNum], 
                    includeInactiveParts: true, 
                    includeInactiveVendors: true);
        
                var vendorPartInfo = partsWithVendorInfo.FirstOrDefault();
                if (vendorPartInfo != null)
                {
                    tracker.PartEolt.VendorPartNum = vendorPartInfo.VendorPartNum;
                    tracker.PartEolt.MfgPartNum = vendorPartInfo.MfgPartNum;
                    tracker.PartEolt.MfgName = vendorPartInfo.MfgName;
                }

                return tracker;
            }

            return null;
        }

        public async Task<CMHub_VendorCommsTrackerModel?> GetTrackerByPartNumAsync(string partNum)
        {
            var trackerColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_VendorCommsTrackerDTO>();
            var query = $"SELECT {trackerColumns} FROM {TrackerTable} T WHERE T.PartNum = @PartNum;";

            using var conn = new SqlConnection(_connectionString);
            var trackerDto = await conn.QuerySingleOrDefaultAsync<CMHub_VendorCommsTrackerDTO>(query, new { PartNum = partNum });

            if (trackerDto != null)
            {
                var tracker = new CMHub_VendorCommsTrackerModel { Tracker = trackerDto };

                var partEoltTask = _partService.GetPartsByPartNumbersAsync<PartEoltDTO>(new[] { tracker.Tracker.PartNum });
                var vendorTask = _vendorService.GetVendorsByNumbersAsync(new[] { tracker.Tracker.VendorNum });
                var logsTask = GetTrackerLogsAsync(tracker.Tracker.Id);

                await Task.WhenAll(partEoltTask, vendorTask, logsTask);

                tracker.PartEolt = partEoltTask.Result.FirstOrDefault() ?? new PartEoltDTO();
                tracker.Vendor = vendorTask.Result.FirstOrDefault();
                tracker.TrackerLogs = logsTask.Result;

                var partsWithVendorInfo = await _vendorService.GetPartsWithPrimaryVendorByPartNum(
                    [tracker.Tracker.PartNum], 
                    includeInactiveParts: true, 
                    includeInactiveVendors: true);
        
                var vendorPartInfo = partsWithVendorInfo.FirstOrDefault();
                if (vendorPartInfo != null)
                {
                    tracker.PartEolt.VendorPartNum = vendorPartInfo.VendorPartNum;
                    tracker.PartEolt.MfgPartNum = vendorPartInfo.MfgPartNum;
                    tracker.PartEolt.MfgName = vendorPartInfo.MfgName;
                }

                return tracker;
            }

            return null;
        }

        public async Task<List<CMHub_VendorCommsTrackerModel>> GetTrackersAsync(bool includeInactiveVendors = false, bool includeDeleted = false)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);

                // 1. Get all parts with their primary vendor
                var allPartsWithVendors = await _vendorService.GetAllPartsWithPrimaryVendorAsync(includeInactiveVendors: includeInactiveVendors);

                // Then get the EOLT data for those parts (stored in the KineticERP database)
                var partNumbers = allPartsWithVendors.Select(p => p.PartNum).ToList();
                var eoltData = await _partService.GetPartsByPartNumbersAsync<PartEoltDTO>(partNumbers);

                // Get vendor part information to populate VendorPartNum, MfgPartNum, and MfgName
                var partsWithVendorInfo = await _vendorService.GetPartsWithPrimaryVendorByPartNum(partNumbers, includeInactiveParts: true, includeInactiveVendors: includeInactiveVendors);

                // Create a dictionary for quick EOLT data lookup by PartNum
                var eoltDataDict = eoltData.ToDictionary(e => e.PartNum, e => e);

                // Create a dictionary for quick vendor part info lookup by PartNum
                var vendorPartInfoDict = partsWithVendorInfo.ToDictionary(p => p.PartNum, p => p);

                // Populate the vendor part information in the EOLT data
                foreach (var eoltItem in eoltData)
                {
                    if (vendorPartInfoDict.TryGetValue(eoltItem.PartNum, out var vendorPartInfo))
                    {
                        eoltItem.VendorPartNum = vendorPartInfo.VendorPartNum;
                        eoltItem.MfgPartNum = vendorPartInfo.MfgPartNum;
                        eoltItem.MfgName = vendorPartInfo.MfgName;
                    }
                }

                // 2. Get all existing trackers from the database using DTO mapping
                var trackerColumns = DataHelpers.DTOPropertiesToSQLColumnsString<CMHub_VendorCommsTrackerDTO>();
                var trackerQuery = $@"
                    SELECT {trackerColumns}
                    FROM {TrackerTable} T
                    WHERE T.Deleted = @IncludeDeleted OR @IncludeDeleted = 1;";
                var existingTrackerDtos = (await conn.QueryAsync<CMHub_VendorCommsTrackerDTO>(trackerQuery, new { IncludeDeleted = includeDeleted })).ToList();

                // Convert DTOs to Models
                var existingTrackers = existingTrackerDtos.Select(dto => new CMHub_VendorCommsTrackerModel { Tracker = dto }).ToList();
                var existingTrackersDict = existingTrackers.ToDictionary(t => t.Tracker.PartNum);

                // 3. Batch load all latest response dates for existing trackers
                if (existingTrackers.Any())
                {
                    var trackerIds = existingTrackers.Select(t => t.Tracker.Id).ToList();
                    var responseDates = await _surveyService.GetLatestResponseDatesForTrackersAsync(trackerIds);
                    
                    foreach (var tracker in existingTrackers)
                    {
                        if (responseDates.TryGetValue(tracker.Tracker.Id, out var responseDate))
                        {
                            tracker.LatestSurveyResponseDate = responseDate;
                        }
                    }
                }

                // 4. Merge the two lists
                var finalTrackers = new List<CMHub_VendorCommsTrackerModel>();
                foreach (var partVendorPair in allPartsWithVendors)
                {
                    if (existingTrackersDict.TryGetValue(partVendorPair.PartNum, out var existingTracker))
                    {
                        // This part has a tracker, use the data from the DB
                        existingTracker.Vendor = partVendorPair.PrimaryVendor;

                        // Assign EOLT data if available
                        if (eoltDataDict.TryGetValue(partVendorPair.PartNum, out var partEoltData))
                        {
                            existingTracker.PartEolt = partEoltData;
                        }

                        finalTrackers.Add(existingTracker);
                    }
                    else
                    {
                        // This part does not have a tracker, create a new in-memory model
                        var newTracker = new CMHub_VendorCommsTrackerModel
                        {
                            Tracker = new CMHub_VendorCommsTrackerDTO
                            {
                                Id = 0, // Indicates it's not in the DB
                                PartNum = partVendorPair.PartNum,
                                VendorNum = partVendorPair.PrimaryVendor?.VendorNum ?? 0
                            },
                            Vendor = partVendorPair.PrimaryVendor,
                            IsNew = true
                        };

                        // Assign EOLT data if available
                        if (eoltDataDict.TryGetValue(partVendorPair.PartNum, out var partEoltData))
                        {
                            newTracker.PartEolt = partEoltData;
                        }

                        finalTrackers.Add(newTracker);
                    }
                }

                // 5. Batch load processable responses for trackers with unprocessed responses
                var trackersWithUnprocessedResponses = finalTrackers
                    .Where(t => t.Tracker.Id > 0 && t.HasUnprocessedResponseDate)
                    .ToList();
                    
                if (trackersWithUnprocessedResponses.Any())
                {
                    var trackerIds = trackersWithUnprocessedResponses.Select(t => t.Tracker.Id).ToList();
                    var processableResponsesMap = await _surveyService.GetProcessableResponsesForTrackersAsync(trackerIds);
                    
                    foreach (var tracker in trackersWithUnprocessedResponses)
                    {
                        if (processableResponsesMap.TryGetValue(tracker.Tracker.Id, out var hasProcessable))
                        {
                            tracker.HasProcessableResponses = hasProcessable;
                        }
                    }
                }

                return finalTrackers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Vendor Communication Trackers.");
                throw;
            }
        }

        private async Task<Dictionary<(string PartNum, int VendorNum), (string? VendorPartNum, string? MfgPartNum, string? MfgName)>> GetVendorPartInfoAsync(List<string> partNumbers)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                
                var query = @"
                    SELECT 
                        PartNum,
                        VendorNum,
                        VendPartNum,
                        MfgPartNum,
                        MfgName
                    FROM (
                        SELECT 
                            vp.PartNum, 
                            vp.VendorNum, 
                            vx.VendPartNum, 
                            vx.MfgPartNum, 
                            m.Name AS MfgName,
                            ROW_NUMBER() OVER (PARTITION BY vp.PartNum, vp.VendorNum ORDER BY vp.PartNum, vp.VendorNum, vp.EffectiveDate DESC) rn
                        FROM Erp.VendPart vp
                        LEFT JOIN Erp.PartXRefVend vx ON vx.Company = vp.Company AND vx.PartNum = vp.PartNum AND vx.VendorNum = vp.VendorNum
                        LEFT JOIN Erp.Manufacturer m ON m.Company = vx.Company AND vx.MfgNum = m.MfgNum
                        WHERE vp.Company = 'CG' AND vp.PartNum IN @PartNumbers
                    ) sub
                    WHERE rn = 1";

                var results = await conn.QueryAsync<dynamic>(query, new { PartNumbers = partNumbers });
                
                return results.ToDictionary(
                    r => ((string)r.PartNum, (int)r.VendorNum),
                    r => ((string?)r.VendPartNum, (string?)r.MfgPartNum, (string?)r.MfgName)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vendor part info");
                // Return empty dictionary if there's an error, so the main process can continue
                return new Dictionary<(string, int), (string?, string?, string?)>();
            }
        }

        public async Task<List<CMHub_VendorCommsTrackerLogModel>> GetTrackerLogsAsync(int trackerId, bool includeDeleted = false)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = $@"
                SELECT
                    Id, TrackerId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry
                FROM {LogTable}
                WHERE TrackerId = @TrackerId
                  AND (Deleted = @IncludeDeleted OR @IncludeDeleted = 1)
                ORDER BY LogDate DESC;";

            var parameters = new
            {
                TrackerId = trackerId,
                IncludeDeleted = includeDeleted ? 1 : 0
            };

            try
            {
                var logs = (await conn.QueryAsync<CMHub_VendorCommsTrackerLogModel>(query, parameters)).ToList();

                var employeeNumbers = logs
                    .Where(l => !string.IsNullOrEmpty(l.LoggedByUser))
                    .Select(l => l.LoggedByUser!)
                    .Distinct()
                    .ToList();

                if (employeeNumbers.Count > 0)
                {
                    var employees = await _adUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(employeeNumbers);
                    var empDict = employees.ToDictionary(e => e.EmployeeNumber, e => e);

                    foreach (var log in logs)
                    {
                        if (empDict.TryGetValue(log.LoggedByUser!, out var emp))
                            log.Employee = emp;
                    }
                }

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching tracker-level logs for Tracker ID {trackerId}");
                throw;
            }
        }

        public async Task CreateTrackerLogAsync(CMHub_VendorCommsTrackerLogModel log, string loggedByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            const string query = $@"
                INSERT INTO {LogTable} (TrackerId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry)
                OUTPUT INSERTED.Id
                VALUES (@TrackerId, @LogMessage, @LogDate, @LoggedByUser, @Deleted, @ManualLogEntry);";

            var parameters = new
            {
                log.TrackerId,
                log.LogMessage,
                LogDate = DateTime.UtcNow,
                LoggedByUser = loggedByEmpId,
                Deleted = log.Deleted ? 1 : 0,
                ManualLogEntry = log.ManualLogEntry ? 1 : 0
            };

            try
            {
                var insertedId = await conn.ExecuteScalarAsync<int>(query, parameters, transaction);

                log.Id = insertedId;

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Error creating log for Tracker ID {log.TrackerId}");
                throw;
            }
        }

        private async Task<int?> GetMostRecentBatchIdForTrackerAsync(int trackerId)
        {
            using var conn = new SqlConnection(_connectionString);
            
            var query = $@"
                SELECT TOP 1 bp.SurveyBatchID
                FROM {SurveyBatchPartTable} bp
                WHERE bp.PartStatusTrackerID = @TrackerId
                AND bp.SubmissionStatus = 'Submitted'
                ORDER BY bp.Id DESC";
            
            return await conn.QueryFirstOrDefaultAsync<int?>(query, new { TrackerId = trackerId });
        }
    }
}