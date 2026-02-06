using Blazorise;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.Internal.Common.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components.Modals
{
    public class EditMetadataModalBase : ComponentBase
    {
        [Inject] protected IRMAFileService RMAFileService { get; set; } = default!;
        [Inject] protected ILogger<EditMetadataModalBase>? Logger { get; set; }
        [Inject] protected IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

        [Parameter] public bool IsVisible { get; set; }
        [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }
        [Parameter] public List<RMAFileAttachmentDTO> FilesToEdit { get; set; } = new();
        [Parameter] public List<RMAFileCategoryDTO> AvailableCategories { get; set; } = new();
        [Parameter] public EventCallback<EditMetadataResult> OnMetadataUpdated { get; set; }
        [Parameter] public EventCallback OnModalClosed { get; set; }

        // Form fields
        protected int? NewRMANumber { get; set; }
        protected int? NewRMALineNumber { get; set; }
        protected int? NewCategoryId { get; set; }
        protected string? ValidationMessage { get; set; }
        protected bool IsUpdateInProgress { get; set; }
        
        // Line change options
        protected string LineChangeAction { get; set; } = "no-change"; // Default to no change
        protected bool ShowLineNumberInput { get; set; } = false;
        
        // Track if user has made changes
        protected bool _hasUserMadeChanges = false;

        protected override void OnParametersSet()
        {
            if (IsVisible && FilesToEdit.Any())
            {
                // ALWAYS populate RMA Number since files can't span multiple RMAs
                var firstFile = FilesToEdit.First();
                NewRMANumber = firstFile.RMANumber;
                
                // For single file, pre-populate category
                if (FilesToEdit.Count == 1)
                {
                    NewCategoryId = firstFile.CategoryId;
                }
                else
                {
                    NewCategoryId = null; // Multiple files - let user choose
                }
                
                // Always start with "No Change" for line number
                LineChangeAction = "no-change";
                NewRMALineNumber = null;
                ShowLineNumberInput = false;
                ValidationMessage = null;
                _hasUserMadeChanges = false;
            }
        }

        protected async Task OnModalClosing(ModalClosingEventArgs e)
        {
            // Clear internal state
            NewRMANumber = null;
            NewRMALineNumber = null;
            NewCategoryId = null;
            ValidationMessage = null;
            IsUpdateInProgress = false;
            LineChangeAction = "no-change";
            ShowLineNumberInput = false;
            _hasUserMadeChanges = false;
            
            // CRITICAL: Tell the parent to hide the modal
            await IsVisibleChanged.InvokeAsync(false);
        }

        protected async Task CloseModal()
        {
            // Clear internal state
            NewRMANumber = null;
            NewRMALineNumber = null;
            NewCategoryId = null;
            ValidationMessage = null;
            IsUpdateInProgress = false;
            LineChangeAction = "no-change";
            ShowLineNumberInput = false;
            _hasUserMadeChanges = false;
            
            // Tell the parent to hide the modal
            await IsVisibleChanged.InvokeAsync(false);
        }

        protected async Task ValidateRMAAndLine()
        {
            ValidationMessage = null;
            
            if (!NewRMANumber.HasValue)
            {
                ValidationMessage = "RMA Number is required.";
                return;
            }

            // Only validate if user is changing to a specific line
            if (LineChangeAction == "change-line" && NewRMALineNumber.HasValue)
            {
                try
                {
                    var validation = await RMAFileService.ValidateRMAAndLineAsync(NewRMANumber.Value, NewRMALineNumber);
                    
                    if (!validation.IsValid)
                    {
                        ValidationMessage = validation.ErrorMessage;
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error validating RMA {RMANumber} and Line {LineNumber}", NewRMANumber, NewRMALineNumber);
                    ValidationMessage = $"Error validating RMA and line: {ex.Message}";
                }
            }
        }

        protected void ValidateFileTypes()
        {
            if (!NewCategoryId.HasValue)
            {
                ValidationMessage = null;
                return;
            }

            var newCategory = AvailableCategories.FirstOrDefault(c => c.Id == NewCategoryId.Value);
            if (newCategory == null)
            {
                ValidationMessage = null;
                return;
            }

            // Check file type compatibility for each file
            var incompatibleFiles = new List<string>();
            
            foreach (var file in FilesToEdit)
            {
                var fileExtension = file.GetFileExtension();
                if (!newCategory.IsFileTypeAllowed(fileExtension))
                {
                    incompatibleFiles.Add($"{file.FileName} ({fileExtension})");
                }
            }

            if (incompatibleFiles.Any())
            {
                var acceptedTypes = newCategory.GetAcceptedExtensions();
                var acceptedTypesText = acceptedTypes.Any() 
                    ? string.Join(", ", acceptedTypes) 
                    : "any file type";

                ValidationMessage = $"The following files are not compatible with the '{newCategory.DisplayValue}' category:\n" +
                                    $"• {string.Join("\n• ", incompatibleFiles)}\n\n" +
                                    $"This category accepts: {acceptedTypesText}";
            }
            else
            {
                ValidationMessage = null;
            }
        }

        protected async Task ValidateHeaderToDetailMove()
        {
            // Special validation for when moving files from header to detail level
            if (LineChangeAction == "change-line" && NewRMALineNumber.HasValue && FilesToEdit.Any())
            {
                foreach (var file in FilesToEdit)
                {
                    // If the file is currently at header level and we're moving to detail level
                    if (!file.RMALineNumber.HasValue)
                    {
                        // If keeping current category, check if that category exists at detail level
                        if (!NewCategoryId.HasValue && file.Category != null)
                        {
                            var currentCategoryForDetail = AvailableCategories.FirstOrDefault(c => 
                                c.ShortName == file.Category.ShortName && c.IsDetailLevel);
                            
                            if (currentCategoryForDetail == null)
                            {
                                ValidationMessage = $"The current category '{file.Category.DisplayValue}' is not available for detail/line level files.\n" +
                                                   "Please select a different category that supports detail level files.";
                                return;
                            }

                            var fileExtension = file.GetFileExtension();
                            if (!currentCategoryForDetail.IsFileTypeAllowed(fileExtension))
                            {
                                var acceptedTypes = currentCategoryForDetail.GetAcceptedExtensions();
                                var acceptedTypesText = acceptedTypes.Any() 
                                    ? string.Join(", ", acceptedTypes) 
                                    : "any file type";

                                ValidationMessage = $"File '{file.FileName}' ({fileExtension}) is not compatible with the current category '{file.Category.DisplayValue}' at detail level.\n" +
                                                   $"Detail level category accepts: {acceptedTypesText}\n\n" +
                                                   "Please select a different category that supports this file type at detail level.";
                                return;
                            }
                        }
                    }
                }
            }
        }

        protected async Task OnRMANumberChanged(int? value)
        {
            NewRMANumber = value;
            _hasUserMadeChanges = true;
            await ValidateRMAAndLine();
            StateHasChanged();
        }

        protected async Task OnLineActionChanged(string value)
        {
            LineChangeAction = value;
            _hasUserMadeChanges = true;
            
            switch (value)
            {
                case "move-to-header":
                    ShowLineNumberInput = false;
                    NewRMALineNumber = null; // Explicitly set to null for header
                    break;
                    
                case "change-line":
                    ShowLineNumberInput = true;
                    NewRMALineNumber = null; // Start blank, let user enter
                    break;
                    
                case "no-change":
                default:
                    ShowLineNumberInput = false;
                    NewRMALineNumber = null; // Will be handled in ConfirmUpdate
                    break;
            }
            
            await ValidateRMAAndLine();
            await ValidateHeaderToDetailMove();
            StateHasChanged();
        }

        protected async Task OnRMALineNumberChanged(int? value)
        {
            NewRMALineNumber = value;
            await ValidateRMAAndLine();
            await ValidateHeaderToDetailMove();
            StateHasChanged();
        }

        protected void OnCategoryChanged(int? value)
        {
            NewCategoryId = value;
            _hasUserMadeChanges = true;
            ValidateFileTypes();
            StateHasChanged();
        }

        protected bool CanConfirmUpdate()
        {
            return _hasUserMadeChanges && 
                   FilesToEdit.Any() && 
                   !IsUpdateInProgress &&
                   string.IsNullOrEmpty(ValidationMessage);
        }

        protected string GetLineActionDisplayText()
        {
            return LineChangeAction switch
            {
                "move-to-header" => "Move to Header Level",
                "change-line" => "Change/Move to Line",
                "no-change" => "No Change",
                _ => "No Change"
            };
        }

        protected async Task ConfirmUpdate()
        {
            if (!CanConfirmUpdate()) return;

            IsUpdateInProgress = true;
            
            try
            {
                EditMetadataResult result;
                
                if (FilesToEdit.Count == 1)
                {
                    // Single file update
                    var file = FilesToEdit.First();
                    
                    // Determine the final line number based on action
                    int? finalLineNumber = LineChangeAction switch
                    {
                        "move-to-header" => null,
                        "change-line" => NewRMALineNumber,
                        "no-change" => file.RMALineNumber, // Keep existing
                        _ => file.RMALineNumber
                    };
                    
                    var request = new FileMetadataUpdateRequest
                    {
                        FileAttachmentId = file.Id,
                        NewRMANumber = NewRMANumber ?? file.RMANumber,
                        NewRMALineNumber = finalLineNumber,
                        NewCategoryId = NewCategoryId,
                        ModifiedByUsername = WebHelpers.GetCurrentUsername(HttpContextAccessor)
                    };

                    var response = await RMAFileService.UpdateFileMetadataAsync(request);
                    result = new EditMetadataResult
                    {
                        Success = response.Success,
                        ErrorMessage = response.ErrorMessage,
                        FilesUpdated = response.Success ? 1 : 0,
                        FilesMoved = response.FileRenamed ? new List<string> { file.FileName } : new List<string>(),
                        FilesSkipped = response.Success ? 0 : 1
                    };

                    if (!response.Success && !string.IsNullOrEmpty(response.ErrorMessage))
                    {
                        ValidationMessage = response.ErrorMessage;
                        StateHasChanged();
                        return;
                    }
                }
                else
                {
                    // Bulk update
                    int? finalLineNumber = LineChangeAction switch
                    {
                        "move-to-header" => null,
                        "change-line" => NewRMALineNumber,
                        "no-change" => null, // Don't change existing line numbers
                        _ => null
                    };
                    
                    var request = new BulkFileMetadataUpdateRequest
                    {
                        FileAttachmentIds = FilesToEdit.Select(f => f.Id).ToList(),
                        NewRMANumber = NewRMANumber,
                        NewRMALineNumber = finalLineNumber,
                        NewCategoryId = NewCategoryId,
                        ModifiedByUsername = WebHelpers.GetCurrentUsername(HttpContextAccessor)
                    };

                    var response = await RMAFileService.UpdateMultipleFilesMetadataAsync(request);
                    result = new EditMetadataResult
                    {
                        Success = response.Success,
                        ErrorMessage = response.ErrorMessage,
                        FilesUpdated = response.FilesUpdated,
                        FilesMoved = response.FilesMoved.Select(fm => fm.OldPath).ToList(),
                        FilesSkipped = response.FilesSkipped
                    };

                    if (!response.Success && !string.IsNullOrEmpty(response.ErrorMessage))
                    {
                        ValidationMessage = response.ErrorMessage;
                        StateHasChanged();
                        return;
                    }
                }

                if (OnMetadataUpdated.HasDelegate)
                    await OnMetadataUpdated.InvokeAsync(result);

                if (result.Success)
                {
                    await CloseModal();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error updating file metadata");
                ValidationMessage = $"Error updating metadata: {ex.Message}";
                
                var errorResult = new EditMetadataResult
                {
                    Success = false,
                    ErrorMessage = $"Error updating metadata: {ex.Message}",
                    FilesUpdated = 0,
                    FilesMoved = new List<string>(),
                    FilesSkipped = FilesToEdit.Count
                };
                
                if (OnMetadataUpdated.HasDelegate)
                    await OnMetadataUpdated.InvokeAsync(errorResult);
            }
            finally
            {
                IsUpdateInProgress = false;
                StateHasChanged();
            }
        }
    }

    public class EditMetadataResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int FilesUpdated { get; set; }
        public List<string> FilesMoved { get; set; } = new();
        public int FilesSkipped { get; set; }
    }
}