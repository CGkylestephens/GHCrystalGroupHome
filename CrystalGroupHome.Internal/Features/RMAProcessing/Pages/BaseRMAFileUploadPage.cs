using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using Microsoft.Extensions.Logging;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Pages
{
    public abstract class BaseRMAFileUploadPage : ComponentBase
    {
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
        [Inject] protected IRMAFileService RMAFileService { get; set; } = default!;
        [Inject] protected ILogger<BaseRMAFileUploadPage>? Logger { get; set; }

        // URL Parameters
        protected string? RmaNumber { get; set; }
        protected string? RmaLineNumber { get; set; }
        protected string? SerialNumber { get; set; }
        protected string? Category { get; set; }

        // Categories for the upload component
        protected List<FileCategory> AvailableCategories { get; set; } = new();

        // Abstract properties to be implemented by derived classes
        protected abstract string ComponentName { get; }
        protected abstract string ListUrlPath { get; }

        protected override void OnInitialized()
        {
            ParseUrlParameters();
        }

        protected override async Task OnParametersSetAsync()
        {
            await LoadAvailableCategoriesAsync();
        }

        private void ParseUrlParameters()
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);

            if (query.TryGetValue("rmaNumber", out var rma)) RmaNumber = rma;
            if (query.TryGetValue("rmaLineNumber", out var line)) RmaLineNumber = line;
            if (query.TryGetValue("serialNumber", out var serial)) SerialNumber = serial;
            if (query.TryGetValue("category", out var cat)) Category = cat;
        }

        private async Task LoadAvailableCategoriesAsync()
        {
            try
            {
                // Always load ALL categories regardless of current line selection
                var headerCategories = await RMAFileService.GetAvailableCategoriesAsync(false);
                var detailCategories = await RMAFileService.GetAvailableCategoriesAsync(true);
                
                // Don't deduplicate - keep both header and detail versions
                AvailableCategories = headerCategories.Concat(detailCategories)
                    .OrderBy(c => c.DisplayValue)
                    .ToList();
                
                // Log all categories with their detail level
                foreach (var cat in AvailableCategories)
                {
                    Logger?.LogInformation("{ComponentName} Category: {ShortName} - {DisplayValue} (IsDetail: {IsDetail})", 
                        ComponentName, cat.ShortName, cat.DisplayValue, cat.IsDetailLevel);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "{ComponentName}: Error loading categories", ComponentName);
                AvailableCategories = new();
            }
        }

        // Virtual methods that can be overridden by derived classes
        protected virtual Task OnUploadComplete()
        {
            // Default behavior: stay on upload form
            return Task.CompletedTask;
        }

        protected Task NavigateToList()
        {
            var listUrl = GetListUrl();
            NavigationManager.NavigateTo(listUrl);
            return Task.CompletedTask;
        }

        protected Task NavigateToListWithScope((string? rmaLineNumber, string? serialNumber) scope)
        {
            var listUrl = GetListUrlWithScope(scope.rmaLineNumber, scope.serialNumber);
            NavigationManager.NavigateTo(listUrl);
            return Task.CompletedTask;
        }

        protected string GetListUrl()
        {
            return GetListUrlWithScope(RmaLineNumber, SerialNumber);
        }

        protected string GetListUrlWithScope(string? rmaLineNumber, string? serialNumber)
        {
            var queryParams = new Dictionary<string, string?>
            {
                ["rmaNumber"] = RmaNumber,
                ["rmaLineNumber"] = rmaLineNumber,
                ["serialNumber"] = serialNumber
            };

            // Filter out null values
            var filteredParams = queryParams
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value);

            return QueryHelpers.AddQueryString(ListUrlPath, filteredParams);
        }
    }
}