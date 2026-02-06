using Blazorise;
using CrystalGroupHome.Internal.Common.Data.Jobs;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Models;
using CrystalGroupHome.SharedRCL.Data.Employees;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Components
{
    public class FirstTimeYield_FailureDialogBase : ComponentBase, IDisposable
    {
        [Inject] public IModalService Modal { get; set; } = default!;
        [Inject] public IJobService JobService { get; set; } = default!;
        [Inject] public ILaborService LaborService { get; set; } = default!;
        [Inject] public IFirstTimeYield_Service FTYService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

        [Parameter] public FirstTimeYield_EntryModel ParentEntry { get; set; } = default!;
        [Parameter] public FirstTimeYield_FailureModel Failure { get; set; } = default!;
        [Parameter] public int FailureEditIndex { get; set; }
        [Parameter] public bool IsEdit { get; set; }
        [Parameter] public List<FirstTimeYield_FailureReasonDTO> ValidFailureReasons { get; set; } = new();
        [Parameter] public List<FirstTimeYield_AreaDTO> Areas { get; set; } = new();
        [Parameter] public List<FirstTimeYield_AreaDTO> ValidAreasToBlame { get; set; } = new();
        [Parameter] public EventCallback<FirstTimeYield_FailureModel> OnAddSubmit { get; set; }
        [Parameter] public EventCallback<(FirstTimeYield_FailureModel EditedFailure, int FailureIndex)> OnEditSubmit { get; set; }
        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        public string Title => IsEdit ? "Edit Failure Reasons" : "Add Failure Reason";

        // The draft model we bind our form to.
        public FirstTimeYield_FailureModel DraftFailure { get; set; } = new();

        // FailureReason selection
        public int? DraftFailureReasonId { get; set; } = 11; // defaults to "Other"

        // Area selection
        public int? _draftAreaToBlameId = 12; // defaults to "N/A"
        public int? DraftAreaToBlameId
        {
            get => _draftAreaToBlameId;
            set
            {
                if (_draftAreaToBlameId != value)
                {
                    _draftAreaToBlameId = value;
                    DraftFailure.AreaToBlame = Areas
                        .FirstOrDefault(a => a.Id == _draftAreaToBlameId)
                        ?? Areas.FirstOrDefault(a => a.Id == 12);

                    _ = UpdateFailureReasonsAsync(_draftAreaToBlameId);
                }
            }
        }

        // For job-number text input
        private string _draftJobNumToBlame = string.Empty;
        public string DraftJobNumToBlame
        {
            get => _draftJobNumToBlame;
            set
            {
                if (_draftJobNumToBlame != value)
                {
                    _draftJobNumToBlame = value;
                    DraftFailure.JobNumToBlame = value;
                    DraftFailure.JobNumToBlameIsValid = false;
                    CurrentJobNumHasBeenSearched = false;

                    // If the user manually types in a 6-length job number or >, auto-load
                    if (value?.Trim().Length >= 6)
                    {
                        _ = LoadJobDataAsync(value);
                    }
                }
            }
        }

        // UI selection for Opers / Employees
        public List<JobOperDTO_Base> ValidJobOpers { get; set; } = new();
        public List<EmpBasicDTO_Base> ValidJobEmployees { get; set; } = new();

        // Which oper is selected?
        private int _draftValidJobOperIndex = -1;
        public int DraftValidJobOperIndex
        {
            get => _draftValidJobOperIndex;
            set
            {
                if (_draftValidJobOperIndex != value)
                {
                    _draftValidJobOperIndex = value;
                    if (value >= 0 && value < ValidJobOpers.Count)
                    {
                        DraftFailure.OpCodeToBlame = ValidJobOpers[value].OpCode;
                    }
                    else
                    {
                        DraftFailure.OpCodeToBlame = null;
                    }
                    _ = LoadEmployeeDataAsync(); // refresh employees for chosen opcode
                }
            }
        }

        // Which employee is selected?
        public int DraftValidJobEmployeeIndex { get; set; } = -1;

        // Meta/flags
        public bool CurrentJobNumHasBeenSearched { get; set; } = false;
        private bool _isLoadingJobData = false;

        // -------------------------------------------------------------
        // Lifecycle

        protected override void OnInitialized()
        {
            NavigationManager.LocationChanged += OnLocationChanged;
        }


        protected override async Task OnParametersSetAsync()
        {
            // Each time parameters are set (such as on first render or if parent changes), set up local state.

            // If we are editing, we want to load the previously saved data into DraftFailure
            if (IsEdit && ParentEntry != null && Failure != null)
            {
                DraftFailure = new FirstTimeYield_FailureModel(ParentEntry.Id)
                {
                    FailureReason = Failure.FailureReason,
                    ParentEntryQtyFailed = ParentEntry.QtyFailed,
                    Qty = Failure.Qty,
                    AreaToBlame = Failure.AreaToBlame,
                    JobNumToBlame = Failure.JobNumToBlame,
                    OpCodeToBlame = Failure.OpCodeToBlame,
                    OperatorToBlame = Failure.OperatorToBlame
                };

                // Grab IDs for initial selection in the dropdowns
                DraftFailureReasonId = DraftFailure.FailureReason?.Id ?? 11;
                DraftAreaToBlameId = DraftFailure.AreaToBlame?.Id ?? 12;
                _draftJobNumToBlame = DraftFailure.JobNumToBlame ?? string.Empty;
            }
            else
            {
                // Creating a new record
                DraftFailure = new FirstTimeYield_FailureModel(ParentEntry?.Id ?? 0, ParentEntry?.QtyFailed)
                {
                    AreaToBlame = ParentEntry?.Area
                };
            }

            // Attempt to load data if we already have a job number
            if (!string.IsNullOrWhiteSpace(DraftFailure.JobNumToBlame) && DraftFailure.JobNumToBlame.Length == 6)
            {
                await LoadJobDataAsync(DraftFailure.JobNumToBlame);
            }

            // Select the area ID if found, else default to N/A
            DraftAreaToBlameId = Areas.FirstOrDefault(a => a.Id == DraftFailure.AreaToBlame?.Id)?.Id ?? 12;

            ValidFailureReasons = await FTYService.GetFailureReasonsByArea<FirstTimeYield_FailureReasonDTO>(DraftAreaToBlameId.Value);

            await base.OnParametersSetAsync();
        }

        // -------------------------------------------------------------
        // Loading methods

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

                // If the list has any items, automatically set DraftFailureReasonId:
                // If "Other" (Id 11) exists, select it; otherwise, default to the first item.
                if (ValidFailureReasons.Count != 0)
                {
                    DraftFailureReasonId = ValidFailureReasons.Any(fr => fr.Id == 11)
                        ? 11
                        : ValidFailureReasons.First().Id;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update failure reasons: {ex.Message}");
                ValidFailureReasons.Clear();
            }
            StateHasChanged();
        }


        protected async Task LoadJobDataAsync(string? jobNum)
        {
            if (jobNum == null || _isLoadingJobData) return;

            _isLoadingJobData = true;
            try
            {
                // 1) Validate job
                var head = await JobService.GetJobHeadByJobNumAsync<JobHeadDTO_Base>(jobNum.Trim());
                DraftFailure.JobNumToBlameIsValid = head != null;
                CurrentJobNumHasBeenSearched = true;

                // 2) If valid, load oper + employees
                if (DraftFailure.JobNumToBlameIsValid)
                {
                    await FindJobOpersAsync(jobNum);
                    // Now that ValidJobOpers and ValidJobEmployees are loaded, we can set the Op/Emp index
                    _draftValidJobOperIndex = GetIndexOfOpCode(DraftFailure.OpCodeToBlame) ?? -1;
                    DraftValidJobEmployeeIndex = GetIndexOfEmployee(DraftFailure.OperatorToBlame) ?? -1;
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

        private async Task FindJobOpersAsync(string jobNum)
        {
            // Load all operations for this job
            ValidJobOpers = await JobService.GetUniqueRecordedOpCodesByJobNumAsync<JobOperDTO_Base>(jobNum.Trim());

            // Load employees who have labor records on this job (for any opcode)
            ValidJobEmployees = await LaborService.GetLaborEmployeesByJobNum<EmpBasicDTO_Base>(jobNum.Trim());
        }

        private async Task LoadEmployeeDataAsync()
        {
            // If no job num or no valid job ops, we can't load employees
            if (string.IsNullOrWhiteSpace(DraftFailure.JobNumToBlame))
                return;

            // If user selected an opcode, filter employees to that opcode
            if (DraftValidJobOperIndex >= 0 && DraftValidJobOperIndex < ValidJobOpers.Count)
            {
                var opcode = ValidJobOpers[DraftValidJobOperIndex].OpCode;
                ValidJobEmployees = await LaborService.GetLaborEmployeesByJobNumAndOpCode<EmpBasicDTO_Base>(
                    DraftFailure.JobNumToBlame.Trim(), opcode
                );

                // Attempt to re-select operator if it was previously set
                DraftValidJobEmployeeIndex = GetIndexOfEmployee(DraftFailure.OperatorToBlame) ?? -1;
            }
            else
            {
                // No valid opcode selected, revert to employees who worked on entire job
                ValidJobEmployees = await LaborService.GetLaborEmployeesByJobNum<EmpBasicDTO_Base>(
                    DraftFailure.JobNumToBlame.Trim()
                );
                DraftValidJobEmployeeIndex = GetIndexOfEmployee(DraftFailure.OperatorToBlame) ?? -1;
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

        private int? GetIndexOfEmployee(EmpBasicDTO_Base? emp)
        {
            if (emp == null) return null;
            var match = ValidJobEmployees.FirstOrDefault(e => e.EmpID == emp.EmpID);
            return match == null ? null : ValidJobEmployees.IndexOf(match);
        }

        // -------------------------------------------------------------
        // Form submission

        public async Task HandleValidSubmit()
        {
            // FailureReason
            DraftFailure.FailureReason = ValidFailureReasons
                .FirstOrDefault(fr => fr.Id == DraftFailureReasonId)
                ?? new FirstTimeYield_FailureReasonDTO(CurrentUser?.DBUser.EmployeeNumber ?? "N/A");

            // Area
            DraftFailure.AreaToBlame = Areas
                .FirstOrDefault(a => a.Id == DraftAreaToBlameId)
                ?? Areas.FirstOrDefault(a => a.Id == 12); // default "N/A"

            // OpCode
            if (DraftValidJobOperIndex >= 0 && DraftValidJobOperIndex < ValidJobOpers.Count)
            {
                DraftFailure.OpCodeToBlame = ValidJobOpers[DraftValidJobOperIndex].OpCode;
            }
            else
            {
                DraftFailure.OpCodeToBlame = "N/A";
            }

            // Operator
            if (DraftValidJobEmployeeIndex >= 0 && DraftValidJobEmployeeIndex < ValidJobEmployees.Count)
            {
                DraftFailure.OperatorToBlame = ValidJobEmployees[DraftValidJobEmployeeIndex];
            }
            else
            {
                DraftFailure.OperatorToBlame = null;
            }

            // Invoke the appropriate callback
            if (IsEdit)
            {
                await OnEditSubmit.InvokeAsync((DraftFailure, FailureEditIndex));
            }
            else
            {
                await OnAddSubmit.InvokeAsync(DraftFailure);
            }

            await Modal.Hide();
        }

        public void Cancel()
        {
            Modal.Hide();
        }

        // -------------------------------------------------------------
        // Navigation event: close modal if user navigates away

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            Cancel();
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
            GC.SuppressFinalize(this);
        }
    }
}