using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.CMHub.CMNotif.Data;
using CrystalGroupHome.Internal.Features.CMHub.CMNotif.Models;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Components
{
    public class CMHub_CMNotifRecordGridBase : ComponentBase
    {
        [Inject] public ICMHub_CMNotifService CMNotifService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        // ------------------------------------------------------
        // Filter backing fields
        private int _currentPage = 1;
        private string _selectedPM = "All";
        private string _selectedStatus = "Incomplete";
        private string _selectedPartNum = string.Empty;
        private string _selectedECNNumber = string.Empty;

        // ------------------------------------------------------
        // Lifecycle flags
        private bool _hasInitialFilterApplied = false;

        // We keep track of the current full URI with query strings
        public string NewParameterUri { get; private set; } = string.Empty;

        // ------------------------------------------------------
        // Filter properties with on-change triggers
        [Parameter]
        public int CurrentPage
        {
            get => _currentPage <= 0 ? 1 : _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value <= 0 ? 1 : value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus != value)
                {
                    _selectedStatus = value;
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string SelectedPM
        {
            get => _selectedPM;
            set
            {
                if (_selectedPM != value)
                {
                    _selectedPM = value;
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string SelectedPartNum
        {
            get => _selectedPartNum;
            set
            {
                if (_selectedPartNum != value)
                {
                    _selectedPartNum = value;
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string SelectedECNNumber
        {
            get => _selectedECNNumber;
            set
            {
                if (_selectedECNNumber != value)
                {
                    _selectedECNNumber = value;
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        protected DataGrid<CMHub_CMNotifECNGroupedRecordModel>? ECNMatchedGroupedRecordGrid;
        protected bool IsLoading = false;

        protected List<string> ValidPrimaryPMs = new();
        protected List<CMHub_CMNotifECNGroupedRecordModel> FilteredGroups = new();
        
        protected List<CMHub_CMNotifECNMatchedRecordModel> ECNRecords = new();
        protected List<CMHub_CMNotifECNGroupedRecordModel> GroupedRecords = new();

        // We toggle detail rows, so keep track if we should ignore a row click
        private string _ignoreRowClickForECNNumber = "";

        // ------------------------------------------------------
        // Lifecycle

        protected override async Task OnInitializedAsync()
        {
            System.Diagnostics.Debug.WriteLine("OnInitializedAsync: Starting");
            
            // 1) Parse the query params to pre-populate filter fields
            GetQueryParams();

            // 2) Load the data (hide the grid until we're done)
            IsLoading = true;
            try
            {
                var allRecords = await CMNotifService.GetHeldECNPartsAsync(null, true);

                // Group ECN records
                GroupedRecords = allRecords
                    .GroupBy(r => r.ECNNumber)
                    .Select(g => new CMHub_CMNotifECNGroupedRecordModel
                    {
                        ECNNumber = g.Key,
                        Status = GetGroupStatus(g),
                        AllRecords = g.ToList()
                    })
                    .ToList();

                // Populate Primary PM names (unique)
                ValidPrimaryPMs = allRecords
                    .Select(r => r.PrimaryPMName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();

                // Add "All" as the first option
                ValidPrimaryPMs.Insert(0, "All");

                System.Diagnostics.Debug.WriteLine($"OnInitializedAsync: Loaded {GroupedRecords.Count} grouped records");

                // If the initial filter has already been applied (but with no data), reapply it now with data
                if (_hasInitialFilterApplied)
                {
                    System.Diagnostics.Debug.WriteLine("OnInitializedAsync: Reapplying filters after data load");
                    FilterRecords();
                }
            }
            finally
            {
                IsLoading = false;
                System.Diagnostics.Debug.WriteLine("OnInitializedAsync: Completed, IsLoading = false");
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            System.Diagnostics.Debug.WriteLine($"OnAfterRenderAsync: firstRender={firstRender}, _hasInitialFilterApplied={_hasInitialFilterApplied}");
            
            if (firstRender && !_hasInitialFilterApplied)
            {
                // After we're truly interactive, apply the filters once
                _hasInitialFilterApplied = true;
                System.Diagnostics.Debug.WriteLine("OnAfterRenderAsync: About to call FilterRecords()");
                FilterRecords();
                System.Diagnostics.Debug.WriteLine("OnAfterRenderAsync: FilterRecords() completed");
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        protected override async Task OnParametersSetAsync()
        {
            // Handle navigation back - re-parse query params if URI has changed
            var currentUri = NavigationManager.Uri;
            if (currentUri != NewParameterUri && _hasInitialFilterApplied && GroupedRecords.Count > 0)
            {
                GetQueryParams();
                FilterRecords();
            }
            
            await base.OnParametersSetAsync();
        }

        // ------------------------------------------------------
        // Filter logic

        /// <summary>
        /// Applies the filters if we've already completed the initial load/phase.
        /// This prevents repeated filtering during prerender.
        /// </summary>
        private void ApplyFiltersIfReady()
        {
            if (_hasInitialFilterApplied && GroupedRecords.Count > 0)
            {
                FilterRecords();
            }
        }

        /// <summary>
        /// Actually filters the in-memory data, updates the grid, and updates the URL.
        /// </summary>
        private void FilterRecords()
        {
            System.Diagnostics.Debug.WriteLine($"FilterRecords: Called with GroupedRecords.Count={GroupedRecords.Count}");
            
            // Ensure we have data to filter
            if (GroupedRecords.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("FilterRecords: GroupedRecords is empty, setting FilteredGroups to empty list");
                FilteredGroups = new();
                StateHasChanged();
                return;
            }

            // 1) Start with all data
            var query = GroupedRecords.AsEnumerable();
            System.Diagnostics.Debug.WriteLine($"FilterRecords: Starting with {GroupedRecords.Count} records");

            // 2) Apply filters in order
            if (SelectedStatus != "All")
            {
                System.Diagnostics.Debug.WriteLine($"Applying Status filter (SelectedStatus='{SelectedStatus}')");
                if (SelectedStatus == "Incomplete")
                {
                    query = query.Where(g => (g.Status == "New" || g.Status == "In Progress"));
                }
                else
                {
                    query = query.Where(g => g.Status == SelectedStatus);
                }
            }

            if (SelectedPM != "All")
            {
                System.Diagnostics.Debug.WriteLine($"Applying PM filter (SelectedPM='{SelectedPM}')");
                query = query.Where(g => g.AllRecords.Any(r => r.PrimaryPMName == SelectedPM));
            }

            if (!string.IsNullOrWhiteSpace(SelectedPartNum))
            {
                System.Diagnostics.Debug.WriteLine($"Applying PartNum filter (SelectedPartNum='{SelectedPartNum}')");
                query = query.Where(g => g.AllRecords.Any(r => r.ECNParts.Any(p => p.PartNum.Contains(SelectedPartNum, StringComparison.OrdinalIgnoreCase))));
            }

            if (!string.IsNullOrWhiteSpace(SelectedECNNumber))
            {
                System.Diagnostics.Debug.WriteLine($"Applying ECNNumber filter (SelectedECNNumber='{SelectedECNNumber}')");
                query = query.Where(g => g.ECNNumber.Contains(SelectedECNNumber, StringComparison.OrdinalIgnoreCase));
            }

            FilteredGroups = query.ToList();

            // 3) Debug logging
            System.Diagnostics.Debug.WriteLine($"FilterRecords FINAL: GroupedRecords={GroupedRecords.Count}, FilteredGroups={FilteredGroups.Count}");
            System.Diagnostics.Debug.WriteLine($"Filters: Status='{SelectedStatus}', PM='{SelectedPM}', PartNum='{SelectedPartNum}', ECNNumber='{SelectedECNNumber}'");
            
            // 4) Update the UI
            StateHasChanged();
            System.Diagnostics.Debug.WriteLine("FilterRecords: StateHasChanged() called");

            // 5) Update the grid pagination
            if (ECNMatchedGroupedRecordGrid != null)
            {
                ECNMatchedGroupedRecordGrid.CurrentPage = CurrentPage;
                ECNMatchedGroupedRecordGrid.Refresh();
                System.Diagnostics.Debug.WriteLine("FilterRecords: ECNMatchedGroupedRecordGrid.Refresh() called");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FilterRecords: ECNMatchedGroupedRecordGrid is null");
            }

            // 6) Update the query params in the browser URL (only if this isn't from a back navigation)
            var currentUri = NavigationManager.Uri;
            if (currentUri == NewParameterUri || string.IsNullOrEmpty(NewParameterUri))
            {
                UpdateAllQueryParameters();
                System.Diagnostics.Debug.WriteLine("FilterRecords: UpdateAllQueryParameters() called");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FilterRecords: Skipping UpdateAllQueryParameters() due to back navigation");
            }
        }

        /// <summary>
        /// Reads the current query string values from the URL and populates
        /// filter properties if they exist.
        /// </summary>
        private void GetQueryParams()
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);

            if (query.TryGetValue("status", out var statusValue))
            {
                var status = statusValue.ToString();
                if (!string.IsNullOrWhiteSpace(status) &&
                    (status.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                     status.Equals("New", StringComparison.OrdinalIgnoreCase) ||
                     status.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
                     status.Equals("Incomplete", StringComparison.OrdinalIgnoreCase) ||
                     status.Equals("Completed", StringComparison.OrdinalIgnoreCase)))
                {
                    _selectedStatus = status;
                }
            }
            else
            {
                _selectedStatus = "Incomplete"; // Default value
            }

            if (query.TryGetValue("pm", out var pmValue))
            {
                var pm = pmValue.ToString();
                if (!string.IsNullOrWhiteSpace(pm))
                {
                    _selectedPM = pm;
                }
            }
            else
            {
                _selectedPM = "All"; // Default value
            }

            if (query.TryGetValue("partNum", out var partNumValue))
                _selectedPartNum = partNumValue.ToString() ?? string.Empty;
            else
                _selectedPartNum = string.Empty;

            if (query.TryGetValue("ecn", out var ecnValue))
                _selectedECNNumber = ecnValue.ToString() ?? string.Empty;
            else
                _selectedECNNumber = string.Empty;

            if (query.TryGetValue("page", out var pageValue))
                _currentPage = int.TryParse(pageValue, out var parsedPage) && parsedPage > 0 ? parsedPage : 1;
            else
                _currentPage = 1;

            // Debug logging
            System.Diagnostics.Debug.WriteLine($"GetQueryParams: status='{_selectedStatus}', pm='{_selectedPM}', partNum='{_selectedPartNum}', ecn='{_selectedECNNumber}', page={_currentPage}");

            // Record the current URI
            NewParameterUri = NavigationManager.Uri;
        }

        /// <summary>
        /// Builds a new query string from the current filter values
        /// and navigates without reloading the page (replace: true).
        /// </summary>
        private void UpdateAllQueryParameters()
        {
            var queryParams = new Dictionary<string, string?>();

            // Status Filter - only add if not default
            if (SelectedStatus != "Incomplete")
            {
                queryParams["status"] = SelectedStatus;
            }

            // PM Filter - only add if not default
            if (SelectedPM != "All")
            {
                queryParams["pm"] = SelectedPM;
            }

            // Part Number Filter
            if (!string.IsNullOrWhiteSpace(SelectedPartNum))
            {
                queryParams["partNum"] = SelectedPartNum;
            }

            // ECN Number Filter
            if (!string.IsNullOrWhiteSpace(SelectedECNNumber))
            {
                queryParams["ecn"] = SelectedECNNumber;
            }

            // Page
            if (CurrentPage > 1)
            {
                queryParams["page"] = CurrentPage.ToString();
            }

            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var baseUrl = uri.GetLeftPart(UriPartial.Path);
            var newUri = QueryHelpers.AddQueryString(baseUrl, queryParams);

            if (newUri != NewParameterUri)
            {
                NewParameterUri = newUri;
                NavigationManager.NavigateTo(newUri, forceLoad: false, replace: true);
            }
        }

        // ------------------------------------------------------
        // Public methods for external filter updates

        protected void OnPageChanged(DataGridPageChangedEventArgs args)
        {
            CurrentPage = args.Page;
            // FilterRecords() will be called automatically via the property setter
        }

        // ------------------------------------------------------
        // Helper methods

        private string GetGroupStatus(IGrouping<string, CMHub_CMNotifECNMatchedRecordModel> group)
        {
            if (group.Any(r => r.QuickStatus == "In Progress"))
                return "In Progress";
            if (group.All(r => r.QuickStatus == "Completed"))
                return "Completed";
            return "New";
        }

        protected void OnRowStyling(CMHub_CMNotifECNGroupedRecordModel groupedRecord, DataGridRowStyling styling)
        {
            if (groupedRecord.IsDetailExpanded)
            {
                styling.Class = "active-row";
            }
        }

        protected void ToggleDetailRow(DataGridRowMouseEventArgs<CMHub_CMNotifECNGroupedRecordModel> args)
        {
            if (_ignoreRowClickForECNNumber != args.Item.ECNNumber && GroupedRecords != null)
            {
                args.Item.IsDetailExpanded = !args.Item.IsDetailExpanded;

                // Close other expanded rows (only one open at a time)
                foreach (var record in GroupedRecords.Where(e => e != args.Item && e.IsDetailExpanded))
                {
                    ECNMatchedGroupedRecordGrid?.ToggleDetailRow(record);
                    record.IsDetailExpanded = false;
                }
            }

            _ignoreRowClickForECNNumber = "";
        }

        private void ResetAllSelectedRows()
        {
            if (ECNMatchedGroupedRecordGrid is null)
                return;

            foreach (var groupedRecord in GroupedRecords)
            {
                if (groupedRecord.IsDetailExpanded)
                {
                    ECNMatchedGroupedRecordGrid.ToggleDetailRow(groupedRecord);
                    groupedRecord.IsDetailExpanded = false;
                }
            }
        }

        protected void NavigateToEditNotifRecord(CMHub_CMNotifECNGroupedRecordModel groupedRecordModel)
        {
            _ignoreRowClickForECNNumber = groupedRecordModel.ECNNumber;
            ResetAllSelectedRows();
            NavigationManager.NavigateTo($"{NavHelpers.CMHub_CMNotifRecordDetails}/{groupedRecordModel.ECNNumber}", forceLoad: false, replace: false);
        }

        protected IEnumerable<CMHub_CMNotifECNGroupedRecordModel> GetFilteredGroups()
        {
            return FilteredGroups;
        }
    }
}