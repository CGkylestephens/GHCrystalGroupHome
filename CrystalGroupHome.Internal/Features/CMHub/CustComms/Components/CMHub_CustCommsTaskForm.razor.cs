using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Data;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Models;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Components
{
    public class CMHub_CustCommsTaskFormBase : ComponentBase
    {
        [Inject] public ICMHub_CustCommsService CustCommsService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public ICMHub_CMDexService CMDexService { get; set; } = default!;
        [Inject] public ICustomerService CustomerService { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;

        [Parameter] public int? TaskId { get; set; }
        [Parameter] public List<CMHub_CustCommsPartChangeTaskStatusDTO> Statuses { get; set; } = [];

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        protected CMHub_CustCommsPartChangeTaskModel? TaskModel;
        protected bool IsLoading = false;

        protected CMHub_CustCommsNewLogModal? RecordLogModal { get; set; }
        protected CMHub_CustCommsECNModal? ECNModal { get; set; }
        protected ConfirmationModal? ErrorModal { get; set; }
        protected string? ErrorMessage { get; set; } = null;

        private int _finalStatusId = -1;

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            _finalStatusId = await CustCommsService.GetFinalTaskStatusIdAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            try
            {
                if (TaskId != null)
                {
                    TaskModel = await CustCommsService.GetPartChangeTaskByTaskIdAsync((int)TaskId);
                    if (TaskModel != null)
                    {
                        TaskModel.CMDexPart = await CMDexService.GetCMDexPartAsync(TaskModel.ImpactedPartNum);
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }

            await base.OnParametersSetAsync();
        }

        protected void NavigateToTracker()
        {
            if (TaskModel != null)
            {
                NavigationManager.NavigateTo($"{NavHelpers.CMHub_CustCommsTrackerDetails}/{TaskModel.TrackerPartNum}");
            }
        }

        protected async Task HandleNewLogSubmitted(CMHub_CustCommsPartChangeTaskLogModel newLog)
        {
            if (TaskModel == null || CurrentUser == null) return;

            // Ensure LoggedByUser is set before saving
            newLog.LoggedByUser = CurrentUser.DBUser.EmployeeNumber;

            await CustCommsService.CreatePartChangeTaskLogAsync(newLog, CurrentUser.DBUser.EmployeeNumber);

            if (TaskModel != null)
            {
                await RefreshTaskLogs(TaskModel);
                TaskModel.LastUpdated = DateTime.UtcNow;
            }

            StateHasChanged();
        }

        // Helper method to refresh task logs efficiently
        private async Task RefreshTaskLogs(CMHub_CustCommsPartChangeTaskModel task)
        {
            if (task == null) return;

            task.Logs = await CustCommsService.GetPartChangeTaskLogsAsync(task.Id) ?? new List<CMHub_CustCommsPartChangeTaskLogModel>();
        }

        protected void ShowAddLogModalForTask(CMHub_CustCommsPartChangeTaskModel task)
        {
            if (RecordLogModal != null)
            {
                RecordLogModal.Show(
                    trackerId: task.TrackerId,
                    taskId: task.Id,
                    context: $"{task.ImpactedPartNum} (Rev {task.ImpactedPartRev})"
                );
            }
        }

        // Method to show the ECN Modal
        protected Task ShowECNModal(CMHub_CustCommsPartChangeTaskModel task, bool isPrompt = false)
        {
            // clone minimal state
            var clone = new CMHub_CustCommsPartChangeTaskModel
            {
                Id = task.Id,
                ECNNumber = task.ECNNumber,
                TrackerPartNum = task.TrackerPartNum ?? string.Empty,
                ImpactedPartNum = task.ImpactedPartNum
            };
            return ECNModal?.Show(clone, isPrompt) ?? Task.CompletedTask;
        }

        protected async Task HandleECNSaved(CMHub_CustCommsPartChangeTaskModel updatedClone)
        {
            if (TaskModel == null || CurrentUser == null)
                return;

            var success = await CustCommsService.UpdateTaskECNAsync(
                TaskModel,
                updatedClone.ECNNumber ?? "",
                CurrentUser.DBUser.EmployeeNumber
            );

            if (!success)
            {
                SetError($"Failed to save ECN for task {TaskModel.ImpactedPartNum}.");
                return;
            }

            // Refresh logs and last updated timestamp
            if (TaskId != null)
            {
                TaskModel = await CustCommsService.GetPartChangeTaskByTaskIdAsync((int)TaskId);
            }

            if (TaskModel != null)
            {
                TaskModel.LastUpdated = DateTime.UtcNow;
            }

            StateHasChanged();
        }

        protected async Task HandleStatusChange(CMHub_CustCommsPartChangeTaskModel task, int? newStatusId)
        {
            if (task == null || CurrentUser == null) return;

            var oldStatusId = task.StatusId;

            var success = await CustCommsService.UpdateTaskStatusAsync(task, newStatusId, CurrentUser.DBUser.EmployeeNumber);

            if (!success)
            {
                SetError("Failed to update task status.");
                return;
            }

            await RefreshTaskLogs(task);
            task.LastUpdated = DateTime.UtcNow;

            // ECN modal trigger logic (UI responsibility)
            if (task.StatusId == _finalStatusId && oldStatusId != _finalStatusId && string.IsNullOrEmpty(task.ECNNumber))
            {
                await InvokeAsync(() => ShowECNModal(task, isPrompt: true));
            }

            StateHasChanged();
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
    }
}
