using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Models
{
    /// <summary>
    /// Represents an application from Epicor Application Studio (MetaFX)
    /// </summary>
    public class ApplicationDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public bool IsLayerDisabled { get; set; }
        public bool SystemFlag { get; set; }
        public bool HasDraftContent { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public bool CanAccessBase { get; set; }
        public string SecurityCode { get; set; } = string.Empty;
        public List<ApplicationLayerDefinition> Layers { get; set; } = new();
    }

    /// <summary>
    /// Represents a customization layer for an application in Epicor Application Studio
    /// </summary>
    public class ApplicationLayerDefinition
    {
        // Layer identification
        public string Id { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public string Type { get; set; } = "view";
        public string TypeCode { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string CGCCode { get; set; } = string.Empty;
        
        // Layer metadata
        public DateTime LastUpdated { get; set; }
        public bool IsPublished { get; set; }
        public bool SystemFlag { get; set; }
        public bool HasDraftContent { get; set; }
        public string LastUpdatedBy { get; set; } = string.Empty;
        public bool IsLayerDisabled { get; set; }

        // Layer content (populated when exported)
        /// <summary>
        /// The full raw JSON content of the layer file
        /// </summary>
        public string LayerContent { get; set; } = string.Empty;

        /// <summary>
        /// The extracted and formatted "Content" field - PUBLISHED customizations
        /// </summary>
        public string PublishedContent { get; set; } = string.Empty;

        /// <summary>
        /// The extracted and formatted "SysCharacter03" field - DRAFT/unpublished changes
        /// </summary>
        public string DraftContent { get; set; } = string.Empty;

        // Computed hashes for comparison
        /// <summary>
        /// Hash of the published content (Content field)
        /// </summary>
        public string PublishedContentHash { get; private set; } = string.Empty;

        /// <summary>
        /// Hash of the draft content (SysCharacter03 field)
        /// </summary>
        public string DraftContentHash { get; private set; } = string.Empty;

        /// <summary>
        /// Combined hash for overall comparison (considers both published and draft)
        /// </summary>
        public string ContentHash { get; private set; } = string.Empty;

        /// <summary>
        /// Gets a unique identifier for this layer combining application ID and layer name
        /// </summary>
        public string GetUniqueIdentifier() => $"{Id}|{LayerName}|{TypeCode}";

        /// <summary>
        /// Computes hashes for published content, draft content, and combined
        /// </summary>
        public void ComputeContentHash()
        {
            using var sha256 = SHA256.Create();

            // Hash published content
            if (!string.IsNullOrEmpty(PublishedContent))
            {
                var publishedBytes = Encoding.UTF8.GetBytes(PublishedContent);
                var publishedHash = sha256.ComputeHash(publishedBytes);
                PublishedContentHash = BitConverter.ToString(publishedHash).Replace("-", "").ToLowerInvariant();
            }

            // Hash draft content
            if (!string.IsNullOrEmpty(DraftContent))
            {
                var draftBytes = Encoding.UTF8.GetBytes(DraftContent);
                var draftHash = sha256.ComputeHash(draftBytes);
                DraftContentHash = BitConverter.ToString(draftHash).Replace("-", "").ToLowerInvariant();
            }

            // Combined hash for overall comparison
            var combined = new
            {
                Id,
                LayerName,
                TypeCode,
                DeviceType,
                Published = PublishedContent,
                Draft = DraftContent
            };
            var json = JsonSerializer.Serialize(combined);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = sha256.ComputeHash(bytes);
            ContentHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Extracts and formats the Content (published) and SysCharacter03 (draft) fields from the layer JSON
        /// </summary>
        public void ExtractContentFields()
        {
            if (string.IsNullOrEmpty(LayerContent))
                return;

            try
            {
                using var doc = JsonDocument.Parse(LayerContent);
                var root = doc.RootElement;

                // Extract and format the "Content" field (PUBLISHED content)
                if (root.TryGetProperty("Content", out var contentElement))
                {
                    var contentString = contentElement.GetString();
                    if (!string.IsNullOrEmpty(contentString))
                    {
                        PublishedContent = FormatJsonString(contentString);
                    }
                }

                // Extract and format the "SysCharacter03" field (DRAFT content)
                if (root.TryGetProperty("SysCharacter03", out var sysChar03Element))
                {
                    var sysChar03String = sysChar03Element.GetString();
                    if (!string.IsNullOrEmpty(sysChar03String) && 
                        (sysChar03String.TrimStart().StartsWith("{") || sysChar03String.TrimStart().StartsWith("[")))
                    {
                        DraftContent = FormatJsonString(sysChar03String);
                    }
                }
            }
            catch
            {
                // If parsing fails, leave extracted content empty
            }
        }

        /// <summary>
        /// Attempts to parse and format a JSON string for better readability
        /// </summary>
        private static string FormatJsonString(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                return JsonSerializer.Serialize(doc, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch
            {
                // If it's not valid JSON, return as-is
                return jsonString;
            }
        }

        /// <summary>
        /// Gets a display-friendly name for the layer
        /// </summary>
        public string DisplayName => $"{Id} - {LayerName}";

        /// <summary>
        /// Returns true if published content is available
        /// </summary>
        public bool HasPublishedContent => !string.IsNullOrEmpty(PublishedContent);

        /// <summary>
        /// Returns true if draft (unpublished) content exists
        /// </summary>
        public bool HasDraft => !string.IsNullOrEmpty(DraftContent);

        // Legacy property aliases for backward compatibility
        public string ExtractedContent => PublishedContent;
        public string ExtractedSysCharacter03 => DraftContent;
        public bool HasExtractedContent => HasPublishedContent;
    }

    /// <summary>
    /// Categorizes the type of difference between two application layers
    /// </summary>
    public enum LayerDifferenceType
    {
        /// <summary>
        /// No differences - layers are identical
        /// </summary>
        None,

        /// <summary>
        /// Only published content differs
        /// </summary>
        PublishedOnly,

        /// <summary>
        /// Only draft content differs (one or both have unpublished changes)
        /// </summary>
        DraftOnly,

        /// <summary>
        /// Both published and draft content differ
        /// </summary>
        Both
    }

    /// <summary>
    /// Extension methods for layer comparison
    /// </summary>
    public static class ApplicationLayerComparisonExtensions
    {
        /// <summary>
        /// Determines the type of difference between two layers
        /// </summary>
        public static LayerDifferenceType GetDifferenceType(
            this ApplicationLayerDefinition source, 
            ApplicationLayerDefinition target)
        {
            bool publishedDiffers = source.PublishedContentHash != target.PublishedContentHash;
            bool draftDiffers = source.DraftContentHash != target.DraftContentHash ||
                               source.HasDraft != target.HasDraft;

            return (publishedDiffers, draftDiffers) switch
            {
                (false, false) => LayerDifferenceType.None,
                (true, false) => LayerDifferenceType.PublishedOnly,
                (false, true) => LayerDifferenceType.DraftOnly,
                (true, true) => LayerDifferenceType.Both
            };
        }
    }
}
