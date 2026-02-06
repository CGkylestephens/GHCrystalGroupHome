using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Components
{
    public class CMHub_CMDexPartGridBase : ComponentBase, IDisposable
    {
        [Inject] public IPartService PartService { get; set; } = default!;
        [Inject] public ICMHub_CMDexService CMDexService { get; set; } = default!;
        [Inject] public ICustomerService CustomerService { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public ProtectedSessionStorage SessionStorage { get; set; } = default!;
        [Inject] public IJSRuntime JsRuntime { get; set; } = default!;

        protected List<ADUserDTO_Base> PrimaryPMsForFilter { get; set; } = [];
        protected DataGrid<CMHub_CMDexPartModel>? PartGrid { get; set; }

        // ------------------------------------------------------
        // Filter backing fields
        private string _selectedPrimaryPm = "all";
        private bool _includeInactiveParts = false;
        private bool _showMissingDataParts = false;
        private bool _showCMManagedParts = false;

        // ------------------------------------------------------
        // Lifecycle flags
        private bool _hasInitialFilterApplied = false;

        // We keep track of the current full URI with query strings
        public string NewParameterUri { get; private set; } = string.Empty;

        // ------------------------------------------------------
        // Filter properties with on-change triggers
        [Parameter]
        public string SelectedPrimaryPm
        {
            get => _selectedPrimaryPm;
            set
            {
                if (_selectedPrimaryPm != value)
                {
                    _selectedPrimaryPm = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public bool IncludeInactiveParts
        {
            get => _includeInactiveParts;
            set
            {
                if (_includeInactiveParts != value)
                {
                    _includeInactiveParts = value;
                    if (_hasInitialFilterApplied)
                    {
                        ReloadDataAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        [Parameter]
        public bool ShowMissingDataParts
        {
            get => _showMissingDataParts;
            set
            {
                if (_showMissingDataParts != value)
                {
                    _showMissingDataParts = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public bool ShowCMManagedParts
        {
            get => _showCMManagedParts;
            set
            {
                if (_showCMManagedParts != value)
                {
                    _showCMManagedParts = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        // We toggle detail rows, so keep track if we should ignore a row click
        private string _ignoreRowClickForPartNum = "";

        public bool IsLoading;
        protected List<CMHub_CMDexPartModel> Parts = new();

        // ------------------------------------------------------
        // Lifecycle

        protected override async Task OnInitializedAsync()
        {
            System.Diagnostics.Debug.WriteLine("OnInitializedAsync: Starting");
            
            // 1) Parse the query params to pre-populate filter fields
            GetQueryParams();

            // 2) Subscribe to navigation events
            NavigationManager.LocationChanged += OnLocationChanged;

            // 3) Load the data (hide the grid until we're done)
            IsLoading = true;
            try
            {
                await LoadData();
                System.Diagnostics.Debug.WriteLine($"OnInitializedAsync: Loaded {Parts.Count} parts");

                // If the initial filter has already been applied (but with no data), reapply it now with data
                if (_hasInitialFilterApplied)
                {
                    System.Diagnostics.Debug.WriteLine("OnInitializedAsync: Reapplying filters after data load");
                    FilterParts();
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
                System.Diagnostics.Debug.WriteLine("OnAfterRenderAsync: About to call FilterParts()");
                FilterParts();
                System.Diagnostics.Debug.WriteLine("OnAfterRenderAsync: FilterParts() completed");
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        protected override async Task OnParametersSetAsync()
        {
            // Handle navigation back - re-parse query params if URI has changed
            var currentUri = NavigationManager.Uri;
            if (currentUri != NewParameterUri && _hasInitialFilterApplied && Parts.Count > 0)
            {
                GetQueryParams();
                FilterParts();
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
            if (_hasInitialFilterApplied && Parts.Count > 0)
            {
                FilterParts();
            }
        }

        /// <summary>
        /// Actually filters the in-memory data, updates the grid, and updates the URL.
        /// </summary>
        private void FilterParts()
        {
            System.Diagnostics.Debug.WriteLine($"FilterParts: Called with Parts.Count={Parts.Count}");
            
            // Ensure we have data to filter
            if (Parts.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("FilterParts: Parts is empty");
                StateHasChanged();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"FilterParts: Starting with {Parts.Count} records");
            System.Diagnostics.Debug.WriteLine($"Filters: PrimaryPM='{SelectedPrimaryPm}', MissingData={ShowMissingDataParts}, CMManaged={ShowCMManagedParts}");
            
            // Update the UI
            CollapseExpandedRows();
            StateHasChanged();
            System.Diagnostics.Debug.WriteLine("FilterParts: StateHasChanged() called");

            // Update the query params in the browser URL (only if this isn't from a back navigation)
            var currentUri = NavigationManager.Uri;
            if (currentUri == NewParameterUri || string.IsNullOrEmpty(NewParameterUri))
            {
                UpdateAllQueryParameters();
                System.Diagnostics.Debug.WriteLine("FilterParts: UpdateAllQueryParameters() called");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FilterParts: Skipping UpdateAllQueryParameters() due to back navigation");
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

            if (query.TryGetValue("primary-pm", out var primaryPmValue))
            {
                var pm = primaryPmValue.ToString();
                if (!string.IsNullOrWhiteSpace(pm))
                {
                    _selectedPrimaryPm = pm;
                }
            }
            else
            {
                _selectedPrimaryPm = "all"; // Default value
            }

            if (query.TryGetValue("includeInactive", out var includeInactiveValue))
                _includeInactiveParts = bool.TryParse(includeInactiveValue, out var parsedIncludeInactive) && parsedIncludeInactive;
            else
                _includeInactiveParts = false;

            if (query.TryGetValue("showMissingData", out var showMissingDataValue))
                _showMissingDataParts = bool.TryParse(showMissingDataValue, out var parsedShowMissingData) && parsedShowMissingData;
            else
                _showMissingDataParts = false;

            if (query.TryGetValue("showCMManaged", out var showCMManagedValue))
                _showCMManagedParts = bool.TryParse(showCMManagedValue, out var parsedShowCMManaged) && parsedShowCMManaged;
            else
                _showCMManagedParts = false;

            // Debug logging
            System.Diagnostics.Debug.WriteLine($"GetQueryParams: primaryPM='{_selectedPrimaryPm}', includeInactive={_includeInactiveParts}, showMissingData={_showMissingDataParts}, showCMManaged={_showCMManagedParts}");

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

            // Primary PM Filter - only add if not default
            if (SelectedPrimaryPm != "all")
            {
                queryParams["primary-pm"] = SelectedPrimaryPm;
            }

            // Include Inactive Parts Filter
            if (IncludeInactiveParts)
            {
                queryParams["includeInactive"] = "true";
            }

            // Show Missing Data Parts Filter
            if (ShowMissingDataParts)
            {
                queryParams["showMissingData"] = "true";
            }

            // Show CM Managed Parts Filter
            if (ShowCMManagedParts)
            {
                queryParams["showCMManaged"] = "true";
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
        // Data loading

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            // Only perform minimal cleanup if needed
            InvokeAsync(() =>
            {
                StateHasChanged();
            });
        }

        private async Task LoadData()
        {
            Parts = await CMDexService.GetAllCMDexPartsAsync(IncludeInactiveParts);

            // Then we need to get the list of Primary PMS associated with these parts (for use in the filter)
            var primaryPmEmpIds = Parts
                .Select(part => part.PartEmployees.FirstOrDefault(pe => pe.Type == 1 && pe.IsPrimary == true))
                .Where(pe => pe != null)
                .Select(pe => pe?.EmpID ?? "")
                .Distinct();

            if (primaryPmEmpIds == null) return;
            var adUsersForPm = await ADUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(primaryPmEmpIds);

            var adUserLookup = adUsersForPm.ToDictionary(u => u.EmployeeNumber);

            foreach (var part in Parts)
            {
                var primaryPm = part.PartEmployees.FirstOrDefault(pe => pe.Type == 1 && pe.IsPrimary == true);
                if (primaryPm != null && adUserLookup.TryGetValue(primaryPm.EmpID, out var adUser))
                {
                    part.ADEmployees = [adUser];
                }
            }

            PrimaryPMsForFilter = adUsersForPm.ToList();
        }

        private async Task ReloadDataAsync()
        {
            IsLoading = true;
            try
            {
                await LoadData();
            }
            finally
            {
                IsLoading = false;
            }

            CollapseExpandedRows();
            StateHasChanged();
        }

        // ------------------------------------------------------
        // Computed properties and filtering

        protected List<CMHub_CMDexPartModel> FilteredParts
        {
            get
            {
                IEnumerable<CMHub_CMDexPartModel> query = Parts ?? Enumerable.Empty<CMHub_CMDexPartModel>();

                // 1. Apply Primary PM filter
                if (SelectedPrimaryPm != "all")
                {
                    query = query.Where(part =>
                    {
                        // Ensure PartEmployees is not null before checking
                        var primaryPm = part.PartEmployees?.FirstOrDefault(pe => pe.Type == (int)PartEmployeeType.PM && pe.IsPrimary == true);
                        return primaryPm != null && primaryPm.EmpID == SelectedPrimaryPm;
                    });
                }

                // 2. Apply Missing Data filter
                if (ShowMissingDataParts)
                {
                    query = query.Where(part =>
                    {
                        // Check if a Primary PM is missing
                        bool hasPrimaryPM = part.PartEmployees?.Any(pe => pe.Type == (int)PartEmployeeType.PM && pe.IsPrimary == true) ?? false;

                        // Check if Owner CC is missing
                        bool hasOwnerContact = part.PartCustomerContacts?.Any(pcc => pcc.IsOwner == true) ?? false;

                        return !hasPrimaryPM || !hasOwnerContact;
                    });
                }

                // 3. Apply CM Managed filter
                if (ShowCMManagedParts)
                {
                    query = query.Where(part => part.Part.CMManaged_c);
                }

                return query.ToList();
            }
        }

        // ------------------------------------------------------
        // Public methods for external filter updates

        protected async Task OnChangePMFilter(string value)
        {
            SelectedPrimaryPm = value;

            // Store in session storage for fallback (keeping existing behavior)
            await SessionStorage.SetAsync("cmhub-primary-pm", SelectedPrimaryPm);
        }

        protected void OnFilteredDataChanged(DataGridFilteredDataEventArgs<CMHub_CMDexPartModel> args)
        {
            CollapseExpandedRows();
        }

        // ------------------------------------------------------
        // Helper methods

        protected async Task LoadDisplayDataForPartModel(CMHub_CMDexPartModel partModel)
        {
            if (partModel != null && partModel.PartEmployees != null)
            {
                var empIds = partModel.PartEmployees.Select(pe => pe.EmpID).ToList();
                partModel.ADEmployees = await ADUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(empIds);
            }
            if (partModel != null && partModel.PartCustomerContacts != null)
            {
                partModel.PartCustomerContactsForDisplay.Clear();
                foreach (var custCon in partModel.PartCustomerContacts)
                {
                    var custConForDisplay = await CustomerService.GetCustomerContactByCompoundKeyAsync<CustomerContactDTO_Base>(custCon.CustNum, custCon.ConNum, custCon.PerConID);
                    if (custConForDisplay != null)
                    {
                        partModel.PartCustomerContactsForDisplay.Add(custConForDisplay);
                    }
                }
            }
        }

        private void ResetAllDetailRows()
        {
            if (Parts is null || PartGrid is null)
                return;

            foreach (var part in Parts)
            {
                if (part.IsDetailExpanded)
                {
                    PartGrid.ToggleDetailRow(part);
                    part.IsDetailExpanded = false;
                }
            }
        }

        protected async Task OnEditPartRelationClick(MouseEventArgs e, CMHub_CMDexPartModel model)
        {
            var url = $"{NavHelpers.CMHub_CMDexPartDetails}/{model.Part.PartNum}";

            // e.Button == 1 is middle-click, e.CtrlKey covers Ctrl-click (Win/Linux), e.MetaKey covers Cmd-click (Mac)
            if (e.CtrlKey || e.MetaKey)
            {
                // open in new tab
                await JsRuntime.InvokeVoidAsync("open", url, "_blank");
            }
            else
            {
                // your original logic
                _ignoreRowClickForPartNum = model.Part.PartNum;
                ResetAllDetailRows();
                NavigationManager.NavigateTo(url, forceLoad: false, replace: false);
            }
        }

        protected void OnRowStyling(CMHub_CMDexPartModel part, DataGridRowStyling styling)
        {
            if (part.IsDetailExpanded)
            {
                styling.Class = "active-row";
            }
        }

        protected async Task DetailRowToggle(DataGridRowMouseEventArgs<CMHub_CMDexPartModel> args)
        {
            if (_ignoreRowClickForPartNum != args.Item.Part.PartNum && Parts != null)
            {
                args.Item.IsDetailExpanded = !args.Item.IsDetailExpanded;

                // Close other expanded rows (only one open at a time)
                foreach (var part in Parts.Where(e => e != args.Item && e.IsDetailExpanded))
                {
                    PartGrid?.ToggleDetailRow(part);
                    part.IsDetailExpanded = false;
                }

                if (args.Item.IsDetailExpanded && (args.Item.ADEmployees.Count < 1 || args.Item.PartCustomerContactsForDisplay.Count < 1))
                {
                    await LoadDisplayDataForPartModel(args.Item);
                }
            }

            _ignoreRowClickForPartNum = "";
        }

        protected void CollapseExpandedRows()
        {
            foreach (var part in Parts.Where(e => e.IsDetailExpanded))
            {
                PartGrid?.ToggleDetailRow(part);
                part.IsDetailExpanded = false;
            }
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
            GC.SuppressFinalize(this);
        }
    }
}
