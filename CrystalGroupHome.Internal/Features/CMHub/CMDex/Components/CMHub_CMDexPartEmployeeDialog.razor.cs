using Blazorise;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.SharedRCL.Data.Labor;
using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Components
{
    public class CMHub_CMDexPartEmployeeDialogBase : ComponentBase
    {
        [Inject] public IModalService Modal { get; set; } = default!;
        [Parameter] public List<ADUserDTO_Base> PartEmployees { get; set; } = [];
        [Parameter] public EventCallback<ADUserDTO_Base> OnEmployeeSelected { get; set; }
        [Parameter] public PartEmployeeType EmployeeType { get; set; }

        public int DraftPartEmployeeIndex { get; set; } = -1;

        public async Task HandleValidSubmit()
        {
            if (DraftPartEmployeeIndex >= 0 && DraftPartEmployeeIndex < PartEmployees.Count)
            {
                // Invoke the callback with the selected employee
                await OnEmployeeSelected.InvokeAsync(PartEmployees[DraftPartEmployeeIndex]);
            }

            await Modal.Hide();
        }

        public void Cancel()
        {
            Modal.Hide();
        }
    }
}