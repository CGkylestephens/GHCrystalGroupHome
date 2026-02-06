namespace CrystalGroupHome.SharedRCL.Data.Parts
{
    public class PartEoltDTO : PartDTO_Base
    {
        [TableColumn("P", "EOLT_EolDate_c")]
        public DateTime? EolDate { get; set; } = null;

        [TableColumn("P", "EOLTNotifyIntervalDays_c")]
        public int NotifyIntervalDays { get; set; } = 91;

        [TableColumn("P", "EOLTLastNotify_c")]
        public DateTime? LastContactDate { get; set; } = null;

        [TableColumn("P", "EOLTLastResponseDate_c")]
        public DateTime? LastProcessedSurveyResponseDate { get; set; } = null;

        [TableColumn("P", "EOLTLastTimeBuyDate_c")]
        public DateTime? LastTimeBuyDate { get; set; } = null;

        [TableColumn("P", "LTBDateConfirmed_c")]
        public bool LastTimeBuyDateConfirmed { get; set; } = false;

        [TableColumn("P", "ExcludeVendorComms_c")]
        public bool ExcludeVendorComms { get; set; } = false;

        [TableColumn("P", "EOLTReplacement_c")]
        public string ReplacementPartNum { get; set; } = string.Empty;

        [TableColumn("P", "TECH_Notes_c")]
        public string TechNotes { get; set; } = string.Empty;

        // Vendor and Manufacturer Part Information
        // These are excluded from automatic SQL generation and loaded separately
        [ExcludeFromTableColumn]
        public string? VendorPartNum { get; set; }

        [ExcludeFromTableColumn]
        public string? MfgPartNum { get; set; }

        [ExcludeFromTableColumn]
        public string? MfgName { get; set; }
    }
}
