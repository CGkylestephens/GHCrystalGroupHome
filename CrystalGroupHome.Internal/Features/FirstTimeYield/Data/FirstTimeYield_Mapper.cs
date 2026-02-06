using CrystalGroupHome.Internal.Features.FirstTimeYield.Models;
using CrystalGroupHome.SharedRCL.Data.Employees;
using CrystalGroupHome.SharedRCL.Data.Labor;
using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.FirstTimeYield.Data
{
    public static class FirstTimeYield_Mapper
    {
        #region EntryDTO -> EntryModel

        /// <summary>
        /// Converts a <see cref="FirstTimeYield_EntryDTO"/> to a <see cref="FirstTimeYield_EntryModel"/>.
        /// </summary>
        /// <param name="entryDto">The source DTO for an FTY Entry.</param>
        /// <param name="areaDto">Optional <see cref="FirstTimeYield_AreaDTO"/> that represents the area related to this entry.</param>
        /// <param name="opCodeOperator">Optional <see cref="EmpBasicDTO_Base"/> that represents the operator for this entry.</param>
        /// <param name="failureDtos">Optional list of <see cref="FirstTimeYield_FailureDTO"/> to map into <see cref="FirstTimeYield_FailureModel"/> objects.</param>
        /// <param name="failureReasonDtos">A dictionary of FailureReasonDTOs keyed by their ID, used to properly map the FailureReason for each failure.</param>
        /// <param name="allAreaDtos">Optional list of <see cref="FirstTimeYield_AreaDTO"/> for mapping areas.</param>
        /// <param name="allFailureOperators">Optional list of <see cref="EmpBasicDTO_Base"/> for mapping employees/operators.</param>
        /// <returns></returns>
        public static FirstTimeYield_EntryModel ToModel(
            this FirstTimeYield_EntryDTO entryDto,
            FirstTimeYield_AreaDTO? areaDto,
            ADUserDTO_Base? opCodeOperator,
            List<FirstTimeYield_FailureDTO>? failureDtos,
            List<FirstTimeYield_FailureReasonDTO>? failureReasonDtos,
            ADUserDTO_Base entryUser,
            ADUserDTO_Base lastModifiedUser,
            List<FirstTimeYield_AreaDTO>? allAreaDtos = null,
            List<EmpBasicDTO_Base>? allFailureOperators = null)
        {
            if (entryDto == null)
                throw new ArgumentNullException(nameof(entryDto));

            // Map basic fields
            var model = new FirstTimeYield_EntryModel
            {
                Id = entryDto.Id,
                JobNum = entryDto.JobNum,
                OpCode = entryDto.OpCode,
                QtyTested = entryDto.QtyTested,
                QtyFailed = entryDto.QtyTested - entryDto.QtyPassed,
                Notes = entryDto.Notes,
                EntryUser = entryUser,
                EntryDate = entryDto.EntryDate,
                LastModifiedUser = lastModifiedUser,
                LastModifiedDate = entryDto.LastModifiedDate,
                IsDetailExpanded = false // Not stored in the DTO, so default as needed
            };

            // Map area if provided
            if (areaDto != null)
            {
                model.Area = new FirstTimeYield_AreaDTO
                {
                    Id = areaDto.Id,
                    AreaDescription = areaDto.AreaDescription,
                    Deleted = areaDto.Deleted
                };
            }

            // Map operator if provided
            if (opCodeOperator != null)
            {
                model.OpCodeOperator = new ADUserDTO_Base
                {
                    EmployeeNumber = opCodeOperator.EmployeeNumber,
                    DisplayName = opCodeOperator.DisplayName
                };
            }

            // Map failures if a set of FailureDTOs and ReasonDTOs are provided
            if (failureDtos != null && failureReasonDtos != null)
            {
                var relevantFailures = failureDtos.Where(f => f.EntryId == entryDto.Id);
                model.Failures = new List<FirstTimeYield_FailureModel>();

                foreach (var failureDto in relevantFailures)
                {
                    // Look up the associated reason
                    var reasonDto = failureReasonDtos?.FirstOrDefault(fr => fr.Id == failureDto.ReasonID);

                    // Look up the associated area
                    var failureAreaDto = allAreaDtos?.FirstOrDefault(area => area.Id == failureDto.AreaIdToBlame);

                    // Look up the associated failure operator to blame
                    var failureOperatorToBlameDto = allFailureOperators?.FirstOrDefault(oper => oper.EmpID == failureDto.OpCodeOperatorToBlame);

                    // Map the failure
                    var failureModel = failureDto.ToModel(reasonDto, model.QtyFailed, failureAreaDto, failureOperatorToBlameDto);
                    model.Failures.Add(failureModel);
                }
            }
            else
            {
                model.Failures = new List<FirstTimeYield_FailureModel>();
            }

            return model;
        }

        #endregion

        #region FailureDTO -> FailureModel

        /// <summary>
        /// Converts a <see cref="FirstTimeYield_FailureDTO"/> to a <see cref="FirstTimeYield_FailureModel"/>.
        /// </summary>
        /// <param name="failureDto">The source DTO.</param>
        /// <param name="failureReason">The already-resolved <see cref="FirstTimeYield_FailureReasonDTO"/> that matches the DTO's ReasonID.</param>
        /// <param name="parentEntryQtyFailed">Optional number that represents the parent entry's total QtyFailed.</param>
        /// <param name="areaDto">Optional DTO to fill the area-related data for the entry.</param>
        /// <param name="operatorDto">Optional DTO to fill the operator-related data for the entry.</param>
        public static FirstTimeYield_FailureModel ToModel(
            this FirstTimeYield_FailureDTO failureDto,
            FirstTimeYield_FailureReasonDTO? failureReason,
            int parentEntryQtyFailed,
            FirstTimeYield_AreaDTO? areaDto = null,
            EmpBasicDTO_Base? operatorDto = null)
        {
            if (failureDto == null)
                throw new ArgumentNullException(nameof(failureDto));
            if (failureReason == null)
                throw new ArgumentNullException(nameof(failureReason));

            var model = new FirstTimeYield_FailureModel
            {
                Id = failureDto.Id,
                EntryId = failureDto.EntryId,
                Qty = failureDto.Qty,
                ParentEntryQtyFailed = parentEntryQtyFailed,
                FailureReason = failureReason ?? new FirstTimeYield_FailureReasonDTO(operatorDto?.EmpID ?? "N/A")
                {
                    Id = failureDto.ReasonID
                },
                AreaToBlame = areaDto ?? new FirstTimeYield_AreaDTO
                {
                    Id = failureDto.AreaIdToBlame
                },
                JobNumToBlame = failureDto.JobNumToBlame,
                OpCodeToBlame = failureDto.OpCodeToBlame,
                OperatorToBlame = operatorDto ?? new EmpBasicDTO_Base
                {
                    EmpID = failureDto.OpCodeOperatorToBlame
                },
                IsSelected = false
            };

            return model;
        }

        #endregion

        #region EntryModel -> EntryDTO/FailureDTOs

        /// <summary>
        /// Maps a <see cref="FirstTimeYield_EntryModel"/> to a <see cref="FirstTimeYield_EntryDTO"/> 
        /// (without handling child Failures).
        /// </summary>
        public static FirstTimeYield_EntryDTO ToDto(this FirstTimeYield_EntryModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            return new FirstTimeYield_EntryDTO
            {
                Id = model.Id,
                JobNum = model.JobNum ?? string.Empty,
                OpCode = model.OpCode ?? string.Empty,
                // The model stores operator info as EmpBasicDTO_Base; 
                // the DTO wants a string EmpID here:
                OpCodeOperator = model.OpCodeOperator?.EmployeeNumber ?? string.Empty,

                // The model references an Area object, but the DTO only needs an int ID:
                AreaId = model.Area?.Id ?? 12, // default if area is null

                QtyTested = model.QtyTested,
                QtyPassed = model.QtyTested - model.QtyFailed,
                Notes = model.Notes ?? string.Empty,

                EntryUser = model.EntryUser.EmployeeNumber,
                EntryDate = model.EntryDate,
                LastModifiedUser = model.LastModifiedUser.EmployeeNumber,
                LastModifiedDate = model.LastModifiedDate,

                Deleted = false
            };
        }

        /// <summary>
        /// Maps the Failures in a <see cref="FirstTimeYield_EntryModel"/> to a list of <see cref="FirstTimeYield_FailureDTO"/>.
        /// By default, uses the parent entry's <c>EntryUser</c> and <c>EntryDate</c> for each failure record.
        /// </summary>
        public static List<FirstTimeYield_FailureDTO> ToFailureDtos(this FirstTimeYield_EntryModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            // TODO: We don't have code to support putting in a per-failure entry user and date yet.
            // Just doing this for now.
            var entryUser = model.EntryUser.EmployeeNumber;
            var entryDate = model.EntryDate;

            return model.Failures?.Select(f => f.ToDto(entryUser, entryDate)).ToList()
                   ?? new List<FirstTimeYield_FailureDTO>();
        }

        /// <summary>
        /// Convenience method that returns both the <see cref="FirstTimeYield_EntryDTO"/>
        /// and its corresponding <see cref="FirstTimeYield_FailureDTO"/> list 
        /// in a single call.
        /// </summary>
        public static (FirstTimeYield_EntryDTO entryDto, List<FirstTimeYield_FailureDTO> failureDtos)
            ToDtosWithFailures(this FirstTimeYield_EntryModel model)
        {
            var entryDto = model.ToDto();
            var failureDtos = model.ToFailureDtos();
            return (entryDto, failureDtos);
        }

        #endregion

        #region FailureModel -> FailureDTO

        /// <summary>
        /// Maps a single <see cref="FirstTimeYield_FailureModel"/> to a <see cref="FirstTimeYield_FailureDTO"/>.
        /// </summary>
        /// <param name="model">The failure model.</param>
        /// <param name="parentEntryUser">Typically the parent entry's user (if each failure doesn't store a user separately).</param>
        /// <param name="parentEntryDate">Typically the parent entry's date (if each failure doesn't store a date separately).</param>
        public static FirstTimeYield_FailureDTO ToDto(
            this FirstTimeYield_FailureModel model,
            string parentEntryUser,
            DateTime parentEntryDate)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            return new FirstTimeYield_FailureDTO
            {
                Id = model.Id,
                EntryId = model.EntryId,

                // The model's reason is a FailureReasonDTO with Id, etc.
                // The FTY_FailureDTO expects ReasonID (int) and ReasonDescriptionOther (string).
                ReasonID = model.FailureReason?.Id ?? 11,   // fallback to "Other"?
                ReasonDescriptionOther = string.Empty,      // Not currently stored in the FailureModel. 
                                                            // If your UI provides a text box for "Other," pass it in.

                Qty = model.Qty,

                // The model's "AreaToBlame" is a FirstTimeYield_AreaDTO; 
                // The DTO only needs an int AreaIdToBlame:
                AreaIdToBlame = model.AreaToBlame?.Id ?? 12, // fallback if null

                JobNumToBlame = model.JobNumToBlame ?? string.Empty,
                OpCodeToBlame = model.OpCodeToBlame ?? string.Empty,

                // The operator to blame is an EmpBasicDTO_Base, 
                // but the FTY_FailureDTO expects just a string EmpID:
                OpCodeOperatorToBlame = model.OperatorToBlame?.EmpID ?? string.Empty,

                // The FTY_FailureDTO includes EntryUser & EntryDate, 
                // but the FailureModel doesn't store these. 
                // We'll default them to the parent's user & date:
                EntryUser = parentEntryUser,
                EntryDate = parentEntryDate,

                Deleted = false
            };
        }

        #endregion

        #region AreaFailureReason Conversion

        /// <summary>
        /// Converts a list of FirstTimeYield_AreaFailureReasonDTO to a list of FirstTimeYield_AreaFailureReasonModel.
        /// Groups the DTOs by FailureId and aggregates the related AreaDTOs.
        /// </summary>
        /// <param name="dtos">The list of AreaFailureReasonDTOs.</param>
        /// <returns>A list of grouped AreaFailureReasonModels.</returns>
        public static List<FirstTimeYield_AreaFailureReasonModel> ToModels(this List<FirstTimeYield_AreaFailureReasonDTO> dtos)
        {
            if (dtos == null || !dtos.Any())
                return new List<FirstTimeYield_AreaFailureReasonModel>();

            // Group by FailureId and aggregate areas
            var groupedByFailure = dtos.GroupBy(dto => dto.FailureId)
                .Select(group => new
                {
                    FailureId = group.Key,
                    FailureDescription = group.First().FailureDescription,
                    FailureDeleted = group.First().FailureDeleted,
                    EntryUser = group.First().EntryUser,
                    EntryDate = group.First().EntryDate,
                    LastModifiedUser = group.First().LastModifiedUser,
                    LastModifiedDate = group.First().LastModifiedDate,
                    Areas = group.Select(dto => new FirstTimeYield_AreaDTO
                    {
                        Id = dto.AreaId,
                        AreaDescription = dto.AreaDescription,
                        Deleted = dto.AreaDeleted
                    }).ToList()
                });

            var models = new List<FirstTimeYield_AreaFailureReasonModel>();

            foreach (var group in groupedByFailure)
            {
                var failureReason = new FirstTimeYield_FailureReasonDTO
                {
                    Id = group.FailureId,
                    FailureDescription = group.FailureDescription,
                    Deleted = group.FailureDeleted,
                    EntryUser = group.EntryUser,
                    EntryDate = group.EntryDate,
                    LastModifiedUser = group.LastModifiedUser,
                    LastModifiedDate = group.LastModifiedDate
                };

                models.Add(new FirstTimeYield_AreaFailureReasonModel(failureReason, group.Areas));
            }
            return models;
        }

        /// <summary>
        /// Converts a single FirstTimeYield_AreaFailureReasonDTO to a FirstTimeYield_AreaFailureReasonModel.
        /// </summary>
        /// <param name="dto">The AreaFailureReasonDTO instance.</param>
        /// <returns>The corresponding AreaFailureReasonModel.</returns>
        public static FirstTimeYield_AreaFailureReasonModel ToModel(this FirstTimeYield_AreaFailureReasonDTO dto)
        {
            if (dto == null)
                return new FirstTimeYield_AreaFailureReasonModel();

            var failureReason = new FirstTimeYield_FailureReasonDTO
            {
                Id = dto.FailureId,
                FailureDescription = dto.FailureDescription,
                Deleted = dto.FailureDeleted,
                EntryUser = dto.EntryUser,
                EntryDate = dto.EntryDate,
                LastModifiedUser = dto.LastModifiedUser,
                LastModifiedDate = dto.LastModifiedDate
            };

            var area = new FirstTimeYield_AreaDTO
            {
                Id = dto.AreaId,
                AreaDescription = dto.AreaDescription,
                Deleted = dto.AreaDeleted
            };

            return new FirstTimeYield_AreaFailureReasonModel(failureReason, new List<FirstTimeYield_AreaDTO> { area });
        }

        #endregion
    }
}
