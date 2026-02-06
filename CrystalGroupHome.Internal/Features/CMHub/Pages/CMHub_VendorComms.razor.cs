using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.CMHub.VendorComms.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.RegularExpressions;

namespace CrystalGroupHome.Internal.Features.CMHub.Pages
{
    public class CMHub_VendorCommsBase : ComponentBase
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public ICMHub_VendorCommsService VendorCommsService { get; set; } = default!;
        [Inject] public IWebHostEnvironment Environment { get; set; } = default!;

        protected string? CurrentView { get; set; }
        [Parameter] public string? PartNum { get; set; }
        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        // Store the return URL when navigating to tracker details
        private string? _returnUrl;

        protected override void OnInitialized()
        {
            // Subscribe to location changes so that the UI updates when the page parameters change
            NavigationManager.LocationChanged += OnLocationChanged;

            UpdateView();
        }

        protected override void OnParametersSet()
        {
            UpdateView();
            base.OnParametersSet();
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            UpdateView();
        }

        public void NavigateToTrackerGrid()
        {
            // Check if we have a return URL with preserved state
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);

            if (query.TryGetValue("returnUrl", out var returnUrl) && !string.IsNullOrWhiteSpace(returnUrl))
            {
                // Navigate back to the preserved state
                NavigationManager.NavigateTo(Uri.UnescapeDataString(returnUrl));
            }
            else
            {
                // Fallback to main page
                NavigationManager.NavigateTo(NavHelpers.CMHub_VendorCommsMainPage);
            }
        }

        private void UpdateView()
        {
            var path = new Uri(NavigationManager.Uri).AbsolutePath;

            if (string.Equals(path, NavHelpers.CMHub_VendorCommsMainPage, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "TrackerGrid";
                // Store the current URL as return URL when we're on the grid
                _returnUrl = NavigationManager.Uri;
            }
            else if (path.Contains(NavHelpers.CMHub_VendorCommsTrackerDetails))
            {
                // Use regex to match the tracker details route when it has a valid identifier.
                var pattern = $"^{Regex.Escape(NavHelpers.CMHub_VendorCommsTrackerDetails)}/([^/]+)$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(path);

                if (match.Success)
                {
                    CurrentView = "TrackerDetails";
                    // Extract PartNum from the route
                    PartNum = match.Groups[1].Value;
                }
                else
                {
                    // Improperly formatted ID redirects back to the main page
                    NavigationManager.NavigateTo(NavHelpers.CMHub_VendorCommsMainPage);
                }
            }

            // Trigger UI refresh
            StateHasChanged();
        }

        // Method to navigate to tracker details while preserving the current grid state
        public void NavigateToTrackerDetails(string partNum)
        {
            var returnUrl = Uri.EscapeDataString(_returnUrl ?? NavHelpers.CMHub_VendorCommsMainPage);
            NavigationManager.NavigateTo($"{NavHelpers.CMHub_VendorCommsTrackerDetails}/{partNum}?returnUrl={returnUrl}");
        }
    }
}