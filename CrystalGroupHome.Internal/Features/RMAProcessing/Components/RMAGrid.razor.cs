using Blazorise;
using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using static CrystalGroupHome.Internal.Features.RMAProcessing.Components.RMATypeSegmentedButton;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components
{
    public class RMAGridBase : ComponentBase
    {
        [Inject] protected IRMAValidationService RMAValidationService { get; set; } = default!;
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
        [Inject] protected ILogger<RMAGridBase> Logger { get; set; } = default!;

        protected RMASummaryQuery Query { get; set; } = new();
        protected RMASummaryResponse RMAResponse { get; set; } = new();
        protected bool IsLoading { get; set; } = false;
        protected bool ShowFilters { get; set; } = false;

        // NEW: RMA Details Modal state
        protected bool ShowDetailsModal { get; set; } = false;
        protected RMASummaryModel? SelectedRMA { get; set; }

        // NEW: RMA Type Filter
        protected RMATypeFilter CurrentRMATypeFilter { get; set; } = RMATypeFilter.Epicor;
        protected RMATypeCounts? TypeCounts { get; set; }

        // CHANGED: Set default to show all RMAs (changed from true to null)
        protected bool? DefaultOpenRMAFilter { get; set; } = null;

        // ORIGINAL: Sorting properties
        protected string CurrentSortBy { get; set; } = "RMANum";
        protected string CurrentSortDirection { get; set; } = "desc";
        private const string DefaultSortBy = "RMANum";
        private const string DefaultSortDirection = "desc";

        protected override async Task OnInitializedAsync()
        {
            // Initialize query with defaults
            Query = new RMASummaryQuery
            {
                SortBy = DefaultSortBy,
                SortDirection = DefaultSortDirection,
                OpenRMAFilter = DefaultOpenRMAFilter,
                PageSize = 50,
                Page = 1,
                LegacyRMAFilter = null // Default to Epicor only
            };

            CurrentSortBy = DefaultSortBy;
            CurrentSortDirection = DefaultSortDirection;

            Logger?.LogInformation("RMAGrid initialized with default filters: OpenRMA={OpenRMA}, LegacyRMA={LegacyRMA}", 
                Query.OpenRMAFilter, Query.LegacyRMAFilter);

            await LoadData();
            await LoadTypeCounts();
        }

        // ORIGINAL: Sorting methods
        protected bool IsDefaultSort()
        {
            return CurrentSortBy == DefaultSortBy && CurrentSortDirection == DefaultSortDirection;
        }

        protected async Task ResetSortToDefault()
        {
            CurrentSortBy = DefaultSortBy;
            CurrentSortDirection = DefaultSortDirection;
            Query.SortBy = DefaultSortBy;
            Query.SortDirection = DefaultSortDirection;
            await LoadData();
            StateHasChanged();
        }

        protected async Task OnColumnHeaderClicked(string fieldName)
        {
            // If clicking the same column, toggle direction
            if (CurrentSortBy == fieldName)
            {
                CurrentSortDirection = CurrentSortDirection == "asc" ? "desc" : "asc";
            }
            else
            {
                // New column, start with ascending (except for dates which should start descending)
                CurrentSortBy = fieldName;
                CurrentSortDirection = fieldName == "RMADate" ? "desc" : "asc";
            }

            Query.SortBy = CurrentSortBy;
            Query.SortDirection = CurrentSortDirection;
            await LoadData();
            StateHasChanged();
        }

        protected string GetSortIcon(string fieldName)
        {
            if (CurrentSortBy != fieldName)
                return "fas fa-sort text-muted"; // Unsorted

            return CurrentSortDirection == "asc" 
                ? "fas fa-sort-up text-primary" 
                : "fas fa-sort-down text-primary";
        }

        protected string GetSortTitle(string fieldName)
        {
            if (CurrentSortBy != fieldName)
                return $"Click to sort by {fieldName}";

            var currentDirection = CurrentSortDirection == "asc" ? "ascending" : "descending";
            var nextDirection = CurrentSortDirection == "asc" ? "descending" : "ascending";
            
            return $"Currently sorted by {fieldName} ({currentDirection}). Click to sort {nextDirection}.";
        }

        // ORIGINAL: Filter methods
        protected bool HasActiveFilters()
        {
            return !string.IsNullOrEmpty(Query.RMANumberFilter) ||
                   Query.HDCaseNumFilter.HasValue ||
                   Query.RMADateFrom.HasValue ||
                   Query.RMADateTo.HasValue ||
                   !string.IsNullOrEmpty(Query.SerialNumberFilter) ||
                   !string.IsNullOrEmpty(Query.NotesFilter);
        }

        protected async Task OnHDCaseFilterChanged(int? value)
        {
            Query.HDCaseNumFilter = value;
            Query.Page = 1; // Reset to first page
            await LoadData(); // Automatically refresh data
            Logger?.LogInformation("HD Case filter changed to: {Value}", value);
        }

        // ORIGINAL: Navigation methods (FIXED to use original query parameter approach)
        protected void NavigateToFiles(int rmaNumber)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["rmaNumber"] = rmaNumber.ToString()
            };

            var url = QueryHelpers.AddQueryString("/rma-processing/files", queryParams);
            NavigationManager.NavigateTo(url);
        }

        protected void NavigateToUpload(int rmaNumber)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["rmaNumber"] = rmaNumber.ToString(),
                ["tab"] = "upload"
            };

            var url = QueryHelpers.AddQueryString("/rma-processing/files", queryParams);
            NavigationManager.NavigateTo(url);
        }

        // ORIGINAL: Pagination methods
        protected async Task ChangePage(int page)
        {
            Query.Page = page;
            await LoadData();
        }

        protected List<int> GetVisiblePages()
        {
            var totalPages = RMAResponse.TotalPages;
            var currentPage = RMAResponse.Page;
            var visiblePages = new List<int>();

            if (totalPages <= 7)
            {
                // Show all pages if 7 or fewer
                for (int i = 1; i <= totalPages; i++)
                {
                    visiblePages.Add(i);
                }
            }
            else
            {
                // Always show first page
                visiblePages.Add(1);

                var startPage = Math.Max(2, currentPage - 2);
                var endPage = Math.Min(totalPages - 1, currentPage + 2);

                // Add ellipsis if there's a gap after page 1
                if (startPage > 2)
                {
                    visiblePages.Add(-1); // -1 represents ellipsis
                }

                // Add pages around current page
                for (int i = startPage; i <= endPage; i++)
                {
                    visiblePages.Add(i);
                }

                // Add ellipsis if there's a gap before last page
                if (endPage < totalPages - 1)
                {
                    visiblePages.Add(-1); // -1 represents ellipsis
                }

                // Always show last page
                if (totalPages > 1)
                {
                    visiblePages.Add(totalPages);
                }
            }

            return visiblePages;
        }

        // NEW: RMA Details Modal methods
        protected void ShowRMADetails(RMASummaryModel rma)
        {
            SelectedRMA = rma;
            ShowDetailsModal = true;
        }

        protected async Task LoadData()
        {
            IsLoading = true;
            try
            {
                Logger?.LogInformation("Loading RMA data with query: Page={Page}, PageSize={PageSize}, OpenRMAFilter={OpenRMA}, LegacyRMAFilter={LegacyRMA}", 
                    Query.Page, Query.PageSize, Query.OpenRMAFilter, Query.LegacyRMAFilter);

                RMAResponse = await RMAValidationService.GetRMASummariesAsync(Query);
                
                Logger?.LogInformation("Loaded {DisplayCount} of {TotalCount} RMAs. OpenRMAFilter: {OpenRMAFilter}, HasFilesFilter: {HasFilesFilter}", 
                    RMAResponse.RMAs.Count, RMAResponse.TotalCount, Query.OpenRMAFilter, Query.HasFilesFilter);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error loading RMA data");
                RMAResponse = new RMASummaryResponse
                {
                    RMAs = new List<RMASummaryModel>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = 50
                };
            }
            finally
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        // NEW: Load type counts for the segmented button badges
        protected async Task LoadTypeCounts()
        {
            try
            {
                // Create queries for each type to get counts
                var epicorQuery = new RMASummaryQuery
                {
                    LegacyRMAFilter = null, // Epicor only
                    PageSize = 1
                };

                var legacyQuery = new RMASummaryQuery
                {
                    LegacyRMAFilter = true, // Legacy only
                    PageSize = 1
                };

                var allQuery = new RMASummaryQuery
                {
                    LegacyRMAFilter = false, // Both systems
                    PageSize = 1
                };

                var epicorResponse = await RMAValidationService.GetRMASummariesAsync(epicorQuery);
                var legacyResponse = await RMAValidationService.GetRMASummariesAsync(legacyQuery);
                var allResponse = await RMAValidationService.GetRMASummariesAsync(allQuery);

                TypeCounts = new RMATypeCounts
                {
                    EpicorCount = epicorResponse.TotalCount,
                    LegacyCount = legacyResponse.TotalCount,
                    TotalCount = allResponse.TotalCount
                };

                Logger?.LogInformation("Loaded type counts: Epicor={Epicor}, Legacy={Legacy}, Total={Total}", 
                    TypeCounts.EpicorCount, TypeCounts.LegacyCount, TypeCounts.TotalCount);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error loading RMA type counts");
                TypeCounts = null;
            }
        }

        protected async Task RefreshData()
        {
            await Task.WhenAll(LoadData(), LoadTypeCounts());
        }

        // NEW: Handle RMA type filter changes
        protected async Task OnRMATypeFilterChanged(RMATypeFilter newFilter)
        {
            if (CurrentRMATypeFilter == newFilter) return;

            CurrentRMATypeFilter = newFilter;
            
            // Map the segmented button selection to the query filter
            Query.LegacyRMAFilter = newFilter switch
            {
                RMATypeFilter.Epicor => null,    // Epicor only (default)
                RMATypeFilter.Legacy => true,    // Legacy only
                RMATypeFilter.All => false,      // Both systems
                _ => null
            };

            Query.Page = 1; // Reset to first page when changing RMA type
            
            Logger?.LogInformation("RMA Type filter changed to: {Filter} (LegacyRMAFilter: {LegacyRMAFilter})", 
                newFilter, Query.LegacyRMAFilter);

            await LoadData();
        }

        protected void ToggleFilters()
        {
            ShowFilters = !ShowFilters;
        }

        protected async Task ApplyFilters()
        {
            Query.Page = 1; // Reset to first page when applying filters
            await LoadData();
        }

        protected async Task ClearFilters()
        {
            // Reset RMA type filter to default (Epicor)
            CurrentRMATypeFilter = RMATypeFilter.Epicor;
            
            Query = new RMASummaryQuery
            {
                SortBy = Query.SortBy, // Preserve current sort
                SortDirection = Query.SortDirection,
                OpenRMAFilter = DefaultOpenRMAFilter, // KEEP default behavior when clearing
                LegacyRMAFilter = null, // Reset to Epicor only
                PageSize = Query.PageSize,
                Page = 1,
                // Explicitly set all other filters to null
                RMANumberFilter = null,
                HDCaseNumFilter = null,
                RMADateFrom = null,
                RMADateTo = null,
                HasFilesFilter = null,
                SerialNumberFilter = null,
                NotesFilter = null
            };
            
            await LoadData();
            StateHasChanged();
            Logger?.LogInformation("All filters cleared, reset to defaults");
        }

        // String-based status filter methods for better Blazorise compatibility
        protected string GetStatusFilterString()
        {
            return Query.OpenRMAFilter switch
            {
                true => "open",
                false => "closed", 
                null => "all"
            };
        }

        protected async Task OnStatusFilterStringChanged(string newValue)
        {
            bool? newBoolValue = newValue switch
            {
                "open" => true,
                "closed" => false,
                "all" => null,
                _ => null
            };
            
            if (Query.OpenRMAFilter != newBoolValue)
            {
                Query.OpenRMAFilter = newBoolValue;
                Query.Page = 1; // Reset to first page
                await LoadData(); // Automatically refresh data
                Logger?.LogInformation("Status filter changed to: {NewValue} (bool: {BoolValue})", 
                    newValue, newBoolValue);
            }
        }

        // String-based Has Files filter for better reliability
        protected string GetHasFilesFilterString()
        {
            return Query.HasFilesFilter switch
            {
                true => "with",
                false => "without", 
                null => "all"
            };
        }

        protected async Task OnHasFilesFilterStringChanged(string newValue)
        {
            bool? newBoolValue = newValue switch
            {
                "with" => true,
                "without" => false,
                "all" => null,
                _ => null
            };
            
            if (Query.HasFilesFilter != newBoolValue)
            {
                Query.HasFilesFilter = newBoolValue;
                Query.Page = 1; // Reset to first page
                await LoadData(); // Automatically refresh data
                Logger?.LogInformation("Has Files filter changed to: {NewValue} (bool: {BoolValue})", 
                    newValue, newBoolValue);
            }
        }

        protected void NavigateToRMAFiles(int rmaNum)
        {
            NavigationManager.NavigateTo($"/rma-processing/files/{rmaNum}");
        }

        protected void NavigateToRMAFilesEmbedded(int rmaNum)
        {
            NavigationManager.NavigateTo($"/rma-processing/files-embedded/{rmaNum}");
        }

        // NEW: Helper class for type counts
        protected class RMATypeCounts
        {
            public int EpicorCount { get; set; }
            public int LegacyCount { get; set; }
            public int TotalCount { get; set; }
        }
    }
}