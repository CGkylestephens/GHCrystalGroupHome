using Microsoft.AspNetCore.Components;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using Microsoft.Extensions.Logging;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components
{
    public class RMAFileManagerBase : ComponentBase
    {
        [Inject] protected IRMAFileService RMAFileService { get; set; } = default!;
        [Inject] protected ILogger<RMAFileManagerBase>? Logger { get; set; }

        // Component Parameters
        [Parameter] public string? RmaNumber { get; set; }
        [Parameter] public string? RmaLineNumber { get; set; }
        [Parameter] public string? SerialNumber { get; set; }
        [Parameter] public bool ShowAllFiles { get; set; }
        [Parameter] public string? InitialTab { get; set; }
        [Parameter] public EventCallback<string> OnTabChanged { get; set; }
        [Parameter] public EventCallback<(string? lineNumber, string? serialNumber)> OnLineSerialFilterChanged { get; set; }
        [Parameter] public string? Category { get; set; }

        protected string selectedTab = "upload";
        protected List<FileCategory> availableCategories = new();

        protected override void OnInitialized()
        {
            selectedTab = InitialTab ?? "upload";
        }

        protected override async Task OnParametersSetAsync()
        {
            // Load available categories asynchronously
            await UpdateAvailableCategoriesAsync();
        }

        private async Task UpdateAvailableCategoriesAsync()
        {
            try
            {
                // Get all categories (both header and detail) for the file manager
                var headerCategories = await RMAFileService.GetAvailableCategoriesAsync(false);
                var detailCategories = await RMAFileService.GetAvailableCategoriesAsync(true);
                
                // Combine and deduplicate categories
                availableCategories = headerCategories.Concat(detailCategories)
                    .GroupBy(c => c.ShortName)
                    .Select(g => g.First())
                    .OrderBy(c => c.DisplayValue)
                    .ToList();
                    
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error loading categories in RMAFileManager");
                availableCategories = new();
            }
        }

        protected async Task OnSelectedTabChanged(string newTab)
        {
            selectedTab = newTab;
            Logger?.LogInformation("RMAFileManager tab changed to: {Tab}, categories count: {Count}", newTab, availableCategories.Count);
            if (OnTabChanged.HasDelegate)
                await OnTabChanged.InvokeAsync(newTab);
        }

        // Single unified handler
        protected async Task HandleLineSerialFilterChanged((string? lineNumber, string? serialNumber) arg)
        {
            if (arg.lineNumber == "ALL")
            {
                ShowAllFiles = true;
                RmaLineNumber = null;
                SerialNumber = null;
            }
            else
            {
                ShowAllFiles = false;
                RmaLineNumber = arg.lineNumber;
                SerialNumber = arg.serialNumber;
            }

            // Update available categories when line context changes
            await UpdateAvailableCategoriesAsync();

            // Bubble up if parent chain needs it
            if (OnLineSerialFilterChanged.HasDelegate)
                await OnLineSerialFilterChanged.InvokeAsync(arg);

            StateHasChanged();
        }

        protected Task OnUploadComplete() => Task.CompletedTask;
    }
}