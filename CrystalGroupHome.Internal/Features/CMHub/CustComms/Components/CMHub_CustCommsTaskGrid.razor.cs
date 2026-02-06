using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Data;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Models;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Components
{
    public class CMHub_CustCommsTaskGridBase : ComponentBase
    {
        [Inject] public ICMHub_CustCommsService CustCommsService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

        protected List<CMHub_CustCommsPartChangeTaskModel> AllTasks = new();
        protected List<CMHub_CustCommsPartChangeTaskModel> Tasks = new();
        protected DataGrid<CMHub_CustCommsPartChangeTaskModel>? TaskGrid;
        protected bool IsLoading = false;

        protected List<string> PrimaryPMs = new();
        protected string SelectedPM = string.Empty;

        protected List<CMHub_CustCommsPartChangeTaskStatusDTO> Statuses = new();

        protected const string TechServicesAssignee = "Tech Services";

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            try
            {
                Statuses = await CustCommsService.GetTaskStatusesAsync();

                var trackers = await CustCommsService.GetPartChangeTrackersAsync();
                
                // Get CM Part Tasks (Type 1)
                var cmPartTasks = trackers
                    .SelectMany(t => t.CMPartTasks)
                    .Where(t => !t.Deleted && t.StatusId != 4) // 4 = Completed Task
                    .ToList();

                // Get Tech Services Tasks (Type 3) and set their assignee
                var techServicesTasks = trackers
                    .Where(t => t.TechServicesTask != null && !t.TechServicesTask.Deleted)
                    .Select(t => t.TechServicesTask!)
                    .Where(t => !IsCompletedTechServicesStatus(t.StatusId))
                    .ToList();

                // Combine all tasks
                AllTasks = cmPartTasks.Concat(techServicesTasks).ToList();

                foreach (var task in AllTasks)
                {
                    var status = Statuses.FirstOrDefault(s => s.Id == task.StatusId);
                    task.StatusDescription = status?.Description ?? "Unknown";
                    task.StatusCode = status?.Code ?? "";
                }

                // Build the list of PMs (excluding Tech Services tasks)
                PrimaryPMs = cmPartTasks
                    .Select(t => t.CMDexPart?.PrimaryPMName ?? "")
                    .Where(pm => !string.IsNullOrWhiteSpace(pm))
                    .Distinct()
                    .OrderBy(pm => pm)
                    .ToList();

                var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
                if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("pm", out var pm))
                {
                    SelectedPM = pm.FirstOrDefault() ?? string.Empty;
                }

                FilterTasks();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Check if a status ID corresponds to a completed Tech Services status (TS_CONFIRMED)
        /// </summary>
        private bool IsCompletedTechServicesStatus(int? statusId)
        {
            if (statusId == null) return false;
            var status = Statuses.FirstOrDefault(s => s.Id == statusId);
            return status?.Code?.Equals("TS_CONFIRMED", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Check if a task is a Tech Services task (Type 3)
        /// </summary>
        protected bool IsTechServicesTask(CMHub_CustCommsPartChangeTaskModel task)
        {
            return task.Type == 3;
        }

        /// <summary>
        /// Get the display name for the "Assigned To" column
        /// </summary>
        protected string GetAssignedToDisplay(CMHub_CustCommsPartChangeTaskModel task)
        {
            return IsTechServicesTask(task) ? TechServicesAssignee : task.PrimaryPMName;
        }

        protected void OnSelectedPMChanged(string newPM)
        {
            SelectedPM = newPM;
            FilterTasks();
            TaskGrid?.Reload();

            var uri = NavigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                ["pm"] = string.IsNullOrWhiteSpace(SelectedPM) ? null : SelectedPM
            });

            NavigationManager.NavigateTo(uri, forceLoad: false, replace: true);
        }

        private void FilterTasks()
        {
            if (string.IsNullOrWhiteSpace(SelectedPM))
            {
                // "All PMs" - exclude Tech Services tasks
                Tasks = AllTasks
                    .Where(t => !IsTechServicesTask(t))
                    .ToList();
            }
            else if (SelectedPM == TechServicesAssignee)
            {
                // "Tech Services" - only show Tech Services tasks
                Tasks = AllTasks
                    .Where(t => IsTechServicesTask(t))
                    .ToList();
            }
            else
            {
                // Specific PM - filter by PM name
                Tasks = AllTasks
                    .Where(t => !IsTechServicesTask(t) && t.CMDexPart?.PrimaryPMName == SelectedPM)
                    .ToList();
            }
        }

        protected void NavigateToTask(int taskId)
        {
            NavigationManager.NavigateTo($"{NavHelpers.CMHub_CustCommsTaskDetails}/{taskId}");
        }
    }
}