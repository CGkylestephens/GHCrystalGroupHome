using Microsoft.AspNetCore.Components;
using Blazorise;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Models;

namespace CrystalGroupHome.Internal.Features.CMHub.CustComms.Components
{
    public class CMHub_CustCommsECNModalBase : ComponentBase
    {
        [Parameter] public EventCallback<CMHub_CustCommsPartChangeTaskModel> OnECNSaved { get; set; }

        protected Modal? ModalRef;
        protected CMHub_CustCommsPartChangeTaskModel? PartChangeTask;
        protected string? CurrentECNValue;
        protected bool IsPrompt = false; // Indicates if shown automatically on final status
        protected bool IsSaving = false;

        public Task Show(CMHub_CustCommsPartChangeTaskModel task, bool isPrompt = false)
        {
            PartChangeTask = task;
            CurrentECNValue = task?.ECNNumber ?? string.Empty;
            IsPrompt = isPrompt;
            return ModalRef?.Show() ?? Task.CompletedTask;
        }

        protected async Task SaveChanges()
        {
            if (PartChangeTask != null)
            {
                IsSaving = true;
                StateHasChanged(); // Ensure UI updates to show the overlay

                // Basic trim, add more validation if needed
                PartChangeTask.ECNNumber = CurrentECNValue?.Trim() ?? string.Empty;
                await OnECNSaved.InvokeAsync(PartChangeTask);
            }

            await Hide();
            IsSaving = false;
            StateHasChanged(); // Ensure overlay is removed
        }

        protected Task Hide()
        {
            return ModalRef?.Hide() ?? Task.CompletedTask;
        }

        protected void Cancel()
        {
            Hide();
        }
    }
}