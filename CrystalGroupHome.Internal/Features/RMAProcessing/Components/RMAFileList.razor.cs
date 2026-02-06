using Blazorise;
using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.Internal.Features.RMAProcessing.Pages;
using CrystalGroupHome.Internal.Features.RMAProcessing.Components.Modals;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Linq;
using System.IO.Compression;
using CrystalGroupHome.Internal.Common.Helpers;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components
{
    public class RMAFileListBase : ComponentBase
    {
        [Inject] protected INotificationService NotificationService { get; set; } = default!;
        [Inject] protected IRMAFileService RMAFileService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<RMAFileListBase>? Logger { get; set; }
        [Inject] protected IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

        [Parameter] public string? RmaNumber { get; set; }
        [Parameter] public string? RmaLineNumber { get; set; }
        [Parameter] public string? SerialNumber { get; set; }
        [Parameter] public bool ShowAllFiles { get; set; }
        [Parameter] public EventCallback<(string? lineNumber, string? serialNumber)> OnLineSerialFilterChanged { get; set; }
        [Parameter] public EventCallback OnGoToUpload { get; set; }
        [Parameter] public List<FileCategory>? AvailableCategories { get; set; }

        protected string? CategoryFilter { get; set; }
        protected List<RMAFileAttachmentDTO> TrackedFiles { get; set; } = new();
        protected List<RMAFileAttachmentDTO> AllTrackedFiles { get; set; } = new();
        protected List<RmaLineSummary> RmaLines { get; set; } = new();
        protected RmaLineSummary? SelectedLineSummary { get; set; }

        protected bool ShowMultiSerialContext => SelectedLineSummary != null && SelectedLineSummary.Serials.Count > 1;

        protected bool ShowPrintTestLogsModal { get; set; }
        protected bool ShowRMAHistoryModal { get; set; }
        protected bool ShowEditMetadataModal { get; set; }
        
        protected List<RMAFileAttachmentDTO> AvailableTestLogFiles { get; set; } = new();
        private readonly string[] TestLogCategoryShortNames = { "TestBurnInLogs", "TestLogs" };

        protected ConfirmationModal? DeleteConfirmationModal { get; set; }
        protected ConfirmationModal? AccessDeniedModal { get; set; }
        protected RMAFileAttachmentDTO? FileToDelete { get; set; }
        
        protected bool CanDeleteFiles => RMAProcessingBase.HasFileDeletePermission(HttpContextAccessor.HttpContext?.User);
        protected bool CanUploadFiles => RMAProcessingBase.HasFileUploadEditPermission(HttpContextAccessor.HttpContext?.User);
        protected bool CanEditMetadata => RMAProcessingBase.HasFileUploadEditPermission(HttpContextAccessor.HttpContext?.User);

        // Edit Metadata Modal related properties
        protected List<RMAFileAttachmentDTO> FilesToEditMetadata { get; set; } = new();

        // Internal scope: "header", "all", or numeric line
        protected string _scopeValue = "all"; // default to "all"
        private bool _userSetScope;

        protected void CloseAllModals()
        {
            ShowPrintTestLogsModal = false;
            ShowRMAHistoryModal = false;
            ShowEditMetadataModal = false;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!_userSetScope)
            {
                if (ShowAllFiles) _scopeValue = "all";
                else if (!string.IsNullOrEmpty(RmaLineNumber) && int.TryParse(RmaLineNumber, out _))
                    _scopeValue = RmaLineNumber!;
                else
                    _scopeValue = "all"; // default to "all"
            }

            await LoadRMATypeInfo(); 
            await LoadRmaLines();
            await LoadAllAvailableCategories(); 
            await LoadTrackedFiles();
        }

        protected string GetCurrentFilterValue() => _scopeValue;

        protected string GetCurrentFilterDisplayText() => _scopeValue switch
        {
            "all" => "All RMA Files",
            "header" => "RMA Header Files Only",
            var v when int.TryParse(v, out _) =>
                SelectedLineSummary != null
                    ? $"Line {v} ({SelectedLineSummary.SerialDisplay})"
                    : $"Line {v}",
            _ => "All RMA Files" // Default to "All RMA Files"
        };

        protected bool HasLineSerialContext() => int.TryParse(_scopeValue, out _);
        protected bool ShouldShowLineSerialColumns() => _scopeValue == "all";

        protected List<(string value, string label)> GetLineFilterOptions() =>
            RmaLines.Select(l => (l.LineNumber.ToString(),
                $"Line {l.LineNumber} - {(l.Serials.Count switch { 0 => "No Serial", 1 => l.Serials[0], _ => l.Serials[0] + $" (+{l.Serials.Count - 1} more)" })}"))
            .ToList();

        protected async Task OnScopeChanged(ChangeEventArgs e)
        {
            var selected = e.Value?.ToString() ?? "header";
            _userSetScope = true;
            _scopeValue = selected;

            if (selected == "all")
            {
                ShowAllFiles = true;
                RmaLineNumber = null;
                SerialNumber = null;
                if (OnLineSerialFilterChanged.HasDelegate)
                    await OnLineSerialFilterChanged.InvokeAsync(("ALL", null));
            }
            else if (selected == "header")
            {
                ShowAllFiles = false;
                RmaLineNumber = null;
                SerialNumber = null;
                if (OnLineSerialFilterChanged.HasDelegate)
                    await OnLineSerialFilterChanged.InvokeAsync((null, null));
            }
            else if (int.TryParse(selected, out _))
            {
                ShowAllFiles = false;
                RmaLineNumber = selected;
                SerialNumber = null;
                if (OnLineSerialFilterChanged.HasDelegate)
                    await OnLineSerialFilterChanged.InvokeAsync((selected, null));
            }

            await LoadRmaLines();
            await LoadTrackedFiles();
            StateHasChanged();
        }

        /// <summary>
        /// Navigates to the upload page with the appropriate scope based on current file list selection
        /// </summary>
        protected void NavigateToUpload()
        {
            if (!CanUploadFiles)
            {
                // Don't navigate, instead show access denied
                ShowAccessDeniedModal("upload files").ConfigureAwait(false);
                return;
            }

            var queryParams = new Dictionary<string, string?>
            {
                ["rmaNumber"] = RmaNumber,
                ["tab"] = "upload"  // Direct to upload tab
            };

            // Set scope parameters based on current filter selection
            if (_scopeValue == "all")
            {
                // For "All Files" view, route to RMA Header upload (most common case)
                // User can change scope in the upload form if needed
                queryParams["showAll"] = null;  // Don't pass showAll to upload
                queryParams["rmaLineNumber"] = null;
                queryParams["serialNumber"] = null;
            }
            else if (_scopeValue == "header")
            {
                // Already at header level - no additional params needed
                queryParams["rmaLineNumber"] = null;
                queryParams["serialNumber"] = null;
            }
            else if (int.TryParse(_scopeValue, out _))
            {
                // Line-specific - pass the line number
                queryParams["rmaLineNumber"] = _scopeValue;
                queryParams["serialNumber"] = SerialNumber;
            }

            // Filter out null values
            var filteredParams = queryParams
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value);

            // Determine if we're in embedded context by checking current URL
            var currentUri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var isEmbedded = currentUri.AbsolutePath.Contains("/files-embedded");
            
            var uploadUrl = isEmbedded 
                ? QueryHelpers.AddQueryString("/rma-processing/files-embedded/upload", filteredParams)
                : QueryHelpers.AddQueryString("/rma-processing/files/upload", filteredParams);
                
            NavigationManager.NavigateTo(uploadUrl);
        }

        // Add synchronous versions for Razor template usage:
        protected List<FileCategory> CurrentAvailableCategories { get; set; } = new();

        protected List<FileCategory> GetFilteredAvailableCategories()
        {
            // Use AvailableCategories parameter if provided, otherwise use CurrentAvailableCategories
            if (AvailableCategories is { Count: > 0 })
                return AvailableCategories;
                
            return CurrentAvailableCategories;
        }

        protected string GetCategoryDisplayName(string shortName)
        {
            var categories = GetFilteredAvailableCategories();
            return categories.FirstOrDefault(c => c.ShortName == shortName)?.DisplayValue ?? shortName;
        }

        // Keep async versions for component initialization:
        protected async Task<List<FileCategory>> GetFilteredAvailableCategoriesAsync()
        {
            if (AvailableCategories is { Count: > 0 })
                return AvailableCategories;

            return _scopeValue switch
            {
                "header" => await RMAFileService.GetAvailableCategoriesAsync(false),
                "all" => await GetAllDistinctCategoriesAsync(),
                var line when int.TryParse(line, out _) => await RMAFileService.GetAvailableCategoriesAsync(true),
                _ => await RMAFileService.GetAvailableCategoriesAsync(false)
            };
        }

        protected async Task<List<FileCategory>> GetAllDistinctCategoriesAsync()
        {
            var header = await RMAFileService.GetAvailableCategoriesAsync(false);
            var detail = await RMAFileService.GetAvailableCategoriesAsync(true);
            return header.Concat(detail)
                .GroupBy(c => c.ShortName)
                .Select(g => g.First())
                .OrderBy(c => c.DisplayValue)
                .ToList();
        }

        private async Task LoadTrackedFiles()
        {
            // Clear selection when loading new files
            SelectedFileIds.Clear();
            
            TrackedFiles = new();
            AllTrackedFiles = new();
            if (string.IsNullOrEmpty(RmaNumber)) return;

            try
            {
                // Load categories based on scope
                CurrentAvailableCategories = await GetFilteredAvailableCategoriesAsync();
                
                var baseQuery = new RMAFileQuery { RmaNumber = RmaNumber };
                AllTrackedFiles = await RMAFileService.GetTrackedFilesAsync(baseQuery);
                

                if (_scopeValue == "header")
                {
                    TrackedFiles = AllTrackedFiles
                        .Where(f => !f.RMALineNumber.HasValue &&
                                    (string.IsNullOrEmpty(CategoryFilter) || f.Category?.ShortName == CategoryFilter))
                        .ToList(); 
                }
                else if (_scopeValue == "all")
                {
                    TrackedFiles = AllTrackedFiles
                        .Where(f => string.IsNullOrEmpty(CategoryFilter) || f.Category?.ShortName == CategoryFilter)
                        .ToList(); 
                }
                else if (int.TryParse(_scopeValue, out var lineNumber))
                {
                    TrackedFiles = AllTrackedFiles
                        .Where(f => f.RMALineNumber.HasValue && 
                                   f.RMALineNumber.Value == lineNumber &&
                                   (string.IsNullOrEmpty(CategoryFilter) || f.Category?.ShortName == CategoryFilter))
                        .ToList(); 
                }

                // Apply current sorting after filtering
                await ApplySorting();

                LoadStorageInfo();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "LoadTrackedFiles failed for RMA {RmaNumber}", RmaNumber);
                TrackedFiles = new();
                AllTrackedFiles = new();
                CurrentAvailableCategories = new();
            }
        }

        private async Task LoadRmaLines()
        {
            RmaLines = new();
            if (!string.IsNullOrEmpty(RmaNumber))
            {
                try
                {
                    // Use the updated service that handles both Epicor and Legacy RMAs
                    RmaLines = await RMAFileService.GetRmaLinesAsync(RmaNumber);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "Failed to load RMA lines for {RmaNumber}", RmaNumber);
                }
            }

            if (HasLineSerialContext() && int.TryParse(_scopeValue, out var ln))
                SelectedLineSummary = RmaLines.FirstOrDefault(l => l.LineNumber == ln);
            else
                SelectedLineSummary = null;
        }

        protected async Task RefreshFileList()
            {
            await LoadTrackedFiles();
            StateHasChanged();
        }

        protected async Task OnCategoryFilterChanged(string selectedValue)
                {
            CategoryFilter = string.IsNullOrEmpty(selectedValue) ? null : selectedValue;
            await LoadTrackedFiles();
            StateHasChanged();
                    }

        protected bool HasTestLogFiles()
        {
            return _scopeValue switch
            {
                "all" => AllTrackedFiles.Any(f => IsTestLogFile(f)), // All test logs across entire RMA
                "header" => AllTrackedFiles.Where(f => !f.RMALineNumber.HasValue).Any(f => IsTestLogFile(f)), // Header-level test logs only
                var line when int.TryParse(line, out var lineNumber) => AllTrackedFiles.Where(f => f.RMALineNumber == lineNumber).Any(f => IsTestLogFile(f)), // Specific line test logs
                _ => false
            };
        }

        protected bool IsTestLogFile(RMAFileAttachmentDTO file) =>
            TestLogCategoryShortNames.Contains(file.Category?.ShortName, StringComparer.OrdinalIgnoreCase);

        protected async Task OpenPrintTestLogsModal()
        {
            EnsureSingleModal(nameof(ShowPrintTestLogsModal));
            
            // Load test log files based on current scope
            AvailableTestLogFiles = _scopeValue switch
            {
                "all" => AllTrackedFiles.Where(f => IsTestLogFile(f)).OrderBy(f => f.FileName).ToList(),
                "header" => AllTrackedFiles.Where(f => !f.RMALineNumber.HasValue && IsTestLogFile(f)).OrderBy(f => f.FileName).ToList(),
                var line when int.TryParse(line, out var lineNumber) => AllTrackedFiles.Where(f => f.RMALineNumber == lineNumber && IsTestLogFile(f)).OrderBy(f => f.FileName).ToList(),
                _ => new List<RMAFileAttachmentDTO>()
            };

            ShowPrintTestLogsModal = true;
            await InvokeAsync(StateHasChanged);
        }

        protected string GetPrintModalScopeDescription()
        {
            return _scopeValue switch
            {
                "all" => "All Files",
                "header" => "Header Files",
                var line when int.TryParse(line, out _) => $"Line {line} Files",
                _ => "Files"
            };
        }

        protected string? GetPrintModalLineNumber()
        {
            return int.TryParse(_scopeValue, out _) ? _scopeValue : null;
        }

        protected string GetDownloadUrl(RMAFileAttachmentDTO file) =>
            $"{NavHelpers.FileServe}{Uri.EscapeDataString(file.FilePath)}&ct={RMAFileService.GetContentType(file.FileName)}";

        protected string GetDownloadTooltip(string fileName) => $"Click to download {fileName}";
        protected string GetDeleteTooltip(string fileName) => CanDeleteFiles ? $"Delete {fileName}" : $"Delete {fileName} (Permission required)";

        protected async Task ConfirmDeleteFile(RMAFileAttachmentDTO file)
        {
            if (!CanDeleteFiles) 
            {
                if (AccessDeniedModal != null)
                    await AccessDeniedModal.ShowAsync(errorMode: true);
                return;
            }
            FileToDelete = file;
            if (DeleteConfirmationModal != null)
                await DeleteConfirmationModal.ShowAsync();
        }

        protected async Task OnDeleteConfirmation(bool confirmed)
        {
            if (confirmed && FileToDelete != null && CanDeleteFiles)
            {
                var success = await RMAFileService.DeleteFileAsync(FileToDelete.FilePath);
                if (success) await RefreshFileList();
                }
            FileToDelete = null;
        }

        protected Task OnAccessDeniedModalClose(bool _) => Task.CompletedTask;
        
        private string? _accessDeniedOperation;
        private void SetAccessDeniedMessage(string operation) => _accessDeniedOperation = operation;
        
        protected string GetAccessDeniedMessage() => _accessDeniedOperation switch
        {
            "upload files" => "You do not have permission to upload files.<br/><br/>This feature is restricted to Technical Services users (Crystal Technical Services SG group).",
            "edit file metadata" => "You do not have permission to edit file metadata.<br/><br/>This feature is restricted to Technical Services users (Crystal Technical Services SG group).",
            _ => "You do not have permission to delete files.<br/><br/>Only Technical Services Coordinators can delete files."
        };

        protected async Task ViewRMAHistory()
        {
            EnsureSingleModal(nameof(ShowRMAHistoryModal));
            ShowRMAHistoryModal = true;
            await InvokeAsync(StateHasChanged);
        }

        protected HashSet<int> SelectedFileIds { get; set; } = new();
        protected List<RMAFileAttachmentDTO> SelectedFiles => TrackedFiles.Where(f => SelectedFileIds.Contains(f.Id)).ToList();

        protected ConfirmationModal? MultiDeleteConfirmationModal { get; set; }

        // Multi-file selection methods
        protected bool IsFileSelected(RMAFileAttachmentDTO file) => SelectedFileIds.Contains(file.Id);
        protected bool IsAllFilesSelected() => TrackedFiles.Any() && TrackedFiles.All(f => SelectedFileIds.Contains(f.Id));

        protected void OnFileSelectionChanged(RMAFileAttachmentDTO file, bool isSelected)
        {
            if (isSelected)
                SelectedFileIds.Add(file.Id);
            else
                SelectedFileIds.Remove(file.Id);
            
            StateHasChanged();
        }

        protected void OnSelectAllChanged(ChangeEventArgs e)
        {
            var isSelected = (bool)(e.Value ?? false);
            
            if (isSelected)
            {
                // Select all visible files
                foreach (var file in TrackedFiles)
                    SelectedFileIds.Add(file.Id);
            }
            else
            {
                // Deselect all files
                SelectedFileIds.Clear();
            }
            
            StateHasChanged();
        }

        protected void ClearSelection()
        {
            SelectedFileIds.Clear();
            StateHasChanged();
        }

        // Multi-file actions
        protected async Task DownloadSelectedFiles()
        {
            if (!SelectedFiles.Any()) return;

            try
            {
                // Handle single file download differently - use direct navigation
                if (SelectedFiles.Count == 1)
                {
                    var singleFile = SelectedFiles.First();
                    var downloadUrl = GetDownloadUrl(singleFile);

                    // Use direct navigation instead of fetch for single files
                    await JSRuntime.InvokeVoidAsync("downloadFileDirect", downloadUrl);
                    await NotificationService.Success($"Download started for {singleFile.FileName}");
                    
                    Logger?.LogInformation("Single file download initiated for {FileName} from RMA {RmaNumber}", 
                        singleFile.FileName, RmaNumber);
                    return;
                }

                // Handle multiple files with zip archive 
                await NotificationService.Info($"Preparing download for {SelectedFiles.Count} files...");

                var request = new BulkDownloadRequest
                {
                    RmaNumber = RmaNumber!,
                    FileIds = SelectedFiles.Select(f => f.Id).ToList(),
                    ArchiveName = $"RMA_{RmaNumber}_Files_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                Logger?.LogInformation("Creating bulk download for {FileCount} files from RMA {RmaNumber}", 
                    SelectedFiles.Count, RmaNumber);

                var result = await RMAFileService.CreateBulkDownloadAsync(request);
                
                if (result.Success && result.DownloadUrl != null)
                {
                    await JSRuntime.InvokeVoidAsync("downloadFileFromUrl", result.DownloadUrl, $"{request.ArchiveName}.zip");
                    await NotificationService.Success($"Download started for {SelectedFiles.Count} files ({FileHelpers.FormatFileSize(result.ArchiveSize)})");
                    
                    Logger?.LogInformation("Successfully initiated bulk download for RMA {RmaNumber}", RmaNumber);
                }
                else
                {
                    Logger?.LogError("Bulk download failed for RMA {RmaNumber}: {Error}", RmaNumber, result.ErrorMessage);
                    await NotificationService.Error($"Failed to create download: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error downloading selected files for RMA {RmaNumber}", RmaNumber);
                await NotificationService.Error($"Error downloading files: {ex.Message}");
            }
        }

        protected async Task ConfirmDeleteSelectedFiles()
        {
            if (!SelectedFiles.Any() || !CanDeleteFiles) return;
            
            if (MultiDeleteConfirmationModal != null)
                await MultiDeleteConfirmationModal.ShowAsync();
        }

        protected async Task OnMultiDeleteConfirmation(bool confirmed)
        {
            if (confirmed && SelectedFiles.Any() && CanDeleteFiles)
            {
                var successCount = 0;
                var errorCount = 0;

                foreach (var file in SelectedFiles.ToList()) // ToList to avoid modification during iteration
                {
                    var success = await RMAFileService.DeleteFileAsync(file.FilePath);
                    if (success)
                    {
                        successCount++;
                        SelectedFileIds.Remove(file.Id); // Remove from selection
                    }
                    else
                    {
                        errorCount++;
                    }
                }

                if (successCount > 0)
                {
                    await NotificationService.Success($"Successfully deleted {successCount} file(s).");
                    await RefreshFileList(); // Refresh the list
                }
                
                if (errorCount > 0)
                {
                    await NotificationService.Warning($"Failed to delete {errorCount} file(s).");
                }
            }
        }

        // Use the shared FileHelpers.FormatFileSize method
        protected string FormatTotalFileSize(long totalBytes) => FileHelpers.FormatFileSize(totalBytes);

        protected string FormatFileSize(long bytes) => FileHelpers.FormatFileSize(bytes);

        protected List<RMAFileCategoryDTO> AllAvailableCategories { get; set; } = new();

        private async Task LoadAllAvailableCategories()
        {
            try
            {
                // Load both header and detail categories
                var headerCategories = await RMAFileService.GetFileCategoriesAsync(false);
                var detailCategories = await RMAFileService.GetFileCategoriesAsync(true);
                
                // Combine and deduplicate
                AllAvailableCategories = headerCategories.Concat(detailCategories)
                    .GroupBy(c => c.ShortName)
                    .Select(g => g.First())
                    .OrderBy(c => c.DisplayValue)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error loading all available categories");
                AllAvailableCategories = new();
            }
        }

        protected async Task OpenEditMetadataModal()
        {
            if (!SelectedFiles.Any()) return;

            if (!CanEditMetadata)
            {
                await ShowAccessDeniedModal("edit file metadata");
                return;
            }

            EnsureSingleModal(nameof(ShowEditMetadataModal));
            FilesToEditMetadata = SelectedFiles.ToList();
            ShowEditMetadataModal = true;
            await InvokeAsync(StateHasChanged);
        }

        protected async Task OpenSingleFileEditMetadataModal(RMAFileAttachmentDTO file)
        {
            // Check permission before opening modal
            if (!CanEditMetadata)
            {
                await ShowAccessDeniedModal("edit file metadata");
                return;
            }

            CloseAllModals(); // Ensure no other modals are open
            FilesToEditMetadata = new List<RMAFileAttachmentDTO> { file };
            ShowEditMetadataModal = true;
            await InvokeAsync(StateHasChanged);
        }

        protected async Task ShowAccessDeniedModal(string operation)
        {
            if (AccessDeniedModal != null)
            {
                // Set a custom message for the specific operation
                SetAccessDeniedMessage(operation);
                await AccessDeniedModal.ShowAsync(errorMode: true);
            }
        }

        protected async Task OnEditMetadataResult(EditMetadataResult result)
        {
            if (result.Success)
            {
                var message = $"Metadata updated for {result.FilesUpdated} files.";
                if (result.FilesMoved.Any())
                {
                    message += $" {result.FilesMoved.Count} files were moved to new directories.";
                }
                if (result.FilesSkipped > 0)
                {
                    message += $" {result.FilesSkipped} files could not be updated.";
                    await NotificationService.Warning(message);
                }
                else
                {
                    await NotificationService.Success(message);
                }
                
                await RefreshFileList();
            }
            else
            {
                await NotificationService.Error(result.ErrorMessage ?? "Failed to update file metadata. Please try again.");
            }
        }

        // sorting properties
        protected string CurrentSortBy { get; set; } = "UploadedDate";
        protected string CurrentSortDirection { get; set; } = "desc";

        // Default sort settings
        private const string DefaultSortBy = "UploadedDate";
        private const string DefaultSortDirection = "desc";

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
                CurrentSortDirection = fieldName == "UploadedDate" ? "desc" : "asc";
            }

            await ApplySorting();
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

        protected bool IsDefaultSort()
        {
            return CurrentSortBy == DefaultSortBy && CurrentSortDirection == DefaultSortDirection;
        }

        protected async Task ResetSortToDefault()
        {
            CurrentSortBy = DefaultSortBy;
            CurrentSortDirection = DefaultSortDirection;
            await ApplySorting();
            StateHasChanged();
        }

        private Task ApplySorting()
        {
            if (!TrackedFiles.Any()) return Task.CompletedTask;

            TrackedFiles = CurrentSortBy switch
            {
                "FileName" => CurrentSortDirection == "asc" 
                    ? TrackedFiles.OrderBy(f => f.FileName).ToList()
                    : TrackedFiles.OrderByDescending(f => f.FileName).ToList(),
                    
                "Category" => CurrentSortDirection == "asc" 
                    ? TrackedFiles.OrderBy(f => f.Category?.DisplayValue ?? "").ToList()
                    : TrackedFiles.OrderByDescending(f => f.Category?.DisplayValue ?? "").ToList(),
                    
                "RMALineNumber" => CurrentSortDirection == "asc" 
                    ? TrackedFiles.OrderBy(f => f.RMALineNumber ?? int.MaxValue).ToList()
                    : TrackedFiles.OrderByDescending(f => f.RMALineNumber ?? int.MinValue).ToList(),
                    
                "SerialNumber" => CurrentSortDirection == "asc" 
                    ? TrackedFiles.OrderBy(f => f.SerialNumber ?? "").ToList()
                    : TrackedFiles.OrderByDescending(f => f.SerialNumber ?? "").ToList(),
                    
                "UploadedDate" => CurrentSortDirection == "asc" 
                    ? TrackedFiles.OrderBy(f => f.UploadedDate).ToList()
                    : TrackedFiles.OrderByDescending(f => f.UploadedDate).ToList(),
                    
                "UploadedByUsername" => CurrentSortDirection == "asc" 
                    ? TrackedFiles.OrderBy(f => f.UploadedByUsername).ToList()
                    : TrackedFiles.OrderByDescending(f => f.UploadedByUsername).ToList(),
                    
                "FileSize" => CurrentSortDirection == "asc" 
                    ? TrackedFiles.OrderBy(f => f.FileSize).ToList()
                    : TrackedFiles.OrderByDescending(f => f.FileSize).ToList(),
                    
                _ => TrackedFiles.OrderByDescending(f => f.UploadedDate).ToList()
            };

            return Task.CompletedTask;
        }

        protected RMAFileStorageInfo? StorageInfo { get; set; }

        private void LoadStorageInfo()
        {
            if (!string.IsNullOrEmpty(RmaNumber))
            {
                var lineNumber = HasLineSerialContext() ? _scopeValue : null;
                StorageInfo = RMAFileService.GetStorageInfo(RmaNumber, lineNumber);
            }
        }

        // Method to ensure only one modal is open
        private void EnsureSingleModal(string modalToShow)
        {
            switch (modalToShow)
            {
                case nameof(ShowPrintTestLogsModal):
                    ShowRMAHistoryModal = false;
                    ShowEditMetadataModal = false;
                    FilesToEditMetadata.Clear();
                    break;
                    
                case nameof(ShowRMAHistoryModal):
                    ShowPrintTestLogsModal = false;
                    ShowEditMetadataModal = false;
                    AvailableTestLogFiles.Clear();
                    FilesToEditMetadata.Clear();
                    break;
                    
                case nameof(ShowEditMetadataModal):
                    ShowPrintTestLogsModal = false;
                    ShowRMAHistoryModal = false;
                    AvailableTestLogFiles.Clear();
                    break;
            }
        }

        protected void OnPrintTestLogsModalVisibilityChanged(bool isVisible)
        {
            ShowPrintTestLogsModal = isVisible;
            if (!isVisible)
            {
                AvailableTestLogFiles.Clear();
            }
        }

        protected void OnRMAHistoryModalVisibilityChanged(bool isVisible)
        {
            ShowRMAHistoryModal = isVisible;
        }

        protected void OnEditMetadataModalVisibilityChanged(bool isVisible)
        {
            ShowEditMetadataModal = isVisible;
            if (!isVisible)
            {
                FilesToEditMetadata.Clear();
            }
        }

        protected bool? IsLegacyRMA { get; set; }
        protected string RMATypeDisplay => IsLegacyRMA == true ? "Legacy" : IsLegacyRMA == false ? "Epicor" : "Unknown";

        private async Task LoadRMATypeInfo()
        {
            if (!string.IsNullOrEmpty(RmaNumber))
            {
                try
                {
                    var rmaSummary = await RMAFileService.GetRMASummaryAsync(int.Parse(RmaNumber));
                    IsLegacyRMA = rmaSummary?.IsLegacyRMA;
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "Failed to load RMA type info for {RmaNumber}", RmaNumber);
                    IsLegacyRMA = null;
                }
            }
        }
    }
}