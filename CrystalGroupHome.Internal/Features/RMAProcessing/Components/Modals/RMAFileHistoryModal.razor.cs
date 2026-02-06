using Blazorise;
using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components.Modals
{
    public class RMAFileHistoryModalBase : ComponentBase
    {
        [Inject] protected IRMAFileService RMAFileService { get; set; } = default!;
        [Inject] protected ILogger<RMAFileHistoryModalBase>? Logger { get; set; }

        [Parameter] public bool IsVisible { get; set; }
        [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }
        [Parameter] public string? RmaNumber { get; set; }
        [Parameter] public string? RmaLineNumber { get; set; }
        [Parameter] public string? SerialNumber { get; set; }

        protected List<RMAFileHistoryModel> RMAFileHistory { get; set; } = new();
        protected List<RMAFileHistoryModel> FilteredRMAFileHistory { get; set; } = new();
        protected DataGrid<RMAFileHistoryModel>? HistoryDataGrid { get; set; }
        protected bool ShowFilters { get; set; }

        // Filter properties
        protected string? ActionFilter { get; set; }
        protected string? FileNameFilter { get; set; }
        protected string? CategoryHistoryFilter { get; set; }
        protected string? SerialNumberHistoryFilter { get; set; }
        protected string? UsernameFilter { get; set; }
        protected string? ActionDetailsFilter { get; set; }
        protected DateTime? DateFromFilter { get; set; }
        protected DateTime? DateToFilter { get; set; }

        protected int FilteredHistoryCount => FilteredRMAFileHistory.Count;

        protected bool HasLineSerialContext() => !string.IsNullOrEmpty(RmaLineNumber);

        protected override async Task OnParametersSetAsync()
        {
            if (IsVisible && RMAFileHistory.Count == 0)
            {
                await LoadRMAFileHistory();
            }
        }

        // Handle all modal closing scenarios (X button, ESC, outside click, Cancel button)
        protected async Task OnModalClosing(ModalClosingEventArgs e)
        {
            // Clear internal state
            RMAFileHistory.Clear();
            FilteredRMAFileHistory.Clear();
            ClearAllHistoryFiltersInternal();

            // CRITICAL: Tell the parent to hide the modal
            await IsVisibleChanged.InvokeAsync(false);
        }

        // Handle explicit close button clicks
        protected async Task CloseModal()
        {
            // Clear internal state
            RMAFileHistory.Clear();
            FilteredRMAFileHistory.Clear();
            ClearAllHistoryFiltersInternal();

            // Tell the parent to hide the modal
            await IsVisibleChanged.InvokeAsync(false);
        }

        protected async Task LoadRMAFileHistory()
        {
            if (string.IsNullOrEmpty(RmaNumber)) return;

            try
            {
                var query = new RMAFileQuery
                {
                    RmaNumber = RmaNumber,
                    RmaLineNumber = RmaLineNumber,
                    SerialNumber = SerialNumber
                };

                RMAFileHistory = await RMAFileService.GetRMAFileHistoryAsync(query);
                ApplyHistoryFilters();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error loading RMA file history for RMA {RmaNumber}", RmaNumber);
                RMAFileHistory = new();
                FilteredRMAFileHistory = new();
            }
        }

        private void ClearAllHistoryFiltersInternal()
        {
            ActionFilter = null;
            FileNameFilter = null;
            CategoryHistoryFilter = null;
            SerialNumberHistoryFilter = null;
            UsernameFilter = null;
            ActionDetailsFilter = null;
            DateFromFilter = null;
            DateToFilter = null;
            ShowFilters = false;
        }

        protected void ToggleFilters() => ShowFilters = !ShowFilters;

        protected void ClearAllHistoryFilters()
        {
            ActionFilter = null;
            FileNameFilter = null;
            CategoryHistoryFilter = null;
            SerialNumberHistoryFilter = null;
            UsernameFilter = null;
            ActionDetailsFilter = null;
            DateFromFilter = null;
            DateToFilter = null;
            ApplyHistoryFilters();
        }

        protected bool HasActiveFilters() =>
            !string.IsNullOrEmpty(ActionFilter) ||
            !string.IsNullOrEmpty(FileNameFilter) ||
            !string.IsNullOrEmpty(CategoryHistoryFilter) ||
            !string.IsNullOrEmpty(SerialNumberHistoryFilter) ||
            !string.IsNullOrEmpty(UsernameFilter) ||
            !string.IsNullOrEmpty(ActionDetailsFilter) ||
            DateFromFilter.HasValue ||
            DateToFilter.HasValue;

        // Filter event handlers
        protected void OnActionFilterChanged(string v) { ActionFilter = v; ApplyHistoryFilters(); }
        protected void OnFileNameFilterChanged(string v) { FileNameFilter = v; ApplyHistoryFilters(); }
        protected void OnCategoryHistoryFilterChanged(string v) { CategoryHistoryFilter = string.IsNullOrEmpty(v) ? null : v; ApplyHistoryFilters(); }
        protected void OnSerialNumberHistoryFilterChanged(string v) { SerialNumberHistoryFilter = v; ApplyHistoryFilters(); }
        protected void OnUsernameFilterChanged(string v) { UsernameFilter = string.IsNullOrEmpty(v) ? null : v; ApplyHistoryFilters(); }
        protected void OnActionDetailsFilterChanged(string v) { ActionDetailsFilter = v; ApplyHistoryFilters(); }
        protected void OnDateFromFilterChanged(DateTime? v) { DateFromFilter = v; ApplyHistoryFilters(); }
        protected void OnDateToFilterChanged(DateTime? v) { DateToFilter = v; ApplyHistoryFilters(); }

        protected void ApplyHistoryFilters()
        {
            FilteredRMAFileHistory = RMAFileHistory.Where(h =>
                (string.IsNullOrEmpty(ActionFilter) || h.Action.Contains(ActionFilter, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(FileNameFilter) || h.FileName.Contains(FileNameFilter, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(SerialNumberHistoryFilter) || (!string.IsNullOrEmpty(h.SerialNumber) && h.SerialNumber.Contains(SerialNumberHistoryFilter, StringComparison.OrdinalIgnoreCase))) &&
                (string.IsNullOrEmpty(ActionDetailsFilter) || (!string.IsNullOrEmpty(h.ActionDetails) && h.ActionDetails.Contains(ActionDetailsFilter, StringComparison.OrdinalIgnoreCase))) &&
                (string.IsNullOrEmpty(CategoryHistoryFilter) || h.CategoryDisplayValue == CategoryHistoryFilter) &&
                (string.IsNullOrEmpty(UsernameFilter) || h.ActionByUsername == UsernameFilter) &&
                (DateFromFilter == null || h.ActionDate.Date >= DateFromFilter.Value.Date) &&
                (DateToFilter == null || h.ActionDate.Date <= DateToFilter.Value.Date)
            ).ToList();
        }

        protected string GetActionBadgeClass(string action) =>
            action?.ToLowerInvariant() switch
            {
                "upload" => "bg-success",
                "delete" => "bg-danger",
                "restore" => "bg-info",
                "print" => "bg-primary",
                "download" => "bg-secondary",
                _ => "bg-secondary"
            };

        protected List<string> GetUniqueCategories() =>
            RMAFileHistory.Select(h => h.CategoryDisplayValue)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

        protected List<string> GetUniqueUsers() =>
            RMAFileHistory.Select(h => h.ActionByUsername)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .OrderBy(u => u)
                .ToList();

        // Use the shared FileHelpers.FormatFileSize method
        protected string FormatFileSize(long bytes) => FileHelpers.FormatFileSize(bytes);
    }
}