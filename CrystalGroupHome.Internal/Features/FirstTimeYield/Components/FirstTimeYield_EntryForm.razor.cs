using Blazorise;
using Blazorise.DataGrid;
using CrystalGroupHome.Internal.Common.Data.Jobs;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Models;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.SharedRCL.Data.Employees;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Components
{
    public partial class FirstTimeYield_EntryFormBase : ComponentBase
    {
        [Inject] public IModalService Modal { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public IFirstTimeYield_Service FTYService { get; set; } = default!;
        [Inject] public IJobService JobService { get; set; } = default!;
        [Inject] public ILaborService LaborService { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;

        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        // ----------------------------------------------------
        // Parameters
        [Parameter] public bool IsEdit { get; set; }
        [Parameter] public int? EditId { get; set; }
        [Parameter] public List<FirstTimeYield_AreaDTO> Areas { get; set; } = new();

        // ----------------------------------------------------
        // Internal States & Flags
        public bool IsLoading = false;
        private bool _isLoadingJobData = false; // concurrency guard for job number lookups
        private bool _initialized = false;      // track first render or full init

        // ----------------------------------------------------
        // The original entry (if editing), otherwise null
        public FirstTimeYield_EntryModel? Entry { get; set; }

        // The in-progress draft that the form is bound to
        public FirstTimeYield_EntryModel DraftEntry { get; set; } = new();

        // The area ID the user selected
        private int? _draftAreaId;
        public int? DraftAreaId
        {
            get => _draftAreaId;
            set
            {
                if (_draftAreaId != value)
                {
                    _draftAreaId = value;
                    if(DraftEntry.Area != null)
                    {
                        DraftEntry.Area.Id = value ?? -1;
                    }
                    // asynchronously load possible failure reasons for this area
                    _ = UpdateFailureReasonsAsync(_draftAreaId);
                }
            }
        }

        // Available areas (filtered if not editing)
        public List<FirstTimeYield_AreaDTO> ValidAreas { get; set; } = new();

        // The job number that is typed/bound
        public JobHeadDTO_Base? FoundJob { get; set; }
        public int TotalTestedQtyForJob { get; set; } = 0;
        private string _draftJobNum = string.Empty;
        public string DraftJobNum
        {
            get => _draftJobNum;
            set
            {
                if (_draftJobNum != value)
                {
                    FoundJob = null;
                    // reset op selection if user changes job
                    DraftValidJobOperIndex = -1;
                    _draftJobNum = value;
                    DraftEntry.JobNum = _draftJobNum;
                    DraftEntry.IsValidJobNum = false;
                    DraftEntry.CurrentJobNumHasBeenSearched = false;

                    // auto-search job if user typed the minimum number of characters
                    if (_draftJobNum.Trim().Length >= 6)
                    {
                        _ = LoadJobDataAsync(_draftJobNum);
                    }
                }
            }
        }

        // Operation selection
        private int? _draftValidJobOperIndex;
        public int DraftValidJobOperIndex
        {
            get => _draftValidJobOperIndex ?? -1;
            set
            {
                if (_draftValidJobOperIndex != value)
                {
                    _draftValidJobOperIndex = value;
                    if (_draftValidJobOperIndex >= 0 && _draftValidJobOperIndex < ValidJobOpers.Count)
                    {
                        DraftEntry.OpCode = ValidJobOpers[_draftValidJobOperIndex.Value].OpCode;
                    }
                    else
                    {
                        DraftEntry.OpCode = string.Empty;
                    }
                    _ = LoadEmployeeDataAsync(); // refresh employees for chosen opcode
                }
            }
        }

        // UI selection for Opers / Employees
        public List<JobOperDTO_Base> ValidJobOpers { get; set; } = new();
        public List<EmpBasicDTO_Base> ValidJobEmployees { get; set; } = new();

        // Which employee is selected?
        public int DraftValidJobEmployeeIndex { get; set; } = -1;

        // Failure reasons for the selected area
        public List<FirstTimeYield_FailureReasonDTO> ValidFailureReasons { get; set; } = new();

        // DataGrid for listing/editing failures inline
        public DataGrid<FirstTimeYield_FailureModel>? FailureGrid { get; set; }

        // For the modal confirmation of deleting a Failure
        public ConfirmationModal? ConfirmationModal;
        public FirstTimeYield_FailureModel? FailureToDelete;

        private Uri? _prevUri;

        // For page heading
        public string Title => IsEdit ? "Edit Entry" : "Add New Entry";

        // ----------------------------------------------------
        // Lifecycle

        protected override void OnInitialized()
        {
            IsLoading = true;
            base.OnInitialized();
        }

        protected override async Task OnParametersSetAsync()
        {
            try
            {
                while (string.IsNullOrEmpty(CurrentUser?.DBUser.EmployeeNumber))
                {
                    await Task.Delay(100); // Ensure we have the logged in user before continuing to pull other params
                }

                // 1) Set valid areas (filter out "deleted" if not editing)
                ValidAreas = GetValidAreas(IsEdit);

                // 2) Load failure reasons associated with the selected area ID
                //    (DraftAreaId might be null at this moment, or if we set it via query parsing later)
                await UpdateFailureReasonsAsync(DraftAreaId);

                // 3) If editing, load the existing record
                if (IsEdit)
                {
                    await CloneEntryForDraftEdits();
                }
                else
                {
                    // If creating a new entry, pre-fill from query params (if any)
                    await PrefillNewDraftEntryFromQueryParams();
                }
            }
            finally
            {
                IsLoading = false;
            }

            await base.OnParametersSetAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && !_initialized)
            {
                _initialized = true;
                StateHasChanged();
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        // ----------------------------------------------------
        // Data Loading / Helpers

        /// <summary>
        /// Get the valid list of areas. 
        /// If adding a new entry, exclude "deleted" areas. 
        /// If editing an entry, the user can still select from all areas (including obsolete).
        /// </summary>
        private List<FirstTimeYield_AreaDTO> GetValidAreas(bool forEdit)
        {
            return forEdit
                ? Areas
                : Areas.Where(a => !a.Deleted).ToList();
        }

        private readonly List<EmpBasicDTO_Base> FailureEmployees = [];

        /// <summary>
        /// If editing, fetch the entry by EditId, clone it into DraftEntry.
        /// Also load job operations for the job number, then set the correct index for OpCode.
        /// </summary>
        private async Task CloneEntryForDraftEdits()
        {
            if (EditId == null) return;

            Entry ??= await FTYService.GetEntryByIdAsync(EditId.Value);
            if (Entry == null) return;

            // Set draft area
            _draftAreaId = Entry.Area?.Id ?? 12;
            // Set job
            _draftJobNum = Entry.JobNum ?? string.Empty;

            // Clone the entry so we don't mutate the original in memory
            DraftEntry = new FirstTimeYield_EntryModel
            {
                Id = Entry.Id,
                JobNum = Entry.JobNum ?? string.Empty,
                OpCode = Entry.OpCode,
                OpCodeOperator = Entry.OpCodeOperator,
                Area = Entry.Area,
                QtyTested = Entry.QtyTested,
                QtyFailed = Entry.QtyFailed,
                EntryUser = Entry.EntryUser,
                EntryDate = Entry.EntryDate,
                LastModifiedUser = Entry.LastModifiedUser,
                LastModifiedDate = Entry.LastModifiedDate,
                Notes = Entry.Notes,
                Failures = Entry.Failures
                    .Select(f => new FirstTimeYield_FailureModel(f.EntryId)
                    {
                        Id = f.Id,
                        FailureReason = f.FailureReason,
                        ParentEntryQtyFailed = Entry.QtyFailed,
                        Qty = f.Qty,
                        AreaToBlame = f.AreaToBlame,
                        JobNumToBlame = f.JobNumToBlame,
                        OpCodeToBlame = f.OpCodeToBlame,
                        OperatorToBlame = f.OperatorToBlame
                    })
                    .ToList()
            };

            if (!string.IsNullOrWhiteSpace(DraftEntry.JobNum) && DraftEntry.JobNum.Trim().Length >= 6)
            {
                await LoadJobDataAsync(DraftEntry.JobNum.Trim());
            }
        }

        protected string GetFailureEmployeeNameByEmpID(string? empID)
        {
            if (empID == null) return "";
            var employee = FailureEmployees.FirstOrDefault(emp => emp.EmpID == empID);
            return employee?.Name ?? $"ID {empID} Not Found";
        }

        /// <summary>
        /// Parse any relevant query params to initialize a new DraftEntry.
        /// </summary>
        private async Task PrefillNewDraftEntryFromQueryParams()
        {
            DraftEntry = new FirstTimeYield_EntryModel();

            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);

            if (_prevUri?.Query.ToString() == uri.Query.ToString()) return;

            _prevUri = uri;
            var query = QueryHelpers.ParseQuery(uri.Query);

            if (query.TryGetValue(FirstTimeYield_EntryModel.jobNumParamName, out var jobNumValue))
            {
                _draftJobNum = jobNumValue.ToString();

                // Ensure that we have the job data loaded before trying to fill the following fields
                if (!string.IsNullOrWhiteSpace(_draftJobNum) && _draftJobNum.Trim().Length >= 6)
                {
                    await LoadJobDataAsync(_draftJobNum.Trim());
                }
            }

            if (query.TryGetValue(FirstTimeYield_EntryModel.opCodeParamName, out var opCodeValue))
            {
                foreach (var jobOperation in ValidJobOpers)
                {
                    if (jobOperation.OpCode == opCodeValue)
                    {
                        _draftValidJobOperIndex = ValidJobOpers.IndexOf(jobOperation);
                    }
                }

                await LoadEmployeeDataAsync();
            }

            if (query.TryGetValue(FirstTimeYield_EntryModel.opCodeOperatorIDParamName, out var opCodeOperatorIDValue))
            {
                foreach (var jobOperator in ValidJobEmployees)
                {
                    if (jobOperator.EmpID == opCodeOperatorIDValue)
                    {
                        DraftValidJobEmployeeIndex = ValidJobEmployees.IndexOf(jobOperator);
                    }
                }

                // Match to a valid operator/employee ID number
                DraftEntry.OpCodeOperator = await ADUserService.GetADUserByEmployeeNumberAsync<ADUserDTO_Base>(opCodeOperatorIDValue.ToString().Trim()) ?? new();
            }
            else
            {
                // default to environment user
                DraftEntry.OpCodeOperator = CurrentUser?.DBUser;
            }

            if (query.TryGetValue(FirstTimeYield_EntryModel.areaIdParamName, out var areaIdValue))
            {
                // This tries to match the provided area param if it exists
                _draftAreaId = Areas.FirstOrDefault(area => area.Id == areaIdValue)?.Id;
            }

            if (query.TryGetValue(FirstTimeYield_EntryModel.qtyTestedParamName, out var qtyTestedValue))
            {
                if (int.TryParse(qtyTestedValue, out var qtyTestedValueInt))
                    DraftEntry.QtyTested = qtyTestedValueInt;
            }

            if (query.TryGetValue(FirstTimeYield_EntryModel.qtyFailedParamName, out var qtyFailedValue))
            {
                if (int.TryParse(qtyFailedValue, out var qtyFailedValueInt))
                    DraftEntry.QtyFailed = qtyFailedValueInt;
            }

            // default entry user to environment user
            DraftEntry.EntryUser = CurrentUser?.DBUser ?? new()
            {
                EmployeeNumber = "UNKNOWN",
                DisplayName = "Unkown User"
            };

            // on entry, entry user and last modified user are the same.
            DraftEntry.LastModifiedUser = DraftEntry.EntryUser;

            if (!string.IsNullOrWhiteSpace(_draftJobNum) && _draftJobNum.Trim().Length >= 6)
            {
                await ValidateJobNum(_draftJobNum);
            }

            StateHasChanged();
        }

        public async Task ValidateJobNum(string jobNum)
        {
            FoundJob = await JobService.GetJobHeadByJobNumAsync<JobHeadDTO_Base>(jobNum.Trim());
            DraftEntry.IsValidJobNum = FoundJob != null;
            DraftEntry.CurrentJobNumHasBeenSearched = true;
            DraftEntry.OrigProdQty = FoundJob?.ProdQty ?? 0;
        }

        /// <summary>
        /// Load job data to determine if it's valid and retrieve job operations.
        /// Then pick the correct OpIndex if we already have an OpCode set.
        /// </summary>
        public async Task LoadJobDataAsync(string jobNum)
        {
            if (jobNum == null || _isLoadingJobData) return;

            _isLoadingJobData = true;
            try
            {
                await ValidateJobNum(jobNum);

                // 2) If valid, load oper + employees
                if (DraftEntry.IsValidJobNum)
                {
                    TotalTestedQtyForJob = await FTYService.GetTestedQtyForEntriesByJobNum(jobNum);

                    await FindJobOpersAsync(jobNum);
                    // Now that ValidJobOpers and ValidJobEmployees are loaded, we can set the Op/Emp index
                    _draftValidJobOperIndex = GetIndexOfOpCode(DraftEntry.OpCode) ?? -1;
                    DraftValidJobEmployeeIndex = GetIndexOfEmployee(DraftEntry.OpCodeOperator) ?? -1;
                }
                else
                {
                    // Reset
                    ValidJobOpers.Clear();
                    ValidJobEmployees.Clear();
                    _draftValidJobOperIndex = -1;
                    DraftValidJobEmployeeIndex = -1;
                }
            }
            finally
            {
                _isLoadingJobData = false;
                StateHasChanged();
            }
        }

        public async Task FindJobOpersAsync(string jobNum)
        {
            // Load all operations for this job
            ValidJobOpers = await JobService.GetUniqueRecordedOpCodesByJobNumAsync<JobOperDTO_Base>(jobNum.Trim());

            // Load employees who have labor records on this job
            await LoadEmployeeDataAsync();
        }

        private async Task LoadEmployeeDataAsync()
        {
            // If no job num or no valid job ops, we can't load employees
            if (string.IsNullOrWhiteSpace(_draftJobNum))
                return;

            // If user selected an opcode, filter employees to that opcode
            if (_draftValidJobOperIndex >= 0 && _draftValidJobOperIndex < ValidJobOpers.Count)
            {
                var opcode = ValidJobOpers[_draftValidJobOperIndex ?? 0].OpCode;
                ValidJobEmployees = await LaborService.GetLaborEmployeesByJobNumAndOpCode<EmpBasicDTO_Base>(
                   _draftJobNum.Trim(), opcode
                );

                // Attempt to re-select operator if it was previously set
                DraftValidJobEmployeeIndex = GetIndexOfEmployee(DraftEntry.OpCodeOperator) ?? -1;
            }
            else
            {
                // No valid opcode selected, revert to employees who worked on entire job
                ValidJobEmployees = await LaborService.GetLaborEmployeesByJobNum<EmpBasicDTO_Base>(
                    _draftJobNum.Trim()
                );
                DraftValidJobEmployeeIndex = GetIndexOfEmployee(DraftEntry.OpCodeOperator) ?? -1;
            }
            StateHasChanged();
        }

        /// <summary>
        /// Retrieves failure reasons for the selected area (if any).
        /// </summary>
        private async Task UpdateFailureReasonsAsync(int? areaId)
        {
            if (!areaId.HasValue)
            {
                ValidFailureReasons.Clear();
                return;
            }

            try
            {
                ValidFailureReasons = await FTYService.GetFailureReasonsByArea<FirstTimeYield_FailureReasonDTO>(areaId.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update failure reasons: {ex.Message}");
                ValidFailureReasons.Clear();
            }
            StateHasChanged();
        }

        // -------------------------------------------------------------
        // Index helpers

        private int? GetIndexOfOpCode(string? opCode)
        {
            if (string.IsNullOrWhiteSpace(opCode) || ValidJobOpers.Count == 0) return null;
            var match = ValidJobOpers.FirstOrDefault(o => o.OpCode == opCode);
            return match == null ? null : ValidJobOpers.IndexOf(match);
        }

        private int? GetIndexOfEmployee(ADUserDTO_Base? emp)
        {
            if (emp == null) return null;
            var match = ValidJobEmployees.FirstOrDefault(e => e.EmpID == emp.EmployeeNumber);
            return match == null ? null : ValidJobEmployees.IndexOf(match);
        }

        // ----------------------------------------------------
        // Form Submission & Navigation

        protected async Task HandleSubmit(EditContext editContext)
        {
            // Check the JobNum if it hasn't been yet for some reason
            if (!DraftEntry.CurrentJobNumHasBeenSearched)
            {
                await LoadJobDataAsync(_draftJobNum);
            }

            if (editContext.Validate())
            {
                await HandleValidSubmit();
            }
        }

        private async Task HandleValidSubmit()
        {
            // Job Num
            DraftEntry.JobNum = _draftJobNum;

            // Area
            DraftEntry.Area = Areas.FirstOrDefault(a => a.Id == DraftAreaId)
                           ?? Areas.FirstOrDefault(a => a.Id == 12);

            // OpCode
            if (_draftValidJobOperIndex >= 0 && _draftValidJobOperIndex < ValidJobOpers.Count)
            {
                DraftEntry.OpCode = ValidJobOpers[_draftValidJobOperIndex ?? 0].OpCode;
            }
            else
            {
                DraftEntry.OpCode = "N/A";
            }

            // Operator
            if (DraftValidJobEmployeeIndex >= 0 && DraftValidJobEmployeeIndex < ValidJobEmployees.Count)
            {
                DraftEntry.OpCodeOperator = (ADUserDTO_Base)ValidJobEmployees[DraftValidJobEmployeeIndex];
            }
            else
            {
                DraftEntry.OpCodeOperator = null;
            }

            // Entry User
            // Only set to current user if it's currently null/empty,
            // because if we are editing then the value should have already
            // come from the original entry.
            if (string.IsNullOrEmpty(DraftEntry.EntryUser.EmployeeNumber))
            {
                DraftEntry.EntryUser = CurrentUser?.DBUser ?? new()
                {
                    SAMAccountName = "UNKNOWN",
                    DisplayName = "Unknown User"
                };
            }

            // Modified User
            DraftEntry.LastModifiedUser = CurrentUser?.DBUser ?? new()
            {
                SAMAccountName = "UNKNOWN",
                DisplayName = "Unknown User"
            };

            // Modified Date
            DraftEntry.LastModifiedDate = DateTime.Now;

            // Save
            if (IsEdit)
            {
                await FTYService.UpdateEntryAsync(DraftEntry);
            }
            else
            {
                await FTYService.CreateEntryAsync(DraftEntry);
            }

            // Go back to the main DataGrid
            NavigationManager.NavigateTo(NavHelpers.FirstTimeYieldMainPage);
        }

        public void NavigateToEntryDataGrid()
        {
            NavigationManager.NavigateTo(NavHelpers.FirstTimeYieldMainPage);
        }

        // ----------------------------------------------------
        // Failure Management

        protected void FailureRowClicked(DataGridRowMouseEventArgs<FirstTimeYield_FailureModel> args)
        {
            if (DraftEntry.Failures == null) return;

            foreach (var failure in DraftEntry.Failures)
            {
                failure.IsSelected = (failure.Id == args.Item.Id);
            }
        }

        public Task OpenAddFailureDialog()
        {
            return Modal.Show<FirstTimeYield_FailureDialog>(dlg =>
            {
                dlg.Add(p => p.IsEdit, false);
                dlg.Add(p => p.ParentEntry, DraftEntry);
                dlg.Add(p => p.ValidFailureReasons, ValidFailureReasons);
                dlg.Add(p => p.Areas, Areas);
                dlg.Add(p => p.ValidAreasToBlame, ValidAreas);
                dlg.Add(p => p.OnAddSubmit,
                    EventCallback.Factory.Create<FirstTimeYield_FailureModel>(this, AddFailure)
                );
            },
            new ModalInstanceOptions { UseModalStructure = false, Centered = true });
        }

        public void AddFailure(FirstTimeYield_FailureModel failure)
        {
            DraftEntry.Failures.Add(failure);
            StateHasChanged();
            FailureGrid?.Reload();
        }

        public Task OpenEditFailureDialog(FirstTimeYield_FailureModel failureToEdit)
        {
            if (DraftEntry.Failures == null) return Task.CompletedTask;

            var failureToEditIndex = DraftEntry.Failures.IndexOf(failureToEdit);

            return Modal.Show<FirstTimeYield_FailureDialog>(dlg =>
            {
                dlg.Add(p => p.IsEdit, true);
                dlg.Add(p => p.ParentEntry, DraftEntry);
                dlg.Add(p => p.Failure, failureToEdit);
                dlg.Add(p => p.ValidFailureReasons, ValidFailureReasons);
                dlg.Add(p => p.Areas, Areas);
                dlg.Add(p => p.ValidAreasToBlame, ValidAreas);
                dlg.Add(p => p.FailureEditIndex, failureToEditIndex);
                dlg.Add(p => p.OnEditSubmit,
                    EventCallback.Factory.Create<(FirstTimeYield_FailureModel EditedFailure, int FailureIndex)>(
                        this, data => ApplyChangesToEditedFailure(data.EditedFailure, data.FailureIndex)
                    )
                );
            },
            new ModalInstanceOptions { UseModalStructure = false, Centered = true });
        }

        public void ApplyChangesToEditedFailure(FirstTimeYield_FailureModel failureWithChanges, int failureToEditIndex)
        {
            if (DraftEntry.Failures == null) return;

            DraftEntry.Failures[failureToEditIndex] = failureWithChanges;
            StateHasChanged();
            FailureGrid?.Reload();
        }

        protected async Task DeleteFailure(FirstTimeYield_FailureModel failure)
        {
            FailureToDelete = failure;
            if (ConfirmationModal != null)
            {
                await ConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmDeletion(bool confirmed)
        {
            if (!confirmed || FailureToDelete == null || DraftEntry.Failures == null)
            {
                FailureToDelete = null;
                return;
            }

            DraftEntry.Failures.Remove(FailureToDelete);
            FailureToDelete = null;
            StateHasChanged();
            if (FailureGrid != null)
            {
                await FailureGrid.Reload();
            }
        }
    }
}
