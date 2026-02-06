using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.SharedRCL.Data.Labor;
using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Models
{
    public class FirstTimeYield_EntryModel : IValidatableObject
    {
        public const string jobNumParamName = "JobNum";
        public const string opCodeParamName = "OpCode";
        public const string opCodeOperatorIDParamName = "OperatorID";
        public const string areaIdParamName = "AreaId";
        public const string qtyTestedParamName = "QtyTested";
        public const string qtyFailedParamName = "QtyFailed";
        public const string entryUserParamName = "EntryUser";
        public const string dateEnteredOnOrBeforeParamName = "DateEnteredOnOrBefore";
        public const string dateEnteredOnOrAfterParamName = "DateEnteredOnOrAfter";
        public const string modifiedUserParamName = "ModifiedUser";
        public const string dateModifiedOnOrBeforeParamName = "DateModifiedOnOrBefore";
        public const string dateModifiedOnOrAfterParamName = "DateModifiedOnOrAfter";

        public int Id { get; set; }
        public string JobNum { get; set; }
        public string OpCode { get; set; }
        public ADUserDTO_Base? OpCodeOperator { get; set; }
        public FirstTimeYield_AreaDTO? Area { get; set; }
        public decimal OrigProdQty = 0;
        private int _qtyTested = 0;
        public int QtyTested { 
            get => _qtyTested;
            set
            {
                if (value < 0)
                {
                    _qtyTested = 0;
                }
                else
                {
                    _qtyTested = value;
                }
            }
        }
        public int QtyPassed { get { return QtyTested - QtyFailed; } }
        public int QtyFailed { get; set; }
        public float YieldPct { get { return (float)QtyPassed / (float)QtyTested; } }
        public ADUserDTO_Base EntryUser { get; set; }
        public DateTime EntryDate { get; set; }
        public ADUserDTO_Base LastModifiedUser { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public List<FirstTimeYield_FailureModel> Failures { get; set; }
        public string Notes { get; set; }
        public bool IsDetailExpanded { get; set; } = false;
        public bool IsValidJobNum { get; set; } = false;
        public bool CurrentJobNumHasBeenSearched { get; set; } = false;

        public FirstTimeYield_EntryModel()
        {
            Id = -1;
            JobNum = string.Empty;
            OpCode = string.Empty;
            OpCodeOperator = new();
            Area = new();
            QtyTested = 0;
            QtyFailed = 0;
            EntryUser = new();
            EntryDate = DateTime.Now;
            LastModifiedUser = new();
            LastModifiedDate = DateTime.Now;
            Failures = new List<FirstTimeYield_FailureModel>();
            Notes = string.Empty;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Ensure positive QtyTested
            if (QtyTested <= 0)
            {
                yield return new ValidationResult(
                    "Quantity Tested must be greater than zero.",
                    new[] { nameof(QtyTested) }
                );
            }

            // Ensure the QtyTested is not greater than the original ProdQty
            // CNC op codes are exempt from this limit as they may intentionally test higher quantities
            bool isCncOpCode = !string.IsNullOrEmpty(OpCode) && OpCode.Contains("CNC-", StringComparison.OrdinalIgnoreCase);
            if (!isCncOpCode && QtyTested > OrigProdQty)
            {
                yield return new ValidationResult(
                    $"The Quantity Tested cannot be greater than the original Production Quantity ({Decimal.Round(OrigProdQty)}).",
                    new[] { nameof(QtyTested) }
                );
            }

            // Ensure QtyFailed <= QtyTested
            if (QtyFailed > QtyTested)
            {
                yield return new ValidationResult(
                    "Quantity Failed cannot exceed Quantity Tested.",
                    new[] { nameof(QtyFailed) }
                );
            }

            // Ensure minimum number of Failure Reasons have been entered when QtyFailed > 0
            if (QtyFailed > 0)
            {
                int enteredFailures = 0;
                foreach (var failure in Failures)
                {
                    enteredFailures += failure.Qty;
                }

                if (enteredFailures < QtyFailed)
                {
                    yield return new ValidationResult(
                        "Not enough Failure Reasons have been entered to account for the Quantity Failed value.",
                        new[] { nameof(QtyFailed) }
                    );
                }
            }

            // Ensure the JobNum has been searched
            if (!CurrentJobNumHasBeenSearched)
            {
                yield return new ValidationResult(
                    "Please search the entered Job Number.",
                    new[] { nameof(JobNum) }
                );
            }

            // Ensure the user selected a valid Area (12 is N/A)
            if (Area == null || Area.Id <= 0 || Area.Id == 12)
            {
                yield return new ValidationResult(
                    "Please select an Area.",
                    new[] { nameof(Area) }
                );
            }

            // Ensure a notes explanation if OpCode and OpCodeOperator are not selected.
            if (OpCode == "N/A" && string.IsNullOrEmpty(OpCodeOperator?.EmployeeNumber) && string.IsNullOrEmpty(Notes))
            {
                yield return new ValidationResult(
                    "You have not provided an Op Code or Operator. Please explain in the Notes section.",
                    new[] { nameof(OpCode) }
                );
            }

            // Ensure valid JobNum
            if (CurrentJobNumHasBeenSearched && !IsValidJobNum)
            {
                yield return new ValidationResult(
                    "The entered Job Number could not be found.",
                    new[] { nameof(JobNum) }
                );
            }
        }

        public void AddFailure(FirstTimeYield_FailureModel failure)
        {
            Failures.Add(failure);
        }
    }
}
