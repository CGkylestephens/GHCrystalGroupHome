using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using System.Text.RegularExpressions;

namespace CrystalGroupHome.Internal.Features.CMHub.Pages
{
    public class CMHub_CMNotifBase : ComponentBase
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

        protected string? CurrentView { get; set; }

        [Parameter] public string? ECNNumber { get; set; }

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

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

        private void UpdateView()
        {
            var path = new Uri(NavigationManager.Uri).AbsolutePath;

            if (string.Equals(path, NavHelpers.CMHub_CMNotifMainPage, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "RecordGrid";
            }
            else if (string.Equals(path, NavHelpers.CMHub_CMNotifRecordDetails, StringComparison.OrdinalIgnoreCase))
            {
                // Editing without supplying a PartNum redirects back to the main page.
                NavigationManager.NavigateTo(NavHelpers.CMHub_CMNotifMainPage);
            }
            else
            {
                // Use regex to match the part details route when it has a valid part identifier.
                // This pattern matches exactly one non-empty segment after the base and no additional '/' are allowed.
                var pattern = $"^{Regex.Escape(NavHelpers.CMHub_CMNotifRecordDetails)}/([^/]+)$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(path);

                if (match.Success)
                {
                    CurrentView = "RecordDetails";
                }
            }

            // Trigger UI refresh
            StateHasChanged();
        }

        public void NavigateToCMNotifications()
        {
            NavigationManager.NavigateTo(NavHelpers.CMHub_CMNotifMainPage);
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
            GC.SuppressFinalize(this);
        }
    }
}
