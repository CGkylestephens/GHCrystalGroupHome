using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public class ECNHelpers
    {
        private readonly NavigationManager _navigationManager;
        private readonly IJSRuntime _js;

        public const string EcxTestSubdomain = "ecntest";
        public const string EcxProdSubdomain = "ecn";

        public ECNHelpers(
            NavigationManager navigationManager,
            IJSRuntime jSRuntime
            )
        {
            _navigationManager = navigationManager;
            _js = jSRuntime;
        }

        public string GetEcnDirectUrl(bool isDevelopmentOrStaging)
        {
            return isDevelopmentOrStaging
                ? $"https://{EcxTestSubdomain}.crystalrugged.com/Ecn.aspx?id="
                : $"https://{EcxProdSubdomain}.crystalrugged.com/Ecn.aspx?id=";
        }

        public async Task OpenECXForECNNumInNewTabAsync(int? ecnId, bool isDevelopmentOrStaging)
        {
            if (ecnId == null) return; // Prevent opening blank tabs

            // build the URL
            var url = $"{GetEcnDirectUrl(isDevelopmentOrStaging)}{ecnId}";
            // open in a new tab
            try
            {
                await _js.InvokeVoidAsync("open", url, "_blank");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to open new tab: {ex.Message}");
            }
        }
    }
}