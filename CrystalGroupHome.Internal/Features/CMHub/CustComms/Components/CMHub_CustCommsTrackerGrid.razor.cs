using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Data;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Models;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Components
{
    public class CMHub_CustCommsTrackerGridBase : ComponentBase
    {
        [Inject] public ICMHub_CustCommsService CustCommsService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        protected List<CMHub_CustCommsPartChangeTrackerModel> Trackers = new();
        protected List<CMHub_CustCommsPartChangeTaskStatusDTO> Statuses = new();
        protected DataGrid<CMHub_CustCommsPartChangeTrackerModel>? TrackerGrid;
        protected bool IsLoading = false;

        // We toggle detail rows, so keep track if we should ignore a row click
        private string _ignoreRowClickForPartNum = "";

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            try
            {
                // Fetch statuses first so we can populate StatusCode for Tech Services tasks
                Statuses = await CustCommsService.GetTaskStatusesAsync();
                
                Trackers = await CustCommsService.GetPartChangeTrackersAsync();
                
                // Populate StatusCode for Tech Services tasks so IsTaskComplete works correctly
                foreach (var tracker in Trackers)
                {
                    if (tracker.TechServicesTask != null && tracker.TechServicesTask.StatusId.HasValue)
                    {
                        var status = Statuses.FirstOrDefault(s => s.Id == tracker.TechServicesTask.StatusId);
                        tracker.TechServicesTask.StatusCode = status?.Code ?? "";
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected void OnRowStyling(CMHub_CustCommsPartChangeTrackerModel tracker, DataGridRowStyling styling)
        {
            if (tracker.IsSelected)
            {
                styling.Class = "active-row";
            }
        }

        protected void SelectRow(DataGridRowMouseEventArgs<CMHub_CustCommsPartChangeTrackerModel> args)
        {
            if (_ignoreRowClickForPartNum != args.Item.PartNum && Trackers != null)
            {
                args.Item.IsSelected = true;

                // Only select one row at a time
                foreach (var tracker in Trackers.Where(e => e != args.Item && e.IsSelected))
                {
                    tracker.IsSelected = false;
                }
            }

            _ignoreRowClickForPartNum = "";
        }

        private void ResetAllSelectedRows()
        {
            if (Trackers is null || TrackerGrid is null)
                return;

            foreach (var tracker in Trackers)
            {
                if (tracker.IsSelected)
                {
                    TrackerGrid.ToggleDetailRow(tracker);
                    tracker.IsSelected = false;
                }
            }
        }

        protected void NavigateToEditPartChangeTracker(CMHub_CustCommsPartChangeTrackerModel trackerModel)
        {
            _ignoreRowClickForPartNum = trackerModel.PartNum;
            ResetAllSelectedRows();
            NavigationManager.NavigateTo($"{NavHelpers.CMHub_CustCommsTrackerDetails}/{trackerModel.PartNum}", forceLoad: false, replace: false);
        }

        protected void NavigateToAddTracker()
        {
            ResetAllSelectedRows();
            NavigationManager.NavigateTo($"{NavHelpers.CMHub_CustCommsAddTracker}", forceLoad: false, replace: false);
        }
    }
}