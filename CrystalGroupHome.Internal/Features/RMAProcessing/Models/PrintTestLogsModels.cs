namespace CrystalGroupHome.Internal.Features.RMAProcessing.Models
{
    /// <summary>
    /// Model for print options when printing test logs
    /// </summary>
    public class PrintTestLogsOptions
    {
        /// <summary>
        /// Whether to include page numbers and file names in footer
        /// </summary>
        public bool IncludePageNumbers { get; set; } = true;
        
        /// <summary>
        /// Whether to insert blank pages to ensure each log starts on odd page for duplex printing
        /// </summary>
        public bool InsertBlankPagesForDuplex { get; set; } = true;
    }
    
    /// <summary>
    /// Request model for printing test logs
    /// </summary>
    public class PrintTestLogsRequest
    {
        /// <summary>
        /// RMA number
        /// </summary>
        public string RmaNumber { get; set; } = default!;
        
        /// <summary>
        /// RMA line number
        /// </summary>
        public string? RmaLineNumber { get; set; }
        
        /// <summary>
        /// Serial number
        /// </summary>
        public string? SerialNumber { get; set; }
        
        /// <summary>
        /// List of file attachment IDs to include in print job
        /// </summary>
        public List<int> SelectedFileIds { get; set; } = new();
        
        /// <summary>
        /// Print options
        /// </summary>
        public PrintTestLogsOptions PrintOptions { get; set; } = new();
    }
    
    /// <summary>
    /// Response model for print test logs operation
    /// </summary>
    public class PrintTestLogsResponse
    {
        /// <summary>
        /// Whether the print job was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if not successful
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Number of files processed
        /// </summary>
        public int FilesProcessed { get; set; }
        
        /// <summary>
        /// Total number of pages in the generated document
        /// </summary>
        public int TotalPages { get; set; }
        
        /// <summary>
        /// PDF data as byte array
        /// </summary>
        public byte[]? PdfData { get; set; }
        
        /// <summary>
        /// Generated filename
        /// </summary>
        public string? FileName { get; set; }
    }
}