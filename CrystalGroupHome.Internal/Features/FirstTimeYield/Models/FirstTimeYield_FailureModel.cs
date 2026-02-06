using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.SharedRCL.Data.Employees;
using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Models
{
    public class FirstTimeYield_FailureModel : IValidatableObject
    {
        public int Id { get; set; }
        public int EntryId { get; set; }
        public FirstTimeYield_FailureReasonDTO? FailureReason { get; set; }
        public int? ParentEntryQtyFailed { get; set; }
        public int Qty { get; set; } = 0;
        public FirstTimeYield_AreaDTO? AreaToBlame { get; set; }
        public string? JobNumToBlame { get; set; }
        public bool JobNumToBlameIsValid { get; set; } = false;
        public string? OpCodeToBlame { get; set; }
        public EmpBasicDTO_Base? OperatorToBlame { get; set; }
        public bool IsSelected { get; set; }

        public FirstTimeYield_FailureModel()
        {
        }

        public FirstTimeYield_FailureModel(int entryId)
        {
            EntryId = entryId;
        }

        public FirstTimeYield_FailureModel(int entryId, int? entryQtyFailed)
        {
            EntryId = entryId;
            ParentEntryQtyFailed = entryQtyFailed;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Ensure Qty is < 0
            if (Qty <= 0)
            {
                yield return new ValidationResult(
                    $"Quantity must be greater than 0.",
                    [nameof(Qty)]
                );
            }

            // Ensure Qty <= ParentEntryQtyFailed
            if (ParentEntryQtyFailed != null && Qty > ParentEntryQtyFailed)
            {
                yield return new ValidationResult(
                    $"Total Failure Quantity of the parent entry is {ParentEntryQtyFailed}. The Quantity for this Failure Reason must be equal to or less than {ParentEntryQtyFailed}.",
                    [nameof(Qty)]
                );
            }

            // Ensure an Area was selected
            if (AreaToBlame == null || AreaToBlame.Id == 12)
            {
                yield return new ValidationResult(
                    $"Please select a valid Area of Origin",
                    [nameof(AreaToBlame)]
                );
            }

            // Ensure that the entered JobNumToBlame exists
            if (!JobNumToBlameIsValid)
            {
                yield return new ValidationResult(
                    $"Please enter a valid Original Job #",
                    [nameof(JobNumToBlame)]
                );
            }
        }
    }
}
