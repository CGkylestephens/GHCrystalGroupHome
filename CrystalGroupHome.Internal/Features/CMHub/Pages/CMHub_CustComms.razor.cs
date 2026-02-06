using CrystalGroupHome.Internal.Authorization;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using System.Text.RegularExpressions;

namespace CrystalGroupHome.Internal.Features.CMHub.Pages
{
    public class CMHub_CustCommsBase : ComponentBase
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public ICMHub_CustCommsService CustCommsService { get; set; } = default!;
        [Inject] public IAuthorizationService AuthorizationService { get; set; } = default!;
        [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

        protected string? CurrentView { get; set; }
        [Parameter] public string? PartNum { get; set; }
        [Parameter] public int? TaskId { get; set; }
        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        public List<CMHub_CustCommsPartChangeTaskStatusDTO> Statuses { get; set; } = [];

        protected bool HasEditPermission { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            // Subscribe to location changes so that the UI updates when the page parameters change
            NavigationManager.LocationChanged += OnLocationChanged;

            await CheckAuthorizationAsync();

            UpdateView();
        }

        private async Task CheckAuthorizationAsync()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var result = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubCustCommsEdit);

            HasEditPermission = result.Succeeded;
        }

        protected override async Task OnParametersSetAsync()
        {
            Statuses = await CustCommsService.GetTaskStatusesAsync();

            UpdateView();

            base.OnParametersSet();
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            UpdateView();
        }

        public void NavigateToTrackerGrid()
        {
            NavigationManager.NavigateTo(NavHelpers.CMHub_CustCommsMainPage);
        }

        private void UpdateView()
        {
            var path = new Uri(NavigationManager.Uri).AbsolutePath;

            if (string.Equals(path, NavHelpers.CMHub_CustCommsMainPage, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "TrackerGrid";
            }
            else if (string.Equals(path, NavHelpers.CMHub_CustCommsTrackerDetails, StringComparison.OrdinalIgnoreCase))
            {
                NavigationManager.NavigateTo(NavHelpers.CMHub_CustCommsMainPage);
            }
            else if (string.Equals(path, NavHelpers.CMHub_CustCommsAddTracker, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "TrackerAdd";
            }
            else if (path.Contains(NavHelpers.CMHub_CustCommsTrackerDetails))
            {
                // Use regex to match the part details route when it has a valid part identifier.
                // This pattern matches exactly one non-empty segment after the base and no additional '/' are allowed.
                var pattern = $"^{Regex.Escape(NavHelpers.CMHub_CustCommsTrackerDetails)}/([^/]+)$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(path);

                if (match.Success)
                {
                    CurrentView = "TrackerDetails";
                }
                else
                {
                    // Improperly formatted ID redirects back to the main page
                    NavigationManager.NavigateTo(NavHelpers.CMHub_CustCommsMainPage);
                }
            }
            else if (path.Contains(NavHelpers.CMHub_CustCommsAddTracker))
            {
                if (!HasEditPermission)
                {
                    // Don't have permission to access the Add Tracker page
                    NavigationManager.NavigateTo(NavHelpers.CMHub_CustCommsMainPage);
                    return;
                }

                // Use regex to match the part details route when it has a valid part identifier.
                // This pattern matches exactly one non-empty segment after the base and no additional '/' are allowed.
                var pattern = $"^{Regex.Escape(NavHelpers.CMHub_CustCommsAddTracker)}/([^/]+)$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(path);

                if (match.Success)
                {
                    CurrentView = "TrackerAdd";
                }
                else
                {
                    // Improperly formatted ID redirects back to the main page
                    NavigationManager.NavigateTo(NavHelpers.CMHub_CustCommsMainPage);
                }
            }
            else if (string.Equals(path, NavHelpers.CMHub_CustCommsTaskList, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "TaskGrid";
            }
            else if (path.Contains(NavHelpers.CMHub_CustCommsTaskDetails))
            {
                // Use regex to match the part details route when it has a valid task identifier.
                // This pattern matches exactly one non-empty segment after the base and no additional '/' are allowed.
                var pattern = $"^{Regex.Escape(NavHelpers.CMHub_CustCommsTaskDetails)}/([^/]+)$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(path);

                if (match.Success)
                {
                    CurrentView = "TaskDetails";
                }
                else
                {
                    // Improperly formatted ID redirects back to the task list
                    NavigationManager.NavigateTo(NavHelpers.CMHub_CustCommsTaskList);
                }
            }

            // Trigger UI refresh
            StateHasChanged();
        }
    }
}
