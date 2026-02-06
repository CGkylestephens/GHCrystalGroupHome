using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Helpers;
using Blazorise;
using CrystalGroupHome.Internal.Authorization;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Data;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Models;
using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.SharedRCL.Data;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Components
{
    public class CMHub_CustCommsTrackerFormBase : ComponentBase
    {
        [Inject] public ICMHub_CustCommsService CustCommsService { get; set; } = default!;
        [Inject] public IPartService PartService { get; set; } = default!;
        [Inject] public ICustomerService CustomerService { get; set; } = default!;
        [Inject] public ICMHub_CMDexService CMDexService { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;
        [Inject] public IModalService ModalService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public ILogger<CMHub_CustCommsTrackerFormBase> _logger { get; set; } = default!;
        [Inject] public IAuthorizationService AuthorizationService { get; set; } = default!;
        [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] public EmailHelpers EmailHelpers { get; set; } = default!;
        [Inject] public IHostEnvironment HostEnvironment { get; set; } = default!;

        [Parameter] public string? PartNum { get; set; }
        [Parameter] public bool IsEditMode { get; set; }
        [Parameter] public List<CMHub_CustCommsPartChangeTaskStatusDTO> Statuses { get; set; } = [];

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }
        
        /// <summary>
        /// Cascading parameter from MainLayout that changes when impersonation changes.
        /// When this value changes, the component re-renders, updating permission-based UI.
        /// </summary>
        [CascadingParameter(Name = "ImpersonationVersion")] public int ImpersonationVersion { get; set; }

        protected CustomSearchInput? PartNumSearchInput;

        protected bool IsLoading = true;
        protected bool HasUnsavedChanges = false;
        protected bool IsSaving = false;
        protected bool HasEditPermission { get; set; } = false;
        protected bool HasTaskStatusEditPermission { get; set; } = false;
        protected bool HasTechServicesEditPermission { get; set; } = false;
        protected CMHub_CustCommsPartChangeTrackerModel? TrackerModel;
        protected CMHub_CustCommsPartChangeTrackerModel? OriginalTrackerModel; // For change tracking

        protected CMHub_CustCommsTaskLogsModal? TaskLogsModal { get; set; }
        protected CMHub_CustCommsNewLogModal? NewLogModal { get; set; }

        protected CMHub_CustCommsECNModal? ECNModal { get; set; }

        protected ConfirmationModal? FoundExistingTrackerConfirmationModal { get; set; }
        protected CMHub_CustCommsPartChangeTaskModel? TaskToDelete { get; set; } = null;
        protected ConfirmationModal? DeleteTaskConfirmationModal { get; set; }
        protected string DeleteTaskConfirmationMessage { get; set; } = string.Empty;
        protected ConfirmationModal? DeletePartTrackerConfirmationModal { get; set; }
        protected string DeletePartTrackerConfirmationMessage { get; set; } = string.Empty;
        protected ConfirmationModal? ErrorModal { get; set; }
        protected string? ErrorMessage { get; set; } = null;

        protected Modal? ManualTaskModal;
        protected string ManualTaskPartNum { get; set; } = string.Empty;
        protected string ManualTaskError { get; set; } = string.Empty;

        protected string? LastTimeBuyDateValidationMessage { get; set; } = null;
        protected bool ShowLastTimeBuySaveFailedWarning { get; set; } = false;
        protected bool ShowValidationSummary { get; set; } = false;

        private bool _isFirstRender = true; // Flag to run logic only once after initial render
        private int _finalStatusId = -1;

        public bool showCMTasks = true;
        public bool showWhereUsedTasks = true;

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            HasUnsavedChanges = false;

            await CheckAuthorizationAsync();

            _finalStatusId = await CustCommsService.GetFinalTaskStatusIdAsync();
            
            // Check if we're in Edit mode and the ltbFailed query parameter is present
            if (IsEditMode)
            {
                var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
                if (Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query).TryGetValue("ltbFailed", out var ltbFailedValue))
                {
                    if (bool.TryParse(ltbFailedValue, out var ltbFailed) && ltbFailed)
                    {
                        ShowLastTimeBuySaveFailedWarning = true;
                        
                        // Remove the query parameter from the URL
                        var cleanUrl = uri.GetLeftPart(UriPartial.Path);
                        NavigationManager.NavigateTo(cleanUrl, replace: true);
                    }
                }
            }
        }

        private async Task CheckAuthorizationAsync()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            
            var editResult = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubCustCommsEdit);
            HasEditPermission = editResult.Succeeded;

            var taskStatusResult = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubCustCommsTaskStatusEdit);
            HasTaskStatusEditPermission = taskStatusResult.Succeeded;

            var techServicesResult = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubTechServicesEdit);
            HasTechServicesEditPermission = techServicesResult.Succeeded;
        }

        protected override async Task OnParametersSetAsync()
        {
            try
            {
                if (IsEditMode)
                {
                    await LoadData();
                }
                else
                {
                    TrackerModel = new();
                }
            }
            finally
            {
                if (string.IsNullOrEmpty(PartNumSearchInput?.TextValue) && !IsEditMode)
                {
                    IsLoading = false;
                }
            }

            await base.OnParametersSetAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && !IsEditMode && PartNumSearchInput != null)
            {
                _isFirstRender = false; // Update our tracking flag

                // PartNum will already be supplied if it came from the url route
                if (!string.IsNullOrEmpty(PartNum))
                {
                    IsLoading = true;
                    // Set the text from the route PartNum and immediately search for it
                    PartNumSearchInput.SetText(PartNum);
                    // Use InvokeAsync to ensure LoadData runs in the correct context after render
                    await InvokeAsync(async () => {
                        await OnSearchPartNum(PartNum); // Trigger search automatically
                    });
                }
            }
        }

        private CMHub_CustCommsPartChangeTrackerModel? CreateDeepCopy(CMHub_CustCommsPartChangeTrackerModel? original)
        {
            if (original == null) return null;

            // Using JSON serialization for deep copy
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
            var json = JsonSerializer.Serialize(original, options);
            return JsonSerializer.Deserialize<CMHub_CustCommsPartChangeTrackerModel>(json, options);
        }

        protected async void SetError(string message)
        {
            ErrorMessage = message;
            IsLoading = false;

            if (ErrorModal != null)
            {
                await ErrorModal.ShowAsync(errorMode: true);
            }

            StateHasChanged();
        }

        protected void OnPartNumTextValueChanged(string newText)
        {
            HasUnsavedChanges = false;
            TrackerModel = new();
            PartNumSearchInput?.SetCurrentInputHasBeenSearched(false);
        }

        public async Task OnSearchPartNum(string partNum)
        {
            if (string.IsNullOrEmpty(partNum) || (PartNumSearchInput?.CurrentInputHasBeenSearched ?? false))
            {
                return;
            }

            IsLoading = true;

            PartNum = partNum.Trim();

            if (!IsEditMode)
            {
                NavigationManager.NavigateTo($"{NavHelpers.CMHub_CustCommsAddTracker}/{PartNum}", replace: true);
            }
            await LoadData();

            PartNumSearchInput?.SetTextSilently(PartNum, PartNumSearchInput.CurrentInputIsValid);

            await InvokeAsync(StateHasChanged);
        }

        protected Task ShowAllLogsAsync()
        {
            if (TrackerModel == null)
                return Task.CompletedTask;

            var logs = new List<CMHub_CustCommsPartChangeTaskLogModel>();

            // Combine logs from CM and WhereUsed tasks, adding part context
            logs.AddRange(TrackerModel.CMPartTasks.SelectMany(task => task.Logs.Select(log => { log.ImpactedPartNum = task.ImpactedPartNum; log.ImpactedPartRev = task.ImpactedPartRev; return log; })));
            logs.AddRange(TrackerModel.WhereUsedPartTasks.SelectMany(task => task.Logs.Select(log => { log.ImpactedPartNum = task.ImpactedPartNum; log.ImpactedPartRev = task.ImpactedPartRev; return log; })));
            
            // Add Tech Services task logs
            if (TrackerModel.TechServicesTask != null)
            {
                logs.AddRange(TrackerModel.TechServicesTask.Logs.Select(log => { log.ImpactedPartNum = "Tech Services"; log.ImpactedPartRev = null; return log; }));
            }

            // Add tracker logs (null part context)
            logs.AddRange((TrackerModel.TrackerLogs ?? []).Select(log => { log.ImpactedPartNum = null; log.ImpactedPartRev = null; return log; }));

            TaskLogsModal?.Show(
                title: $"All Logs for {TrackerModel.PartNum}",
                taskLogs: logs.OrderByDescending(x => x.LogDate).ToList()
            );

            return Task.CompletedTask;
        }

        protected Task ShowTaskLogsAsync(int taskId)
        {
            var task = TrackerModel?.CMPartTasks.FirstOrDefault(t => t.Id == taskId)
                    ?? TrackerModel?.WhereUsedPartTasks.FirstOrDefault(t => t.Id == taskId);

            if (task != null)
            {
                // Clone logs before modifying properties to avoid side effects
                var taskLogs = task.Logs
                    .Select(log => {
                        var clonedLog = log;
                        clonedLog.ImpactedPartNum = task.ImpactedPartNum;
                        clonedLog.ImpactedPartRev = task.ImpactedPartRev;
                        return clonedLog;
                    })
                    .OrderByDescending(l => l.LogDate)
                    .ToList();

                TaskLogsModal?.Show(
                    title: $"Task Logs for {task.ImpactedPartNum} (Rev {task.ImpactedPartRev})",
                    taskLogs: taskLogs
                );
            }

            return Task.CompletedTask;
        }

        protected void ShowAddLogModalForTask(CMHub_CustCommsPartChangeTaskModel task)
        {
            if (NewLogModal != null && TrackerModel != null)
            {
                NewLogModal.Show(
                    trackerId: TrackerModel.Id,
                    taskId: task.Id,
                    context: $"{task.ImpactedPartNum} (Rev {task.ImpactedPartRev})"
                );
            }
        }

        protected void ShowAddLogModalForTracker()
        {
            if (NewLogModal != null && TrackerModel != null)
            {
                NewLogModal.Show(TrackerModel.Id, null, "Tracker-Level Log");
            }
        }

        protected async Task HandleNewLogSubmitted(CMHub_CustCommsPartChangeTaskLogModel newLog)
        {
            if (TrackerModel == null || CurrentUser == null) return;

            // Ensure LoggedByUser is set before saving
            newLog.LoggedByUser = CurrentUser.DBUser.EmployeeNumber;

            await CustCommsService.CreatePartChangeTaskLogAsync(newLog, CurrentUser.DBUser.EmployeeNumber);

            // Refresh logs and update UI
            if (newLog.TaskId == null) // Tracker log
            {
                TrackerModel.TrackerLogs = await CustCommsService.GetPartChangeTrackerLogsAsync(TrackerModel.Id);
            }
            else // Task log
            {
                // Check if it's the Tech Services task
                if (TrackerModel.TechServicesTask != null && newLog.TaskId == TrackerModel.TechServicesTask.Id)
                {
                    TrackerModel.TechServicesTask.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(TrackerModel.TechServicesTask.Id)
                        ?? new List<CMHub_CustCommsPartChangeTaskLogModel>();
                    TrackerModel.TechServicesTask.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    // Check CM and WhereUsed tasks
                    var task = TrackerModel.CMPartTasks.FirstOrDefault(t => t.Id == newLog.TaskId)
                            ?? TrackerModel.WhereUsedPartTasks.FirstOrDefault(t => t.Id == newLog.TaskId);

                    if (task != null)
                    {
                        await RefreshTaskLogs(task);
                        task.LastUpdated = DateTime.UtcNow;
                    }
                }
            }

            StateHasChanged();
        }

        private async Task LoadData()
        {
            IsLoading = true; // Set loading true at the start
            StateHasChanged(); // Update UI to show loading indicator

            try
            {
                // --- EDIT MODE ---
                if (IsEditMode)
                {
                    var canEditExisting = await ValidateAbilityToEditExistingTracker();

                    if (!canEditExisting) return;

                    await GetContextualDataForExistingLoadedTasks();

                    // Create a deep copy for change tracking after loading
                    OriginalTrackerModel = CreateDeepCopy(TrackerModel);
                    HasUnsavedChanges = false;
                }
                // --- ADD MODE ---
                else
                {
                    HasUnsavedChanges = false; // Reset flag for add mode

                    var canAddNew = await ValidateAbilityToAddNewTracker();

                    if (!canAddNew) return;

                    await InitializeNewTracker();

                    await CreateTempTasksForNewTracker();
                }
            }
            catch (Exception ex)
            {
                SetError($"An error occurred while loading data. Please try again. Error: {ex}");
                TrackerModel = new(); // Ensure model is reset on error
            }
            finally
            {
                if (TrackerModel != null)
                {
                    await CustCommsService.GetCMDexPartChangeTaskData(TrackerModel);
                }
                IsLoading = false;
                StateHasChanged(); // Final UI update after loading/error
            }
        }

        protected async Task<bool> ValidateAbilityToAddNewTracker()
        {
            if (string.IsNullOrEmpty(PartNum))
            {
                // No part number provided yet for Add mode, initialize empty model
                TrackerModel = new();
                if (PartNumSearchInput != null)
                {
                    PartNumSearchInput.SetCurrentInputHasBeenSearched(false);
                    PartNumSearchInput.SetCurrentInputIsValid(false);
                }

                IsLoading = false; // Stop loading, ready for input
                StateHasChanged();
                return true; // Exit LoadData, wait for user search
            }

            // Check for existing tracker *before* loading part info
            var existingTracker = await CustCommsService.GetPartChangeTrackerByPartNumAsync(PartNum, false);
            if (existingTracker != null)
            {
                if (FoundExistingTrackerConfirmationModal != null)
                {
                    await FoundExistingTrackerConfirmationModal.ShowAsync();
                    StateHasChanged();
                    return false;
                }
            }

            return true;
        }

        protected async Task<bool> ValidateAbilityToEditExistingTracker()
        {
            if (string.IsNullOrEmpty(PartNum))
            {
                SetError("Part number is missing for editing.");
                return false;
            }

            TrackerModel = await CustCommsService.GetPartChangeTrackerByPartNumAsync(PartNum, false);
            if (TrackerModel == null)
            {
                SetError($"Tracker for Part Number '{PartNum}' not found.");
                return false;
            }

            // Ensure Tech Services Task exists for existing trackers (backward compatibility)
            if (TrackerModel.TechServicesTask == null && CurrentUser != null)
            {
                try
                {
                    var techServicesTaskId = await CustCommsService.CreateOrGetTechServicesTaskAsync(
                        TrackerModel.Id, 
                        CurrentUser.DBUser.EmployeeNumber
                    );
                    
                    // Fetch the newly created task with its logs
                    var task = await CustCommsService.GetPartChangeTaskByTaskIdAsync(techServicesTaskId);
                    if (task != null)
                    {
                        task.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(techServicesTaskId) 
                            ?? new List<CMHub_CustCommsPartChangeTaskLogModel>();
                        TrackerModel.TechServicesTask = task;
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - the tracker can still be viewed without Tech Services task
                    Console.Error.WriteLine($"Failed to create Tech Services task for existing tracker: {ex}");
                }
            }

            // Populate StatusCode for Tech Services task so IsTaskComplete works correctly for progress calculations
            if (TrackerModel.TechServicesTask != null && TrackerModel.TechServicesTask.StatusId.HasValue && Statuses.Count > 0)
            {
                var status = Statuses.FirstOrDefault(s => s.Id == TrackerModel.TechServicesTask.StatusId);
                TrackerModel.TechServicesTask.StatusCode = status?.Code ?? "";
            }

            return true;
        }

        protected async Task InitializeNewTracker()
        {
            if (PartNum == null) return;

            // Load Part details for the new Tracker
            var foundParts = await PartService.GetPartsByPartNumbersAsync<PartDTO_Base>([(string)PartNum]);
            var foundPart = foundParts.FirstOrDefault();
            if (foundPart == null)
            {
                SetError($"Part Number '{PartNum}' not found. Cannot create tracker.");
                if (PartNumSearchInput != null)
                {
                    PartNumSearchInput.SetCurrentInputIsValid(false);
                    PartNumSearchInput.SetCurrentInputHasBeenSearched(true); // Mark as searched even if invalid
                }

                TrackerModel = new(); // Reset model
                return;
            }

            // Part found, create the base TrackerModel in memory
            TrackerModel = new()
            {
                PartNum = foundPart.PartNum,
                PartDesc = foundPart.PartDescription,
                PartRev = foundPart.RevisionNum
            };
            
            // Initialize PartEolt with the PartNum so Last Time Buy date can be saved properly
            TrackerModel.PartEolt.PartNum = foundPart.PartNum;
            
            HasUnsavedChanges = true; // Now we have unsaved data
            if (PartNumSearchInput != null)
            {
                PartNumSearchInput.SetCurrentInputHasBeenSearched(true);
                PartNumSearchInput.SetCurrentInputIsValid(true);
            }
        }

        // The Tasks have technically already been loaded with GetPartChangeTrackerByPartNumAsync above, but we need to
        // still match them up with indented parts here since some info for the task is not saved in the Task table
        protected async Task GetContextualDataForExistingLoadedTasks()
        {
            if (TrackerModel == null || PartNum == null) return;

            var indentedParts = await PartService.GetPartsIndentedWhereUsedByPartNumAsync<PartIndentedWhereUsedDTO>(PartNum, includeInactive: false, filterCmManaged: false);
            var higherLevelParts = new List<PartIndentedWhereUsedDTO>();
            var missingPartNums = new HashSet<string>();

            if (indentedParts != null)
            {
                // Populate CMPartTasks
                foreach (var cmTask in TrackerModel.CMPartTasks)
                {
                    var partNum = cmTask.ImpactedPartNum?.Trim();
                    var data = indentedParts.FirstOrDefault(p => p.PartNum == partNum);

                    if (data != null)
                    {
                        cmTask.ImpactedPartDesc = data.PartDescription;
                        cmTask.QtyPer = data.QtyPer;
                        cmTask.ImpactedPartRev = data.RevisionNum;
                        cmTask.Sort = data.Sort;
                        cmTask.Lvl = data.Lvl;
                    }
                    else if (!string.IsNullOrWhiteSpace(partNum))
                    {
                        missingPartNums.Add(partNum);
                    }
                }

                // Populate WhereUsedPartTasks
                foreach (var wuTask in TrackerModel.WhereUsedPartTasks)
                {
                    var partNum = wuTask.ImpactedPartNum?.Trim();
                    var data = indentedParts.FirstOrDefault(p => p.PartNum == partNum);

                    if (data != null)
                    {
                        wuTask.ImpactedPartDesc = data.PartDescription;
                        wuTask.QtyPer = data.QtyPer;
                        wuTask.ImpactedPartRev = data.RevisionNum;
                        wuTask.Sort = data.Sort;
                        wuTask.Lvl = data.Lvl;
                    }
                    else if (!string.IsNullOrWhiteSpace(partNum))
                    {
                        missingPartNums.Add(partNum);
                    }

                    wuTask.LinkedHigherLevelParts.Clear();
                }
                
                higherLevelParts = indentedParts
                    .Where(_ => _.CMManaged_c == false && _.PartNum != PartNum && _.Lvl > 1)
                    .ToList();

                // Link higher level parts to their base WhereUsed task using Sort as prefix
                foreach (var hlp in higherLevelParts)
                {
                    var baseTask = TrackerModel.WhereUsedPartTasks
                        .FirstOrDefault(wut => hlp.Sort.StartsWith(wut.Sort) && wut.Lvl == (hlp.Lvl - 1));

                    if (baseTask != null)
                    {
                        baseTask.LinkedHigherLevelParts.Add(hlp);
                    }
                }
            }

            // Fetch fallback part data for those not found in the indented call
            if (missingPartNums.Count != 0)
            {
                var fallbackParts = await PartService.GetPartsByPartNumbersAsync<PartDTO_Base>(missingPartNums.ToList());

                foreach (var cmTask in TrackerModel.CMPartTasks)
                {
                    var part = fallbackParts.FirstOrDefault(p => p.PartNum == cmTask.ImpactedPartNum?.Trim());
                    if (part != null && string.IsNullOrWhiteSpace(cmTask.ImpactedPartDesc))
                    {
                        cmTask.ImpactedPartDesc = part.PartDescription;
                        cmTask.ImpactedPartRev = part.RevisionNum;
                    }
                }

                foreach (var wuTask in TrackerModel.WhereUsedPartTasks)
                {
                    var part = fallbackParts.FirstOrDefault(p => p.PartNum == wuTask.ImpactedPartNum?.Trim());
                    if (part != null && string.IsNullOrWhiteSpace(wuTask.ImpactedPartDesc))
                    {
                        wuTask.ImpactedPartDesc = part.PartDescription;
                        wuTask.ImpactedPartRev = part.RevisionNum;
                    }
                }
            }
        }

        protected bool IsVisibleTaskStatus(CMHub_CustCommsPartChangeTaskModel task)
        {
            if (Statuses.Count <= 0) return true;

            var foundTask = Statuses.FirstOrDefault(_ => _.Id == (task.StatusId ?? -1));
            if (foundTask != null)
            {
                return foundTask.Sequence >= 0;
            }

            return false;
        }

        // This effectively builds our new Tasks that will eventually be saved when the user decided to create a new Tracker
        protected async Task CreateTempTasksForNewTracker()
        {
            if (TrackerModel == null || PartNum == null) return;

            var indentedParts = await PartService.GetPartsIndentedWhereUsedByPartNumAsync<PartIndentedWhereUsedDTO>(PartNum, includeInactive: false, filterCmManaged: false, filterNotCmManaged: false);
            var higherLevelParts = new List<PartIndentedWhereUsedDTO>();

            if (indentedParts != null)
            {
                TrackerModel.CMPartTasks = indentedParts
                    .Where(_ => _.CMManaged_c == true)
                    .Select(ip => new CMHub_CustCommsPartChangeTaskModel
                    {
                        TrackerPartNum = TrackerModel.PartNum,
                        ImpactedPartNum = ip.PartNum,
                        ImpactedPartDesc = ip.PartDescription,
                        ImpactedPartRev = ip.RevisionNum,
                        Deleted = false,
                        Lvl = ip.Lvl,
                        QtyPer = ip.QtyPer,
                        Sort = ip.Sort,
                        Type = 1
                    })
                    .ToList();

                TrackerModel.WhereUsedPartTasks = indentedParts
                    .Where(_ => _.CMManaged_c == false && _.PartNum != PartNum)
                    .Select(ip => new CMHub_CustCommsPartChangeTaskModel
                    {
                        TrackerPartNum = TrackerModel.PartNum,
                        ImpactedPartNum = ip.PartNum,
                        ImpactedPartDesc = ip.PartDescription,
                        ImpactedPartRev = ip.RevisionNum,
                        Deleted = false,
                        Lvl = ip.Lvl,
                        QtyPer = ip.QtyPer,
                        Sort = ip.Sort,
                        Type = 2
                    })
                    .Where(_ => _.Lvl == 1) // We only want 1st level for Where Used parts so we can just address the minimum number of changes needed.
                    .ToList();

                higherLevelParts = indentedParts
                    .Where(_ => _.CMManaged_c == false && _.PartNum != PartNum && _.Lvl > 1)
                    .ToList();

                // Link higher level parts to their base WhereUsed task using Sort as prefix
                foreach (var hlp in higherLevelParts)
                {
                    var baseTask = TrackerModel.WhereUsedPartTasks
                        .FirstOrDefault(wut => hlp.Sort.StartsWith(wut.Sort));

                    if (baseTask != null)
                    {
                        baseTask.LinkedHigherLevelParts.Add(hlp);
                    }
                }
            }
            else
            {
                TrackerModel.CMPartTasks = [];
                TrackerModel.WhereUsedPartTasks = [];
            }
        }

        protected void ConfirmNavigateToExistingTracker(bool confirm)
        {
            if (confirm && !string.IsNullOrEmpty(PartNum)) // Ensure PartNum is valid
            {
                NavigationManager.NavigateTo($"{NavHelpers.CMHub_CustCommsTrackerDetails}/{PartNum}");
            }
            else if (!confirm && PartNumSearchInput != null)
            {
                // User chose not to navigate, clear the search input if they want to search again
                PartNumSearchInput.SetText("");
                PartNumSearchInput.SetCurrentInputHasBeenSearched(false);
                PartNumSearchInput.SetCurrentInputIsValid(false);
                TrackerModel = new(); // Reset the model
                PartNum = null; // Clear the PartNum variable
                StateHasChanged();
            }
        }

        protected async Task ConfirmNavigationAsync(LocationChangingContext context)
        {
            if (HasUnsavedChanges && TrackerModel != null && IsEditMode)
            {
                context.PreventNavigation();
                var saveSuccessful = await SaveChangesAsync();

                if (saveSuccessful)
                {
                    NavigationManager.NavigateTo(context.TargetLocation);
                }
            }
        }

        protected async Task HardDeleteTracker()
        {
            // Ensure it's edit mode and model exists
            if (!IsEditMode || TrackerModel == null)
            {
                SetError("Cannot delete tracker: Invalid state.");
                return;
            }

            if (DeletePartTrackerConfirmationModal != null)
            {
                DeletePartTrackerConfirmationMessage = $"Are you sure you want to Delete the Part Tracker and all Tasks for {TrackerModel.PartNum}? This cannot be undone.";
                await DeletePartTrackerConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmHardDeletePartTracker(bool confirm)
        {
            if (!IsEditMode || TrackerModel == null)
            {
                SetError("Cannot delete tracker: Invalid state.");
                return;
            }

            if (confirm)
            {
                IsLoading = true; // Show loading indicator during deletion
                StateHasChanged();

                try
                {
                    await CustCommsService.HardDeletePartChangeTrackerAsync(TrackerModel.Id);
                    // Navigate away after successful deletion
                    NavigationManager.NavigateTo(NavHelpers.CMHub_CustCommsMainPage); // Navigate to grid or main page
                }
                catch (Exception ex)
                {
                    SetError("Failed to delete tracker. Please try again.");
                    Console.Error.WriteLine($"HardDelete Error: {ex}");
                    IsLoading = false; // Hide loading indicator on error
                    StateHasChanged();
                }
            }
        }

        protected async Task SoftDeleteTask(CMHub_CustCommsPartChangeTaskModel task)
        {
            // Ensure it's edit mode and models exists
            if (!IsEditMode || TrackerModel == null || task == null)
            {
                SetError("Cannot delete tracker: Invalid state.");
                return;
            }

            if (DeleteTaskConfirmationModal != null)
            {
                TaskToDelete = task;
                DeleteTaskConfirmationMessage = $"Are you sure you want to Delete the Part Task for {task.ImpactedPartNum}? This is a Soft Delete. You can add this Task to this Tracker again later and all logs will be retained.";
                await DeleteTaskConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmSoftDeleteTask(bool confirm)
        {
            if (!IsEditMode || TrackerModel == null || TaskToDelete == null)
            {
                SetError("Cannot delete task: Invalid state.");
                return;
            }

            if (confirm)
            {
                try
                {
                    await CustCommsService.SoftDeleteTaskByIdAsync(TaskToDelete.Id, CurrentUser?.DBUser.EmployeeNumber ?? "Unknown User");

                    TrackerModel.WhereUsedPartTasks.Remove(TaskToDelete);
                }
                catch (Exception ex)
                {
                    SetError("Failed to delete task. Please try again.");
                    Console.Error.WriteLine($"SoftDelete Error: {ex}");
                    StateHasChanged();
                }
            }

            TaskToDelete = null;
        }

        private void SetUnsavedChanges()
        {
            if (!HasUnsavedChanges)
            {
                HasUnsavedChanges = true;
                StateHasChanged();
            }
        }

        protected void OnChangeLastTimeBuyDate(DateTime? date)
        {
            if (TrackerModel == null) return;

            var newDate = date;
            if (TrackerModel.LastTimeBuyDate == newDate) return;

            // Update the model
            TrackerModel.PartEolt.LastTimeBuyDate = newDate;
            
            // Clear validation message when date changes
            LastTimeBuyDateValidationMessage = null;
            ShowValidationSummary = false;
            
            SetUnsavedChanges();
        }

        private bool ValidateAllDates()
        {
            bool isValid = true;

            // Clear all validation messages first
            LastTimeBuyDateValidationMessage = null;
            ShowValidationSummary = false;

            if (TrackerModel == null) return false;

            // Validate Last Time Buy Date
            if (TrackerModel.PartEolt.LastTimeBuyDate.HasValue && 
                !DataHelpers.IsBusinessReasonableDateTime(TrackerModel.PartEolt.LastTimeBuyDate))
            {
                LastTimeBuyDateValidationMessage = "Invalid date";
                isValid = false;
            }

            // Show validation summary if any validation failed
            if (!isValid)
            {
                ShowValidationSummary = true;
            }

            return isValid;
        }

        protected async Task HandleValidSubmit()
        {
            if (TrackerModel == null || CurrentUser == null || IsSaving)
                return;

            // Validate all dates before saving
            if (!ValidateAllDates())
            {
                StateHasChanged();
                return;
            }

            IsSaving = true;
            StateHasChanged();

            try
            {
                // Use the service method with change logging
                await CustCommsService.UpdateTrackerWithChangeLoggingAsync(
                    TrackerModel,
                    OriginalTrackerModel,
                    CurrentUser.DBUser.EmployeeNumber
                );

                // Update the original tracker model with the current state for future comparisons
                OriginalTrackerModel = CreateDeepCopy(TrackerModel);

                // Refresh logs to show the new change logs
                TrackerModel.TrackerLogs = await CustCommsService.GetPartChangeTrackerLogsAsync(TrackerModel.Id);
                
                // Refresh task logs for all tasks
                foreach (var task in TrackerModel.CMPartTasks)
                {
                    task.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(task.Id) ?? [];
                }
                foreach (var task in TrackerModel.WhereUsedPartTasks)
                {
                    task.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(task.Id) ?? [];
                }
                if (TrackerModel.TechServicesTask != null)
                {
                    TrackerModel.TechServicesTask.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(TrackerModel.TechServicesTask.Id) ?? [];
                }

                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                SetError($"Failed to save changes: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StateHasChanged();
            }
        }

        protected async Task<bool> SaveChangesAsync()
        {
            if (TrackerModel == null || CurrentUser == null || IsSaving)
                return false;

            // Validate all dates before saving
            if (!ValidateAllDates())
            {
                StateHasChanged();
                return false;
            }

            IsSaving = true;
            StateHasChanged();

            try
            {
                // Use the service method with change logging
                await CustCommsService.UpdateTrackerWithChangeLoggingAsync(
                    TrackerModel,
                    OriginalTrackerModel,
                    CurrentUser.DBUser.EmployeeNumber
                );

                // Update the original tracker model with the current state for future comparisons
                OriginalTrackerModel = CreateDeepCopy(TrackerModel);

                // Refresh logs to show the new change logs
                TrackerModel.TrackerLogs = await CustCommsService.GetPartChangeTrackerLogsAsync(TrackerModel.Id);
                
                // Refresh task logs for all tasks
                foreach (var task in TrackerModel.CMPartTasks)
                {
                    task.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(task.Id) ?? [];
                }
                foreach (var task in TrackerModel.WhereUsedPartTasks)
                {
                    task.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(task.Id) ?? [];
                }
                if (TrackerModel.TechServicesTask != null)
                {
                    TrackerModel.TechServicesTask.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(TrackerModel.TechServicesTask.Id) ?? [];
                }

                HasUnsavedChanges = false;
                return true;
            }
            catch (Exception ex)
            {
                SetError($"Failed to save changes: {ex.Message}");
                return false;
            }
            finally
            {
                IsSaving = false;
                StateHasChanged();
            }
        }

        protected async Task ReloadTracker()
        {
            if (!IsEditMode || string.IsNullOrWhiteSpace(PartNum))
                return;

            IsLoading = true;
            StateHasChanged();

            try
            {
                TrackerModel = await CustCommsService.GetPartChangeTrackerByPartNumAsync(PartNum, false);
                
                if (TrackerModel != null)
                {
                    await GetContextualDataForExistingLoadedTasks();
                    await CustCommsService.GetCMDexPartChangeTaskData(TrackerModel);

                    // Populate StatusCode for Tech Services task so IsTaskComplete works correctly
                    if (TrackerModel.TechServicesTask != null && TrackerModel.TechServicesTask.StatusId.HasValue && Statuses.Count > 0)
                    {
                        var status = Statuses.FirstOrDefault(s => s.Id == TrackerModel.TechServicesTask.StatusId);
                        TrackerModel.TechServicesTask.StatusCode = status?.Code ?? "";
                    }

                    // Update the original tracker model with the reloaded data
                    OriginalTrackerModel = CreateDeepCopy(TrackerModel);
                }

                HasUnsavedChanges = false;

                // Clear all validation messages on reload
                LastTimeBuyDateValidationMessage = null;
                ShowValidationSummary = false;
            }
            catch (Exception ex)
            {
                SetError($"Failed to reload tracker: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected void HandleStatusChange(CMHub_CustCommsPartChangeTaskModel task, int? newStatusId)
        {
            if (task == null || CurrentUser == null || !IsEditMode) return;

            var oldStatusId = task.StatusId;
            
            // Don't update if value hasn't changed
            if (oldStatusId == newStatusId) return;

            // Update the local model
            task.StatusId = newStatusId;
            task.Completed = newStatusId == _finalStatusId;

            // ECN modal trigger logic (UI responsibility)
            // Only prompt for ECN on CM Part tasks (Type 1) or Where Used tasks (Type 2)
            if ((task.Type == 1 || task.Type == 2) && task.StatusId == _finalStatusId && oldStatusId != _finalStatusId && string.IsNullOrEmpty(task.ECNNumber))
            {
                InvokeAsync(() => ShowECNModal(task, isPrompt: true));
            }

            SetUnsavedChanges();
        }

        // Method to show the ECN Modal
        protected Task ShowECNModal(CMHub_CustCommsPartChangeTaskModel task, bool isPrompt = false)
        {
            // clone minimal state
            var clone = new CMHub_CustCommsPartChangeTaskModel
            {
                Id = task.Id,
                ECNNumber = task.ECNNumber,
                TrackerPartNum = TrackerModel?.PartNum ?? string.Empty,
                ImpactedPartNum = task.ImpactedPartNum
            };
            return ECNModal?.Show(clone, isPrompt) ?? Task.CompletedTask;
        }

        protected async Task HandleECNSaved(CMHub_CustCommsPartChangeTaskModel updatedClone)
        {
            if (TrackerModel == null || CurrentUser == null || !IsEditMode)
                return;

            // Find the original task in CM Part Tasks
            var original = TrackerModel.CMPartTasks.FirstOrDefault(t => t.Id == updatedClone.Id);

            // If not found in CM tasks, check Where Used tasks
            if (original == null)
            {
                original = TrackerModel.WhereUsedPartTasks.FirstOrDefault(t => t.Id == updatedClone.Id);
            }

            if (original == null)
                return;

            // Don't update if value hasn't changed
            if (original.ECNNumber == updatedClone.ECNNumber)
                return;

            // Update the local model
            original.ECNNumber = updatedClone.ECNNumber;

            SetUnsavedChanges();
        }


        // Helper method to refresh task logs efficiently
        private async Task RefreshTaskLogs(CMHub_CustCommsPartChangeTaskModel task)
        {
            if (TrackerModel == null) return;

            var logs = await CustCommsService.GetPartChangeTaskLogsAsync(task.Id) ?? new List<CMHub_CustCommsPartChangeTaskLogModel>();

            // Find the task in the model and update its logs
            var cmTaskInModel = TrackerModel.CMPartTasks.FirstOrDefault(_ => _.Id == task.Id);
            if (cmTaskInModel != null)
            {
                cmTaskInModel.Logs = logs;
            }
            else
            {
                var wuTaskInModel = TrackerModel.WhereUsedPartTasks.FirstOrDefault(_ => _.Id == task.Id);
                if (wuTaskInModel != null)
                {
                    wuTaskInModel.Logs = logs;
                }
            }
        }

        protected async Task SaveTracker()
        {
            if (IsEditMode || TrackerModel == null || CurrentUser == null || !HasUnsavedChanges)
            {
                // Only save if in Add mode, model exists, user logged in, and there are changes
                return;
            }

            if (string.IsNullOrWhiteSpace(TrackerModel.PartNum))
            {
                SetError("Part Number cannot be empty.");
                return;
            }

            // Validate Last Time Buy Date before saving
            if (TrackerModel.PartEolt.LastTimeBuyDate != null && 
                !DataHelpers.IsBusinessReasonableDateTime(TrackerModel.PartEolt.LastTimeBuyDate))
            {
                LastTimeBuyDateValidationMessage = "Invalid date";
                SetError("Cannot save: Last Time Buy Date is invalid. Date must be between 1/1/1900 and 12/31/9999.");
                return;
            }

            IsLoading = true;
            StateHasChanged();

            CMHub_CustCommsPartChangeTrackerModel? createdTracker = null;
            bool lastTimeBuyDateSaved = false;

            try
            {
                // Create the tracker and its initial tasks
                createdTracker = await CustCommsService.CreatePartChangeTrackerAsync(TrackerModel, CurrentUser.DBUser.EmployeeNumber);

                // Save the PartEolt data (including Last Time Buy Date) if it has been modified
                if (TrackerModel.PartEolt.LastTimeBuyDate.HasValue)
                {
                    try
                    {
                        // Fetch the current part data from Epicor to preserve base fields
                        var existingPartData = await PartService.GetPartsByPartNumbersAsync<PartEoltDTO>(new[] { TrackerModel.PartNum });
                        var existingPart = existingPartData.FirstOrDefault();
                        
                        if (existingPart != null)
                        {
                            // Preserve the base fields from the existing part
                            TrackerModel.PartEolt.PartDescription = existingPart.PartDescription;
                            TrackerModel.PartEolt.RevisionNum = existingPart.RevisionNum;
                            TrackerModel.PartEolt.InActive = existingPart.InActive;
                            TrackerModel.PartEolt.Deprecated_c = existingPart.Deprecated_c;
                            TrackerModel.PartEolt.CMManaged_c = existingPart.CMManaged_c;
                            TrackerModel.PartEolt.CMOrignationDate_c = existingPart.CMOrignationDate_c;
                            
                            // Now update with the Last Time Buy date we want to set
                            await CustCommsService.UpdatePartEoltDataAsync(TrackerModel.PartEolt);
                            
                            // Log the Last Time Buy date that was saved during creation
                            string logDateString = TrackerModel.PartEolt.LastTimeBuyDate?.ToShortDateString() ?? "null";
                            await CustCommsService.CreatePartChangeTaskLogAsync(
                                new CMHub_CustCommsPartChangeTaskLogModel
                                {
                                    TrackerId = createdTracker.Id,
                                    TaskId = null, // Tracker-level log
                                    LogMessage = $"Last Time Buy Date Set To: {logDateString}"
                                },
                                CurrentUser.DBUser.EmployeeNumber
                            );
                            
                            lastTimeBuyDateSaved = true;
                        }
                        else
                        {
                            Console.Error.WriteLine($"Could not fetch existing part data for {TrackerModel.PartNum}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to update Last Time Buy Date for new tracker {createdTracker.PartNum}: {ex}");
                        // Don't return - we'll show a message but still navigate
                    }
                }

                // Send email notification for the new tracker
                SendNewTechServicesNotificationEmail(createdTracker);

                HasUnsavedChanges = false; // Reset flag

                // Navigate to the Edit page of the newly created tracker
                var targetUrl = $"{NavHelpers.CMHub_CustCommsTrackerDetails}/{createdTracker.PartNum}";
                
                // If Last Time Buy date wasn't saved, add a query parameter to show a message
                if (TrackerModel.PartEolt.LastTimeBuyDate.HasValue && !lastTimeBuyDateSaved)
                {
                    targetUrl += "?ltbFailed=true";
                }
                
                NavigationManager.NavigateTo(targetUrl);
            }
            catch (Exception ex)
            {
                // Keep HasUnsavedChanges = true on failure
                HasUnsavedChanges = true;
                IsLoading = false;
                SetError("Failed to save tracker. Please try again.");
                Console.Error.WriteLine($"SaveTracker Error: {ex}");
                StateHasChanged();
            }
        }

        protected void NavigateToTaskDetails(CMHub_CustCommsPartChangeTaskModel task)
        {
            NavigationManager.NavigateTo($"{NavHelpers.CMHub_CustCommsTaskDetails}/{task.Id}", forceLoad: false, replace: true);
        }

        // Tech Services handlers
        protected void HandleTechServicesLTBQuantityChanged(int? newQuantity)
        {
            if (TrackerModel == null || CurrentUser == null || !IsEditMode) return;

            // Don't update if value hasn't changed
            if (TrackerModel.TechServicesLTBQuantity == newQuantity) return;

            // Update the local model
            TrackerModel.TechServicesLTBQuantity = newQuantity;

            SetUnsavedChanges();
        }

        protected void HandleTechServicesStatusChanged((CMHub_CustCommsPartChangeTaskModel task, int? statusId) args)
        {
            if (args.task == null || CurrentUser == null || !IsEditMode) return;

            // Update StatusCode when status changes so IsTaskComplete works correctly
            if (args.statusId.HasValue && Statuses.Count > 0)
            {
                var status = Statuses.FirstOrDefault(s => s.Id == args.statusId);
                args.task.StatusCode = status?.Code ?? "";
            }

            HandleStatusChange(args.task, args.statusId);
        }

        protected Task ShowTechServicesLogs()
        {
            if (TrackerModel?.TechServicesTask == null)
                return Task.CompletedTask;

            var taskLogs = TrackerModel.TechServicesTask.Logs
                .OrderByDescending(l => l.LogDate)
                .ToList();

            TaskLogsModal?.Show(
                title: "Tech Services Task Logs",
                taskLogs: taskLogs
            );

            return Task.CompletedTask;
        }

        protected async void ShowAddLogModalForTechServices()
        {
            if (NewLogModal == null || TrackerModel == null || CurrentUser == null) return;

            // If Tech Services task doesn't exist yet, create it on-demand
            if (TrackerModel.TechServicesTask == null)
            {
                try
                {
                    _logger.LogInformation("Creating Tech Services task for Tracker ID {TrackerId}", TrackerModel.Id);
                    
                    var techServicesTaskId = await CustCommsService.CreateOrGetTechServicesTaskAsync(
                        TrackerModel.Id,
                        CurrentUser.DBUser.EmployeeNumber
                    );

                    _logger.LogInformation("Tech Services task created with ID {TaskId}", techServicesTaskId);

                    var task = await CustCommsService.GetPartChangeTaskByTaskIdAsync(techServicesTaskId);
                    if (task != null)
                    {
                        task.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(techServicesTaskId)
                            ?? new List<CMHub_CustCommsPartChangeTaskLogModel>();
                        TrackerModel.TechServicesTask = task;
                        await InvokeAsync(StateHasChanged);
                    }
                    else
                    {
                        _logger.LogWarning("GetPartChangeTaskByTaskIdAsync returned null for Task ID {TaskId}", techServicesTaskId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create Tech Services task for Tracker ID {TrackerId}", TrackerModel.Id);
                    SetError($"Failed to create Tech Services task: {ex.Message}");
                    return;
                }
            }

            if (TrackerModel.TechServicesTask != null)
            {
                NewLogModal.Show(
                    trackerId: TrackerModel.Id,
                    taskId: TrackerModel.TechServicesTask.Id,
                    context: "Tech Services Task"
                );
            }
        }

        protected void ShowManualTaskModal(string fillPartNum = "")
        {
            ManualTaskPartNum = fillPartNum;
            ManualTaskError = string.Empty;
            ManualTaskModal?.Show();
        }

        protected void HideManualTaskModal()
        {
            ManualTaskModal?.Hide();
        }

        protected async Task AddManualTaskToTracker()
        {
            ManualTaskError = string.Empty;

            if (string.IsNullOrWhiteSpace(ManualTaskPartNum))
            {
                ManualTaskError = "Part number cannot be empty.";
                return;
            }

            if (CurrentUser == null)
            {
                ManualTaskError = "User not found.";
                return;
            }

            try
            {
                if (TrackerModel != null && ManualTaskPartNum.Trim() == TrackerModel.PartNum)
                {
                    ManualTaskError = $"'{ManualTaskPartNum}' is already the primary tracked part for this Part Change Tracker.";
                    return;
                }

                IsLoading = true;

                var parts = await PartService.GetPartsByPartNumbersAsync<PartDTO_Base>([ManualTaskPartNum.Trim()]);
                var part = parts.FirstOrDefault();
                if (part == null)
                {
                    ManualTaskError = $"Part '{ManualTaskPartNum}' not found.";
                    return;
                }

                if (TrackerModel == null)
                {
                    ManualTaskError = "Tracker model is not loaded.";
                    return;
                }

                var alreadyExists = TrackerModel.CMPartTasks.Any(t => t.ImpactedPartNum == part.PartNum)
                                 || TrackerModel.WhereUsedPartTasks.Any(t => t.ImpactedPartNum == part.PartNum);

                if (alreadyExists)
                {
                    ManualTaskError = $"Part '{ManualTaskPartNum}' is already in the task list.";
                    return;
                }

                var newTask = new CMHub_CustCommsPartChangeTaskModel
                {
                    TrackerPartNum = TrackerModel.PartNum,
                    ImpactedPartNum = part.PartNum,
                    ImpactedPartDesc = part.PartDescription,
                    ImpactedPartRev = part.RevisionNum,
                    QtyPer = 0, // or default if unknown
                    Lvl = 0,
                    Sort = "",
                    Deleted = false,
                    StatusId = 5,
                    Type = part.CMManaged_c ? 1 : 2
                };

                ManualTaskModal?.Hide();

                if (newTask.Type == 1)
                    TrackerModel.CMPartTasks.Add(newTask);
                else
                    TrackerModel.WhereUsedPartTasks.Add(newTask);

                IsLoading = false;

                var newTaskId = await CustCommsService.CreatePartChangeTaskAsync(newTask, TrackerModel.Id, newTask.Type, manual: true, CurrentUser.DBUser.EmployeeNumber);

                newTask.Logs = (await CustCommsService.GetPartChangeTaskLogsAsync(newTaskId)) ?? new();

                await GetContextualDataForExistingLoadedTasks();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error adding manual task: {ex}");
                ManualTaskError = "An error occurred while adding the task.";
            }

            IsLoading = false;
        }

        /// <summary>
        /// Sends an email notification when a new Part Change Tracker is created.
        /// </summary>
        private void SendNewTechServicesNotificationEmail(CMHub_CustCommsPartChangeTrackerModel tracker)
        {
            try
            {
                const string notificationRecipient = "eoltnotifmembers@crystalrugged.com";

                var trackerUrl = $"{NavigationManager.BaseUri.TrimEnd('/')}{NavHelpers.CMHub_CustCommsTrackerDetails}/{tracker.PartNum}";
                
                var subject = $"New Tech Services EOLT Task Created: {tracker.PartNum}";
                var messageHtml = $@"
                    <h2>New Tech Services EOLT Task Created</h2>
                    <p>A new Part Change Tracker has been created and Tech Services is requested to enter a LTB quantity estimate:</p>
                    <ul>
                        <li><strong>Part Number:</strong> {tracker.PartNum}</li>
                        <li><strong>Part Description:</strong> {tracker.PartDesc}</li>
                        <li><strong>Part Revision:</strong> {tracker.PartRev ?? "N/A"}</li>
                        <li><strong>Created By:</strong> {CurrentUser?.DBUser?.DisplayName ?? CurrentUser?.DBUser?.EmployeeNumber ?? "Unknown"}</li>
                        <li><strong>CM Part Tasks:</strong> {tracker.CMPartTasks.Count}</li>
                        <li><strong>Where Used Part Tasks:</strong> {tracker.WhereUsedPartTasks.Count}</li>
                    </ul>
                    <p><a href=""{trackerUrl}"">View Tracker Details</a></p>
                ";

                EmailHelpers.SendEmail(
                    subject: subject,
                    messageHtml: messageHtml,
                    toRecipients: [notificationRecipient],
                    environmentName: HostEnvironment.EnvironmentName
                );

                _logger.LogInformation("Sent new tracker notification email for Part {PartNum} to {Recipient}", 
                    tracker.PartNum, notificationRecipient);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the tracker creation
                _logger.LogError(ex, "Failed to send new tracker notification email for Part {PartNum}", tracker.PartNum);
            }
        }
    }
}
