using Blazorise;
using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Components
{
    public class CMHub_CMDexPartCustomerContactDialogBase : ComponentBase
    {
        [Inject] public IModalService Modal { get; set; } = default!;
        [Inject] public ICustomerService CustomerService { get; set; } = default!;
        [Parameter] public CMHub_PartEmployeeDTO? PartEmployee { get; set; }
        [Parameter] public List<CustomerContactDTO_Base> PartCustomerContacts { get; set; } = [];
        [Parameter] public EventCallback<(CMHub_PartEmployeeDTO, CustomerContactDTO_Base)> OnCustomerContactSelected { get; set; }
        public DataGrid<CustomerContactDTO_Base>? CustomerContactGrid { get; set; }
        public int DraftPartCustomerContactIndex { get; set; } = -1;
        public bool IsLoading = false;
        //private CustomerContactDTO_Base? selectedContact;

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            try
            {
                await LoadData();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadData()
        {
            PartCustomerContacts = await CustomerService.GetCustomerContactsAsync<CustomerContactDTO_Base>();
        }

        public async Task HandleValidSubmit()
        {
            if (CustomerContactGrid != null && CustomerContactGrid.SelectedRow != null && PartEmployee != null)
            {
                // Invoke the callback with the selected customer contact
                await OnCustomerContactSelected.InvokeAsync((PartEmployee, CustomerContactGrid.SelectedRow));
            }
            await Modal.Hide();
        }

        public void Cancel()
        {
            Modal.Hide();
        }

        public void OnFilterChanged(DataGridFilteredDataEventArgs<CustomerContactDTO_Base> e)
        {
            var selectedRow = CustomerContactGrid?.SelectedRow;
            // If we have a selected contact, check if it's still in the filtered data
            if (selectedRow != null)
            {
                // Get the current filtered data
                var filteredData = CustomerContactGrid?.FilteredData.ToList() ?? [];

                // If the selected contact is not in the filtered results, deselect it
                if (!filteredData.Contains(selectedRow))
                {
                    // Clear the selection in the grid
                    if (CustomerContactGrid != null)
                    {
                        CustomerContactGrid.Reload();
                        DraftPartCustomerContactIndex = -1;
                        CustomerContactGrid?.Select(null);
                    }
                }
            }
        }
    }
}