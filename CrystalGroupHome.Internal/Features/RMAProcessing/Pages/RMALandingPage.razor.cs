using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Pages
{
    [Authorize]
    public class RMALandingPageBase : ComponentBase
    {
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

        protected void NavigateToUpload()
        {
            NavigationManager.NavigateTo("/rma-processing/files/upload");
        }
    }
}