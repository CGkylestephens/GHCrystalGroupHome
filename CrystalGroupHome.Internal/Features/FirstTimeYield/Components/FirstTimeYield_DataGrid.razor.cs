using Blazorise;
using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Models;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Components
{
    public partial class FirstTimeYield_DataGridBase : ComponentBase
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public IFirstTimeYield_Service FTYService { get; set; } = default!;
        [Inject] public IModalService Modal { get; set; } = default!;

        [Parameter] public List<FirstTimeYield_AreaDTO> Areas { get; set; } = new();

        // Main collections
        public List<FirstTimeYield_EntryModel> Entries { get; private set; } = new();
        public List<FirstTimeYield_EntryModel> DisplayEntries { get; private set; } = new();

        public DataGrid<FirstTimeYield_EntryModel>? EntryGrid { get; set; }

        // Confirmation modal (for deleting)
        public ConfirmationModal? ConfirmationModal { get; set; }
        public FirstTimeYield_EntryModel? EntryToDelete { get; set; }

        // We toggle detail rows, so keep track if we should ignore a row click
        private int _ignoreRowClickForEntryId = -1;

        // Filter collapse/expand
        protected bool ShowFilterInputs { get; set; } = false;

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        // ------------------------------------------------------
        // Filter backing fields
        private string? _jobNum;
        private string? _opCode;
        private string? _opCodeOperator;
        private string? _areaDesc;
        private string? _entryUser;
        private DateTime? _dateEnteredOnOrAfter;
        private DateTime? _dateEnteredOnOrBefore;
        private string? _modifiedUser;
        private DateTime? _dateModifiedOnOrAfter;
        private DateTime? _dateModifiedOnOrBefore;

        // ------------------------------------------------------
        // Lifecycle flags
        private bool _hasInitialFilterApplied = false;
        public bool IsLoading = false;

        // We keep track of the current full URI with query strings
        public string NewParameterUri { get; private set; } = string.Empty;

        // ------------------------------------------------------
        // Filter properties with on-change triggers
        [Parameter]
        public string? JobNumFilter
        {
            get => _jobNum;
            set
            {
                if (_jobNum != value)
                {
                    _jobNum = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string? OpCodeFilter
        {
            get => _opCode;
            set
            {
                if (_opCode != value)
                {
                    _opCode = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string? OpCodeOperatorFilter
        {
            get => _opCodeOperator;
            set
            {
                if (_opCodeOperator != value)
                {
                    _opCodeOperator = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string? AreaFilter
        {
            get => _areaDesc;
            set
            {
                if (_areaDesc != value)
                {
                    _areaDesc = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string? EntryUserFilter
        {
            get => _entryUser;
            set
            {
                if (_entryUser != value)
                {
                    _entryUser = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public DateTime? DateEnteredOnOrAfterFilter
        {
            get => _dateEnteredOnOrAfter;
            set
            {
                if (_dateEnteredOnOrAfter != value)
                {
                    _dateEnteredOnOrAfter = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public DateTime? DateEnteredOnOrBeforeFilter
        {
            get => _dateEnteredOnOrBefore;
            set
            {
                if (_dateEnteredOnOrBefore != value)
                {
                    _dateEnteredOnOrBefore = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public string? ModifiedUserFilter
        {
            get => _modifiedUser;
            set
            {
                if (_modifiedUser != value)
                {
                    _modifiedUser = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public DateTime? DateModifiedOnOrAfterFilter
        {
            get => _dateModifiedOnOrAfter;
            set
            {
                if (_dateModifiedOnOrAfter != value)
                {
                    _dateModifiedOnOrAfter = value;
                    ApplyFiltersIfReady();
                }
            }
        }

        [Parameter]
        public DateTime? DateModifiedOnOrBeforeFilter
        {
            get => _dateModifiedOnOrBefore;
            set
            {
                if (_dateModifiedOnOrBefore != value)
                {
                    _dateModifiedOnOrBefore = value;
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

            // 2) Show the filter inputs if we already have something set
            if (!NoFiltersApplied())
            {
                ShowFilterInputs = true;
            }

            // 3) Load the data (hide the grid until we’re done)
            IsLoading = true;
            try
            {
                // Wait until user's permissions have loaded
                while (CurrentUser?.ADUser?.Claims?.Count() <= 0)
                {
                    await Task.Delay(100); // Wait for a short time before checking again
                }

                Entries = await FTYService.GetEntriesAsync();
                DisplayEntries = Pages.FirstTimeYield.IsAdmin(CurrentUser?.ADUser)
                    ? new List<FirstTimeYield_EntryModel>(Entries)
                    : new List<FirstTimeYield_EntryModel>(Entries).Where(entry => (entry.EntryUser.EmployeeNumber == CurrentUser?.DBUser.EmployeeNumber || entry.LastModifiedUser.EmployeeNumber == CurrentUser?.DBUser.EmployeeNumber)).ToList();
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
                // After we’re truly interactive, apply the filters once
                _hasInitialFilterApplied = true;
                FilterDisplayEntries();
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        // ------------------------------------------------------
        // Filter logic

        /// <summary>
        /// Applies the filters if we've already completed the initial load/phase.
        /// This prevents repeated filtering during prerender.
        /// </summary>
        private void ApplyFiltersIfReady()
        {
            if (_hasInitialFilterApplied)
            {
                FilterDisplayEntries();
            }
        }

        /// <summary>
        /// Actually filters the in-memory data, resets detail rows, updates the URL.
        /// </summary>
        private void FilterDisplayEntries()
        {
            // 1) Filter in memory
            DisplayEntries = Entries
                .Where(entry =>
                    string.IsNullOrEmpty(JobNumFilter)
                    || entry.JobNum.Contains(JobNumFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry =>
                    string.IsNullOrEmpty(OpCodeFilter)
                    || entry.OpCode.Contains(OpCodeFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry =>
                    string.IsNullOrEmpty(OpCodeOperatorFilter)
                    || (entry.OpCodeOperator != null && entry.OpCodeOperator.DisplayName.Contains(OpCodeOperatorFilter, StringComparison.OrdinalIgnoreCase)))
                .Where(entry =>
                    string.IsNullOrEmpty(AreaFilter)
                    || (entry.Area != null && entry.Area.AreaDescription.Contains(AreaFilter, StringComparison.OrdinalIgnoreCase)))
                .Where(entry =>
                    string.IsNullOrEmpty(EntryUserFilter)
                    || entry.EntryUser.DisplayName.Contains(EntryUserFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry =>
                    !DateEnteredOnOrAfterFilter.HasValue
                    || entry.EntryDate.Date >= DateEnteredOnOrAfterFilter.Value.Date)
                .Where(entry =>
                    !DateEnteredOnOrBeforeFilter.HasValue
                    || entry.EntryDate.Date <= DateEnteredOnOrBeforeFilter.Value.Date)
                .Where(entry =>
                    string.IsNullOrEmpty(ModifiedUserFilter)
                    || entry.LastModifiedUser.DisplayName.Contains(ModifiedUserFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry =>
                    !DateModifiedOnOrAfterFilter.HasValue
                    || entry.LastModifiedDate.Date >= DateModifiedOnOrAfterFilter.Value.Date)
                .Where(entry =>
                    !DateModifiedOnOrBeforeFilter.HasValue
                    || entry.LastModifiedDate.Date <= DateModifiedOnOrBeforeFilter.Value.Date)
                .ToList();

            if (!Pages.FirstTimeYield.HasEditPermission(CurrentUser?.ADUser))
            {
                DisplayEntries = DisplayEntries
                    .Where(entry => entry.EntryUser.EmployeeNumber == CurrentUser?.DBUser.EmployeeNumber).ToList();
            }

            // 2) Close any expanded detail rows to prevent rendering issues
            ResetAllDetailRows();

            // 3) Update the UI
            StateHasChanged();

            // 4) Update the query params in the browser URL
            UpdateAllQueryParameters();
        }

        /// <summary>
        /// Reads the current query string values from the URL and populates
        /// filter properties if they exist.
        /// </summary>
        private void GetQueryParams()
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);

            if (query.TryGetValue(FirstTimeYield_EntryModel.jobNumParamName, out var jobNumValue))
                _jobNum = jobNumValue.ToString();

            if (query.TryGetValue(FirstTimeYield_EntryModel.opCodeParamName, out var opCodeValue))
                _opCode = opCodeValue.ToString();

            if (query.TryGetValue(FirstTimeYield_EntryModel.opCodeOperatorIDParamName, out var opCodeOperatorValue))
                _opCodeOperator = opCodeOperatorValue.ToString();

            if (query.TryGetValue(FirstTimeYield_EntryModel.areaIdParamName, out var areaIdValue))
            {
                var areaDesc = Areas.FirstOrDefault(a => a.Id.ToString() == areaIdValue)?.AreaDescription;
                if (!string.IsNullOrWhiteSpace(areaDesc))
                    _areaDesc = areaDesc;
            }

            if (query.TryGetValue(FirstTimeYield_EntryModel.entryUserParamName, out var entryUserValue))
                _entryUser = entryUserValue.ToString();

            if (query.TryGetValue(FirstTimeYield_EntryModel.dateEnteredOnOrAfterParamName, out var dateEnteredOnOrAfterValue))
                _dateEnteredOnOrAfter = DateTime.TryParse(dateEnteredOnOrAfterValue, out var parsedOnOrAfter)
                    ? parsedOnOrAfter
                    : null;

            if (query.TryGetValue(FirstTimeYield_EntryModel.dateEnteredOnOrBeforeParamName, out var dateEnteredOnOrBeforeValue))
                _dateEnteredOnOrBefore = DateTime.TryParse(dateEnteredOnOrBeforeValue, out var parsedOnOrBefore)
                    ? parsedOnOrBefore
                    : null;

            if (query.TryGetValue(FirstTimeYield_EntryModel.modifiedUserParamName, out var modifiedUserValue))
                _modifiedUser = modifiedUserValue.ToString();

            if (query.TryGetValue(FirstTimeYield_EntryModel.dateModifiedOnOrAfterParamName, out var dateModifiedOnOrAfterValue))
                _dateModifiedOnOrAfter = DateTime.TryParse(dateModifiedOnOrAfterValue, out var parsedModOnOrAfter)
                    ? parsedModOnOrAfter
                    : null;

            if (query.TryGetValue(FirstTimeYield_EntryModel.dateModifiedOnOrBeforeParamName, out var dateModifiedOnOrBeforeValue))
                _dateModifiedOnOrBefore = DateTime.TryParse(dateModifiedOnOrBeforeValue, out var parsedModOnOrBefore)
                    ? parsedModOnOrBefore
                    : null;

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

            // Job Number
            if (!string.IsNullOrWhiteSpace(JobNumFilter))
            {
                queryParams[FirstTimeYield_EntryModel.jobNumParamName] = JobNumFilter;
            }

            // Op Code
            if (!string.IsNullOrWhiteSpace(OpCodeFilter))
            {
                queryParams[FirstTimeYield_EntryModel.opCodeParamName] = OpCodeFilter;
            }

            // Op Code Operator
            if (!string.IsNullOrWhiteSpace(OpCodeOperatorFilter))
            {
                queryParams[FirstTimeYield_EntryModel.opCodeOperatorIDParamName] = OpCodeOperatorFilter;
            }

            // Area
            if (!string.IsNullOrWhiteSpace(AreaFilter))
            {
                queryParams[FirstTimeYield_EntryModel.areaIdParamName] = AreaFilter;
            }

            // Entry User
            if (!string.IsNullOrWhiteSpace(EntryUserFilter))
            {
                queryParams[FirstTimeYield_EntryModel.entryUserParamName] = EntryUserFilter;
            }

            // Date Entered On or After
            if (DateEnteredOnOrAfterFilter.HasValue)
            {
                queryParams[FirstTimeYield_EntryModel.dateEnteredOnOrAfterParamName]
                    = DateEnteredOnOrAfterFilter.Value.ToString("yyyy-MM-dd");
            }

            // Date Entered On or Before
            if (DateEnteredOnOrBeforeFilter.HasValue)
            {
                queryParams[FirstTimeYield_EntryModel.dateEnteredOnOrBeforeParamName]
                    = DateEnteredOnOrBeforeFilter.Value.ToString("yyyy-MM-dd");
            }

            // Modified User
            if (!string.IsNullOrWhiteSpace(ModifiedUserFilter))
            {
                queryParams[FirstTimeYield_EntryModel.modifiedUserParamName] = ModifiedUserFilter;
            }

            // Date Modified On or After
            if (DateModifiedOnOrAfterFilter.HasValue)
            {
                queryParams[FirstTimeYield_EntryModel.dateModifiedOnOrAfterParamName]
                    = DateModifiedOnOrAfterFilter.Value.ToString("yyyy-MM-dd");
            }

            // Date Modified On or Before
            if (DateModifiedOnOrBeforeFilter.HasValue)
            {
                queryParams[FirstTimeYield_EntryModel.dateModifiedOnOrBeforeParamName]
                    = DateModifiedOnOrBeforeFilter.Value.ToString("yyyy-MM-dd");
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
        // Filter toggles

        protected void ToggleFilters()
        {
            ShowFilterInputs = !ShowFilterInputs;
        }

        protected void ClearAllFilters()
        {
            JobNumFilter = null;
            OpCodeFilter = null;
            OpCodeOperatorFilter = null;
            AreaFilter = null;
            EntryUserFilter = null;
            DateEnteredOnOrAfterFilter = null;
            DateEnteredOnOrBeforeFilter = null;
            ModifiedUserFilter = null;
            DateModifiedOnOrAfterFilter = null;
            DateModifiedOnOrBeforeFilter = null;

            // Force an immediate filter (now that we cleared everything):
            FilterDisplayEntries();
        }

        protected bool NoFiltersApplied()
        {
            return (
                string.IsNullOrWhiteSpace(JobNumFilter)
                && string.IsNullOrWhiteSpace(OpCodeFilter)
                && string.IsNullOrWhiteSpace(OpCodeOperatorFilter)
                && string.IsNullOrWhiteSpace(AreaFilter)
                && string.IsNullOrWhiteSpace(EntryUserFilter)
                && !DateEnteredOnOrAfterFilter.HasValue
                && !DateEnteredOnOrBeforeFilter.HasValue
                && string.IsNullOrWhiteSpace(ModifiedUserFilter)
                && !DateModifiedOnOrAfterFilter.HasValue
                && !DateModifiedOnOrBeforeFilter.HasValue
            );
        }

        // ------------------------------------------------------
        // Helpers to handle DataGrid row expansions, navigation, etc.

        private void ResetAllDetailRows()
        {
            if (DisplayEntries is null || EntryGrid is null)
                return;

            foreach (var entry in DisplayEntries)
            {
                if (entry.IsDetailExpanded)
                {
                    EntryGrid.ToggleDetailRow(entry);
                    entry.IsDetailExpanded = false;
                }
            }
        }

        protected void DetailRowToggle(DataGridRowMouseEventArgs<FirstTimeYield_EntryModel> args)
        {
            if (_ignoreRowClickForEntryId != args.Item.Id)
            {
                args.Item.IsDetailExpanded = !args.Item.IsDetailExpanded;

                // Close other expanded rows (only one open at a time)
                foreach (var entry in DisplayEntries.Where(e => e != args.Item && e.IsDetailExpanded))
                {
                    EntryGrid?.ToggleDetailRow(entry);
                    entry.IsDetailExpanded = false;
                }
            }

            _ignoreRowClickForEntryId = -1;
        }

        protected void OnRowStyling(FirstTimeYield_EntryModel entry, DataGridRowStyling styling)
        {
            if (entry.IsDetailExpanded)
            {
                styling.Class = "active-row";
            }
        }

        protected void NavigateToAddEntry()
        {
            ResetAllDetailRows();
            NavigationManager.NavigateTo(NavHelpers.FirstTimeYieldAddEntry);
        }

        protected void NavigateToEditEntry(FirstTimeYield_EntryModel entry)
        {
            _ignoreRowClickForEntryId = entry.Id;
            ResetAllDetailRows();
            NavigationManager.NavigateTo($"{NavHelpers.FirstTimeYieldEditEntry}/{entry.Id}");
        }

        protected async Task DeleteEntry(FirstTimeYield_EntryModel entry)
        {
            if (EntryGrid != null)
            {
                await EntryGrid.ToggleDetailRow(entry);
                entry.IsDetailExpanded = !entry.IsDetailExpanded;
            }

            EntryToDelete = entry;
            if (ConfirmationModal != null)
            {
                await ConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmDeletion(bool confirmed)
        {
            if (confirmed && EntryToDelete != null)
            {
                await FTYService.DeleteEntryAsync(EntryToDelete.Id);
                Entries = await FTYService.GetEntriesAsync();
                DisplayEntries = new List<FirstTimeYield_EntryModel>(Entries);

                if (EntryGrid != null)
                {
                    await EntryGrid.Reload();
                }
            }

            EntryToDelete = null;
        }
    }
}
