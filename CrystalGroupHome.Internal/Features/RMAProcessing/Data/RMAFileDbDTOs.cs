using System.ComponentModel.DataAnnotations.Schema;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Data
{
    // Database model for file categories
    public class RMAFileCategoryDTO
    {
        public int Id { get; set; }
        public string ShortName { get; set; } = default!;
        public string DisplayValue { get; set; } = default!;
        public string AcceptedFileTypes { get; set; } = default!;
        public bool IsDetailLevel { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CreatedByUsername { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedByUsername { get; set; }
        public DateTime? ModifiedDate { get; set; }

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

    public class RMAFileAttachmentDTO
    {
        public int Id { get; set; }
        public int RMANumber { get; set; }
        public int? RMALineNumber { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int CategoryId { get; set; }
        public string UploadedByUsername { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public bool Deleted { get; set; }
        public string? DeletedByUsername { get; set; }
        public DateTime? DeletedDate { get; set; }

        // Navigation property for display purposes (not stored in DB)
        public RMAFileCategoryDTO? Category { get; set; }

        /// <summary>
        /// Gets the file extension from the file name
        /// </summary>
        public string GetFileExtension()
        {
            return Path.GetExtension(FileName).ToLowerInvariant();
        }

        /// <summary>
        /// Computed property: Serial numbers for this file based on RMA + Line lookup
        /// This replaces the old SerialNumber column with view-based data
        /// </summary>
        [NotMapped]
        public List<string> SerialNumbers { get; set; } = new();

        /// <summary>
        /// Computed property: Primary serial number for display (first in list or null)
        /// This provides backward compatibility for UI that expects a single serial
        /// </summary>
        [NotMapped]
        public string? SerialNumber => SerialNumbers.FirstOrDefault();

        /// <summary>
        /// Computed property: Formatted serial display for UI
        /// </summary>
        [NotMapped]
        public string SerialDisplay => SerialNumbers.Count switch
        {
            0 => "No Serial",
            1 => SerialNumbers[0],
            _ => $"{SerialNumbers[0]} (+{SerialNumbers.Count - 1} more)"
        };
    }

    public class RMAFileAttachmentLogDTO
    {
        public int Id { get; set; }
        public int FileAttachmentId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? ActionDetails { get; set; }
        public string ActionByUsername { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
        public bool IsSystemAction { get; set; }
    }
}
