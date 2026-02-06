using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Pages
{
    public class FilesEmbeddedListPageBase : ComponentBase
    {
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

        // URL Parameters
        protected string? RmaNumber { get; set; }
        protected string? RmaLineNumber { get; set; }
        protected string? SerialNumber { get; set; }
        protected bool ShowAllFiles { get; set; } = false;

        protected override void OnInitialized()
        {
            ParseUrlParameters();
        }

        private void ParseUrlParameters()
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);

            if (query.TryGetValue("rmaNumber", out var rma)) RmaNumber = rma;
            if (query.TryGetValue("rmaLineNumber", out var line)) RmaLineNumber = line;
            if (query.TryGetValue("serialNumber", out var serial)) SerialNumber = serial;
            
            if (query.TryGetValue("showAll", out var showAll))
            {
                ShowAllFiles = showAll.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}