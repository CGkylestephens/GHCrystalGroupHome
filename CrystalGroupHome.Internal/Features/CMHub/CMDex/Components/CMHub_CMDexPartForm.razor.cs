using Blazorise;
using CrystalGroupHome.Internal.Authorization;
using CrystalGroupHome.SharedRCL.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.Internal.Common.Data._Epicor;
using CrystalGroupHome.Internal.Common.Data.Parts;

namespace CrystalGroupHome.Internal.Features.CMHub.CMDex.Components
{
    public partial class CMHub_CMDexPartFormBase : ComponentBase
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public IModalService Modal { get; set; } = default!;
        [Inject] public ICMHub_CMDexService CMDexService { get; set; } = default!;
        [Inject] public ICustomerService CustomerService { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;
        [Inject] public IEpicorPartService EpicorPartService { get; set; } = default!;
        [Inject] public IPartService PartService { get; set; } = default!;
        [Inject] public IAuthorizationService AuthorizationService { get; set; } = default!;
        [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

        [Parameter] public string? PartNum { get; set; }
        [Parameter] public List<ADUserDTO_Base> PMUsers { get; set; } = new();
        [Parameter] public List<ADUserDTO_Base> SAUsers { get; set; } = new();

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        public CMHub_CMDexPartModel? PartModel { get; set; }
        public CMHub_PartEmployeeDTO? PartEmployeeToDelete { get; set; }
        public ConfirmationModal? DeleteEmployeeConfirmationModal { get; set; }
        public CMHub_PartCustomerContactDTO? PartCustomerContactToDelete { get; set; }
        public ConfirmationModal? DeleteCustomerContactConfirmationModal { get; set; }
        public string DeleteConfirmationMessage { get; set; } = string.Empty;
        public bool IsLoading = false;
        public bool EditMode = false;
        public bool HasUnsavedChanges = false;
        protected bool HasEditPermission { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            try
            {
                await CheckAuthorizationAsync();
                await LoadData();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CheckAuthorizationAsync()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var result = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubCMDexEdit);

            HasEditPermission = result.Succeeded;
        }

        private async Task LoadData()
        {
            PartModel = PartNum != null ? await CMDexService.GetCMDexPartAsync(PartNum) : null;
            if (PartModel != null)
            {
                PartModel.PartActivity = PartNum != null ? await PartService.GetPartActivityAsync(PartNum) : null;
            }
        }

        public void OnPrimaryPMCheckedChanged(bool? isChecked, CMHub_PartEmployeeDTO partEmployee)
        {
            if (PartModel == null) return;

            // There can only be one...
            // So we uncheck all the other PMs
            if (isChecked == true)
            {
                foreach (var pm in PartModel.PartEmployees)
                {
                    pm.IsPrimary = pm == partEmployee;

                    foreach (var pcc in PartModel.PartCustomerContacts)
                    {
                        if (pcc.IsOwner.HasValue && pcc.IsOwner.Value && pcc.PartEmployeeId != pm.Id)
                        {
                            pcc.IsOwner = false;
                        }
                    }
                }
            }
            else
            {
                partEmployee.IsPrimary = false;

                // This prevents orphaned owners. Only Primary PMs can have Customer Owners.
                foreach (var pcc in PartModel.PartCustomerContacts)
                {
                    pcc.IsOwner = false;
                }
            }

            // Sort PartEmployees so that primary employees come first
            PartModel.PartEmployees = PartModel.PartEmployees
                .OrderByDescending(pe => pe.IsPrimary)
                .ToList();

            HasUnsavedChanges = true;
            StateHasChanged();
        }

        public void OnCustomerOwnerCheckedChanged(bool? isChecked, CMHub_PartCustomerContactDTO partCustomerContact)
        {
            if (PartModel == null) return;

            // There can only be one...
            // So we uncheck all the other owners
            if (isChecked == true)
            {
                foreach (var pcc in PartModel.PartCustomerContacts)
                {
                    pcc.IsOwner = pcc == partCustomerContact;
                }
            }
            else
            {
                partCustomerContact.IsOwner = false;
            }

            HasUnsavedChanges = true;
            StateHasChanged();
        }

        protected void OnCMManagedChanged(bool isChecked)
        {
            if (PartModel == null) return;

            PartModel.Part.CMManaged_c = isChecked;
            HasUnsavedChanges = true;
        }

        public async Task OpenPartEmployeeDialog(PartEmployeeType type)
        {
            if (HasUnsavedChanges) await SaveChanges();

            var usersToPass = type == PartEmployeeType.PM ? PMUsers : SAUsers;

            // If users aren't loaded yet, show a loading modal and wait
            if (usersToPass == null || usersToPass.Count == 0)
            {
                var loadingModalInstance = await Modal.Show<LoadingModal>(options =>
                    options.Add(p => p.Message, $"Loading {type} list..."),
                    new ModalInstanceOptions { UseModalStructure = false, Centered = true }
                );

                try
                {
                    var startTime = DateTime.Now;
                    var timeout = TimeSpan.FromSeconds(10);

                    while ((usersToPass == null || usersToPass.Count == 0) &&
                           DateTime.Now - startTime < timeout)
                    {
                        await Task.Delay(100);
                        usersToPass = type == PartEmployeeType.PM ? PMUsers : SAUsers;
                    }
                }
                finally
                {
                    await Modal.Hide(loadingModalInstance);
                }
            }

            await Modal.Show<CMHub_CMDexPartEmployeeDialog>(dlg =>
            {
                dlg.Add(p => p.PartEmployees, usersToPass);
                dlg.Add(p => p.EmployeeType, type);
                dlg.Add(p => p.OnEmployeeSelected, EventCallback.Factory.Create<ADUserDTO_Base>(this, employee => HandleEmployeeSelected(employee, type)));
            },
            new ModalInstanceOptions { UseModalStructure = false, Centered = true });
        }

        public async Task OpenCustomerContactDialog(CMHub_PartEmployeeDTO partEmployee)
        {
            if (HasUnsavedChanges) await SaveChanges();

            await Modal.Show<CMHub_CMDexPartCustomerContactDialog>(dlg =>
            {
                dlg.Add(p => p.PartEmployee, partEmployee);
                dlg.Add(p => p.OnCustomerContactSelected, EventCallback.Factory.Create<(CMHub_PartEmployeeDTO, CustomerContactDTO_Base)>(this, tuple => HandleCustomerContactSelected(tuple.Item1, tuple.Item2)));
            },
            new ModalInstanceOptions { UseModalStructure = false, Centered = true, Class = "wide-modal-75" });
        }

        protected async Task ConfirmNavigationAsync(LocationChangingContext context)
        {
            if (HasUnsavedChanges && PartModel != null)
            {
                context.PreventNavigation();
                var saveSuccessful = await SaveChanges();

                if (saveSuccessful)
                {
                    NavigationManager.NavigateTo(context.TargetLocation);
                }
            }
        }

        protected async Task DeletePartEmployee(CMHub_PartEmployeeDTO partEmp)
        {
            if (HasUnsavedChanges) await SaveChanges();
            if (PartModel == null) return;

            PartEmployeeToDelete = partEmp;
            var numberOfCustCons = PartModel.PartCustomerContacts.Where(_ => _.PartEmployeeId == partEmp.Id).Count();
            var isPrimary = partEmp.IsPrimary ?? false;
            var empName = $"{(PartEmployeeType)partEmp.Type} {PartModel.GetADUserForEmployee(partEmp)?.DisplayName ?? ""}";

            DeleteConfirmationMessage = $"Are you sure you want to remove {empName}?";
            DeleteConfirmationMessage += numberOfCustCons > 0 ? $" {empName} has {numberOfCustCons} associated Customer Contacts that will also be removed." : "";
            DeleteConfirmationMessage += isPrimary ? $" {empName} is a Primary PM for this part." : "";

            if (DeleteEmployeeConfirmationModal != null)
            {
                await DeleteEmployeeConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmDeletePartEmployee(bool confirm)
        {
            if (confirm)
            {
                if (PartEmployeeToDelete == null) return;
                if (HasUnsavedChanges) await SaveChanges();

                await CMDexService.DeletePartEmployeeAsync(PartEmployeeToDelete.Id);
                PartEmployeeToDelete = null;
                await LoadData();
                StateHasChanged();
            }
        }

        protected async Task DeletePartCustomerContact(CMHub_PartCustomerContactDTO partCustCon)
        {
            if (HasUnsavedChanges) await SaveChanges();
            if (PartModel == null) return;

            PartCustomerContactToDelete = partCustCon;
            // Find the matching view model for display data
            var viewModel = PartModel.PartCustomerContactModels.FirstOrDefault(vc => vc.PartCustomerContact == partCustCon);
            var conName = viewModel?.DisplayContact.ConName ?? "Unknown Contact";

            DeleteConfirmationMessage = $"Are you sure you want to remove {conName}?";
            DeleteConfirmationMessage += partCustCon.IsOwner ?? false ? $" {conName} is the Owner Contact for this part." : "";

            if (DeleteCustomerContactConfirmationModal != null)
            {
                await DeleteCustomerContactConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmDeletePartCustomerContact(bool confirm)
        {
            if (confirm)
            {
                if (PartCustomerContactToDelete == null) return;
                if (HasUnsavedChanges) await SaveChanges();

                await CMDexService.DeletePartCustomerContactAsync(PartCustomerContactToDelete.Id);
                PartCustomerContactToDelete = null;
                await LoadData();
                StateHasChanged();
            }
        }

        protected void OnChangeOriginationDate(DateTime? newDate)
        {
            if (PartModel != null)
            {
                PartModel.Part.CMOrignationDate_c = newDate;
            }
            HasUnsavedChanges = true;
        }

        protected async Task<bool> SaveChanges()
        {
            if (PartModel == null) return false;

            var savingModalInstance = await Modal.Show<LoadingModal>(options =>
                options.Add(p => p.Message, "Saving changes..."),
                new ModalInstanceOptions { UseModalStructure = false, Centered = true }
            );

            bool saveSuccessful = true;

            foreach (var pe in PartModel.PartEmployees)
            {
                var rowsAffected = await CMDexService.UpdatePartEmployeeAsync(pe);
                if (rowsAffected != 1)
                {
                    saveSuccessful = false;
                }
            }

            foreach (var pcc in PartModel.PartCustomerContacts)
            {
                var rowsAffected = await CMDexService.UpdatePartCustomerContactAsync(pcc);
                if (rowsAffected != 1)
                {
                    saveSuccessful = false;
                }
            }

            var success = await EpicorPartService.SetCMDataAsync(PartModel.Part.PartNum, PartModel.Part.CMManaged_c, PartModel.Part.CMOrignationDate_c);
            if (!success)
            {
                saveSuccessful = false;
            }

            await Modal.Hide(savingModalInstance);
            if (saveSuccessful)
            {
                HasUnsavedChanges = false;
            }
            return saveSuccessful;
        }

        public string CardStyle(string styleType)
        {
            var style = "margin-bottom: 1rem; border: 1px solid;";
            if (styleType == "PrimaryPM")
            {
                style += " background-color: lightgray; border-color: darkgray;";
            }
            else if (styleType == "Owner")
            {
                style += " background-color: var(--light-crystal-blue); border-color: var(--crystal-blue);";
            }
            else
            {
                style += " border-color: lightgray;";
            }
            return style;
        }

        private async Task HandleEmployeeSelected(ADUserDTO_Base employee, PartEmployeeType type)
        {
            if (PartModel == null || employee == null) return;
            await CMDexService.CreatePartEmployeeOnPartAsync(PartModel.Part.PartNum, employee.EmployeeNumber, type);
            await LoadData();
        }


        private async Task HandleCustomerContactSelected(CMHub_PartEmployeeDTO employee, CustomerContactDTO_Base custCon)
        {
            if (PartModel == null || custCon == null) return;
            await CMDexService.CreatePartCustomerContactOnPartAsync(PartModel.Part.PartNum, employee.Id, custCon.CustNum, custCon.ConNum, custCon.PerConID);
            await LoadData();
        }
    }
}