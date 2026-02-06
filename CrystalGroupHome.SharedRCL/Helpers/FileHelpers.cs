namespace CrystalGroupHome.SharedRCL.Helpers
{
    /// <summary>
    /// Helper methods for file operations and formatting
    /// </summary>
    public static class FileHelpers
    {
        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <returns>Formatted file size string (e.g., "1.2 MB", "345 KB", "123 bytes")</returns>
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} bytes";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:N0} KB";
            return $"{bytes / (1024 * 1024):N1} MB";
        }

        /// <summary>
        /// Gets the content type for a file based on its extension
        /// </summary>
        /// <param name="fileName">The file name including extension</param>
        /// <returns>MIME content type</returns>
        public static string GetContentType(string fileName)
        {
            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();

            if (!provider.TryGetContentType(fileName, out var contentType))
            {
                contentType = "application/octet-stream"; // fallback if unknown
            }

            return contentType;
        }
    }
}