using Blazorise;
using System.ComponentModel.DataAnnotations;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Models
{
    public class RMAFileUploadModel : IValidatableObject
    {
        [Required(ErrorMessage = "RMA Number is required")]
        public string RmaNumber { get; set; } = default!;

        public string? RmaLineNumber { get; set; }

        public string? SerialNumber { get; set; }

        [Required(ErrorMessage = "Please select a file category")]
        public string? SelectedCategoryShortName { get; set; }

        public IReadOnlyList<IFileEntry> Files { get; set; } = new List<IFileEntry>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Files == null || !Files.Any())
            {
                yield return new ValidationResult(
                    "Please select one or more files to upload.",
                    [nameof(Files)]
                );
            }
        }
    }

    public class RMAFileUploadRequest
    {
        public string RmaNumber { get; set; } = default!;
        public string? RmaLineNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string CategoryShortName { get; set; } = default!;
        public IReadOnlyList<IFileEntry> Files { get; set; } = new List<IFileEntry>();
        
        /// <summary>
        /// How to handle files that already exist: "overwrite", "rename", "skip"
        /// If null or empty, conflict detection will return error with conflicting files
        /// </summary>
        public string? ConflictResolution { get; set; }
    }

    public class RMAFileUploadResult
    {
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        
        // More detailed error information
        public UploadErrorType? ErrorType { get; set; }
        public bool FileWrittenToDisk { get; set; }
        public bool DatabaseRecorded { get; set; }
        
        // Conflict detection properties
        public List<string>? ConflictingFiles { get; set; }
        public bool WasRenamed { get; set; }
        public string? OriginalFileName { get; set; }
        public bool WasOverwritten { get; set; }
        public int? ReplacedFileId { get; set; }
        public string? ReplacedFileUploadedBy { get; set; }
        public DateTime? ReplacedFileUploadedDate { get; set; }
    }

    public enum UploadErrorType
    {
        ValidationError,    // File type, RMA format, etc.
        FileSystemError,    // Disk write issues
        DatabaseError,      // Database recording issues
        GeneralError,       // Other errors
        ConflictDetected,   // File already exists
        Skipped            // File was skipped due to conflict
    }

    public class RMAFileQuery
    {
        public string RmaNumber { get; set; } = default!;
        public string? RmaLineNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string? CategoryShortName { get; set; }
        
        // Filter mode to distinguish between header-only vs all files
        public RMAFileFilterMode FilterMode { get; set; } = RMAFileFilterMode.HeaderOnly;
    }

    public enum RMAFileFilterMode
    {
        HeaderOnly,     // Only files with RMALineNumber IS NULL
        AllFiles,       // All files regardless of RMALineNumber
        SpecificLine    // Files for specific line/serial
    }

    public class RMAFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? RelativePath { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? Category { get; set; }
    }

    public class FileCategory
    {
        public string ShortName { get; set; } = default!;
        public string DisplayValue { get; set; } = default!;
        public string AcceptedFileTypes { get; set; } = "*";
        
        /// <summary>
        /// Indicates if this category is for detail-level (line-specific) files
        /// </summary>
        public bool IsDetailLevel { get; set; } 
        
        /// <summary>
        /// Gets the accepted file extensions as a list for validation
        /// </summary>
        public List<string> GetAcceptedExtensions()
        {
            if (AcceptedFileTypes == "*")
                return new List<string>();
                
            return AcceptedFileTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(ext => ext.Trim().ToLowerInvariant())
                .Where(ext => !string.IsNullOrEmpty(ext))
                .ToList();
        }
        
        /// <summary>
        /// Checks if a file extension is allowed for this category
        /// </summary>
        public bool IsFileTypeAllowed(string fileExtension)
        {
            if (AcceptedFileTypes == "*")
                return true;
                
            var extensions = GetAcceptedExtensions();
            return extensions.Contains(fileExtension.ToLowerInvariant());
        }
    }

    public class RMAProcessingOptions
    {
        public FileCategoriesOptions FileCategories { get; set; } = new();
        public FileStorageOptions FileStorage { get; set; } = new();
    }

    public class FileCategoriesOptions
    {
        public List<FileCategory> Header { get; set; } = new();
        public List<FileCategory> Detail { get; set; } = new();
    }

    public class FileStorageOptions
    {
        public string RmaFileRoot { get; set; } = default!;
    }

    public class RmaLineSummary
    {
        public int LineNumber { get; set; }
        public string PartNum { get; set; } = string.Empty;
        public decimal ReturnQty { get; set; }
        public List<string> Serials { get; set; } = new();

        public string SerialDisplay =>
            Serials.Count switch
            {
                0 => "No Serial",
                1 => Serials[0],
                _ => $"{Serials[0]} (+{Serials.Count - 1} more)"
            };
    }

    public class RMAFileStorageInfo
    {
        /// <summary>
        /// The root network directory where all RMA files are stored
        /// </summary>
        public string RootNetworkPath { get; set; } = default!;
        
        /// <summary>
        /// The specific directory path for this RMA
        /// </summary>
        public string RmaDirectory { get; set; } = default!;
        
        /// <summary>
        /// User-friendly display path for the network location
        /// </summary>
        public string DisplayPath { get; set; } = default!;
        
        /// <summary>
        /// Brief description of the storage structure
        /// </summary>
        public string StorageDescription { get; set; } = default!;
    }

    public class BulkDownloadRequest
    {
        public string RmaNumber { get; set; } = default!;
        public List<int> FileIds { get; set; } = new();
        public string ArchiveName { get; set; } = default!;
    }

    public class BulkDownloadResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DownloadUrl { get; set; }
        public long ArchiveSize { get; set; }
        public int FilesProcessed { get; set; }
    }
    public class TempFileTokenDTO
    {
        public int Id { get; set; }
        public string Token { get; set; } = default!;
        public string FilePath { get; set; } = default!;
        public string OriginalFileName { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool Deleted { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool PhysicalFileDeleted { get; set; }
    }

    public class BulkCategoryUpdateRequest
    {
        public List<int> FileAttachmentIds { get; set; } = new();
        public int NewCategoryId { get; set; }
        public string ModifiedByUsername { get; set; } = string.Empty;
    }

    public class BulkCategoryUpdateResponse
    {
        public bool Success { get; set; }
        public int FilesUpdated { get; set; }
        public int FilesSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    // File metadata update models 
    public class FileMetadataUpdateRequest
    {
        public int FileAttachmentId { get; set; }
        public int NewRMANumber { get; set; }
        public int? NewRMALineNumber { get; set; }
        public string? NewSerialNumber { get; set; }
        public int? NewCategoryId { get; set; }
        public string ModifiedByUsername { get; set; } = string.Empty;
    }

    public class FileMetadataUpdateResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool FileRenamed { get; set; }
        public string? NewFilePath { get; set; }
        public string? OldFilePath { get; set; }
        public RMAValidationResult? ValidationResult { get; set; }
    }

    public class BulkFileMetadataUpdateRequest
    {
        public List<int> FileAttachmentIds { get; set; } = new();
        public int? NewRMANumber { get; set; }
        public int? NewRMALineNumber { get; set; }
        public string? NewSerialNumber { get; set; }
        public int? NewCategoryId { get; set; }
        public string ModifiedByUsername { get; set; } = string.Empty;
    }

    public class BulkFileMetadataUpdateResponse
    {
        public bool Success { get; set; }
        public int FilesUpdated { get; set; }
        public int FilesSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public List<(string OldPath, string NewPath)> FilesMoved { get; set; } = new();
    }

    public class RMAValidationResult
    {
        public bool RMAExists { get; set; }
        public bool LineExists { get; set; }
        public bool IsValid => RMAExists && (LineNumber == null || LineExists);
        public int RMANumber { get; set; }
        public int? LineNumber { get; set; }
        public bool IsLegacyRMA { get; set; } 
        public string? ErrorMessage { get; set; }
        public List<int> AvailableLines { get; set; } = new();
    }
}