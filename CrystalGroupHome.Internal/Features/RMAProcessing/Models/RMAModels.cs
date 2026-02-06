using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Models
{
    /// <summary>
    /// Model representing an RMA summary from vw_EpicorRMAs
    /// </summary>
    public class RMASummaryModel
    {
        public int RMANum { get; set; }
        public bool OpenRMA { get; set; }
        public string RMAStatus { get; set; } = string.Empty;
        public string? InternalNotes_c { get; set; }
        public DateTime RMADate { get; set; }
        public int? HDCaseNum { get; set; }
        public string CaseDescription { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public string SerialNumbers { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if this is a legacy RMA (from vw_LegacyIRMAs)
        /// </summary>
        public bool IsLegacyRMA { get; set; }
        
        /// <summary>
        /// Formatted RMA number for display
        /// </summary>
        public string RMANumberDisplay => RMANum.ToString();
        
        /// <summary>
        /// Indicates if this RMA has files attached
        /// </summary>
        public bool HasFiles => FileCount > 0;
        
        /// <summary>
        /// Gets a truncated version of internal notes for grid display
        /// </summary>
        public string NotesPreview => string.IsNullOrEmpty(InternalNotes_c) 
            ? string.Empty 
            : InternalNotes_c.Length > 50 
                ? InternalNotes_c.Substring(0, 50) + "..." 
                : InternalNotes_c;
                
        /// <summary>
        /// Gets the RMA type display text
        /// </summary>
        public string RMATypeDisplay => IsLegacyRMA ? "Legacy" : "Epicor";
    }

    /// <summary>
    /// Query parameters for RMA summary filtering
    /// </summary>
    public class RMASummaryQuery
    {
        public string? RMANumberFilter { get; set; }
        public bool? OpenRMAFilter { get; set; }
        public int? HDCaseNumFilter { get; set; }
        public DateTime? RMADateFrom { get; set; }
        public DateTime? RMADateTo { get; set; }
        public bool? HasFilesFilter { get; set; }
        public string? SerialNumberFilter { get; set; }
        public string? NotesFilter { get; set; }
        
        /// <summary>
        /// Filter by RMA type - null = Epicor only (default), true = legacy only, false = both Epicor and legacy
        /// </summary>
        public bool? LegacyRMAFilter { get; set; } = null; // Default to Epicor only
        
        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;
        
        /// <summary>
        /// Number of records per page
        /// </summary>
        public int PageSize { get; set; } = 50;
        
        /// <summary>
        /// Sort column name
        /// </summary>
        public string? SortBy { get; set; }
        
        /// <summary>
        /// Sort direction (asc/desc)
        /// </summary>
        public string SortDirection { get; set; } = "desc";
    }

    /// <summary>
    /// Response model for RMA summary queries
    /// </summary>
    public class RMASummaryResponse
    {
        public List<RMASummaryModel> RMAs { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

}