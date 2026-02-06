using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Pages
{
    public class FilesEmbeddedPageBase : ComponentBase
    {
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

        // This class now only serves as a redirect handler for the embedded iframe version
        // The actual functionality is moved to FilesEmbeddedListPageBase and FilesEmbeddedUploadPageBase

        protected override void OnInitialized()
        {
            // Parse current URL parameters and redirect to appropriate embedded view
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

            // Check if tab parameter specifies upload
            var targetPage = "/rma-processing/files-embedded/list";  // Default to list view

            if (query.TryGetValue("tab", out var tabValue) &&
                tabValue.ToString().Equals("upload", StringComparison.OrdinalIgnoreCase))
            {
                targetPage = "/rma-processing/files-embedded/upload";
            }

            // Build new URL with same query parameters (minus tab)
            var filteredQuery = query
                .Where(kvp => kvp.Key != "tab")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            var redirectUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(targetPage, filteredQuery!);
            NavigationManager.NavigateTo(redirectUrl, replace: true);
        }
    }
}