using Microsoft.AspNetCore.Components;
using CrystalGroupHome.Internal.Features.ProductFailures.Data;

namespace CrystalGroupHome.Internal.Features.ProductFailures
{
    public partial class ProductFailuresBase : ComponentBase
    {
        [Inject] public IProductFailureService ProductFailureLogService { get; set; } = default!;

        protected List<ProductFailureDTO> entries = new();
        protected ProductFailureDTO? selectedEntry;
        protected bool isAddEditVisible = false;
        protected bool isConfirmDeleteVisible = false;
        protected string modalTitle = string.Empty;
        protected ProductFailureDTO currentEntry = new();
        protected int deletePendingId = 0;

        protected override async Task OnInitializedAsync()
        {
            await LoadEntries();
        }

        protected async Task LoadEntries()
        {
            entries = await ProductFailureLogService.GetAllAsync();
        }

        protected void ShowAddModal()
        {
            modalTitle = "Add New Product Failure Entry";
            currentEntry = new ProductFailureDTO
            {
                ProductId = string.Empty,
                Failures = 0,
                TotalTested = 0,
                EnteredBy = Environment.UserName
            };
            isAddEditVisible = true;
        }

        protected void ShowEditModal(ProductFailureDTO entry)
        {
            modalTitle = "Edit Product Failure Entry";
            currentEntry = new ProductFailureDTO
            {
                Id = entry.Id,
                ProductId = entry.ProductId,
                Failures = entry.Failures,
                TotalTested = entry.TotalTested,
                EnteredBy = entry.EnteredBy
            };
            isAddEditVisible = true;
        }

        protected void CloseModal()
        {
            isAddEditVisible = false;
        }

        protected async Task SaveEntry()
        {
            if (currentEntry.Id == 0)
            {
                await ProductFailureLogService.InsertAsync(currentEntry);
            }
            else
            {
                await ProductFailureLogService.UpdateAsync(currentEntry);
            }

            await LoadEntries();
            CloseModal();
        }

        protected void ConfirmDelete(int id)
        {
            deletePendingId = id;
            isConfirmDeleteVisible = true;
        }

        protected void CloseConfirmModal()
        {
            isConfirmDeleteVisible = false;
            deletePendingId = 0;
        }

        protected async Task DeleteConfirmed()
        {
            if (deletePendingId != 0)
            {
                await ProductFailureLogService.DeleteAsync(deletePendingId);
                await LoadEntries();
            }

            CloseConfirmModal();
        }
    }
}