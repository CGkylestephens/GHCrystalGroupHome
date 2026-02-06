using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using CrystalGroupHome.SharedRCL.Helpers;
using System.Text.RegularExpressions;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Labor;

namespace CrystalGroupHome.Internal.Features.CMHub.Pages
{
    public class CMHub_CMDexBase : ComponentBase
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;
        [Inject] public IWebHostEnvironment Environment { get; set; } = default!;

        protected string? CurrentView { get; set; }
        [Parameter] public string? PartNum { get; set; }

        [Parameter] public List<ADUserDTO_Base> PMUsers { get; set; } = [];
        [Parameter] public List<ADUserDTO_Base> SAUsers { get; set; } = [];

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        protected override async Task OnInitializedAsync()
        {
            // Subscribe to location changes so that the UI updates when the page parameters change
            NavigationManager.LocationChanged += OnLocationChanged;

            UpdateView();

            var devs = await ActiveDirectoryHelper.GetGroupUsersAsync("Crystal Software Development SG");
            var devUsers = await ADUserService.GetADUsersBySAMAccountNamesAsync<ADUserDTO_Base>(devs);
            var pms = await ActiveDirectoryHelper.GetGroupUsersAsync("Crystal CMNS PM Users");
            PMUsers = await ADUserService.GetADUsersBySAMAccountNamesAsync<ADUserDTO_Base>(pms);
            var sas = await ActiveDirectoryHelper.GetGroupUsersAsync("Crystal CMNS SA Users");
            SAUsers = await ADUserService.GetADUsersBySAMAccountNamesAsync<ADUserDTO_Base>(sas);

            if (!Environment.IsProduction())
            {
                // Add all devs to both PMUsers and SAUsers for testing purposes
                PMUsers.AddRange(devUsers);
                SAUsers.AddRange(devUsers);
            }
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

            if (string.Equals(path, NavHelpers.CMHub_CMDexMainPage, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "PartGrid";
            }
            else if (string.Equals(path, NavHelpers.CMHub_CMDexPartDetails, StringComparison.OrdinalIgnoreCase))
            {
                // Editing without supplying a PartNum redirects back to the main page.
                NavigationManager.NavigateTo(NavHelpers.CMHub_CMDexMainPage);
            }
            else
            {
                // Use regex to match the part details route when it has a valid part identifier.
                // This pattern matches exactly one non-empty segment after the base and no additional '/' are allowed.
                var pattern = $"^{Regex.Escape(NavHelpers.CMHub_CMDexPartDetails)}/([^/]+)$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(path);

                if (match.Success)
                {
                    CurrentView = "PartDetails";
                }
            }

            // Trigger UI refresh
            StateHasChanged();
        }

        public void NavigateToPartRelations()
        {
            NavigationManager.NavigateTo(NavHelpers.CMHub_CMDexMainPage);
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
            GC.SuppressFinalize(this);
        }
    }
}
