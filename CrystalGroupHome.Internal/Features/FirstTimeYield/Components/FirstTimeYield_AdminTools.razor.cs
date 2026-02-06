using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Models;
using Microsoft.AspNetCore.Components;
using Blazorise;
using Blazorise.DataGrid;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Labor;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Components
{
    public class FirstTimeYield_AdminToolsBase : ComponentBase
    {
        [Inject] public IFirstTimeYield_Service FTYService { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;

        [Parameter] public List<FirstTimeYield_AreaDTO> Areas { get; set; } = [];

        public List<FirstTimeYield_AreaFailureReasonModel> AreaFailureReasonModels { get; set; } = [];
        public DataGrid<FirstTimeYield_AreaFailureReasonModel>? AFRGrid { get; set; }

        public bool IsLoading = false;

        // Selected failure reason for detail view
        public FirstTimeYield_AreaFailureReasonModel? SelectedFailureReason { get; set; }

        // Modal references
        protected Modal? addFailureReasonModal;
        protected Modal? addAreaModal;
        protected Modal? editFailureReasonModal;

        // New failure reason form model
        protected FirstTimeYield_FailureReasonDTO newFailureReason = new();

        // Confirmation modal (for deleting)
        public ConfirmationModal? ConfirmationModal { get; set; }
        // Editing failure reason model (copy for editing)
        protected FirstTimeYield_AreaFailureReasonModel? editingFailureReason;

        // Selected area for adding to a failure reason
        protected int selectedAreaId;
        protected FirstTimeYield_AreaFailureReasonModel? currentFailureModelForArea;

        // Available areas for selection (filtered to not include already associated areas)
        protected List<FirstTimeYield_AreaDTO> AvailableAreas => GetAvailableAreas();

        protected List<string> AllEmpIds = [];
        protected List<ADUserDTO_Base> AllEmployees = [];

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        // We toggle detail rows, so keep track if we should ignore a row click
        private int _ignoreRowClickForEntryId = -1;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();

            await base.OnInitializedAsync();
        }

        protected async Task LoadData()
        {
            IsLoading = true;
            try
            {
                // Get the original DTOs from the service
                var areaFailureReasonDTOs = await FTYService.GetAreaFailureReasons<FirstTimeYield_AreaFailureReasonDTO>();

                // Convert to our the model structure
                AreaFailureReasonModels = FirstTimeYield_Mapper.ToModels(areaFailureReasonDTOs);

                List<string> allEmpIds = [];
                foreach (var model in AreaFailureReasonModels)
                {
                    if (!string.IsNullOrEmpty(model.FailureReason.LastModifiedUser) && model.FailureReason.LastModifiedUser != "0")
                    {
                        AllEmpIds.Add(model.FailureReason.LastModifiedUser);
                    }
                }

                AllEmployees = await ADUserService.GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(AllEmpIds);
            }
            finally
            {
                IsLoading = false;
            }

            StateHasChanged();
        }

        protected void OnFailureReasonSelected(FirstTimeYield_AreaFailureReasonModel model)
        {
            SelectedFailureReason = model;
            StateHasChanged();
        }

        protected void ShowAddFailureReasonModal()
        {
            newFailureReason = new FirstTimeYield_FailureReasonDTO
            {
                EntryDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                // You might want to set EntryUser and LastModifiedUser based on your authentication system
                EntryUser = CurrentUser?.DBUser.EmployeeNumber ?? "0",
                LastModifiedUser = CurrentUser?.DBUser.EmployeeNumber ?? "0"
            };

            addFailureReasonModal?.Show();
        }

        protected void CloseAddFailureReasonModal()
        {
            addFailureReasonModal?.Hide();
        }

        protected async Task SaveNewFailureReason()
        {
            if (string.IsNullOrWhiteSpace(newFailureReason.FailureDescription))
            {
                // Add validation messages if needed
                return;
            }

            try
            {
                // Save the new failure reason to database
                int newId = await FTYService.CreateFailureReasonAsync(newFailureReason);

                // Create a new model with the saved failure reason and empty areas list
                var newModel = new FirstTimeYield_AreaFailureReasonModel(newFailureReason, []);
                newModel.FailureReason.Id = newId;

                // Add it to the list
                AreaFailureReasonModels.Add(newModel);

                // Close the modal
                CloseAddFailureReasonModal();

                await LoadData();
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"Error saving failure reason: {ex.Message}");
            }
        }

        protected void ShowAddAreaModal(FirstTimeYield_AreaFailureReasonModel model)
        {
            currentFailureModelForArea = model;
            selectedAreaId = 0; // Reset selection
            addAreaModal?.Show();
        }

        protected void CloseAddAreaModal()
        {
            addAreaModal?.Hide();
            currentFailureModelForArea = null;
        }

        protected async Task AddAreaToFailureReason()
        {
            if (selectedAreaId <= 0 || currentFailureModelForArea == null)
            {
                return;
            }

            try
            {
                // Find the selected area from the available areas
                var areaToAdd = Areas.FirstOrDefault(a => a.Id == selectedAreaId);
                if (areaToAdd == null) return;

                // Save the association to the database
                await FTYService.AddAreaFailureReasonAsync(
                    selectedAreaId,
                    currentFailureModelForArea.FailureReason.Id);

                // Add the area to the model's area list
                currentFailureModelForArea.Areas.Add(areaToAdd);

                // Close the modal
                CloseAddAreaModal();

                await LoadData();
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"Error adding area: {ex.Message}");
            }
        }

        protected async Task RemoveArea(FirstTimeYield_AreaFailureReasonModel model, FirstTimeYield_AreaDTO area)
        {
            try
            {
                // Remove the association from the database
                await FTYService.DeleteAreaFailureReasonAsync(area.Id, model.FailureReason.Id);

                // Remove the area from the model's area list
                model.Areas.Remove(area);

                await LoadData();
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"Error removing area: {ex.Message}");
            }
        }

        protected void EditFailureReason(FirstTimeYield_AreaFailureReasonModel model)
        {
            // Create a deep copy of the model for editing
            editingFailureReason = new FirstTimeYield_AreaFailureReasonModel(
                new FirstTimeYield_FailureReasonDTO
                {
                    Id = model.FailureReason.Id,
                    FailureDescription = model.FailureReason.FailureDescription,
                    Deleted = model.FailureReason.Deleted,
                    EntryDate = model.FailureReason.EntryDate,
                    EntryUser = model.FailureReason.EntryUser,
                    LastModifiedDate = DateTime.Now, // Update the modification date
                    LastModifiedUser = CurrentUser?.DBUser.EmployeeNumber ?? "0" // Set the current user
                },
                model.Areas.ToList() // Copy the areas list
            );

            editFailureReasonModal?.Show();
        }

        protected void CloseEditFailureReasonModal()
        {
            editFailureReasonModal?.Hide();
        }

        protected async Task SaveFailureReasonChanges()
        {
            if (editingFailureReason == null ||
                string.IsNullOrWhiteSpace(editingFailureReason.FailureReason.FailureDescription))
            {
                return;
            }

            try
            {
                // Update the failure reason in the database
                await FTYService.UpdateFailureReasonAsync(editingFailureReason.FailureReason);

                // Find and update the original model in the list
                var originalModel = AreaFailureReasonModels.FirstOrDefault(
                    m => m.FailureReason.Id == editingFailureReason.FailureReason.Id);

                if (originalModel != null)
                {
                    // Update the failure reason in the original model
                    originalModel.FailureReason.FailureDescription = editingFailureReason.FailureReason.FailureDescription;
                    originalModel.FailureReason.Deleted = editingFailureReason.FailureReason.Deleted;
                    originalModel.FailureReason.LastModifiedDate = editingFailureReason.FailureReason.LastModifiedDate;
                    originalModel.FailureReason.LastModifiedUser = editingFailureReason.FailureReason.LastModifiedUser;
                }

                // Close the modal
                CloseEditFailureReasonModal();

                await LoadData();
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"Error updating failure reason: {ex.Message}");
            }
        }

        protected async Task DeprecateFailureReason(FirstTimeYield_AreaFailureReasonModel model)
        {
            try
            {
                // Set the Deleted flag to true
                model.FailureReason.Deleted = true;
                model.FailureReason.LastModifiedDate = DateTime.Now;
                model.FailureReason.LastModifiedUser = CurrentUser?.DBUser.EmployeeNumber ?? "0";

                // Update in the database
                await FTYService.UpdateFailureReasonAsync(model.FailureReason);

                await LoadData();
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"Error marking failure reason as deleted: {ex.Message}");
            }
        }

        protected async Task DeleteFailureReason()
        {
            if (ConfirmationModal != null)
            {
                await ConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmDeleteFailureReason()
        {
            if (editingFailureReason == null) return;

            try
            {
                await FTYService.DeleteFailureReasonAsync(editingFailureReason.FailureReason.Id);

                // Close the modal
                CloseEditFailureReasonModal();

                await LoadData();
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"Error deleting failure reason: {ex.Message}");
            }
        }

        // Helper method to get available areas for selection
        private List<FirstTimeYield_AreaDTO> GetAvailableAreas()
        {
            if (currentFailureModelForArea == null)
                return Areas;

            // Get IDs of areas already associated with the current failure reason
            var associatedAreaIds = currentFailureModelForArea.Areas.Select(a => a.Id).ToHashSet();

            // Return only areas not already associated and not deleted
            return Areas
                .Where(a => !associatedAreaIds.Contains(a.Id) && !a.Deleted)
                .ToList();
        }

        protected void DetailRowToggle(DataGridRowMouseEventArgs<FirstTimeYield_AreaFailureReasonModel> args)
        {
            if (_ignoreRowClickForEntryId != args.Item.FailureReason.Id)
            {
                args.Item.IsDetailExpanded = !args.Item.IsDetailExpanded;

                // Close other expanded rows (only one open at a time)
                foreach (var entry in AreaFailureReasonModels.Where(e => e != args.Item && e.IsDetailExpanded))
                {
                    AFRGrid?.ToggleDetailRow(entry);
                    entry.IsDetailExpanded = false;
                }
            }

            _ignoreRowClickForEntryId = -1;
        }

        protected void OnRowStyling(FirstTimeYield_AreaFailureReasonModel afr, DataGridRowStyling styling)
        {
            if (afr.IsDetailExpanded)
            {
                styling.Class = "active-row";
            }
        }
    }
}