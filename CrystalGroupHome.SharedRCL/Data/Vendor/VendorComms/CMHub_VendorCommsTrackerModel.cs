using CrystalGroupHome.SharedRCL.Data.Parts;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms
{
    public class CMHub_VendorCommsTrackerModel
    {
        public CMHub_VendorCommsTrackerDTO Tracker { get; set; } = new();

        // Calculated property for the next contact date
        public DateTime? NextContactDate => PartEolt.LastTimeBuyDate != null ? null : LatestSurveyResponseDate?.AddDays(PartEolt.NotifyIntervalDays);

        // Store the latest actual response date (populated separately via service call)
        [NotMapped]
        public DateTime? LatestSurveyResponseDate { get; set; }

        // Calculated property to check if tracker is awaiting a response
        [NotMapped]
        public bool IsAwaitingResponse
        {
            get
            {
                // Must have contact history to be awaiting a response
                if (PartEolt.LastContactDate == null)
                    return false;

                // If no response has ever been received, then awaiting response
                if (LatestSurveyResponseDate == null)
                    return true;

                // If the last response was before the last contact, then awaiting response
                return LatestSurveyResponseDate < PartEolt.LastContactDate;
            }
        }

        // Calculated property to check if a tracker has a response that hasn't been processed yet (date-based only)
        [NotMapped]
        public bool HasUnprocessedResponseDate
        {
            get
            {
                // Must have contact history and a response to have unprocessed response
                if (PartEolt.LastContactDate == null || LatestSurveyResponseDate == null)
                    return false;

                // If no response has ever been processed, or the latest response is newer than the last processed
                return PartEolt.LastProcessedSurveyResponseDate == null ||
                       LatestSurveyResponseDate > PartEolt.LastProcessedSurveyResponseDate;
            }
        }

        // Cache for whether the tracker has processable responses (responses that can update Epicor fields)
        // This is populated by the service layer when loading trackers
        [NotMapped]
        public bool HasProcessableResponses { get; set; }

        // Property that combines date check with processable check for UI filtering
        [NotMapped]
        public bool HasUnprocessedResponse => HasUnprocessedResponseDate && HasProcessableResponses;

        // Calculated property to determine if contact is not necessary due to EOL date and LTB date already entered
        [NotMapped]
        public bool IsContactNecessary => PartEolt.EolDate == null || PartEolt.LastTimeBuyDate == null;

        // Properties to hold related data, not mapped to the DB table directly
        public PartEoltDTO PartEolt { get; set; } = new();
        public VendorDTO_Base? Vendor { get; set; }
        public List<CMHub_VendorCommsTrackerLogModel> TrackerLogs { get; set; } = new();

        [NotMapped]
        public bool IsSelected { get; set; } = false;
        [NotMapped]
        public bool IsNew { get; set; } = false;

        // Flattened properties for the DataGrid
        [NotMapped]
        public string? PartNum => PartEolt?.PartNum;
        [NotMapped]
        public string? VendorPartNum => PartEolt?.VendorPartNum;
        [NotMapped]
        public string? PartDescription => PartEolt?.PartDescription;
        [NotMapped]
        public string? VendorName => Vendor?.VendorName;
        [NotMapped]
        public string? VendorContactEmail => Vendor?.EmailAddress;
        [NotMapped]
        public DateTime? EolDate => PartEolt.EolDate;
        [NotMapped]
        public DateTime? LastTimeBuyDate => PartEolt.LastTimeBuyDate;
    }
}