using CrystalGroupHome.Internal.Features.CMHub.VendorComms.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using System.Text.Json;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.SharedRCL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using CrystalGroupHome.Internal.Authorization;

namespace CrystalGroupHome.Internal.Features.CMHub.VendorComms.Components
{
    public class CMHub_VendorCommsTrackerFormBase : ComponentBase
    {
        [Inject] public ICMHub_VendorCommsService VendorCommsService { get; set; } = default!;
        [Inject] public IPartService PartService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public IAuthorizationService AuthorizationService { get; set; } = default!;
        [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

        [Parameter] public string PartNum { get; set; } = string.Empty;

        [Parameter]
        [SupplyParameterFromQuery(Name = "returnUrl")]
        public string? ReturnUrl { get; set; }

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        protected CMHub_VendorCommsTrackerModel? TrackerModel { get; set; }
        protected CMHub_VendorCommsTrackerModel? OriginalTrackerModel { get; set; } // For change tracking
        protected bool HasUnsavedChanges { get; set; }

        protected bool FormDisabled => IsSaving || TrackerModel == null || CurrentUser == null || !HasEditPermission;
        protected bool HasEditPermission { get; set; } = false;
        protected bool IsSaving { get; set; } = false; // New loading state
        protected CustomSearchInput? ReplacementPartNumSearchInput;
        protected string ReplacementPartDescription { get; set; } = string.Empty;

        // Date validation message (single message for all date fields)
        protected string? DateValidationMessage { get; set; }
        protected bool ShowValidationSummary { get; set; } = false;

        // Log modal references
        protected CMHub_VendorCommsNewLogModal? NewLogModal { get; set; }
        protected CMHub_VendorCommsLogsModal? LogsModal { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await CheckAuthorizationAsync();
        }

        private async Task CheckAuthorizationAsync()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var result = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubVendorCommsEdit);

            HasEditPermission = result.Succeeded;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!string.IsNullOrWhiteSpace(PartNum))
            {
                TrackerModel = await VendorCommsService.GetTrackerByPartNumAsync(PartNum);
                if (TrackerModel == null)
                {
                    var newTracker = new CMHub_VendorCommsTrackerModel { Tracker = new CMHub_VendorCommsTrackerDTO { PartNum = PartNum } };
                    var trackerId = await VendorCommsService.CreateOrUpdateTrackerAsync(newTracker);
                    TrackerModel = await VendorCommsService.GetTrackerByIdAsync(trackerId);
                }

                // Create a deep copy for change tracking
                OriginalTrackerModel = CreateDeepCopy(TrackerModel);

                // Initialize replacement part validation if there's an existing value
                if (!string.IsNullOrEmpty(TrackerModel?.PartEolt.ReplacementPartNum))
                {
                    await ValidateReplacementPartNum(TrackerModel.PartEolt.ReplacementPartNum, silent: true);
                }
                else
                {
                    // Clear description if no replacement part number
                    ReplacementPartDescription = string.Empty;
                }
            }
        }

        protected async Task ReloadTracker()
        {
            if (!string.IsNullOrWhiteSpace(PartNum))
            {
                TrackerModel = await VendorCommsService.GetTrackerByPartNumAsync(PartNum);
                if (TrackerModel == null)
                {
                    // Create a new tracker if one doesn't already exist.
                    var newTracker = new CMHub_VendorCommsTrackerModel { Tracker = new CMHub_VendorCommsTrackerDTO { PartNum = PartNum } };
                    var trackerId = await VendorCommsService.CreateOrUpdateTrackerAsync(newTracker);
                    TrackerModel = await VendorCommsService.GetTrackerByIdAsync(trackerId);
                }

                // Update the original tracker model with the reloaded data
                OriginalTrackerModel = CreateDeepCopy(TrackerModel);
                HasUnsavedChanges = false;

                // Clear all validation messages on reload
                DateValidationMessage = null;
                ShowValidationSummary = false;

                // Re-initialize replacement part validation after reload or clear if empty
                if (!string.IsNullOrEmpty(TrackerModel?.PartEolt.ReplacementPartNum))
                {
                    await ValidateReplacementPartNum(TrackerModel.PartEolt.ReplacementPartNum, silent: true);
                }
                else
                {
                    // Clear description and reset search input state when undoing changes
                    ReplacementPartDescription = string.Empty;
                    ReplacementPartNumSearchInput?.SetCurrentInputHasBeenSearched(false);
                    ReplacementPartNumSearchInput?.SetCurrentInputIsValid(false);
                }

                StateHasChanged();
            }
        }

        private CMHub_VendorCommsTrackerModel? CreateDeepCopy(CMHub_VendorCommsTrackerModel? original)
        {
            if (original == null) return null;

            // Using JSON serialization for deep copy
            var json = JsonSerializer.Serialize(original);
            return JsonSerializer.Deserialize<CMHub_VendorCommsTrackerModel>(json);
        }

        protected void OnReplacementPartNumTextValueChanged(string newText)
        {
            if (TrackerModel == null) return;

            TrackerModel.PartEolt.ReplacementPartNum = newText;
            
            // Always clear the description when the text changes
            ReplacementPartDescription = string.Empty;
            
            // Reset validation state when text changes
            ReplacementPartNumSearchInput?.SetCurrentInputHasBeenSearched(false);
            ReplacementPartNumSearchInput?.SetCurrentInputIsValid(false);
            
            SetUnsavedChanges();
        }

        protected async Task OnSearchReplacementPartNum(string partNum)
        {
            if (string.IsNullOrEmpty(partNum) || (ReplacementPartNumSearchInput?.CurrentInputHasBeenSearched ?? false))
            {
                return;
            }

            await ValidateReplacementPartNum(partNum.Trim());
        }

        private async Task ValidateReplacementPartNum(string partNum, bool silent = false)
        {
            if (string.IsNullOrEmpty(partNum))
            {
                ReplacementPartDescription = string.Empty;
                if (!silent && ReplacementPartNumSearchInput != null)
                {
                    ReplacementPartNumSearchInput.SetCurrentInputHasBeenSearched(false);
                    ReplacementPartNumSearchInput.SetCurrentInputIsValid(false);
                }
                return;
            }

            try
            {
                var foundParts = await PartService.GetPartsByPartNumbersAsync<PartDTO_Base>(new[] { partNum });
                var foundPart = foundParts.FirstOrDefault();

                if (foundPart != null)
                {
                    ReplacementPartDescription = foundPart.PartDescription;
                    if (ReplacementPartNumSearchInput != null)
                    {
                        if (silent)
                        {
                            ReplacementPartNumSearchInput.SetTextSilently(partNum, validSearch: true);
                        }
                        else
                        {
                            ReplacementPartNumSearchInput.SetCurrentInputHasBeenSearched(true);
                            ReplacementPartNumSearchInput.SetCurrentInputIsValid(true);
                        }
                    }
                }
                else
                {
                    ReplacementPartDescription = string.Empty;
                    if (ReplacementPartNumSearchInput != null)
                    {
                        if (silent)
                        {
                            ReplacementPartNumSearchInput.SetTextSilently(partNum, validSearch: false);
                        }
                        else
                        {
                            ReplacementPartNumSearchInput.SetCurrentInputHasBeenSearched(true);
                            ReplacementPartNumSearchInput.SetCurrentInputIsValid(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't break the form
                Console.Error.WriteLine($"Error validating replacement part number: {ex}");
                ReplacementPartDescription = string.Empty;
                if (ReplacementPartNumSearchInput != null)
                {
                    ReplacementPartNumSearchInput.SetCurrentInputHasBeenSearched(true);
                    ReplacementPartNumSearchInput.SetCurrentInputIsValid(false);
                }
            }

            StateHasChanged();
        }

        protected void OnContactIntervalChanged(int? value)
        {
            if (TrackerModel is null || !value.HasValue)
                return;

            if (TrackerModel.PartEolt.NotifyIntervalDays != value.Value)
            {
                TrackerModel.PartEolt.NotifyIntervalDays = value.Value;
                SetUnsavedChanges();
            }
        }

        protected void OnExcludeVendorCommsChanged(bool value)
        {
            if (TrackerModel is null) return;

            if (TrackerModel.PartEolt.ExcludeVendorComms != value)
            {
                TrackerModel.PartEolt.ExcludeVendorComms = value;
                SetUnsavedChanges();
            }
        }

        protected void OnEolDateChanged(DateTime? value)
        {
            if (TrackerModel is null) return;

            if (TrackerModel.PartEolt.EolDate != value)
            {
                TrackerModel.PartEolt.EolDate = value;
                
                // Clear validation message when date changes
                DateValidationMessage = null;
                ShowValidationSummary = false;
                
                SetUnsavedChanges();
            }
        }

        protected void OnLastContactDateChanged(DateTime? value)
        {
            if (TrackerModel is null) return;

            if (TrackerModel.PartEolt.LastContactDate != value)
            {
                TrackerModel.PartEolt.LastContactDate = value;
                
                // Clear validation message when date changes
                DateValidationMessage = null;
                ShowValidationSummary = false;
                
                SetUnsavedChanges();
            }
        }

        protected void OnLastResponseDateChanged(DateTime? value)
        {
            if (TrackerModel is null) return;

            if (TrackerModel.PartEolt.LastProcessedSurveyResponseDate != value)
            {
                TrackerModel.PartEolt.LastProcessedSurveyResponseDate = value;
                
                // Clear validation message when date changes
                DateValidationMessage = null;
                ShowValidationSummary = false;
                
                SetUnsavedChanges();
            }
        }

        protected void OnLastTimeBuyDateChanged(DateTime? value)
        {
            if (TrackerModel is null) return;

            if (TrackerModel.PartEolt.LastTimeBuyDate != value)
            {
                TrackerModel.PartEolt.LastTimeBuyDate = value;
                
                // Clear validation message when date changes
                DateValidationMessage = null;
                ShowValidationSummary = false;

                // If Last Time Buy date is being cleared (set to null), reset the confirmation status to Projected
                if (!value.HasValue)
                {
                    TrackerModel.PartEolt.LastTimeBuyDateConfirmed = false;
                }
                else
                {
                    // If Last Time Buy date is being set and either Last Contact Date or Last Response Date are null,
                    // set them to today's date to assume that today was the day we collected that data from the vendor
                    var today = DateTime.Today;

                    if (TrackerModel.PartEolt.LastContactDate == null)
                    {
                        TrackerModel.PartEolt.LastContactDate = today;
                    }

                    if (TrackerModel.PartEolt.LastProcessedSurveyResponseDate == null)
                    {
                        TrackerModel.PartEolt.LastProcessedSurveyResponseDate = today;
                    }
                }

                SetUnsavedChanges();
            }
        }

        protected void OnLastTimeBuyDateConfirmedChanged(bool value)
        {
            if (TrackerModel is null) return;

            if (TrackerModel.PartEolt.LastTimeBuyDateConfirmed != value)
            {
                TrackerModel.PartEolt.LastTimeBuyDateConfirmed = value;
                SetUnsavedChanges();
            }
        }

        protected void OnTechNotesChanged(string value)
        {
            if (TrackerModel is null) return;
            var normalizedValue = value ?? string.Empty;
            if (TrackerModel.PartEolt.TechNotes != normalizedValue)
            {
                TrackerModel.PartEolt.TechNotes = normalizedValue;
                SetUnsavedChanges();
            }
        }

        private void SetUnsavedChanges()
        {
            if (!HasUnsavedChanges)
            {
                HasUnsavedChanges = true;
                StateHasChanged();
            }
        }

        private bool ValidateAllDates()
        {
            bool isValid = true;

            // Clear all validation messages first
            DateValidationMessage = null;
            ShowValidationSummary = false;

            if (TrackerModel == null) return false;

            // Validate Last Contact Date
            if (TrackerModel.PartEolt.LastContactDate.HasValue && 
                !DataHelpers.IsBusinessReasonableDateTime(TrackerModel.PartEolt.LastContactDate))
            {
                DateValidationMessage = "Invalid date";
                isValid = false;
            }

            // Validate Last Processed Survey Response Date
            if (TrackerModel.PartEolt.LastProcessedSurveyResponseDate.HasValue && 
                !DataHelpers.IsBusinessReasonableDateTime(TrackerModel.PartEolt.LastProcessedSurveyResponseDate))
            {
                DateValidationMessage = "Invalid date";
                isValid = false;
            }

            // Validate EOL Date
            if (TrackerModel.PartEolt.EolDate.HasValue && 
                !DataHelpers.IsBusinessReasonableDateTime(TrackerModel.PartEolt.EolDate))
            {
                DateValidationMessage = "Invalid date";
                isValid = false;
            }

            // Validate Last Time Buy Date
            if (TrackerModel.PartEolt.LastTimeBuyDate.HasValue && 
                !DataHelpers.IsBusinessReasonableDateTime(TrackerModel.PartEolt.LastTimeBuyDate))
            {
                DateValidationMessage = "Invalid date";
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
            if (TrackerModel != null && CurrentUser != null && !IsSaving)
            {
                // Validate all dates before saving
                if (!ValidateAllDates())
                {
                    StateHasChanged();
                    return; // Don't save if validation fails
                }

                IsSaving = true;
                StateHasChanged();

                try
                {
                    // Use the new method with change logging
                    await VendorCommsService.CreateOrUpdateTrackerWithChangeLoggingAsync(
                        TrackerModel, 
                        OriginalTrackerModel, 
                        CurrentUser.DBUser.EmployeeNumber
                    );

                    // Update the original tracker model with the current state for future comparisons
                    OriginalTrackerModel = CreateDeepCopy(TrackerModel);

                    // Refresh logs to show the new change log
                    TrackerModel.TrackerLogs = await VendorCommsService.GetTrackerLogsAsync(TrackerModel.Tracker.Id);

                    HasUnsavedChanges = false;
                }
                finally
                {
                    IsSaving = false;
                    StateHasChanged();
                }
            }
        }

        protected async Task<bool> SaveChanges()
        {
            if (TrackerModel == null || CurrentUser == null || IsSaving)
                return false;

            // Validate all dates before saving
            if (!ValidateAllDates())
            {
                StateHasChanged();
                return false; // Don't save if validation fails
            }

            IsSaving = true;
            StateHasChanged();

            try
            {
                // Use the new method with change logging
                int trackerId = await VendorCommsService.CreateOrUpdateTrackerWithChangeLoggingAsync(
                    TrackerModel, 
                    OriginalTrackerModel, 
                    CurrentUser.DBUser.EmployeeNumber
                );
                
                bool saveSuccessful = trackerId > 0;

                if (saveSuccessful)
                {
                    // Update the original tracker model with the current state for future comparisons
                    OriginalTrackerModel = CreateDeepCopy(TrackerModel);

                    // Refresh logs to show the new change log
                    TrackerModel.TrackerLogs = await VendorCommsService.GetTrackerLogsAsync(TrackerModel.Tracker.Id);

                    HasUnsavedChanges = false;
                }

                return saveSuccessful;
            }
            finally
            {
                IsSaving = false;
                StateHasChanged();
            }
        }

        protected async Task ConfirmNavigationAsync(LocationChangingContext context)
        {
            if (HasUnsavedChanges && TrackerModel != null)
            {
                context.PreventNavigation();
                var saveSuccessful = await SaveChanges();

                if (saveSuccessful)
                {
                    // Check if we have a return URL to navigate back to the exact state
                    if (!string.IsNullOrWhiteSpace(ReturnUrl))
                    {
                        NavigationManager.NavigateTo(Uri.UnescapeDataString(ReturnUrl));
                    }
                    else
                    {
                        NavigationManager.NavigateTo(context.TargetLocation);
                    }
                }
            }
        }

        protected void NavigateBack()
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl))
            {
                NavigationManager.NavigateTo(Uri.UnescapeDataString(ReturnUrl));
            }
            else
            {
                NavigationManager.NavigateTo(NavHelpers.CMHub_VendorCommsMainPage);
            }
        }

        // Log-related methods
        protected void ShowAddLogModal()
        {
            if (NewLogModal != null && TrackerModel != null)
            {
                NewLogModal.Show(TrackerModel.Tracker.Id, $"Tracker Log for {TrackerModel.Tracker.PartNum}");
            }
        }

        protected Task ShowAllLogsAsync()
        {
            if (TrackerModel == null || LogsModal == null)
                return Task.CompletedTask;

            LogsModal.Show(
                title: $"All Logs for {TrackerModel.Tracker.PartNum}",
                logs: TrackerModel.TrackerLogs.OrderByDescending(x => x.LogDate).ToList()
            );

            return Task.CompletedTask;
        }

        protected async Task HandleNewLogSubmitted(CMHub_VendorCommsTrackerLogModel newLog)
        {
            if (TrackerModel == null || CurrentUser == null) return;

            // Ensure LoggedByUser is set before saving and mark as manual entry
            newLog.LoggedByUser = CurrentUser.DBUser.EmployeeNumber;
            newLog.ManualLogEntry = true; // This is a manual log entry from the user

            await VendorCommsService.CreateTrackerLogAsync(newLog, CurrentUser.DBUser.EmployeeNumber);

            // Refresh logs and update UI
            TrackerModel.TrackerLogs = await VendorCommsService.GetTrackerLogsAsync(TrackerModel.Tracker.Id);

            StateHasChanged();
        }
    }
}