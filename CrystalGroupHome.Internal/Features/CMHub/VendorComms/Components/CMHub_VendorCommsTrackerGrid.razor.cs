using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Features.CMHub.VendorComms.Data;
using CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using CrystalGroupHome.Internal.Common.Data.Labor;

namespace CrystalGroupHome.Internal.Features.CMHub.VendorComms.Components
{
    public class CMHub_VendorCommsTrackerGridBase : ComponentBase, IDisposable
    {
        [Inject] public ICMHub_VendorCommsService VendorCommsService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public ICMHub_VendorCommsSurveyService SurveyService { get; set; } = default!;

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }
        [CascadingParameter] public Pages.CMHub_VendorCommsBase? ParentPage { get; set; }

        private List<CMHub_VendorCommsTrackerModel> AllTrackers = new();
        protected List<CMHub_VendorCommsTrackerModel> FilteredTrackers = new();
        protected DataGrid<CMHub_VendorCommsTrackerModel>? TrackerGrid;
        protected bool IsLoading = false;
        protected CMHub_VendorCommsSurveyDialog? surveyDialog;

        // Selection management - persists across pagination
        protected HashSet<string> SelectedTrackerKeys = new(); // Use PartNum as unique key
        protected List<CMHub_VendorCommsTrackerModel> SelectedTrackers = new();

        // We toggle detail rows, so keep track if we should ignore a row click
        private string _ignoreRowClickForPartNum = "";

        // ------------------------------------------------------
        // Filter backing fields
        private int _currentPage = 1;
        private string? _vendorFilter;
        private bool? _hideExcludedFilter;
        private bool _awaitingResponseFilter;
        private bool _awaitingProcessingFilter;

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
        public string? VendorFilter
        {
            get => _vendorFilter;
            set
            {
                if (_vendorFilter != value)
                {
                    _vendorFilter = value;
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public bool HideExcludedFilter
        {
            get => _hideExcludedFilter ?? true; // Default to true if not set in query
            set
            {
                var newValue = value ? (bool?)null : false; // null means true (default), false means false
                if (_hideExcludedFilter != newValue)
                {
                    _hideExcludedFilter = newValue;
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public bool AwaitingResponseFilter
        {
            get => _awaitingResponseFilter;
            set
            {
                if (_awaitingResponseFilter != value)
                {
                    _awaitingResponseFilter = value;
                    
                    // Make filters mutually exclusive - clear awaiting processing filter
                    if (value && _awaitingProcessingFilter)
                    {
                        _awaitingProcessingFilter = false;
                    }
                    
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public bool AwaitingProcessingFilter
        {
            get => _awaitingProcessingFilter;
            set
            {
                if (_awaitingProcessingFilter != value)
                {
                    _awaitingProcessingFilter = value;
                    
                    // Make filters mutually exclusive - clear awaiting response filter
                    if (value && _awaitingResponseFilter)
                    {
                        _awaitingResponseFilter = false;
                    }
                    
                    if (_hasInitialFilterApplied)
                    {
                        _currentPage = 1; // Reset to first page when filter changes
                    }
                    ApplyFiltersIfReady();
                }
            }
        }

        // ------------------------------------------------------
        // Lifecycle

        protected override async Task OnInitializedAsync()
        {
            // 1) Parse the query params to pre-populate filter fields
            GetQueryParams();

            // 2) Load the data (hide the grid until we're done)
            IsLoading = true;
            try
            {
                AllTrackers = await VendorCommsService.GetTrackersAsync();

                // Subscribe to navigation events to clear selections when leaving page
                NavigationManager.LocationChanged += OnLocationChanged;

                // If the initial filter has already been applied (but with no data), reapply it now with data
                if (_hasInitialFilterApplied)
                {
                    FilterTrackers();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && !_hasInitialFilterApplied)
            {
                // After we're truly interactive, apply the filters once
                _hasInitialFilterApplied = true;
                FilterTrackers();
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        protected override async Task OnParametersSetAsync()
        {
            // Handle navigation back - re-parse query params if URI has changed
            var currentUri = NavigationManager.Uri;
            if (currentUri != NewParameterUri && _hasInitialFilterApplied && AllTrackers.Count > 0)
            {
                GetQueryParams();
                FilterTrackers();
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
            if (_hasInitialFilterApplied && AllTrackers.Count > 0)
            {
                FilterTrackers();
            }
        }

        /// <summary>
        /// Actually filters the in-memory data, updates the grid, and updates the URL.
        /// </summary>
        private void FilterTrackers()
        {
            // Ensure we have data to filter
            if (AllTrackers.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("FilterTrackers: AllTrackers is empty, setting FilteredTrackers to empty list");
                FilteredTrackers = new();
                StateHasChanged();
                return;
            }

            // 1) Start with all data
            FilteredTrackers = new List<CMHub_VendorCommsTrackerModel>(AllTrackers);

            // Debug: Check ExcludeVendorComms values
            var excludedCount = AllTrackers.Count(t => t.PartEolt.ExcludeVendorComms);
            var notExcludedCount = AllTrackers.Count(t => !t.PartEolt.ExcludeVendorComms);

            // 2) Apply filters in order
            if (HideExcludedFilter)
            {
                FilteredTrackers = FilteredTrackers.Where(t => !t.PartEolt.ExcludeVendorComms).ToList();
            }

            if (AwaitingResponseFilter)
            {
                FilteredTrackers = FilteredTrackers.Where(t => t.IsAwaitingResponse).ToList();
            }

            if (AwaitingProcessingFilter)
            {
                FilteredTrackers = FilteredTrackers.Where(t => t.HasUnprocessedResponse).ToList();
            }

            if (!string.IsNullOrWhiteSpace(VendorFilter))
            {
                FilteredTrackers = FilteredTrackers.Where(t => t.VendorName != null && t.VendorName.Contains(VendorFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            // 3) Update the UI
            StateHasChanged();

            // 4) Update the grid pagination
            if (TrackerGrid != null)
            {
                TrackerGrid.CurrentPage = CurrentPage;
                TrackerGrid.Refresh();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FilterTrackers: TrackerGrid is null");
            }

            // 5) Update the query params in the browser URL (only if this isn't from a back navigation)
            var currentUri = NavigationManager.Uri;
            if (currentUri == NewParameterUri || string.IsNullOrEmpty(NewParameterUri))
            {
                UpdateAllQueryParameters();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FilterTrackers: Skipping UpdateAllQueryParameters() due to back navigation");
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

            if (query.TryGetValue("vendor", out var vendorValue))
                _vendorFilter = vendorValue.ToString();
            else
                _vendorFilter = null;

            if (query.TryGetValue("awaitingResponse", out var awaitingResponseValue))
                _awaitingResponseFilter = bool.TryParse(awaitingResponseValue, out var parsedAwaitingResponse) && parsedAwaitingResponse;
            else
                _awaitingResponseFilter = false;

            if (query.TryGetValue("awaitingProcessing", out var awaitingProcessingValue))
                _awaitingProcessingFilter = bool.TryParse(awaitingProcessingValue, out var parsedAwaitingProcessing) && parsedAwaitingProcessing;
            else
                _awaitingProcessingFilter = false;

            // Ensure filters are mutually exclusive after loading from query params
            if (_awaitingResponseFilter && _awaitingProcessingFilter)
            {
                // Prioritize the most recently set one, but since we can't determine that from query params,
                // default to keeping awaiting response and clearing awaiting processing
                _awaitingProcessingFilter = false;
            }

            if (query.TryGetValue("hideExcluded", out var hideExcludedValue))
                _hideExcludedFilter = bool.TryParse(hideExcludedValue, out var parsedHideExcluded) ? parsedHideExcluded : null;
            else
                _hideExcludedFilter = null; // Default to true

            if (query.TryGetValue("page", out var pageValue))
                _currentPage = int.TryParse(pageValue, out var parsedPage) && parsedPage > 0 ? parsedPage : 1;
            else
                _currentPage = 1;

            // Debug logging
            System.Diagnostics.Debug.WriteLine($"GetQueryParams: hideExcluded={_hideExcludedFilter}, awaitingResponse={_awaitingResponseFilter}, awaitingProcessing={_awaitingProcessingFilter}, vendor='{_vendorFilter}'");

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

            // Vendor Filter
            if (!string.IsNullOrWhiteSpace(VendorFilter))
            {
                queryParams["vendor"] = VendorFilter;
            }

            // Awaiting Response Filter
            if (AwaitingResponseFilter)
            {
                queryParams["awaitingResponse"] = "true";
            }

            // Awaiting Processing Filter
            if (AwaitingProcessingFilter)
            {
                queryParams["awaitingProcessing"] = "true";
            }

            // Hide Excluded Filter - null means true (default), false means explicitly false
            if (!HideExcludedFilter)
            {
                queryParams["hideExcluded"] = "false";
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
            // FilterTrackers() will be called automatically via the property setter
        }

        protected void FilterByVendor(string? vendorName)
        {
            // If the selected vendor is already the filter, clear the filter.
            VendorFilter = string.Equals(VendorFilter, vendorName, StringComparison.OrdinalIgnoreCase) ? null : vendorName;
            // FilterTrackers() will be called automatically via the property setter
        }

        // ------------------------------------------------------
        // Survey creation methods

        protected bool IsPreparingSurvey { get; set; }
        protected string PreparingSurveyMessage { get; set; } = "Preparing survey...";

        protected async Task OpenSurveyDialog()
        {
            try
            {
                var vendorGroups = SelectedTrackers
                    .Where(t => t.Vendor != null)
                    .GroupBy(t => t.Vendor!.VendorNum)
                    .ToList();
                var vendorGroupsContainsBypassVendor = vendorGroups.Count == 1 && vendorGroups.Any(g => g.Key == SurveyService.GetAllowedVendorExceptionNum());

                // Check if survey sending is enabled
                if (!SurveyService.IsSurveySendingEnabled(vendorGroupsContainsBypassVendor ? SurveyService.GetAllowedVendorExceptionNum() : null))
                {
                    return; // Silently ignore if disabled (button should be hidden anyway)
                }

                // Defensive check - ensure we have selections
                if (SelectedTrackers == null || SelectedTrackers.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("OpenSurveyDialog: No trackers selected!");
                    return;
                }

                // Store the current selections to restore them if needed
                var currentSelectionKeys = new HashSet<string>(SelectedTrackerKeys);
                var currentSelections = new List<CMHub_VendorCommsTrackerModel>(SelectedTrackers);

                IsPreparingSurvey = true;
                PreparingSurveyMessage = "Creating trackers...";
                StateHasChanged();

                // Create any new trackers before opening the dialog
                var trackersToCreate = SelectedTrackers.Where(t => t.IsNew).ToList();

                foreach (var selectedTracker in trackersToCreate)
                {
                    var newId = await VendorCommsService.CreateOrUpdateTrackerAsync(selectedTracker);
                    selectedTracker.Tracker.Id = newId;
                    selectedTracker.IsNew = false;

                    // Update the corresponding tracker in AllTrackers and FilteredTrackers
                    var allTracker = AllTrackers.FirstOrDefault(t => t.Tracker.PartNum == selectedTracker.Tracker.PartNum);
                    if (allTracker != null)
                    {
                        allTracker.Tracker.Id = newId;
                        allTracker.IsNew = false;
                    }

                    var filteredTracker = FilteredTrackers.FirstOrDefault(t => t.Tracker.PartNum == selectedTracker.Tracker.PartNum);
                    if (filteredTracker != null)
                    {
                        filteredTracker.Tracker.Id = newId;
                        filteredTracker.IsNew = false;
                    }
                }

                PreparingSurveyMessage = "Opening survey dialog...";
                StateHasChanged();

                // Ensure selections are still intact
                if (SelectedTrackers.Count != currentSelections.Count)
                {
                    SelectedTrackerKeys = currentSelectionKeys;
                    SelectedTrackers = currentSelections;
                }

                if (surveyDialog != null)
                {
                    // Make a defensive copy of the selected trackers to pass to the dialog
                    var trackersForSurvey = SelectedTrackers.ToList();
                    await surveyDialog.OpenAsync(trackersForSurvey);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("OpenSurveyDialog: surveyDialog is null!");
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                IsPreparingSurvey = false;

                // Force a re-render to ensure UI is in sync
                await InvokeAsync(StateHasChanged);
            }
        }

        protected async Task OnSurveysCreated(List<int> batchIds)
        {
            // Clear selections after successful survey creation
            ClearAllSelections();
            
            // Reload the trackers to reflect updated statuses
            IsLoading = true;
            try
            {
                AllTrackers = await VendorCommsService.GetTrackersAsync();
                FilterTrackers();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ------------------------------------------------------
        // Event handlers

        private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            // Check if we're navigating away from the current page
            var currentPath = new Uri(NavigationManager.Uri).AbsolutePath;
            if (!currentPath.Contains(NavHelpers.CMHub_VendorCommsMainPage))
            {
                ClearAllSelections();
            }
        }

        // ------------------------------------------------------
        // Status and styling methods (unchanged)

        protected string GetStatusColor(CMHub_VendorCommsTrackerModel tracker)
        {
            // Check if awaiting processing first (highest priority for a distinct color)
            if (tracker.HasUnprocessedResponse)
            {
                return "var(--status-blue)"; // Use blue for awaiting processing
            }
            // Check if awaiting response second (high priority for purple)
            else if (tracker.IsAwaitingResponse)
            {
                return "var(--status-purple)";
            }
            else if (tracker.NextContactDate.HasValue)
            {
                var today = DateTime.Today;
                var daysDifference = (tracker.NextContactDate.Value.Date - today).Days;

                if (daysDifference < 0)
                {
                    return "var(--status-dark-red)";
                }
                else if (daysDifference <= 15)
                {
                    return "var(--status-light-red)";
                }
                else if (daysDifference <= 30)
                {
                    return "var(--status-super-light-red)";
                }
            }

            if (tracker.IsNew || tracker.PartEolt.LastContactDate == null)
            {
                return "var(--status-light-yellow)";
            }

            return "transparent"; // No special styling
        }

        protected string GetStatusTooltip(CMHub_VendorCommsTrackerModel tracker)
        {
            // Check if awaiting processing first (highest priority)
            if (tracker.HasUnprocessedResponse)
            {
                if (tracker.LatestSurveyResponseDate.HasValue)
                {
                    var daysSinceResponse = (DateTime.Today - tracker.LatestSurveyResponseDate.Value.Date).Days;
                    return $"Response received and awaiting processing ({daysSinceResponse} day{(daysSinceResponse == 1 ? "" : "s")} ago)";
                }
                else
                {
                    return "Response received and awaiting processing";
                }
            }

            // Check if awaiting response second (high priority)
            if (tracker.IsAwaitingResponse)
            {
                if (tracker.PartEolt.LastContactDate.HasValue)
                {
                    var daysSinceContact = (DateTime.Today - tracker.PartEolt.LastContactDate.Value.Date).Days;
                    return $"Awaiting response from vendor ({daysSinceContact} day{(daysSinceContact == 1 ? "" : "s")} since contacted)";
                }
                else
                {
                    return "Awaiting response from vendor";
                }
            }

            if (tracker.NextContactDate.HasValue)
            {
                var today = DateTime.Today;
                var daysDifference = (tracker.NextContactDate.Value.Date - today).Days;

                if (daysDifference < 0)
                {
                    var overdueDays = Math.Abs(daysDifference);
                    return $"Overdue by {overdueDays} day{(overdueDays == 1 ? "" : "s")}";
                }
                else if (daysDifference <= 15)
                {
                    return $"Due in {daysDifference} day{(daysDifference == 1 ? "" : "s")}";
                }
                else if (daysDifference <= 30)
                {
                    return $"Due in {daysDifference} day{(daysDifference == 1 ? "" : "s")}";
                }
            }

            if (tracker.IsNew || tracker.PartEolt.LastContactDate == null)
            {
                return "New part with no tracking data";
            }

            return "No special status";
        }

        protected void OnRowStyling(CMHub_VendorCommsTrackerModel tracker, DataGridRowStyling styling)
        {
            var classes = new List<string>();

            if (tracker.IsSelected)
            {
                classes.Add("selected-row");
            }
            else if (IsTrackerSelected(tracker))
            {
                classes.Add("multi-selected-row");
            }

            styling.Class = string.Join(" ", classes);
        }

        protected void SelectRow(DataGridRowMouseEventArgs<CMHub_VendorCommsTrackerModel> args)
        {
            if (_ignoreRowClickForPartNum != args.Item.Tracker.PartNum && FilteredTrackers != null)
            {
                args.Item.IsSelected = !args.Item.IsSelected;

                // Only select one row at a time for the detail view
                foreach (var tracker in FilteredTrackers.Where(e => e != args.Item && e.IsSelected))
                {
                    tracker.IsSelected = false;
                }
            }

            _ignoreRowClickForPartNum = "";
        }

        // Multi-selection methods
        protected bool IsTrackerSelected(CMHub_VendorCommsTrackerModel tracker)
        {
            return SelectedTrackerKeys.Contains(tracker.Tracker.PartNum);
        }

        // Helper method to determine if a tracker can be selected (NextContactDate <= 180 days from today OR no LastContactDate)
        protected bool CanTrackerBeSelected(CMHub_VendorCommsTrackerModel tracker)
        {
            // If the tracker is already selected, allow it to remain selectable
            // This prevents the selection from being lost when tracker state changes
            if (IsTrackerSelected(tracker))
            {
                return true;
            }

            // If EOL and LTB dates are already known, no need to contact
            if (!tracker.IsContactNecessary)
            {
                return false;
            }

            var today = DateTime.Today;

            // If tracker is awaiting response, only allow selection if it's also overdue
            if (tracker.IsAwaitingResponse)
            {
                if (!tracker.NextContactDate.HasValue)
                {
                    return false; // Awaiting response but no next contact date - not selectable
                }

                return tracker.NextContactDate.Value.Date < today; // Only selectable if overdue
            }

            // Allow selection of trackers that have never been contacted (no LastContactDate)
            // These trackers need to be contacted
            if (tracker.PartEolt.LastContactDate == null)
            {
                return true;
            }

            // For trackers with contact history, check if NextContactDate is within 30 days
            if (!tracker.NextContactDate.HasValue)
            {
                return false; // Items without NextContactDate can't be selected
            }

            var targetDate = today.AddDays(30);
            return tracker.NextContactDate.Value.Date <= targetDate;
        }

        protected void OnTrackerSelectionChanged(CMHub_VendorCommsTrackerModel tracker, bool isSelected)
        {
            // Don't process selection changes while preparing survey
            if (IsPreparingSurvey)
            {
                return;
            }

            // For deselection, always allow it
            if (!isSelected)
            {
                SelectedTrackerKeys.Remove(tracker.Tracker.PartNum);
                SelectedTrackers.RemoveAll(t => t.Tracker.PartNum == tracker.Tracker.PartNum);
                StateHasChanged();
                return;
            }

            // For selection, check if it can be selected
            if (!CanTrackerBeSelected(tracker))
            {
                return;
            }

            if (!SelectedTrackerKeys.Contains(tracker.Tracker.PartNum))
            {
                SelectedTrackerKeys.Add(tracker.Tracker.PartNum);
                SelectedTrackers.Add(new CMHub_VendorCommsTrackerModel
                {
                    Tracker = new CMHub_VendorCommsTrackerDTO
                    {
                        PartNum = tracker.Tracker.PartNum,
                        VendorNum = tracker.Tracker.VendorNum,
                        Id = tracker.Tracker.Id
                    },
                    Vendor = tracker.Vendor,
                    PartEolt = tracker.PartEolt,
                    IsNew = tracker.IsNew
                });
            }

            StateHasChanged();
        }

        // Cumulative count methods - each category includes all items with dates less than or equal to the threshold
        protected int GetOverdueCount()
        {
            var today = DateTime.Today;
            return FilteredTrackers.Count(t => t.NextContactDate.HasValue && t.NextContactDate.Value.Date < today);
        }

        protected int GetWithin15DaysCount()
        {
            var today = DateTime.Today;
            var targetDate = today.AddDays(15);
            return FilteredTrackers.Count(t => t.NextContactDate.HasValue &&
                                              t.NextContactDate.Value.Date <= targetDate &&
                                              !t.IsAwaitingResponse);
        }

        protected int GetWithin30DaysCount()
        {
            var today = DateTime.Today;
            var targetDate = today.AddDays(30);
            return FilteredTrackers.Count(t => t.NextContactDate.HasValue &&
                                              t.NextContactDate.Value.Date <= targetDate &&
                                              !t.IsAwaitingResponse);
        }

        // New method to count trackers awaiting response
        protected int GetAwaitingResponseCount()
        {
            return FilteredTrackers.Count(t => t.IsAwaitingResponse && CanTrackerBeSelected(t));
        }

        // New method to count trackers awaiting processing
        protected int GetAwaitingProcessingCount()
        {
            return FilteredTrackers.Count(t => t.HasUnprocessedResponse);
        }

        // Cumulative selection methods - each method selects all items with dates less than or equal to the threshold
        protected void SelectAllOverdue()
        {
            ClearAllSelections();

            var today = DateTime.Today;
            var overdueTrackers = FilteredTrackers.Where(t => t.NextContactDate.HasValue && t.NextContactDate.Value.Date < today);

            foreach (var tracker in overdueTrackers)
            {
                OnTrackerSelectionChanged(tracker, true);
            }
        }

        protected void SelectAllWithin15Days()
        {
            ClearAllSelections();

            var today = DateTime.Today;
            var targetDate = today.AddDays(15);
            var withinRangeTrackers = FilteredTrackers.Where(t => t.NextContactDate.HasValue &&
                                                              t.NextContactDate.Value.Date <= targetDate &&
                                                              !t.IsAwaitingResponse);

            foreach (var tracker in withinRangeTrackers)
            {
                OnTrackerSelectionChanged(tracker, true);
            }
        }

        protected void SelectAllWithin30Days()
        {
            ClearAllSelections();

            var today = DateTime.Today;
            var targetDate = today.AddDays(30);
            var withinRangeTrackers = FilteredTrackers.Where(t => t.NextContactDate.HasValue &&
                                                               t.NextContactDate.Value.Date <= targetDate &&
                                                               !t.IsAwaitingResponse);

            foreach (var tracker in withinRangeTrackers)
            {
                OnTrackerSelectionChanged(tracker, true);
            }
        }

        protected void SelectAllAwaitingProcessing()
        {
            ClearAllSelections();

            var awaitingProcessingTrackers = FilteredTrackers.Where(t => t.HasUnprocessedResponse);

            foreach (var tracker in awaitingProcessingTrackers)
            {
                OnTrackerSelectionChanged(tracker, true);
            }
        }

        protected void ClearAllSelections()
        {
            SelectedTrackerKeys.Clear();
            SelectedTrackers.Clear();
            StateHasChanged();
        }

        protected void RemoveSelection(CMHub_VendorCommsTrackerModel tracker)
        {
            SelectedTrackerKeys.Remove(tracker.Tracker.PartNum);
            SelectedTrackers.RemoveAll(t => t.Tracker.PartNum == tracker.Tracker.PartNum);
            StateHasChanged();
        }

        protected void RemoveVendorSelection(string vendorName)
        {
            var trackersToRemove = SelectedTrackers.Where(t =>
                string.Equals(t.VendorName, vendorName, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var tracker in trackersToRemove)
            {
                SelectedTrackerKeys.Remove(tracker.Tracker.PartNum);
                SelectedTrackers.Remove(tracker);
            }

            StateHasChanged();
        }

        private void ResetAllSelectedRows()
        {
            if (FilteredTrackers is null || TrackerGrid is null)
                return;

            foreach (var tracker in FilteredTrackers)
            {
                if (tracker.IsSelected)
                {
                    TrackerGrid.ToggleDetailRow(tracker);
                    tracker.IsSelected = false;
                }
            }
        }

        protected async Task NavigateToEditTracker(CMHub_VendorCommsTrackerModel trackerModel)
        {
            // Set the flag to prevent the row click from firing
            _ignoreRowClickForPartNum = trackerModel.Tracker.PartNum;

            if (trackerModel.IsNew)
            {
                // If the tracker is new, create it first
                var newId = await VendorCommsService.CreateOrUpdateTrackerAsync(trackerModel);
                trackerModel.Tracker.Id = newId;
                trackerModel.IsNew = false; // No longer new

                // Update the corresponding tracker in AllTrackers and FilteredTrackers
                var allTracker = AllTrackers.FirstOrDefault(t => t.Tracker.PartNum == trackerModel.Tracker.PartNum);
                if (allTracker != null)
                {
                    allTracker.Tracker.Id = newId;
                    allTracker.IsNew = false;
                }

                var filteredTracker = FilteredTrackers.FirstOrDefault(t => t.Tracker.PartNum == trackerModel.Tracker.PartNum);
                if (filteredTracker != null)
                {
                    filteredTracker.Tracker.Id = newId;
                    filteredTracker.IsNew = false;
                }
            }

            ResetAllSelectedRows();

            // Use the parent page's navigation method to preserve state
            if (ParentPage != null)
            {
                ParentPage.NavigateToTrackerDetails(trackerModel.Tracker.PartNum);
            }
            else
            {
                // Fallback to direct navigation
                NavigationManager.NavigateTo($"{NavHelpers.CMHub_VendorCommsTrackerDetails}/{trackerModel.Tracker.PartNum}", forceLoad: false, replace: false);
            }
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
        }
    }
}