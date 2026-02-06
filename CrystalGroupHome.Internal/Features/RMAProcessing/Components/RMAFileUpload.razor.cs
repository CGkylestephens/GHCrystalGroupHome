using Blazorise;
using Microsoft.AspNetCore.Components;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.Internal.Features.RMAProcessing.Pages;
using System.Linq;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components
{
    public class RMAFileUploadBase : ComponentBase
    {
        [Inject] protected INotificationService NotificationService { get; set; } = default!;
        [Inject] protected IRMAFileService RMAFileService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<RMAFileUploadBase>? Logger { get; set; }
        [Inject] protected IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

        [Parameter] public string? RmaNumber { get; set; }
        [Parameter] public string? RmaLineNumber { get; set; }
        [Parameter] public string? SerialNumber { get; set; }
        [Parameter] public string? InitialCategory { get; set; }
        [Parameter] public List<FileCategory> AvailableCategories { get; set; } = new();

        [Parameter] public EventCallback OnUploadComplete { get; set; }
        [Parameter] public EventCallback OnViewFiles { get; set; }
        [Parameter] public EventCallback<(string? rmaLineNumber, string? serialNumber)> OnViewFilesWithScope { get; set; }
        [Parameter] public EventCallback<(string? rmaLineNumber, string? serialNumber)> OnLineSerialFilterChanged { get; set; }

        protected RMAFileUploadModel UploadModel { get; set; } = new();
        protected List<RMAFileUploadResult> RecentUploadResults { get; set; } = new();
        protected bool IsUploading { get; set; }
        protected bool IsFilesReadyHidden { get; set; } = true;

        // Permission checking
        protected bool CanUploadFiles => RMAProcessingBase.HasFileUploadEditPermission(HttpContextAccessor.HttpContext?.User);

        // Conflict resolution
        protected bool ShowConflictModal { get; set; }
        protected List<string> ConflictingFileNames { get; set; } = new();
        protected string? SelectedConflictResolution { get; set; }

        private string? lastUploadedCategoryShortName;
        private string? lastUploadedCategoryDisplayValue;

        // Line context
        protected List<RmaLineSummary> RmaLines { get; set; } = new();
        protected RmaLineSummary? SelectedLineSummary { get; set; }
        protected string? SelectedLineNumber { get; set; }
        protected RMAFileStorageInfo? StorageInfo { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            if (string.IsNullOrEmpty(UploadModel.RmaNumber))
                UploadModel.RmaNumber = RmaNumber ?? string.Empty;

            if (SelectedLineNumber == null && !string.IsNullOrEmpty(RmaLineNumber))
                SelectedLineNumber = RmaLineNumber;

            await LoadLines();
            SyncLineSerial();
            InitializeCategoryIfNeeded();
            LoadStorageInfo();
        }

        protected string GetUploadButtonTooltip()
        {
            if (!CanUploadFiles)
                return "Upload access restricted to Technical Services users";
            if (IsUploading)
                return "Upload in progress...";
            if (!UploadModel.Files.Any())
                return "Select files to upload";
            if (string.IsNullOrEmpty(UploadModel.SelectedCategoryShortName))
                return "Select a category first";
            if (HasInvalidFiles())
                return "Some files are invalid for the selected category";
            return "Click to upload the selected files";
        }

        private async Task LoadLines()
        {
            RmaLines = new();
            if (!string.IsNullOrEmpty(RmaNumber))
            {
                try { RmaLines = await RMAFileService.GetRmaLinesAsync(RmaNumber); }
                catch (Exception ex) { await NotificationService.Error($"Failed to load RMA lines: {ex.Message}"); }
            }
        }

        private void SyncLineSerial()
        {
            if (IsLineMode())
            {
                SelectedLineSummary = RmaLines.FirstOrDefault(l => l.LineNumber.ToString() == SelectedLineNumber);
                UploadModel.RmaLineNumber = SelectedLineSummary?.LineNumber.ToString();
                UploadModel.SerialNumber = SelectedLineSummary?.Serials.Count == 1 ? SelectedLineSummary.Serials[0] : SerialNumber;
            }
            else
            {
                SelectedLineSummary = null;
                UploadModel.RmaLineNumber = null;
                UploadModel.SerialNumber = null;
            }
        }

        protected async Task OnUploadFilesSuccess()
        {
            if (OnLineSerialFilterChanged.HasDelegate)
            {
                var lineNumber = UploadModel.RmaLineNumber;
                string? serialDisplay = null;
                
                // Get serial display from selected line summary if available
                if (SelectedLineSummary != null && SelectedLineSummary.Serials.Any())
                {
                    serialDisplay = SelectedLineSummary.Serials.Count == 1 
                        ? SelectedLineSummary.Serials[0] 
                        : SelectedLineSummary.Serials[0] + $" (+{SelectedLineSummary.Serials.Count - 1} more)";
                }
                
                await OnLineSerialFilterChanged.InvokeAsync((lineNumber, serialDisplay));
            }
        }

        protected bool IsLineMode() => !string.IsNullOrEmpty(SelectedLineNumber) && int.TryParse(SelectedLineNumber, out _);

        protected IEnumerable<FileCategory> CurrentCategoryList
        {
            get
            {
                var isLineMode = IsLineMode();
                var filtered = AvailableCategories.Where(c => isLineMode ? c.IsDetailLevel : !c.IsDetailLevel);
                return filtered.ToList();
            }
        }

        private void InitializeCategoryIfNeeded()
        {
            if (string.IsNullOrEmpty(UploadModel.SelectedCategoryShortName) && !string.IsNullOrEmpty(InitialCategory))
            {
                if (CurrentCategoryList.Any(c => c.ShortName == InitialCategory))
                    UploadModel.SelectedCategoryShortName = InitialCategory;
            }
            else if (!string.IsNullOrEmpty(UploadModel.SelectedCategoryShortName))
            {
                if (!CurrentCategoryList.Any(c => c.ShortName == UploadModel.SelectedCategoryShortName))
                    UploadModel.SelectedCategoryShortName = null;
            }
        }

        protected string GetLineDisplay(RmaLineSummary line)
        {
            if (line.Serials.Count == 0) return "No Serial";
            if (line.Serials.Count == 1) return line.Serials[0];
            return $"{line.Serials[0]} (+{line.Serials.Count - 1} more)";
        }

        protected async Task OnLineChanged(string? newValue)
        {
            SelectedLineNumber = string.IsNullOrEmpty(newValue) ? null : newValue;
            SyncLineSerial();
            InitializeCategoryIfNeeded();
            ClearUploadResultsIfPresent();
            LoadStorageInfo();
            await InvokeAsync(StateHasChanged);
        }

        protected async Task OnCategoryChanged(string? newValue)
        {
            var prev = UploadModel.SelectedCategoryShortName;
            UploadModel.SelectedCategoryShortName = string.IsNullOrEmpty(newValue) ? null : newValue;
            ClearUploadResultsIfPresent();

            if (prev != UploadModel.SelectedCategoryShortName && UploadModel.Files.Any() &&
                UploadModel.SelectedCategoryShortName != null)
            {
                var cat = CurrentCategoryList.FirstOrDefault(c => c.ShortName == UploadModel.SelectedCategoryShortName);
                if (cat != null && cat.AcceptedFileTypes != "*" &&
                    UploadModel.Files.Any(f => !cat.IsFileTypeAllowed(Path.GetExtension(f.Name))))
                {
                    UploadModel.Files = new List<IFileEntry>();
                    await NotificationService.Info("Selected files cleared due to file type restrictions for this category.");
                }
            }
            await InvokeAsync(StateHasChanged);
        }

        protected string GetAcceptAttribute()
        {
            var cat = CurrentCategoryList.FirstOrDefault(c => c.ShortName == UploadModel.SelectedCategoryShortName);
            return cat?.AcceptedFileTypes ?? "*/*";
        }

        protected bool HasInvalidFiles()
        {
            var cat = CurrentCategoryList.FirstOrDefault(c => c.ShortName == UploadModel.SelectedCategoryShortName);
            if (cat == null || !UploadModel.Files.Any()) return false;
            return UploadModel.Files.Any(f => !cat.IsFileTypeAllowed(Path.GetExtension(f.Name)));
        }

        protected string GetSelectedCategoryDisplayName()
        {
            if (RecentUploadResults.Any() && !string.IsNullOrEmpty(lastUploadedCategoryDisplayValue))
                return lastUploadedCategoryDisplayValue!;
            return CurrentCategoryList.FirstOrDefault(c => c.ShortName == UploadModel.SelectedCategoryShortName)?.DisplayValue ?? "Unknown Category";
        }

        protected async Task OnFileSelected(FileChangedEventArgs e)
        {
            UploadModel.Files = e.Files;
            ClearUploadResultsIfPresent();
            await ValidateAndFilterSelectedFiles();
            await InvokeAsync(StateHasChanged);
        }

        private async Task ValidateAndFilterSelectedFiles()
        {
            var cat = CurrentCategoryList.FirstOrDefault(c => c.ShortName == UploadModel.SelectedCategoryShortName);
            if (cat == null || cat.AcceptedFileTypes == "*" || !UploadModel.Files.Any()) return;

            var valid = new List<IFileEntry>();
            var invalid = new List<string>();

            foreach (var f in UploadModel.Files)
            {
                if (cat.IsFileTypeAllowed(Path.GetExtension(f.Name))) valid.Add(f);
                else invalid.Add(f.Name);
            }

            if (invalid.Any())
            {
                UploadModel.Files = valid;
                await NotificationService.Error($"Removed invalid files ({cat.AcceptedFileTypes} only): {string.Join(", ", invalid)}");
            }
        }

        protected void ClearSelectedFiles() => UploadModel.Files = new List<IFileEntry>();

        protected void ClearUploadResults()
        {
            RecentUploadResults.Clear();
            lastUploadedCategoryShortName = null;
            lastUploadedCategoryDisplayValue = null;
        }

        private void ClearUploadResultsIfPresent()
        {
            if (!RecentUploadResults.Any()) return;
            RecentUploadResults.Clear();
            lastUploadedCategoryShortName = null;
            lastUploadedCategoryDisplayValue = null;
        }

        protected void ToggleFilesReadyCollapse() => IsFilesReadyHidden = !IsFilesReadyHidden;

        protected async Task HandleViewFiles()
        {
            if (OnViewFilesWithScope.HasDelegate)
            {
                var currentLineNumber = IsLineMode() ? SelectedLineNumber : null;
                var currentSerialNumber = IsLineMode() ? UploadModel.SerialNumber : null;
                await OnViewFilesWithScope.InvokeAsync((currentLineNumber, currentSerialNumber));
            }
            else if (OnViewFiles.HasDelegate)
            {
                await OnViewFiles.InvokeAsync();
            }
        }

        private void LoadStorageInfo()
        {
            if (!string.IsNullOrEmpty(RmaNumber))
            {
                var lineNumber = IsLineMode() ? SelectedLineNumber : null;
                StorageInfo = RMAFileService.GetStorageInfo(RmaNumber, lineNumber);
            }
        }

        protected async Task CopyToClipboard(string text)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
                await NotificationService.Info("Network path copied to clipboard!");
            }
            catch (Exception ex)
            {
                await NotificationService.Warning($"Could not copy to clipboard. Path: {text}");
            }
        }

        protected async Task HandleValidSubmit()
        {
            if (!CanUploadFiles)
            {
                // Log the permission denial for security audit
                Logger?.LogWarning("Upload attempt denied for user {Username} - insufficient permissions", 
                    HttpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous");
                    
                await NotificationService.Error("You do not have permission to upload files. This feature is restricted to Technical Services users (Crystal Technical Services SG group).");
                return;
            }

            if (HasInvalidFiles())
            {
                await NotificationService.Error("Cannot upload: Some files don't match required file types.");
                return;
            }
            if (string.IsNullOrEmpty(UploadModel.SelectedCategoryShortName))
            {
                await NotificationService.Warning("Please select a file category.");
                return;
            }

            // Perform upload with optional conflict resolution
            await PerformUploadAsync(conflictResolution: null);
        }

        private async Task PerformUploadAsync(string? conflictResolution)
        {
            IsUploading = true;
            RecentUploadResults.Clear();
            await InvokeAsync(StateHasChanged);

            try
            {
                lastUploadedCategoryShortName = UploadModel.SelectedCategoryShortName;
                lastUploadedCategoryDisplayValue = CurrentCategoryList.FirstOrDefault(c => c.ShortName == lastUploadedCategoryShortName)?.DisplayValue;

                var request = new RMAFileUploadRequest
                {
                    RmaNumber = UploadModel.RmaNumber,
                    RmaLineNumber = IsLineMode() ? UploadModel.RmaLineNumber : null,
                    SerialNumber = IsLineMode() ? UploadModel.SerialNumber : null,
                    CategoryShortName = UploadModel.SelectedCategoryShortName!,
                    Files = UploadModel.Files,
                    ConflictResolution = conflictResolution
                };

                var results = await RMAFileService.UploadFilesAsync(request);
                
                // Check if we got a conflict detection response
                if (results.Count == 1 && 
                    results[0].ErrorType == UploadErrorType.ConflictDetected && 
                    results[0].ConflictingFiles != null &&
                    results[0].ConflictingFiles.Any())
                {
                    // Show conflict resolution modal - keep upload as "in progress"
                    // so files don't become stale
                    ConflictingFileNames = results[0].ConflictingFiles;
                    SelectedConflictResolution = null; // Reset selection for fresh start
                    ShowConflictModal = true;
                    // DON'T set IsUploading = false here - keep it true
                    await InvokeAsync(StateHasChanged);
                    return;
                }
                
                RecentUploadResults = results;

                var success = results.Count(r => r.Success);
                var fail = results.Count(r => !r.Success);
                var renamed = results.Count(r => r.WasRenamed);
                var overwritten = results.Count(r => r.WasOverwritten);
                var skipped = results.Count(r => r.ErrorType == UploadErrorType.Skipped);

                if (fail == 0)
                {
                    var message = $"All {success} file(s) uploaded and recorded successfully.";
                    if (renamed > 0)
                        message += $" ({renamed} file(s) were renamed to avoid conflicts)";
                    if (overwritten > 0)
                        message += $" ({overwritten} file(s) were overwritten)";
                        
                    await NotificationService.Success(message);
                    UploadModel.Files = new List<IFileEntry>();
                    await OnUploadComplete.InvokeAsync();
                    await OnUploadFilesSuccess();
                }
                else if (success == 0)
                {
                    var message = $"All {fail} file(s) failed to upload.";
                    if (skipped > 0)
                        message = $"All {skipped} file(s) were skipped (already exist).";
                    await NotificationService.Error(message);
                    
                    foreach (var r in results.Where(r => !r.Success && !string.IsNullOrEmpty(r.ErrorMessage) && r.ErrorType != UploadErrorType.Skipped))
                        await NotificationService.Error($"{r.FileName}: {r.ErrorMessage}");
                }
                else
                {
                    var message = $"Partial success: {success} uploaded, {fail} failed.";
                    if (skipped > 0)
                        message += $" ({skipped} skipped)";
                    if (renamed > 0)
                        message += $" ({renamed} renamed)";
                    if (overwritten > 0)
                        message += $" ({overwritten} overwritten)";
                        
                    await NotificationService.Warning(message);
                    
                    foreach (var r in results.Where(r => !r.Success && !string.IsNullOrEmpty(r.ErrorMessage) && r.ErrorType != UploadErrorType.Skipped))
                        await NotificationService.Error($"{r.FileName}: {r.ErrorMessage}");
                    
                    var failedFileNames = results.Where(r => !r.Success).Select(r => r.FileName).ToHashSet();
                    UploadModel.Files = UploadModel.Files.Where(f => failedFileNames.Contains(f.Name)).ToList();
                    
                    if (success > 0)
                        await OnUploadFilesSuccess();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Upload error for RMA {RmaNumber}", UploadModel.RmaNumber);
                await NotificationService.Error($"Upload error: {ex.Message}");
            }
            finally
            {
                IsUploading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected async Task HandleConflictResolution(string resolution)
        {
            ShowConflictModal = false;
            SelectedConflictResolution = resolution;
            
            // Immediately retry upload with the selected resolution
            // Files should still be valid since IsUploading stayed true
            await PerformUploadAsync(resolution);
            
            await InvokeAsync(StateHasChanged);
        }

        protected void CancelConflictResolution()
        {
            ShowConflictModal = false;
            SelectedConflictResolution = null;
            IsUploading = false; // NOW we can set this to false
            StateHasChanged();
        }

        protected string GetConflictCardClass(string resolutionOption)
        {
            return SelectedConflictResolution == resolutionOption 
                ? "conflict-card-selected" 
                : "conflict-card-unselected";
        }

        protected async Task OnConflictResolutionChanged(string value)
        {
            SelectedConflictResolution = value;
            await InvokeAsync(StateHasChanged);
        }

        protected void PrepareForNextUpload()
        {
            RecentUploadResults.Clear();
            UploadModel.Files = new List<IFileEntry>();
            UploadModel.SelectedCategoryShortName = null;
            StateHasChanged();
        }
    }
}