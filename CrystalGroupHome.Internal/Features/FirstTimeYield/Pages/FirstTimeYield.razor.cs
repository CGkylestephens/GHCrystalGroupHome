using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.Internal.Common.Data.Labor;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Pages
{
    public class FirstTimeYieldBase : ComponentBase, IDisposable
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public IFirstTimeYield_Service FTYService { get; set; } = default!;
        [Inject] public JsConsole JsConsole { get; set; } = default!;
        [Inject] public DebugModeService DebugModeService { get; set; } = default!;

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        protected string? CurrentView { get; set; }
        [Parameter] public int? EditId { get; set; }
        [Parameter] public List<FirstTimeYield_AreaDTO> Areas { get; set; } = new();

        // AD Group name - reference central ADGroupRoles for consistency
        public const string FTYAdminRole = ADGroupRoles.FTYAdmin;

        public static bool IsAdmin(ClaimsPrincipal? user)
        {
            return (user?.IsInRole(FTYAdminRole) ?? false) || AccessOverrides.IsInDebugRole(FTYAdminRole);
        }

        public static bool HasEditPermission(ClaimsPrincipal? user)
        {
            return (user?.IsInRole(FTYAdminRole) ?? false) || AccessOverrides.IsInDebugRole(FTYAdminRole);
        }

        protected override async Task OnInitializedAsync()
        {
            // Subscribe to location changes so that the UI updates when the page parameters change
            NavigationManager.LocationChanged += OnLocationChanged;
            DebugModeService.OnChange += StateHasChanged;
            Areas = await FTYService.GetAreasAsync<FirstTimeYield_AreaDTO>();
            // Put "N/A" at the top (Id 12), then order by deleted and alphabetically
            Areas = Areas
                .OrderBy(a => a.Id == 12 ? 0 : 1)
                .ThenBy(a => a.Deleted)
                .ThenBy(a => a.AreaDescription)
                .ToList();
            UpdateView();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && DebugModeService.IsDebugMode)
            {
                await JsConsole.LogAsync("FTY Logged in User: " + CurrentUser?.DBUser.SAMAccountName);
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

            if (string.Equals(path, NavHelpers.FirstTimeYieldMainPage, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "DataGrid";
            }
            else if (string.Equals(path, NavHelpers.FirstTimeYieldAddEntry, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "AddForm";
            }
            else if (string.Equals(path, NavHelpers.FirstTimeYieldAdmin, StringComparison.OrdinalIgnoreCase))
            {
                CurrentView = "AdminTools";
            }
            else if (string.Equals(path, NavHelpers.FirstTimeYieldEditEntry, StringComparison.OrdinalIgnoreCase))
            {
                // Editing without supplying an ID redirects back to the main page
                NavigationManager.NavigateTo(NavHelpers.FirstTimeYieldMainPage);
            }
            else
            {
                // Use regex to match the edit route with exactly one numeric segment after the edit route
                var pattern = $"^{Regex.Escape(NavHelpers.FirstTimeYieldEditEntry)}/(\\d+)$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(path);

                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    CurrentView = "EditForm";
                    EditId = id;
                }
            }

            // Refresh the UI.
            StateHasChanged();
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
            DebugModeService.OnChange -= StateHasChanged;
            GC.SuppressFinalize(this);
        }
    }
}
