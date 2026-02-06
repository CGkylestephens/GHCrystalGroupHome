using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Models;
using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Helpers;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Data
{
    public interface ICMHub_CustCommsService
    {
        Task<List<CMHub_CustCommsPartChangeTrackerModel>> GetPartChangeTrackersAsync(bool includeDeleted = false);
        Task<CMHub_CustCommsPartChangeTrackerModel?> GetPartChangeTrackerByPartNumAsync(string partNum, bool includeDeleted = false);
        Task<CMHub_CustCommsPartChangeTaskModel?> GetPartChangeTaskByTaskIdAsync(int taskId, bool includeDeleted = false);
        Task<List<CMHub_CustCommsPartChangeTaskLogModel>?> GetPartChangeTaskLogsAsync(int taskId, bool includeDeleted = false);
        Task<List<CMHub_CustCommsPartChangeTaskLogModel>> GetPartChangeTrackerLogsAsync(int trackerId, bool includeDeleted = false);
        Task<CMHub_CustCommsPartChangeTrackerModel> GetCMDexPartChangeTaskData(CMHub_CustCommsPartChangeTrackerModel model);

        Task<List<CMHub_CustCommsPartChangeTaskStatusDTO>> GetTaskStatusesAsync(bool includeDeleted = false);
        Task<int> GetInitialTaskStatusIdAsync();
        Task<int> GetInitialTechServicesStatusIdAsync();
        Task<int> GetFinalTaskStatusIdAsync();
        Task<bool> UpdateTaskStatusAsync(CMHub_CustCommsPartChangeTaskModel task, int? newStatusId, string employeeId);
        Task<bool> UpdateTaskECNAsync(CMHub_CustCommsPartChangeTaskModel task, string newECN, string employeeId);

        Task<CMHub_CustCommsPartChangeTrackerModel> CreatePartChangeTrackerAsync(CMHub_CustCommsPartChangeTrackerModel tracker, string loggedByEmpId);
        Task<int> CreatePartChangeTaskAsync(CMHub_CustCommsPartChangeTaskModel task, int trackerId, int type, bool manual = false, string loggedByEmpId = "");
        Task CreatePartChangeTaskLogAsync(CMHub_CustCommsPartChangeTaskLogModel log, string loggedByEmpId);

        Task UpdateTaskLogAsync(CMHub_CustCommsPartChangeTaskLogModel log, string loggedByEmpId);
        Task UpdateTaskAsync(CMHub_CustCommsPartChangeTaskModel task);
        Task UpdatePartEoltDataAsync(PartEoltDTO partEolt);
        Task UpdateTrackerWithChangeLoggingAsync(CMHub_CustCommsPartChangeTrackerModel tracker, CMHub_CustCommsPartChangeTrackerModel? originalTracker, string employeeId);

        Task HardDeletePartChangeTrackerAsync(int trackerId);
        Task SoftDeletePartChangeTrackerAsync(int trackerId, string loggedByEmpId);

        Task SoftDeleteTaskByIdAsync(int taskId, string loggedByEmpId);

        // Tech Services LTB methods
        Task<int> CreateOrGetTechServicesTaskAsync(int trackerId, string loggedByEmpId);
        Task<bool> UpdateTechServicesLTBQuantityAsync(int trackerId, int? quantity, string employeeId);
    }

    public class CMHub_CustCommsService : ICMHub_CustCommsService
    {
        private readonly string _connectionString;
        private readonly ILogger<CMHub_CustCommsService> _logger;
        private readonly DebugModeService _debugModeService;
        private readonly IPartService _partService;
        private readonly IADUserService _adUserService;
        private readonly ICMHub_CMDexService _cmDexService;
        private readonly ICustomerService _customerService;

        // Table Stuff
        private const string TrackerTable = "[dbo].[CMCustComms_PartChangeTracker]";
        private const string TaskTable = "[dbo].[CMCustComms_PartChangeTask]";
        private const string StatusTable = "[dbo].[CMCustComms_PartChangeTaskStatus]";
        private const string LogTable = "[dbo].[CMCustComms_PartChangeTaskLog]" ;

        public CMHub_CustCommsService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<CMHub_CustCommsService> logger,
            DebugModeService debugModeService,
            IPartService partService,
            IADUserService adUserService,
            ICMHub_CMDexService cmDexService,
            ICustomerService customerService,
            NavigationManager navigationManager,
            IJSRuntime js)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _debugModeService = debugModeService;
            _partService = partService;
            _adUserService = adUserService;
            _cmDexService = cmDexService;
            _customerService = customerService;
        }

        public async Task<List<CMHub_CustCommsPartChangeTrackerModel>> GetPartChangeTrackersAsync(bool includeDeleted = false)
        {
            var trackerModels = new List<CMHub_CustCommsPartChangeTrackerModel>();
            try
            {
                using var conn = new SqlConnection(_connectionString);

                // 1. Fetch All Trackers (include TechServicesLTBQuantity)
                var trackerQuery = $@"
                    SELECT t.Id, t.PartNum, t.Deleted, t.TechServicesLTBQuantity
                    FROM {TrackerTable} t
                    WHERE t.Deleted = @IncludeDeleted OR @IncludeDeleted = 1;";
                var trackers = (await conn.QueryAsync<dynamic>(trackerQuery, new { IncludeDeleted = includeDeleted })).ToList();
                await _debugModeService.SqlQueryDebugMessage(
                    trackerQuery.Replace("@IncludeDeleted", includeDeleted ? "1" : "0"),
                    trackers,
                    "Trackers Query Result");

                if (!trackers.Any()) return trackerModels;

                var trackerIds = trackers.Select(t => (int)t.Id).ToList();
                var trackerPartNums = trackers.Select(t => (string)t.PartNum).Distinct().ToList();

                // 2. Fetch Base Part Info (for all tracker parts)
                var partDescriptions = new Dictionary<string, string>();
                var partRevisions = new Dictionary<string, string>();
                if (_partService != null && trackerPartNums.Any())
                {
                    var partsInfo = await _partService.GetPartsByPartNumbersAsync<PartDTO_Base>(trackerPartNums);
                    if (partsInfo != null && partsInfo.Any())
                    {
                        partDescriptions = partsInfo.ToDictionary(p => p.PartNum, p => p.PartDescription ?? "N/A");
                        partRevisions = partsInfo.ToDictionary(p => p.PartNum, p => p.RevisionNum ?? string.Empty);
                    }
                }

                // 3. Fetch EOLT data for all tracker parts using the Epicor API
                var eoltDataDict = new Dictionary<string, PartEoltDTO>();
                if (_partService != null && trackerPartNums.Any())
                {
                    var eoltData = await _partService.GetPartsByPartNumbersAsync<PartEoltDTO>(trackerPartNums);
                    if (eoltData != null && eoltData.Any())
                    {
                        eoltDataDict = eoltData.ToDictionary(e => e.PartNum, e => e);
                    }
                }

                // 4. Fetch All Tasks for these Trackers
                var taskQuery = $@"
                    SELECT tk.Id, tk.TrackerId, tk.PartNum AS ImpactedPartNum, tk.StatusId,
                           tk.Completed, tk.Deleted, tk.Type,
                           (SELECT MAX(l.LogDate) FROM {LogTable} l WHERE l.TaskId = tk.Id AND l.Deleted = 0) AS LastUpdated,
                           tk.ECN
                    FROM {TaskTable} tk
                    WHERE tk.TrackerId IN @TrackerIds AND (tk.Deleted = @IncludeDeleted OR @IncludeDeleted = 1);";
                var allTasks = (await conn.QueryAsync<dynamic>(taskQuery, new { TrackerIds = trackerIds, IncludeDeleted = includeDeleted })).ToList();
                await _debugModeService.SqlQueryDebugMessage(
                     taskQuery.Replace("@TrackerIds", $"({string.Join(",", trackerIds)})").Replace("@IncludeDeleted", includeDeleted ? "1" : "0"),
                     allTasks,
                    "All Tasks Query Result");

                // Create a lookup for tasks by TrackerId
                var tasksLookup = allTasks.ToLookup(task => (int)task.TrackerId);

                // 5. Fetch All Logs (Task and Tracker) for these Trackers
                var logsQuery = $@"
                    SELECT Id, TaskId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry, TrackerId
                    FROM {LogTable}
                    WHERE TrackerId IN @TrackerIds AND (Deleted = @IncludeDeleted OR @IncludeDeleted = 1)
                    ORDER BY LogDate DESC;";
                var allLogs = (await conn.QueryAsync<CMHub_CustCommsPartChangeTaskLogModel>(logsQuery, new { TrackerIds = trackerIds, IncludeDeleted = includeDeleted })).ToList();

                // Create a lookup for logs by TaskId (null key for tracker logs) and group by TrackerId first for efficiency
                var logsGroupedByTracker = allLogs.ToLookup(log => log.TrackerId);
                // Then create the TaskId lookup (null for tracker logs) *within* each tracker group when needed later

                // 6. Collect Required Part Nums for Batch CMDex Fetch (from Type 1 tasks)
                var requiredPartNums = allTasks
                    .Where(task => task.Type == 1 && task.ImpactedPartNum != null)
                    .Select(task => (string)task.ImpactedPartNum)
                    .Distinct()
                    .ToList();

                // 7. Batch Fetch CMDex Data
                Dictionary<string, CMHub_CMDexPartModel> cmDexPartData = new();
                if (requiredPartNums.Any())
                {
                    var fetchedPartsList = await _cmDexService.GetCMDexPartsByPartNumbersAsync(requiredPartNums);
                    if (fetchedPartsList != null)
                    {
                        cmDexPartData = fetchedPartsList.ToDictionary(p => p.Part.PartNum);
                    }
                }

                // 8. Batch Fetch AD Users
                // Collect all needed employee IDs from logs AND from the fetched CMDex parts
                var logEmpNumbers = allLogs
                                    .Where(l => !string.IsNullOrEmpty(l.LoggedByUser))
                                    .Select(l => l.LoggedByUser!);
                var cmDexEmpIds = cmDexPartData.Values
                                    .SelectMany(p => p.PartEmployees)
                                    .Select(pe => pe.EmpID);

                var allEmpIdsToFetch = logEmpNumbers.Concat(cmDexEmpIds)
                                        .Where(id => !string.IsNullOrEmpty(id))
                                        .Distinct()
                                        .ToList();

                Dictionary<string, ADUserDTO_Base> adUserDict = new();
                if (allEmpIdsToFetch.Any())
                {
                    var adUsers = await _adUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(allEmpIdsToFetch);
                    if (adUsers != null)
                    {
                        adUserDict = adUsers.ToDictionary(u => u.EmployeeNumber);
                    }
                }

                // 9. Assemble the Final Models
                foreach (var tracker in trackers)
                {
                    int currentTrackerId = (int)tracker.Id;
                    var currentTrackerLogs = logsGroupedByTracker[currentTrackerId]; // Get logs for this tracker
                    var currentLogLookupByTask = currentTrackerLogs.ToLookup(log => log.TaskId); // Group this tracker's logs by TaskId

                    // Assign pre-fetched AD Users to tracker logs
                    var trackerLevelLogs = currentLogLookupByTask[null].ToList();
                    foreach (var log in trackerLevelLogs)
                    {
                        if (!string.IsNullOrEmpty(log.LoggedByUser) && adUserDict.TryGetValue(log.LoggedByUser, out var logUser))
                        {
                            log.Employee = logUser;
                        }
                    }

                    var model = new CMHub_CustCommsPartChangeTrackerModel
                    {
                        Id = currentTrackerId,
                        PartNum = (string)tracker.PartNum,
                        PartDesc = partDescriptions.GetValueOrDefault((string)tracker.PartNum, "N/A"),
                        PartRev = partRevisions.GetValueOrDefault((string)tracker.PartNum, string.Empty),
                        Deleted = tracker.Deleted,
                        TechServicesLTBQuantity = tracker.TechServicesLTBQuantity,
                        CMPartTasks = new List<CMHub_CustCommsPartChangeTaskModel>(),
                        WhereUsedPartTasks = new List<CMHub_CustCommsPartChangeTaskModel>(),
                        TrackerLogs = trackerLevelLogs
                    };

                    // Assign EOLT data if available (mirroring VendorCommsService approach)
                    if (eoltDataDict.TryGetValue(model.PartNum, out var partEoltData))
                    {
                        model.PartEolt = partEoltData;
                    }

                    // Process tasks for this tracker using the lookup
                    foreach (var task in tasksLookup[currentTrackerId])
                    {
                        // Assign pre-fetched AD Users to task logs
                        var taskLevelLogs = currentLogLookupByTask[(int?)task.Id].ToList();
                        foreach (var log in taskLevelLogs)
                        {
                            if (!string.IsNullOrEmpty(log.LoggedByUser) && adUserDict.TryGetValue(log.LoggedByUser, out var logUser))
                            {
                                log.Employee = logUser;
                            }
                        }

                        var taskModel = new CMHub_CustCommsPartChangeTaskModel
                        {
                            Id = (int)task.Id,
                            TrackerId = currentTrackerId, // Use currentTrackerId
                            TrackerPartNum = model.PartNum,
                            ImpactedPartNum = task.ImpactedPartNum,
                            StatusId = task.StatusId,
                            Completed = task.Completed,
                            Deleted = task.Deleted,
                            LastUpdated = task.LastUpdated ?? DateTime.MinValue,
                            Logs = taskLevelLogs, // Assign logs for this task
                            ECNNumber = task.ECN,
                            Type = task.Type
                        };

                        if (task.Type == 1 && taskModel.ImpactedPartNum != null)
                        {
                            if (cmDexPartData.TryGetValue(taskModel.ImpactedPartNum, out var preFetchedPartModel))
                            {
                                taskModel.CMDexPart = preFetchedPartModel;
                                // Assign pre-fetched AD Users to CMDex part employees
                                if (taskModel.CMDexPart.PartEmployees != null)
                                {
                                    taskModel.CMDexPart.ADEmployees = taskModel.CMDexPart.PartEmployees
                                        .Select(pe => adUserDict.TryGetValue(pe.EmpID, out var adUser) ? adUser : null)
                                        .Where(adUser => adUser != null)
                                        .ToList()!;
                                }
                                model.CMPartTasks.Add(taskModel);
                            }
                            else
                            {
                                _logger.LogWarning("Tracker {TrackerId}, Task {TaskId}: CMDex data not found for required PartNum {PartNum} during batch fetch.", currentTrackerId, taskModel.Id, taskModel.ImpactedPartNum);
                            }
                        }
                        else if (task.Type == 2)
                        {
                            model.WhereUsedPartTasks.Add(taskModel);
                        }
                        else if (task.Type == 3)
                        {
                            // Tech Services task
                            model.TechServicesTask = taskModel;
                        }
                    }
                    trackerModels.Add(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Customer Communication Part Change Trackers.");
                throw; // Re-throw after logging
            }
            return trackerModels;
        }

        public async Task<CMHub_CustCommsPartChangeTrackerModel?> GetPartChangeTrackerByPartNumAsync(string partNum, bool includeDeleted = false)
        {
            CMHub_CustCommsPartChangeTrackerModel? model = null;
            try
            {
                using var conn = new SqlConnection(_connectionString);

                // 1. Fetch Tracker (include TechServicesLTBQuantity)
                var trackerQuery = $@"
                    SELECT t.Id, t.PartNum, t.Deleted, t.TechServicesLTBQuantity
                    FROM {TrackerTable} t
                    WHERE t.PartNum = @PartNum AND (t.Deleted = @IncludeDeleted OR @IncludeDeleted = 1);";
                var tracker = await conn.QuerySingleOrDefaultAsync<dynamic>(trackerQuery, new { PartNum = partNum, IncludeDeleted = includeDeleted });
                await _debugModeService.SqlQueryDebugMessage<dynamic>(
                    trackerQuery.Replace("@PartNum", $"'{partNum}'").Replace("@IncludeDeleted", includeDeleted ? "1" : "0"),
                    tracker,
                    "Tracker Query Result");

                if (tracker == null) return null;

                // 2. Fetch Base Part Info (for the main tracker part)
                string partDesc = "N/A";
                string partRev = string.Empty;
                if (_partService != null)
                {
                    // Use batch fetch even for one, in case PartService is optimized for it
                    var partsInfo = await _partService.GetPartsByPartNumbersAsync<PartDTO_Base>(new List<string> { (string)tracker.PartNum });
                    if (partsInfo != null && partsInfo.Any())
                    {
                        var part = partsInfo.First();
                        partDesc = part.PartDescription ?? "N/A";
                        partRev = part.RevisionNum ?? string.Empty;
                    }
                }

                // 3. Fetch EOLT data for the tracker part using the Epicor API
                PartEoltDTO? partEoltData = null;
                if (_partService != null)
                {
                    var eoltResults = await _partService.GetPartsByPartNumbersAsync<PartEoltDTO>(new List<string> { (string)tracker.PartNum });
                    partEoltData = eoltResults?.FirstOrDefault();
                }

                // Initialize the main model
                model = new CMHub_CustCommsPartChangeTrackerModel
                {
                    Id = (int)tracker.Id,
                    PartNum = (string)tracker.PartNum,
                    PartDesc = partDesc,
                    PartRev = partRev,
                    Deleted = tracker.Deleted,
                    TechServicesLTBQuantity = tracker.TechServicesLTBQuantity,
                    CMPartTasks = new List<CMHub_CustCommsPartChangeTaskModel>(),
                    WhereUsedPartTasks = new List<CMHub_CustCommsPartChangeTaskModel>(),
                    TrackerLogs = new List<CMHub_CustCommsPartChangeTaskLogModel>()
                };

                // Assign EOLT data if available
                if (partEoltData != null)
                {
                    model.PartEolt = partEoltData;
                }

                // 4. Fetch All Tasks for the Tracker
                var taskQuery = $@"
                    SELECT tk.Id, tk.TrackerId, tk.PartNum AS ImpactedPartNum, tk.StatusId,
                           tk.Completed, tk.Deleted, tk.Type,
                           (SELECT MAX(l.LogDate) FROM {LogTable} l WHERE l.TaskId = tk.Id AND l.Deleted = 0) AS LastUpdated,
                           tk.ECN
                    FROM {TaskTable} tk
                    WHERE tk.TrackerId = @TrackerId AND (tk.Deleted = @IncludeDeleted OR @IncludeDeleted = 1);";
                List<CMHub_CustCommsPartChangeTaskModel> tasks = (await conn.QueryAsync<CMHub_CustCommsPartChangeTaskModel>(taskQuery, new { TrackerId = model.Id, IncludeDeleted = includeDeleted })).ToList();
                await _debugModeService.SqlQueryDebugMessage<IEnumerable<dynamic>>(
                    taskQuery.Replace("@TrackerId", model.Id.ToString()).Replace("@IncludeDeleted", includeDeleted ? "1" : "0"),
                    tasks,
                    "Tasks Query Result");

                // 5. Fetch All Logs (Task and Tracker) for the Tracker
                var logsQuery = $@"
                    SELECT Id, TaskId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry, TrackerId
                    FROM {LogTable}
                    WHERE TrackerId = @TrackerId AND (Deleted = @IncludeDeleted OR @IncludeDeleted = 1)
                    ORDER BY LogDate DESC;";
                var allLogs = (await conn.QueryAsync<CMHub_CustCommsPartChangeTaskLogModel>(logsQuery, new { TrackerId = model.Id, IncludeDeleted = includeDeleted })).ToList();
                var logLookup = allLogs.ToLookup(log => log.TaskId);
                model.TrackerLogs = logLookup[null].ToList(); // Assign tracker logs

                // 6. Collect Required Part Nums for Batch CMDex Fetch
                var requiredPartNums = tasks
                    .Where(task => task.Type == 1 && task.ImpactedPartNum != null)
                    .Select(task => task.ImpactedPartNum)
                    .Distinct()
                    .ToList();

                // 7. Batch Fetch CMDex Data
                Dictionary<string, CMHub_CMDexPartModel> cmDexPartData = new();
                if (requiredPartNums.Any())
                {
                    var fetchedPartsList = await _cmDexService.GetCMDexPartsByPartNumbersAsync(requiredPartNums);
                    if (fetchedPartsList != null)
                    {
                        cmDexPartData = fetchedPartsList.ToDictionary(p => p.Part.PartNum);
                    }
                }

                // 8. Process Tasks and Assign Pre-fetched Data
                foreach (var task in tasks)
                {
                    task.Logs = allLogs.Where(_ => _.TaskId != null && _.TaskId == task.Id).ToList();
                    if (task.Type == 1 && task.ImpactedPartNum != null)
                    {
                        if (cmDexPartData.TryGetValue(task.ImpactedPartNum, out var preFetchedPartModel))
                        {
                            task.CMDexPart = preFetchedPartModel;
                        }
                        else
                        {
                            _logger.LogWarning("Task {TaskId}: CMDex data not found for required PartNum {PartNum} during batch fetch.", task.Id, task.ImpactedPartNum);
                        }

                        model.CMPartTasks.Add(task);
                    }
                    else if (task.Type == 2)
                    {
                        model.WhereUsedPartTasks.Add(task);
                    }
                    else if (task.Type == 3)
                    {
                        // Tech Services task
                        model.TechServicesTask = task;
                    }
                }

                // 9. Batch Fetch and Assign AD Users
                // Collect all needed employee IDs from logs AND from the fetched CMDex parts
                var logEmpNumbers = allLogs
                                    .Where(l => !string.IsNullOrEmpty(l.LoggedByUser))
                                    .Select(l => l.LoggedByUser!);
                var cmDexEmpIds = cmDexPartData.Values
                                    .SelectMany(p => p.PartEmployees)
                                    .Select(pe => pe.EmpID);
                var allEmpIdsToFetch = logEmpNumbers.Concat(cmDexEmpIds)
                                        .Where(id => !string.IsNullOrEmpty(id))
                                        .Distinct()
                                        .ToList();

                Dictionary<string, ADUserDTO_Base> adUserDict = new();
                if (allEmpIdsToFetch.Any())
                {
                    var adUsers = await _adUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(allEmpIdsToFetch);
                    if (adUsers != null)
                    {
                        adUserDict = adUsers.ToDictionary(u => u.EmployeeNumber);
                    }
                }

                // Assign AD Users to Logs
                foreach (var log in allLogs) // Iterate the original flat list of all logs
                {
                    if (!string.IsNullOrEmpty(log.LoggedByUser) && adUserDict.TryGetValue(log.LoggedByUser, out var logUser))
                    {
                        log.Employee = logUser;
                    }
                }

                // Assign AD Users to CMDex Parts within Tasks
                foreach (var cmTask in model.CMPartTasks)
                {
                    if (cmTask.CMDexPart != null)
                    {
                        cmTask.CMDexPart.ADEmployees = cmTask.CMDexPart.PartEmployees
                            .Select(pe => adUserDict.TryGetValue(pe.EmpID, out var adUser) ? adUser : null)
                            .Where(adUser => adUser != null)
                            .ToList()!; // Assign pre-fetched AD users
                    }
                }

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Customer Communication Part Change Tracker for PartNum: {partNum}");
                // Consider how to handle partial data if model was partially populated
                return null; // Or throw, depending on desired behavior
            }
        }

        public async Task<CMHub_CustCommsPartChangeTaskModel?> GetPartChangeTaskByTaskIdAsync(int taskId, bool includeDeleted = false)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var taskQuery = $@"
                    SELECT 
                        tk.Id, tk.TrackerId, tk.PartNum AS ImpactedPartNum,
                        tk.StatusId, tk.Completed, tk.Deleted, tk.Type,
                        (SELECT MAX(l.LogDate) FROM {LogTable} l WHERE l.TaskId = tk.Id AND l.Deleted = 0) AS LastUpdated,
                        tk.ECN, t.PartNum AS TrackerPartNum
                    FROM {TaskTable} tk
                    INNER JOIN {TrackerTable} t
                        ON t.Id = tk.TrackerId
                    WHERE tk.Id = @TaskId
                      AND (tk.Deleted = @IncludeDeleted OR @IncludeDeleted = 1);";

                var task = await conn.QuerySingleOrDefaultAsync<dynamic>(taskQuery, new { TaskId = taskId, IncludeDeleted = includeDeleted });

                if (task == null)
                    return null;

                List<PartIndentedWhereUsedDTO> indentedCMParts = await _partService.GetPartsIndentedWhereUsedByPartNumAsync<PartIndentedWhereUsedDTO>(task.TrackerPartNum, includeInactive: false, filterCmManaged: true);
                var parentPart = indentedCMParts.FirstOrDefault(_ => _.PartNum == task.ImpactedPartNum);
                var taskModel = new CMHub_CustCommsPartChangeTaskModel
                {
                    Id = task.Id,
                    TrackerId = task.TrackerId,
                    TrackerPartNum = task.TrackerPartNum,
                    ImpactedPartNum = task.ImpactedPartNum,
                    StatusId = task.StatusId,
                    Completed = task.Completed,
                    Deleted = task.Deleted,
                    LastUpdated = task.LastUpdated ?? DateTime.MinValue,
                    Logs = new List<CMHub_CustCommsPartChangeTaskLogModel>(),
                    ECNNumber = task.ECN,
                    ImpactedPartRev = parentPart?.RevisionNum ?? string.Empty,
                    ImpactedPartDesc = parentPart?.PartDescription ?? string.Empty,
                    Lvl = parentPart?.Lvl ?? -1,
                    QtyPer = parentPart?.QtyPer ?? -1,
                    Sort = parentPart?.Sort ?? string.Empty
                };

                var logsQuery = $@"
                    SELECT
                        Id, TaskId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry, TrackerId
                    FROM {LogTable}
                    WHERE TaskId = @TaskId
                      AND (Deleted = @IncludeDeleted OR @IncludeDeleted = 1)
                    ORDER BY LogDate DESC;";

                var logs = await conn.QueryAsync<CMHub_CustCommsPartChangeTaskLogModel>(
                    logsQuery, new { TaskId = taskId, IncludeDeleted = includeDeleted });

                taskModel.Logs = logs.ToList();

                // Fetch employee info for logs
                var employeeNumbers = taskModel.Logs
                    .Select(l => l.LoggedByUser!)
                    .Where(emp => !string.IsNullOrWhiteSpace(emp))
                    .Distinct()
                    .ToList();

                if (employeeNumbers.Count != 0)
                {
                    var employees = await _adUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(employeeNumbers);
                    var empDict = employees.ToDictionary(e => e.EmployeeNumber, e => e);

                    foreach (var log in taskModel.Logs)
                    {
                        if (empDict.TryGetValue(log.LoggedByUser!, out var emp))
                            log.Employee = emp;
                    }
                }

                return taskModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Task ID {taskId}");
                throw;
            }
        }

        public async Task<List<CMHub_CustCommsPartChangeTaskLogModel>?> GetPartChangeTaskLogsAsync(int taskId, bool includeDeleted = false)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var logsQuery = $@"
                    SELECT
                        Id, TaskId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry, TrackerId
                    FROM {LogTable}
                    WHERE TaskId = @TaskId
                      AND (Deleted = @IncludeDeleted OR @IncludeDeleted = 1)
                    ORDER BY LogDate DESC;";

                var logs = (await conn.QueryAsync<CMHub_CustCommsPartChangeTaskLogModel>(
                    logsQuery, new { TaskId = taskId, IncludeDeleted = includeDeleted }
                )).ToList();

                if (!logs.Any())
                    return new List<CMHub_CustCommsPartChangeTaskLogModel>();

                // Lookup and attach employee info if available
                var employeeNumbers = logs
                    .Where(l => !string.IsNullOrEmpty(l.LoggedByUser))
                    .Select(l => l.LoggedByUser!)
                    .Distinct()
                    .ToList();

                if (employeeNumbers.Count != 0)
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
                _logger.LogError(ex, $"Error fetching logs for Task ID {taskId}");
                throw;
            }
        }

        public async Task<List<CMHub_CustCommsPartChangeTaskLogModel>> GetPartChangeTrackerLogsAsync(int trackerId, bool includeDeleted = false)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = $@"
                SELECT
                    Id, TaskId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry, TrackerId
                FROM {LogTable}
                WHERE TrackerId = @TrackerId
                  AND TaskId IS NULL
                  AND (Deleted = @IncludeDeleted OR @IncludeDeleted = 1)
                ORDER BY LogDate DESC;";

            var parameters = new
            {
                TrackerId = trackerId,
                IncludeDeleted = includeDeleted ? 1 : 0
            };

            try
            {
                var logs = (await conn.QueryAsync<CMHub_CustCommsPartChangeTaskLogModel>(query, parameters)).ToList();

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

        public async Task<CMHub_CustCommsPartChangeTrackerModel> GetCMDexPartChangeTaskData(CMHub_CustCommsPartChangeTrackerModel trackerModel)
        {
            if (trackerModel == null) return new();

            // Get any missing CM Dex part data
            var requiredPartNums = trackerModel.CMPartTasks
                .Where(task => task.Type == 1 && task.ImpactedPartNum != null && task.CMDexPart == null)
                .Select(task => task.ImpactedPartNum)
                .Distinct()
                .ToList();

            Dictionary<string, CMHub_CMDexPartModel> cmDexPartData = new();
            if (requiredPartNums.Count != 0)
            {
                var fetchedPartsList = await _cmDexService.GetCMDexPartsByPartNumbersAsync(requiredPartNums);
                if (fetchedPartsList != null)
                {
                    cmDexPartData = fetchedPartsList.ToDictionary(p => p.Part.PartNum);
                }
            }

            // Assign newly fetched parts to their tasks
            if (trackerModel.CMPartTasks != null)
            {
                foreach (var task in trackerModel.CMPartTasks)
                {
                    if (task.CMDexPart == null && task.ImpactedPartNum != null && cmDexPartData.TryGetValue(task.ImpactedPartNum, out var preFetchedPartModel))
                    {
                        task.CMDexPart = preFetchedPartModel;
                    }
                }
            }

            // Collect all employee IDs from all CMDex parts (both existing and newly fetched)
            var allCmDexEmpIds = trackerModel.CMPartTasks
                .Where(t => t.CMDexPart?.PartEmployees != null)
                .SelectMany(t => t.CMDexPart!.PartEmployees)
                .Select(pe => pe.EmpID)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct();

            Dictionary<string, ADUserDTO_Base> adUserDict = new();
            if (allCmDexEmpIds.Any())
            {
                var adUsers = await _adUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(allCmDexEmpIds);
                if (adUsers != null)
                {
                    adUserDict = adUsers.ToDictionary(u => u.EmployeeNumber);
                }
            }

            if (trackerModel.CMPartTasks != null)
            {
                // Process all tasks that have a CMDexPart
                foreach (var task in trackerModel.CMPartTasks.Where(t => t.CMDexPart != null))
                {
                    // Assign pre-fetched AD Users to CMDex part employees
                    if (task.CMDexPart.PartEmployees != null)
                    {
                        task.CMDexPart.ADEmployees = task.CMDexPart.PartEmployees
                            .Select(pe => adUserDict.TryGetValue(pe.EmpID, out var adUser) ? adUser : null)
                            .Where(adUser => adUser != null)
                            .ToList()!;
                    }

                    // Sort Employees
                    task.CMDexPart.PartEmployees = task.CMDexPart.PartEmployees?
                        .OrderByDescending(pe => pe.IsPrimary)
                        .ToList() ?? new List<CMHub_PartEmployeeDTO>();

                    // Populate Contact Models if they haven't been already
                    if (task.CMDexPart.PartCustomerContactModels.Count == 0 && task.CMDexPart.PartCustomerContacts != null)
                    {
                        var contactViewModels = new List<CMHub_PartCustomerContactModel>();
                        foreach (var custCon in task.CMDexPart.PartCustomerContacts)
                        {
                            var displayContact = await _customerService.GetCustomerContactByCompoundKeyAsync<CustomerContactDTO_Base>(
                                custCon.CustNum, custCon.ConNum, custCon.PerConID);

                            contactViewModels.Add(new CMHub_PartCustomerContactModel
                            {
                                PartCustomerContact = custCon,
                                DisplayContact = displayContact ?? new CustomerContactDTO_Base { ConName = "Unknown Contact" }
                            });
                        }
                        task.CMDexPart.PartCustomerContactModels = contactViewModels;
                    }
                }
            }

            return trackerModel;
        }

        public async Task<List<CMHub_CustCommsPartChangeTaskStatusDTO>> GetTaskStatusesAsync(bool includeDeleted = false)
        {
            var query = $@"
                SELECT 
                    Id,
                    [Desc] as Description,
                    Deleted,
                    Code,
                    Sequence
                FROM {StatusTable}
                WHERE (@includeDeleted = 1 OR Deleted = 0)
                ORDER BY Sequence;";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var statuses = await conn.QueryAsync<CMHub_CustCommsPartChangeTaskStatusDTO>(query, new { includeDeleted });

                await _debugModeService.SqlQueryDebugMessage(query.Replace("@includeDeleted", includeDeleted ? "1" : "0"), statuses);

                return statuses.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Task Statuses.");
                throw;
            }
        }

        public async Task<int> GetInitialTaskStatusIdAsync()
        {
            var statuses = await GetTaskStatusesAsync();
            return statuses
                .Where(s => !s.Deleted && s.Sequence >= 0 && !s.Code.StartsWith("TS_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Sequence)
                .Select(s => s.Id)
                .FirstOrDefault();
        }

        public async Task<int> GetInitialTechServicesStatusIdAsync()
        {
            var statuses = await GetTaskStatusesAsync();
            return statuses
                .Where(s => !s.Deleted && s.Code.StartsWith("TS_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Sequence)
                .Select(s => s.Id)
                .FirstOrDefault();
        }

        public async Task<int> GetFinalTaskStatusIdAsync()
        {
            var statuses = await GetTaskStatusesAsync();
            return statuses
                .Where(s => !s.Deleted && !s.Code.StartsWith("TS_", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.Sequence)
                .Select(s => s.Id)
                .FirstOrDefault();
        }

        public async Task<bool> UpdateTaskStatusAsync(CMHub_CustCommsPartChangeTaskModel task, int? newStatusId, string employeeId)
        {
            var finalStatusId = await GetFinalTaskStatusIdAsync();
            var previousStatusId = task.StatusId;
            try
            {
                task.StatusId = newStatusId;
                task.Completed = newStatusId == finalStatusId;

                await UpdateTaskAsync(task);

                var statuses = await GetTaskStatusesAsync();
                var newStatusDesc = statuses.FirstOrDefault(s => s.Id == newStatusId)?.Description ?? "Unknown";

                var logMessage = $"Status Changed To: {newStatusDesc}";

                await CreatePartChangeTaskLogAsync(
                    new CMHub_CustCommsPartChangeTaskLogModel
                    {
                        TrackerId = task.TrackerId,
                        TaskId = task.Id,
                        LogMessage = logMessage
                    },
                    employeeId
                );

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UpdateStatus failed: {ex}");
                task.StatusId = previousStatusId;
                task.Completed = previousStatusId == finalStatusId;
                return false;
            }
        }

        public async Task<bool> UpdateTaskECNAsync(CMHub_CustCommsPartChangeTaskModel task, string newECN, string employeeId)
        {
            if (task == null || employeeId == null)
                return false;

            var oldECN = task.ECNNumber ?? string.Empty;
            var trimmedNewECN = newECN?.Trim() ?? string.Empty;

            if (oldECN == trimmedNewECN)
                return false;

            try
            {
                task.ECNNumber = trimmedNewECN;

                await UpdateTaskAsync(task);

                string logMessage = string.IsNullOrEmpty(oldECN)
                    ? $"ECN entered as '{trimmedNewECN}'"
                    : $"ECN changed from '{oldECN}' to '{trimmedNewECN}'";

                await CreatePartChangeTaskLogAsync(
                    new CMHub_CustCommsPartChangeTaskLogModel
                    {
                        TrackerId = task.TrackerId,
                        TaskId = task.Id,
                        LogMessage = logMessage
                    },
                    employeeId
                );

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UpdateTaskECNAsync error: {ex}");
                task.ECNNumber = oldECN; // Revert if failed
                return false;
            }
        }

        public async Task<CMHub_CustCommsPartChangeTrackerModel> CreatePartChangeTrackerAsync(CMHub_CustCommsPartChangeTrackerModel tracker, string loggedByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                var trackerInsertQuery = $@"
                    INSERT INTO {TrackerTable} (PartNum, Deleted)
                    OUTPUT INSERTED.Id
                    VALUES (@PartNum, @Deleted);";

                var trackerId = await conn.ExecuteScalarAsync<int>(
                    trackerInsertQuery,
                    new
                    {
                        tracker.PartNum,
                        Deleted = tracker.Deleted ? 1 : 0
                    },
                    transaction
                );
                tracker.Id = trackerId;

                const string logInsertQuery = $@"
                    INSERT INTO {LogTable} (TrackerId, LogMessage, LogDate, LoggedByUser, Deleted)
                    VALUES (@TrackerId, @LogMessage, @LogDate, @LoggedByUser, 0);";

                await conn.ExecuteAsync(
                    logInsertQuery,
                    new
                    {
                        TrackerId = tracker.Id,
                        LogMessage = "Tasks Created",
                        LogDate = DateTime.UtcNow,
                        LoggedByUser = loggedByEmpId
                    },
                    transaction
                );

                transaction.Commit();

                if (tracker.CMPartTasks != null && tracker.CMPartTasks.Count != 0)
                {
                    foreach (var task in tracker.CMPartTasks)
                    {
                        await CreatePartChangeTaskAsync(task, trackerId, 1);
                    }
                }

                if (tracker.WhereUsedPartTasks != null && tracker.WhereUsedPartTasks.Count != 0)
                {
                    foreach (var task in tracker.WhereUsedPartTasks)
                    {
                        await CreatePartChangeTaskAsync(task, trackerId, 2);
                    }
                }

                // Create Tech Services task automatically for each new tracker
                await CreateOrGetTechServicesTaskAsync(trackerId, loggedByEmpId);

                return tracker;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error saving Tracker.");
                throw;
            }
        }

        public async Task<int> CreatePartChangeTaskAsync(CMHub_CustCommsPartChangeTaskModel task, int trackerId, int type, bool manual = false, string loggedByEmpId = "")
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            var statusId = await GetInitialTaskStatusIdAsync();

            const string logInsertQuery = $@"
                INSERT INTO {LogTable} (TrackerId, TaskId, LogMessage, LogDate, LoggedByUser, Deleted)
                VALUES (@TrackerId, @TaskId, @LogMessage, @LogDate, @LoggedByUser, 0);";

            try
            {
                // 1. Check for existing soft-deleted task
                const string undeleteCheckQuery = $@"
                    SELECT Id FROM {TaskTable}
                    WHERE TrackerId = @TrackerId AND PartNum = @ImpactedPartNum AND Deleted = 1 AND Type = @Type;";

                var existingTaskId = await conn.ExecuteScalarAsync<int?>(
                    undeleteCheckQuery,
                    new { TrackerId = trackerId, task.ImpactedPartNum, Type = type },
                    transaction
                );

                if (existingTaskId.HasValue)
                {
                    // 2. Undelete and update the task
                    const string restoreQuery = $@"
                        UPDATE {TaskTable}
                        SET Deleted = 0,
                            StatusId = @StatusId,
                            Completed = 0
                        WHERE Id = @TaskId;";

                    await conn.ExecuteAsync(
                        restoreQuery,
                        new
                        {
                            TaskId = existingTaskId.Value,
                            StatusId = statusId
                        },
                        transaction
                    );

                    // 3. Restore associated logs
                    const string restoreLogsQuery = $@"
                        UPDATE {LogTable}
                        SET Deleted = 0
                        WHERE TaskId = @TaskId;";

                    await conn.ExecuteAsync(
                        restoreLogsQuery,
                        new { TaskId = existingTaskId.Value },
                        transaction
                    );

                    // 4. Log the undelete action
                    if (manual)
                    {
                        await conn.ExecuteAsync(
                            logInsertQuery,
                            new
                            {
                                TrackerId = trackerId,
                                TaskId = existingTaskId.Value,
                                LogMessage = $"Task for '{task.ImpactedPartNum}' restored from deleted",
                                LogDate = DateTime.UtcNow,
                                LoggedByUser = loggedByEmpId
                            },
                            transaction
                        );
                    }

                    task.Id = existingTaskId.Value;
                    task.TrackerId = trackerId;

                    transaction.Commit();
                    return task.Id;
                }

                // 5. Insert new task (original path)
                const string insertQuery = $@"
                    INSERT INTO {TaskTable} (TrackerId, PartNum, StatusId, Completed, Deleted, Type)
                    OUTPUT INSERTED.Id
                    VALUES (@TrackerId, @ImpactedPartNum, @StatusId, @Completed, @Deleted, @Type);";

                var parameters = new
                {
                    TrackerId = trackerId,
                    task.ImpactedPartNum,
                    StatusId = statusId,
                    task.Completed,
                    Deleted = task.Deleted ? 1 : 0,
                    Type = type
                };

                var taskId = await conn.ExecuteScalarAsync<int>(insertQuery, parameters, transaction);
                await _debugModeService.SqlQueryDebugMessage(insertQuery, parameters);

                // 6. Log the task creation
                if (manual)
                {
                    await conn.ExecuteAsync(
                        logInsertQuery,
                        new
                        {
                            TrackerId = trackerId,
                            TaskId = taskId,
                            LogMessage = $"Task added manually for '{task.ImpactedPartNum}'",
                            LogDate = DateTime.UtcNow,
                            LoggedByUser = loggedByEmpId
                        },
                        transaction
                    );
                }

                task.Id = taskId;
                task.TrackerId = trackerId;

                transaction.Commit();

                return task.Id;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error saving Task.");
                throw;
            }
        }

        public async Task CreatePartChangeTaskLogAsync(CMHub_CustCommsPartChangeTaskLogModel log, string loggedByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            const string query = $@"
                INSERT INTO {LogTable} (TaskId, LogMessage, LogDate, LoggedByUser, Deleted, ManualLogEntry, TrackerId)
                OUTPUT INSERTED.Id
                VALUES (@TaskId, @LogMessage, @LogDate, @LoggedByUser, @Deleted, @ManualLogEntry, @TrackerId);";

            var parameters = new
            {
                log.TaskId,
                log.LogMessage,
                LogDate = DateTime.UtcNow,
                LoggedByUser = loggedByEmpId,
                Deleted = log.Deleted ? 1 : 0,
                ManualLogEntry = log.ManualLogEntry ? 1 : 0,
                log.TrackerId
            };

            try
            {
                var insertedId = await conn.ExecuteScalarAsync<int>(query, parameters, transaction);
                await _debugModeService.SqlQueryDebugMessage(query, parameters);

                log.Id = insertedId;

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Error creating log for Task ID {log.TaskId}");
                throw;
            }
        }

        public async Task UpdateTaskLogAsync(CMHub_CustCommsPartChangeTaskLogModel log, string loggedByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string query = $@"
                UPDATE {LogTable}
                SET LogMessage = @LogMessage,
                    LogDate = @LogDate,
                    LoggedByUser = @LoggedByUser,
                    Deleted = @Deleted,
                    ManualLogEntry = @ManualLogEntry,
                    TrackerId = @TrackerId
                WHERE Id = @Id;";

            var parameters = new
            {
                log.Id,
                log.LogMessage,
                LogDate = DateTime.UtcNow,
                LoggedByUser = loggedByEmpId,
                Deleted = log.Deleted ? 1 : 0,
                log.ManualLogEntry,
                log.TrackerId
            };

            try
            {
                await conn.ExecuteAsync(query, parameters);
                await _debugModeService.SqlQueryDebugMessage(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Log ID {log.Id}");
                throw;
            }
        }

        public async Task UpdateTaskAsync(CMHub_CustCommsPartChangeTaskModel task)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string query = $@"
                UPDATE {TaskTable}
                SET PartNum = @ImpactedPartNum,
                    StatusId = @StatusId,
                    Completed = @Completed,
                    Deleted = @Deleted,
                    ECN = @ECN
                WHERE Id = @Id;";

            var parameters = new
            {
                task.Id,
                task.ImpactedPartNum,
                task.StatusId,
                Completed = task.Completed ? 1 : 0,
                Deleted = task.Deleted ? 1 : 0,
                ECN = task.ECNNumber
            };

            try
            {
                await conn.ExecuteAsync(query, parameters);
                await _debugModeService.SqlQueryDebugMessage(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Task ID {task.Id}");
                throw;
            }
        }

        public async Task UpdatePartEoltDataAsync(PartEoltDTO partEolt)
        {
            // Update the PartEoltDTO if it has data and PartNum is set
            if (!string.IsNullOrWhiteSpace(partEolt.PartNum))
            {
                try
                {
                    var updateSuccess = await _partService.UpdatePartAsync(partEolt);
                    if (!updateSuccess)
                    {
                        _logger.LogWarning("Failed to update PartEoltDTO for PartNum: {PartNum}", partEolt.PartNum);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating PartEoltDTO for PartNum: {PartNum}", partEolt.PartNum);
                    // Don't throw here - we still want to return the tracker ID even if the part update fails
                }
            }
        }

        public async Task HardDeletePartChangeTrackerAsync(int trackerId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Delete logs by TrackerId directly
                var deleteLogsByTrackerQuery = $@"
                    DELETE FROM {LogTable}
                    WHERE TrackerId = @TrackerId;";
                await conn.ExecuteAsync(deleteLogsByTrackerQuery, new { TrackerId = trackerId }, transaction);

                // Delete logs by TaskId
                var deleteLogsByTaskQuery = $@"
                    DELETE FROM {LogTable}
                    WHERE TaskId IN (SELECT Id FROM {TaskTable} WHERE TrackerId = @TrackerId);";
                await conn.ExecuteAsync(deleteLogsByTaskQuery, new { TrackerId = trackerId }, transaction);

                // Delete tasks
                var deleteTasksQuery = $@"
                    DELETE FROM {TaskTable}
                    WHERE TrackerId = @TrackerId;";

                await conn.ExecuteAsync(deleteTasksQuery, new { TrackerId = trackerId }, transaction);

                // Delete tracker
                var deleteTrackerQuery = $@"
                    DELETE FROM {TrackerTable}
                    WHERE Id = @TrackerId;";

                await conn.ExecuteAsync(deleteTrackerQuery, new { TrackerId = trackerId }, transaction);

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Error performing hard delete for Tracker ID {trackerId}");
                throw;
            }
        }

        public async Task SoftDeletePartChangeTrackerAsync(int trackerId, string loggedByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Get all Task IDs for this Tracker
                var getTaskIdsQuery = $@"
                    SELECT Id FROM {TaskTable}
                    WHERE TrackerId = @TrackerId;";

                var taskIds = (await conn.QueryAsync<int>(
                    getTaskIdsQuery,
                    new { TrackerId = trackerId },
                    transaction)).ToList();

                // 2. Insert one log for each task
                var insertLogQuery = $@"
                    INSERT INTO {LogTable} (TaskId, LogMessage, LogDate, LoggedByEmpId, Deleted)
                    VALUES (@TaskId, @LogMessage, @LogDate, @LoggedByEmpId, 0);";

                var logTime = DateTime.UtcNow;
                var logMessage = $"Marked Deleted for Tracker {trackerId}";

                foreach (var taskId in taskIds)
                {
                    await conn.ExecuteAsync(
                        insertLogQuery,
                        new
                        {
                            TaskId = taskId,
                            LogMessage = logMessage,
                            LogDate = logTime,
                            LoggedByEmpId = loggedByEmpId
                        },
                        transaction
                    );
                }

                // 3. Soft delete logs
                var updateLogsQuery = $@"
                    UPDATE {LogTable}
                    SET Deleted = 1
                    WHERE TaskId IN @TaskIds;";

                await conn.ExecuteAsync(updateLogsQuery, new { TaskIds = taskIds }, transaction);

                // 4. Soft delete tasks
                var updateTasksQuery = $@"
                    UPDATE {TaskTable}
                    SET Deleted = 1
                    WHERE Id IN @TaskIds;";

                await conn.ExecuteAsync(updateTasksQuery, new { TaskIds = taskIds }, transaction);

                // 5. Soft delete tracker
                var updateTrackerQuery = $@"
                    UPDATE {TrackerTable}
                    SET Deleted = 1
                    WHERE Id = @TrackerId;";

                await conn.ExecuteAsync(updateTrackerQuery, new { TrackerId = trackerId }, transaction);

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Error performing soft delete for Tracker ID {trackerId}");
                throw;
            }
        }

        public async Task SoftDeleteTaskByIdAsync(int taskId, string loggedByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Insert a log entry indicating soft delete
                var insertLogQuery = $@"
                    INSERT INTO {LogTable} (TaskId, TrackerId, LogMessage, LogDate, LoggedByUser, Deleted)
                    SELECT Id, TrackerId, @LogMessage, @LogDate, @LoggedByUser, 0
                    FROM {TaskTable}
                    WHERE Id = @TaskId;";

                await conn.ExecuteAsync(insertLogQuery, new
                {
                    TaskId = taskId,
                    LogMessage = $"Task {taskId} marked as deleted",
                    LogDate = DateTime.UtcNow,
                    LoggedByUser = loggedByEmpId
                }, transaction);

                // 2. Soft delete all logs for the task
                var softDeleteLogsQuery = $@"
                    UPDATE {LogTable}
                    SET Deleted = 1
                    WHERE TaskId = @TaskId;";

                await conn.ExecuteAsync(softDeleteLogsQuery, new { TaskId = taskId }, transaction);

                // 3. Soft delete the task itself
                var softDeleteTaskQuery = $@"
                    UPDATE {TaskTable}
                    SET Deleted = 1
                    WHERE Id = @TaskId;";

                await conn.ExecuteAsync(softDeleteTaskQuery, new { TaskId = taskId }, transaction);

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Error soft deleting Task ID {taskId}");
                throw;
            }
        }

        public async Task<int> CreateOrGetTechServicesTaskAsync(int trackerId, string loggedByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Check if Tech Services task already exists for this tracker (inside transaction for safety)
                // Use MAX(Id) to get the most recent one if duplicates somehow exist
                const string checkQuery = $@"
                    SELECT MAX(Id) FROM {TaskTable}
                    WHERE TrackerId = @TrackerId AND Type = 3 AND Deleted = 0;";

                var existingTaskId = await conn.ExecuteScalarAsync<int?>(
                    checkQuery, 
                    new { TrackerId = trackerId },
                    transaction
                );

                if (existingTaskId.HasValue)
                {
                    transaction.Commit();
                    return existingTaskId.Value;
                }

                // Get the tracker's PartNum to use for the Tech Services task
                // (PartNum column doesn't allow NULL, so we use the tracker's part number)
                const string getTrackerPartNumQuery = $@"
                    SELECT PartNum FROM {TrackerTable}
                    WHERE Id = @TrackerId;";

                var trackerPartNum = await conn.ExecuteScalarAsync<string>(
                    getTrackerPartNumQuery, 
                    new { TrackerId = trackerId },
                    transaction
                );

                if (string.IsNullOrEmpty(trackerPartNum))
                {
                    throw new InvalidOperationException($"Cannot create Tech Services task: Tracker ID {trackerId} not found or has no PartNum.");
                }

                // Double-check no task was created by another request while we were getting the PartNum
                // This handles race conditions
                var recheck = await conn.ExecuteScalarAsync<int?>(
                    checkQuery,
                    new { TrackerId = trackerId },
                    transaction
                );

                if (recheck.HasValue)
                {
                    transaction.Commit();
                    return recheck.Value;
                }

                // Create new Tech Services task with Tech Services-specific initial status
                var statusId = await GetInitialTechServicesStatusIdAsync();

                const string insertQuery = $@"
                    INSERT INTO {TaskTable} (TrackerId, PartNum, StatusId, Completed, Deleted, Type)
                    OUTPUT INSERTED.Id
                    VALUES (@TrackerId, @PartNum, @StatusId, 0, 0, 3);";

                var taskId = await conn.ExecuteScalarAsync<int>(
                    insertQuery,
                    new { TrackerId = trackerId, PartNum = trackerPartNum, StatusId = statusId },
                    transaction
                );

                // Log the creation
                const string logInsertQuery = $@"
                    INSERT INTO {LogTable} (TrackerId, TaskId, LogMessage, LogDate, LoggedByUser, Deleted)
                    VALUES (@TrackerId, @TaskId, @LogMessage, @LogDate, @LoggedByUser, 0);";

                await conn.ExecuteAsync(
                    logInsertQuery,
                    new
                    {
                        TrackerId = trackerId,
                        TaskId = taskId,
                        LogMessage = "Tech Services task created",
                        LogDate = DateTime.UtcNow,
                        LoggedByUser = loggedByEmpId
                    },
                    transaction
                );

                transaction.Commit();
                _logger.LogInformation("Created Tech Services task {TaskId} for Tracker {TrackerId}", taskId, trackerId);
                return taskId;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error creating Tech Services task for Tracker ID {TrackerId}", trackerId);
                throw;
            }
        }

        public async Task<bool> UpdateTechServicesLTBQuantityAsync(int trackerId, int? quantity, string employeeId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Get the current quantity for logging
                const string getCurrentQuery = $@"
                    SELECT TechServicesLTBQuantity FROM {TrackerTable}
                    WHERE Id = @TrackerId;";

                var currentQuantity = await conn.ExecuteScalarAsync<int?>(
                    getCurrentQuery,
                    new { TrackerId = trackerId },
                    transaction
                );

                // Skip update and logging if the value hasn't actually changed
                if (currentQuantity == quantity)
                {
                    transaction.Commit();
                    return true;
                }

                // Update the quantity
                const string updateQuery = $@"
                    UPDATE {TrackerTable}
                    SET TechServicesLTBQuantity = @Quantity
                    WHERE Id = @TrackerId;";

                await conn.ExecuteAsync(
                    updateQuery,
                    new { TrackerId = trackerId, Quantity = quantity },
                    transaction
                );

                // Get Tech Services task for logging - use MAX(Id) to get the most recent task
                // This matches the behavior of GetPartChangeTrackerByPartNumAsync which assigns
                // the last found Type 3 task to TechServicesTask
                const string getTaskQuery = $@"
                    SELECT MAX(Id) FROM {TaskTable}
                    WHERE TrackerId = @TrackerId AND Type = 3 AND Deleted = 0;";

                var techServicesTaskId = await conn.ExecuteScalarAsync<int?>(
                    getTaskQuery,
                    new { TrackerId = trackerId },
                    transaction
                );

                // Log the change
                string logMessage = currentQuantity.HasValue
                    ? $"Tech Services LTB Quantity changed from {currentQuantity} to {quantity?.ToString() ?? "null"}"
                    : $"Tech Services LTB Quantity set to {quantity?.ToString() ?? "null"}";

                const string logInsertQuery = $@"
                    INSERT INTO {LogTable} (TrackerId, TaskId, LogMessage, LogDate, LoggedByUser, Deleted)
                    VALUES (@TrackerId, @TaskId, @LogMessage, @LogDate, @LoggedByUser, 0);";

                await conn.ExecuteAsync(
                    logInsertQuery,
                    new
                    {
                        TrackerId = trackerId,
                        TaskId = techServicesTaskId,
                        LogMessage = logMessage,
                        LogDate = DateTime.UtcNow,
                        LoggedByUser = employeeId
                    },
                    transaction
                );

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error updating Tech Services LTB Quantity for Tracker ID {TrackerId}", trackerId);
                return false;
            }
        }

        public async Task UpdateTrackerWithChangeLoggingAsync(
            CMHub_CustCommsPartChangeTrackerModel tracker, 
            CMHub_CustCommsPartChangeTrackerModel? originalTracker, 
            string employeeId)
        {
            if (tracker == null || string.IsNullOrWhiteSpace(tracker.PartNum))
            {
                throw new ArgumentException("Tracker cannot be null and must have a PartNum");
            }

            var trackerChanges = new List<string>();

            // Compare and log Last Time Buy Date changes
            var originalLTB = originalTracker?.PartEolt.LastTimeBuyDate;
            var newLTB = tracker.PartEolt.LastTimeBuyDate;
            if (originalLTB != newLTB)
            {
                var oldValue = originalLTB?.ToShortDateString() ?? "null";
                var newValue = newLTB?.ToShortDateString() ?? "null";
                trackerChanges.Add($"Last Time Buy Date: {oldValue} ? {newValue}");
            }

            // Compare and log Tech Services LTB Quantity changes
            var originalTSLTBQty = originalTracker?.TechServicesLTBQuantity;
            var newTSLTBQty = tracker.TechServicesLTBQuantity;
            
            if (originalTSLTBQty != newTSLTBQty)
            {
                await UpdateTechServicesLTBQuantityAsync(tracker.Id, newTSLTBQty, employeeId);
            }

            // Update the PartEolt data in Epicor if tracker-level changes exist
            if (trackerChanges.Count > 0)
            {
                // Fetch existing part data to preserve base fields
                var existingPartData = await _partService.GetPartsByPartNumbersAsync<PartEoltDTO>(new[] { tracker.PartNum });
                var existingPart = existingPartData?.FirstOrDefault();

                if (existingPart != null)
                {
                    // Preserve the base fields from the existing part
                    tracker.PartEolt.PartDescription = existingPart.PartDescription;
                    tracker.PartEolt.RevisionNum = existingPart.RevisionNum;
                    tracker.PartEolt.InActive = existingPart.InActive;
                    tracker.PartEolt.Deprecated_c = existingPart.Deprecated_c;
                    tracker.PartEolt.CMManaged_c = existingPart.CMManaged_c;
                    tracker.PartEolt.CMOrignationDate_c = existingPart.CMOrignationDate_c;
                }

                // Update with the new values
                await UpdatePartEoltDataAsync(tracker.PartEolt);

                // Log all tracker-level changes
                var logMessage = string.Join("; ", trackerChanges);
                await CreatePartChangeTaskLogAsync(
                    new CMHub_CustCommsPartChangeTaskLogModel
                    {
                        TrackerId = tracker.Id,
                        TaskId = null, // Tracker-level log
                        LogMessage = logMessage
                    },
                    employeeId
                );
            }

            // Process CM Part Task changes
            await ProcessTaskChangesAsync(tracker.CMPartTasks, originalTracker?.CMPartTasks, employeeId);

            // Process Where Used Part Task changes
            await ProcessTaskChangesAsync(tracker.WhereUsedPartTasks, originalTracker?.WhereUsedPartTasks, employeeId);

            // Process Tech Services Task changes
            if (tracker.TechServicesTask != null)
            {
                var originalTSTask = originalTracker?.TechServicesTask;
                await ProcessSingleTaskChangesAsync(tracker.TechServicesTask, originalTSTask, employeeId);
            }
        }

        private async Task ProcessTaskChangesAsync(
            List<CMHub_CustCommsPartChangeTaskModel>? currentTasks,
            List<CMHub_CustCommsPartChangeTaskModel>? originalTasks,
            string employeeId)
        {
            if (currentTasks == null) return;

            foreach (var task in currentTasks)
            {
                var originalTask = originalTasks?.FirstOrDefault(t => t.Id == task.Id);
                await ProcessSingleTaskChangesAsync(task, originalTask, employeeId);
            }
        }

        private async Task ProcessSingleTaskChangesAsync(
            CMHub_CustCommsPartChangeTaskModel currentTask,
            CMHub_CustCommsPartChangeTaskModel? originalTask,
            string employeeId)
        {
            var taskChanges = new List<string>();
            var statuses = await GetTaskStatusesAsync();

            // Check for Status changes
            if (currentTask.StatusId != originalTask?.StatusId)
            {
                var oldStatusDesc = statuses.FirstOrDefault(s => s.Id == originalTask?.StatusId)?.Description ?? "None";
                var newStatusDesc = statuses.FirstOrDefault(s => s.Id == currentTask.StatusId)?.Description ?? "Unknown";
                taskChanges.Add($"Status Changed: {oldStatusDesc} ? {newStatusDesc}");

                // Update the Completed flag based on final status
                var finalStatusId = await GetFinalTaskStatusIdAsync();
                currentTask.Completed = currentTask.StatusId == finalStatusId;
            }

            // Check for ECN changes
            var originalECN = originalTask?.ECNNumber ?? "";
            var currentECN = currentTask.ECNNumber ?? "";
            if (currentECN != originalECN)
            {
                if (string.IsNullOrEmpty(originalECN))
                {
                    taskChanges.Add($"ECN entered as '{currentECN}'");
                }
                else if (string.IsNullOrEmpty(currentECN))
                {
                    taskChanges.Add($"ECN '{originalECN}' removed");
                }
                else
                {
                    taskChanges.Add($"ECN changed from '{originalECN}' to '{currentECN}'");
                }
            }

            // If there are any changes, update the task and create a log entry
            if (taskChanges.Count > 0)
            {
                // Update the task in the database
                await UpdateTaskAsync(currentTask);

                // Create a single log entry with all changes
                var logMessage = string.Join("; ", taskChanges);
                await CreatePartChangeTaskLogAsync(
                    new CMHub_CustCommsPartChangeTaskLogModel
                    {
                        TrackerId = currentTask.TrackerId,
                        TaskId = currentTask.Id,
                        LogMessage = logMessage
                    },
                    employeeId
                );
            }
        }
    }
}
