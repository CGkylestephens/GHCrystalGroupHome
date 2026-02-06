using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.Internal.Features.RMAProcessing.Pages;
using CrystalGroupHome.SharedRCL.Data;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Data
{
    public interface IRMAFileStorageService
    {
        Task<List<RMAFileUploadResult>> UploadFilesAsync(RMAFileUploadRequest request);
        Task<List<RMAFileInfo>> GetFilesAsync(RMAFileQuery query);
        Task<bool> DeleteFileAsync(string filePath, string? deletedByUsername = null);
        Task<(Stream? fileStream, string fileName, string contentType)> GetFileForDownloadAsync(string filePath);
        string GetContentType(string fileName);
        RMAFileStorageInfo GetStorageInfo(string rmaNumber, string? rmaLineNumber = null);
        Task<string> CalculateDestinationPathAsync(RMAFileUploadRequest request);
        string CalculateBasePath(RMAFileQuery query);
    }

    public class RMAFileStorageService : IRMAFileStorageService
    {
        private readonly RMAProcessingOptions _options;
        private readonly ILogger<RMAFileStorageService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IRMAFileCategoryService _categoryService;
        private readonly IRMAFileDataService _dataService;

        public RMAFileStorageService(
            IOptions<RMAProcessingOptions> options,
            ILogger<RMAFileStorageService> logger,
            IHttpContextAccessor httpContextAccessor,
            IRMAFileCategoryService categoryService,
            IRMAFileDataService dataService)
        {
            _options = options.Value;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _categoryService = categoryService;
            _dataService = dataService;
        }

        public async Task<List<RMAFileUploadResult>> UploadFilesAsync(RMAFileUploadRequest request)
        {
            var results = new List<RMAFileUploadResult>();
            
            // NEW: Check upload permission before processing any files
            var user = _httpContextAccessor.HttpContext?.User;
            if (!RMAProcessingBase.HasFileUploadEditPermission(user))
            {
                _logger.LogWarning("Upload attempt denied for user {Username} - insufficient permissions", GetCurrentUsername());
                results.Add(new RMAFileUploadResult 
                { 
                    Success = false, 
                    ErrorMessage = "You do not have permission to upload files. This feature is restricted to Technical Services users.",
                    ErrorType = UploadErrorType.ValidationError
                });
                return results;
            }

            var currentUsername = GetCurrentUsername();
            var uploadedFiles = new List<string>(); // Track files written to disk for cleanup
            
            try
            {
                // Validate category before any file operations
                var selectedCategory = await _categoryService.GetFileCategoryByShortNameAsync(
                    request.CategoryShortName, 
                    !string.IsNullOrEmpty(request.RmaLineNumber));
                    
                if (selectedCategory == null) 
                {
                    results.Add(new RMAFileUploadResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Selected category is not valid.",
                        ErrorType = UploadErrorType.ValidationError
                    });
                    return results;
                }

                // Validate RMA number format early
                if (!int.TryParse(request.RmaNumber, out int rmaNumber))
                {
                    results.Add(new RMAFileUploadResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Invalid RMA Number format",
                        ErrorType = UploadErrorType.ValidationError
                    });
                    return results;
                }

                string destinationPath = await CalculateDestinationPathAsync(request);
                Directory.CreateDirectory(destinationPath);

                // NEW: Pre-upload conflict detection
                var conflictingFiles = new List<string>();
                var conflictingFileDetails = new Dictionary<string, RMAFileAttachmentDTO>();
                
                foreach (var file in request.Files)
                {
                    var filePath = System.IO.Path.Combine(destinationPath, file.Name);
                    if (File.Exists(filePath))
                    {
                        conflictingFiles.Add(file.Name);
                        
                        // Try to get the existing file info from database
                        var existingFile = await _dataService.GetFileAttachmentByPathAsync(filePath);
                        if (existingFile != null)
                        {
                            conflictingFileDetails[file.Name] = existingFile;
                        }
                    }
                }

                // If conflicts exist and no resolution strategy provided, return conflict info
                if (conflictingFiles.Any() && string.IsNullOrEmpty(request.ConflictResolution))
                {
                    var conflictMessage = $"{conflictingFiles.Count} file(s) already exist: {string.Join(", ", conflictingFiles)}. " +
                                        "Please choose how to handle these conflicts.";
                    
                    results.Add(new RMAFileUploadResult 
                    { 
                        Success = false,
                        ErrorMessage = conflictMessage,
                        ErrorType = UploadErrorType.ConflictDetected,
                        ConflictingFiles = conflictingFiles
                    });
                    return results;
                }

                foreach (var file in request.Files)
                {
                    var uploadResult = new RMAFileUploadResult { FileName = file.Name };
                    string? filePath = null;
                    
                    try
                    {
                        // Validate file extension
                        var fileExtension = System.IO.Path.GetExtension(file.Name).ToLowerInvariant();
                        var fileCategory = new FileCategory 
                        { 
                            ShortName = selectedCategory.ShortName,
                            DisplayValue = selectedCategory.DisplayValue,
                            AcceptedFileTypes = selectedCategory.AcceptedFileTypes
                        };
                        
                        if (!fileCategory.IsFileTypeAllowed(fileExtension))
                        {
                            var acceptedTypes = selectedCategory.AcceptedFileTypes == "*" 
                                ? "any file type" 
                                : selectedCategory.AcceptedFileTypes;
                                
                            uploadResult.Success = false;
                            uploadResult.ErrorMessage = $"File type '{fileExtension}' is not allowed for {selectedCategory.DisplayValue}. Accepted types: {acceptedTypes}";
                            uploadResult.ErrorType = UploadErrorType.ValidationError;
                            results.Add(uploadResult);
                            continue;
                        }

                        // NEW: Handle file conflicts based on resolution strategy
                        filePath = System.IO.Path.Combine(destinationPath, file.Name);
                        RMAFileAttachmentDTO? existingFileForOverwrite = null;
                        bool isOverwritingExistingFile = false;
                        
                        if (File.Exists(filePath))
                        {
                            switch (request.ConflictResolution?.ToLower())
                            {
                                case "skip":
                                    uploadResult.Success = false;
                                    uploadResult.ErrorMessage = $"File already exists (skipped as requested)";
                                    uploadResult.ErrorType = UploadErrorType.Skipped;
                                    results.Add(uploadResult);
                                    continue;
                                    
                                case "rename":
                                    var newFileName = GenerateUniqueFileName(destinationPath, file.Name);
                                    filePath = System.IO.Path.Combine(destinationPath, newFileName);
                                    uploadResult.WasRenamed = true;
                                    uploadResult.OriginalFileName = file.Name;
                                    uploadResult.FileName = newFileName;
                                    _logger.LogInformation("File {OriginalName} renamed to {NewName} to avoid conflict", 
                                        file.Name, newFileName);
                                    break;
                                    
                                case "overwrite":
                                    // Get existing file info - we'll UPDATE this record instead of creating new one
                                    var existingFile = await _dataService.GetFileAttachmentByPathAsync(filePath);
                                    if (existingFile != null)
                                    {
                                        existingFileForOverwrite = existingFile;
                                        isOverwritingExistingFile = true;
                                        uploadResult.WasOverwritten = true;
                                        uploadResult.ReplacedFileId = existingFileForOverwrite.Id;
                                        uploadResult.ReplacedFileUploadedBy = existingFileForOverwrite.UploadedByUsername;
                                        uploadResult.ReplacedFileUploadedDate = existingFileForOverwrite.UploadedDate;
                                        
                                        _logger.LogInformation("File {FileName} (ID: {FileId}) will be overwritten by {Username}", 
                                            existingFileForOverwrite.FileName, existingFileForOverwrite.Id, currentUsername);
                                    }
                                    break;
                                    
                                default:
                                    // Should not reach here if validation is correct
                                    uploadResult.Success = false;
                                    uploadResult.ErrorMessage = "Invalid conflict resolution strategy";
                                    uploadResult.ErrorType = UploadErrorType.ValidationError;
                                    results.Add(uploadResult);
                                    continue;
                            }
                        }

                        // Write file to disk
                        await using (var fs = new FileStream(filePath, FileMode.Create))
                        {
                            await file.WriteToStreamAsync(fs);
                        }
                        uploadedFiles.Add(filePath); // Track for potential cleanup
                        uploadResult.FileWrittenToDisk = true;

                        // Get file info after successful write
                        var fileInfo = new FileInfo(filePath);
                        
                        // Parse line number
                        int? rmaLineNumber = null;
                        if (!string.IsNullOrEmpty(request.RmaLineNumber) && 
                            int.TryParse(request.RmaLineNumber, out int lineNum))
                        {
                            rmaLineNumber = lineNum;
                        }

                        // Database operation - either UPDATE existing record or CREATE new one
                        int fileAttachmentId;
                        try
                        {
                            if (isOverwritingExistingFile && existingFileForOverwrite != null)
                            {
                                // UPDATE: Keep same record, just update size and uploader info
                                await _dataService.UpdateFileAttachmentForOverwriteAsync(
                                    existingFileForOverwrite.Id,
                                    fileInfo.Length,
                                    currentUsername);
                                    
                                fileAttachmentId = existingFileForOverwrite.Id;
                                uploadResult.DatabaseRecorded = true;
                                
                                _logger.LogInformation("Updated existing file attachment (ID: {FileId}) for overwrite", fileAttachmentId);
                            }
                            else
                            {
                                // CREATE: New file, create new record
                                var fileAttachment = new RMAFileAttachmentDTO
                                {
                                    RMANumber = rmaNumber,
                                    RMALineNumber = rmaLineNumber,
                                    FileName = uploadResult.FileName ?? file.Name,
                                    FilePath = filePath,
                                    FileSize = fileInfo.Length,
                                    CategoryId = selectedCategory.Id,
                                    UploadedByUsername = currentUsername,
                                    UploadedDate = DateTime.Now
                                };
                                
                                fileAttachmentId = await _dataService.CreateFileAttachmentAsync(fileAttachment);
                                uploadResult.DatabaseRecorded = true;
                            }
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogError(dbEx, "Database error recording file attachment for {FileName} for RMA {RmaNumber}", file.Name, request.RmaNumber);
                            
                            // Clean up the uploaded file since database recording failed
                            try
                            {
                                if (File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                    uploadedFiles.Remove(filePath); // Remove from tracking
                                    uploadResult.FileWrittenToDisk = false;
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogWarning(cleanupEx, "Failed to clean up file {FilePath} after database error", filePath);
                            }
                            
                            uploadResult.Success = false;
                            uploadResult.ErrorMessage = $"File was uploaded but could not be recorded in database. Please try again. Error: {dbEx.Message}";
                            uploadResult.ErrorType = UploadErrorType.DatabaseError;
                            results.Add(uploadResult);
                            continue;
                        }
                        
                        // Create log entry
                        try
                        {
                            string action;
                            string actionDetails;
                            
                            if (uploadResult.WasRenamed)
                            {
                                action = "Upload";
                                actionDetails = $"File uploaded to category '{selectedCategory.DisplayValue}' (renamed from '{uploadResult.OriginalFileName}')";
                            }
                            else if (uploadResult.WasOverwritten)
                            {
                                action = "Overwrite";
                                actionDetails = $"File overwritten - previous version uploaded {uploadResult.ReplacedFileUploadedDate:yyyy-MM-dd HH:mm} by {uploadResult.ReplacedFileUploadedBy}, " +
                                              $"new version uploaded {DateTime.Now:yyyy-MM-dd HH:mm} by {currentUsername}";
                            }
                            else
                            {
                                action = "Upload";
                                actionDetails = $"File uploaded to category '{selectedCategory.DisplayValue}'";
                            }
                            
                            var logEntry = new RMAFileAttachmentLogDTO
                            {
                                FileAttachmentId = fileAttachmentId,
                                Action = action,
                                ActionDetails = actionDetails,
                                ActionByUsername = currentUsername,
                                ActionDate = DateTime.Now
                            };
                            
                            await _dataService.CreateFileLogAsync(logEntry);
                        }
                        catch (Exception logEx)
                        {
                            // Log entry failure is not critical - file upload still succeeded
                            _logger.LogWarning(logEx, "Failed to create log entry for file {FileName} (ID: {FileAttachmentId})", file.Name, fileAttachmentId);
                        }
                        
                        // Success!
                        uploadResult.Success = true;
                        uploadResult.FilePath = filePath;
                        results.Add(uploadResult);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading file {FileName} for RMA {RmaNumber}", file.Name, request.RmaNumber);
                        
                        // Clean up file if it was written but processing failed
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                                uploadedFiles.Remove(filePath);
                                uploadResult.FileWrittenToDisk = false;
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogWarning(cleanupEx, "Failed to clean up file {FilePath} after error", filePath);
                            }
                        }
                        
                        uploadResult.Success = false;
                        uploadResult.ErrorMessage = $"Failed to upload {file.Name}: {ex.Message}";
                        uploadResult.ErrorType = UploadErrorType.GeneralError;
                        results.Add(uploadResult);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files for RMA {RmaNumber}", request.RmaNumber);
                
                // Clean up any uploaded files if global failure
                foreach (var uploadedFile in uploadedFiles)
                {
                    try
                    {
                        if (File.Exists(uploadedFile))
                        {
                            File.Delete(uploadedFile);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Failed to clean up file {FilePath} after global error", uploadedFile);
                    }
                }
                
                results.Add(new RMAFileUploadResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.Message,
                    ErrorType = UploadErrorType.GeneralError
                });
            }

            return results;
        }

        public async Task<List<RMAFileInfo>> GetFilesAsync(RMAFileQuery query)
        {
            var files = new List<RMAFileInfo>();
            var basePath = CalculateBasePath(query);
            var availableCategories = await _categoryService.GetAvailableCategoriesAsync(!string.IsNullOrEmpty(query.RmaLineNumber));
            
            if (Directory.Exists(basePath))
            {
                var fileInfos = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories);
                
                foreach (var filePath in fileInfos)
                {
                    // Extract category from path structure
                    var category = ExtractCategoryFromPath(filePath, basePath, availableCategories);
                    
                    // Apply category filter if specified
                    if (!string.IsNullOrEmpty(query.CategoryShortName))
                    {
                        if (category?.ShortName != query.CategoryShortName)
                        {
                            continue;
                        }
                    }

                    // Create relative path for display context
                    var relativePath = GetRelativePath(filePath, basePath);
                    
                    files.Add(new RMAFileInfo
                    {
                        FileName = System.IO.Path.GetFileName(filePath),
                        FilePath = filePath,
                        RelativePath = relativePath,
                        FileSize = new FileInfo(filePath).Length,
                        CreatedDate = File.GetCreationTime(filePath),
                        Category = category?.DisplayValue
                    });
                }
            }

            return files;
        }

        public async Task<bool> DeleteFileAsync(string filePath, string? deletedByUsername = null)
        {
            try
            {
                var currentUsername = deletedByUsername ?? GetCurrentUsername();
                
                // Find the file attachment in the database first
                var fileAttachment = await _dataService.GetFileAttachmentByPathAsync(filePath);
                    
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    
                    // Update database record if found
                    if (fileAttachment != null)
                    {
                        await _dataService.MarkFileAsDeletedAsync(fileAttachment.Id, currentUsername);
                        
                        // Add log entry
                        var logEntry = new RMAFileAttachmentLogDTO
                        {
                            FileAttachmentId = fileAttachment.Id,
                            Action = "Delete",
                            ActionDetails = "File deleted from filesystem and marked as deleted in database",
                            ActionByUsername = currentUsername,
                            ActionDate = DateTime.Now
                        };
                        
                        await _dataService.CreateFileLogAsync(logEntry);
                    }
                    
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FilePath}", filePath);
                return false;
            }
        }

        public async Task<(Stream? fileStream, string fileName, string contentType)> GetFileForDownloadAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for download: {FilePath}", filePath);
                    return (null, string.Empty, string.Empty);
                }

                var fileName = System.IO.Path.GetFileName(filePath);
                var contentType = GetContentType(fileName);
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                return (fileStream, fileName, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing file for download: {FilePath}", filePath);
                return (null, string.Empty, string.Empty);
            }
        }

        public string GetContentType(string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" or ".log" => "text/plain",
                ".csv" => "text/csv",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" => "image/tiff",
                ".webp" => "image/webp",
                ".zip" => "application/zip",
                ".7z" => "application/x-7z-compressed",
                ".rar" => "application/x-rar-compressed",
                _ => "application/octet-stream"
            };
        }

        public RMAFileStorageInfo GetStorageInfo(string rmaNumber, string? rmaLineNumber = null)
        {
            var baseRmaDir = System.IO.Path.Combine(_options.FileStorage.RmaFileRoot, $"RMA_{rmaNumber}");
            var specificDir = baseRmaDir;
            var description = "RMA Header Files";
            
            if (!string.IsNullOrEmpty(rmaLineNumber))
            {
                var lineFolder = $"Line_{rmaLineNumber}";
                specificDir = System.IO.Path.Combine(baseRmaDir, lineFolder);
                description = $"Line {rmaLineNumber} Files";
            }
            
            return new RMAFileStorageInfo
            {
                RootNetworkPath = _options.FileStorage.RmaFileRoot,
                RmaDirectory = specificDir,
                DisplayPath = FormatNetworkPathForDisplay(specificDir),
                StorageDescription = description
            };
        }

        public async Task<string> CalculateDestinationPathAsync(RMAFileUploadRequest request)
        {
            var availableCategories = await _categoryService.GetAvailableCategoriesAsync(!string.IsNullOrEmpty(request.RmaLineNumber));
            var selectedCategory = availableCategories.FirstOrDefault(c => c.ShortName == request.CategoryShortName);

            if (selectedCategory == null)
                throw new InvalidOperationException("Selected category is not valid.");

            var baseRmaDir = System.IO.Path.Combine(_options.FileStorage.RmaFileRoot, $"RMA_{request.RmaNumber}");

            // Header-level (no line number)
            if (string.IsNullOrEmpty(request.RmaLineNumber))
            {
                return System.IO.Path.Combine(baseRmaDir, selectedCategory.ShortName);
            }

            // Line-level (centralized; no longer using SerialNumber or alternate root)
            var lineFolder = $"Line_{request.RmaLineNumber}";
            return System.IO.Path.Combine(baseRmaDir, lineFolder, selectedCategory.ShortName);
        }

        public string CalculateBasePath(RMAFileQuery query)
        {
            var baseRmaDir = System.IO.Path.Combine(_options.FileStorage.RmaFileRoot, $"RMA_{query.RmaNumber}");

            if (!string.IsNullOrEmpty(query.RmaLineNumber))
            {
                var lineFolder = $"Line_{query.RmaLineNumber}";
                return System.IO.Path.Combine(baseRmaDir, lineFolder);
            }

            return baseRmaDir;
        }

        private FileCategory? ExtractCategoryFromPath(string filePath, string basePath, List<FileCategory> availableCategories)
        {
            try
            {
                var relativePath = System.IO.Path.GetRelativePath(basePath, filePath);
                var pathSegments = relativePath.Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                foreach (var category in availableCategories)
                {
                    foreach (var seg in pathSegments)
                    {
                        if (seg.Equals(category.ShortName, StringComparison.OrdinalIgnoreCase))
                            return category;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract category from path {FilePath}", filePath);
            }

            return null;
        }

        private string GetRelativePath(string filePath, string basePath)
        {
            try
            {
                var relativePath = System.IO.Path.GetRelativePath(basePath, filePath);
                var directoryPath = System.IO.Path.GetDirectoryName(relativePath);

                if (string.IsNullOrEmpty(directoryPath) || directoryPath == ".")
                {
                    return "RMA Header Level";
                }

                return directoryPath.Replace(System.IO.Path.DirectorySeparatorChar.ToString(), " ? ");
            }
            catch
            {
                return "Unknown Location";
            }
        }

        private string FormatNetworkPathForDisplay(string networkPath)
        {
            try
            {
                // Network drive mappings - could be moved to configuration if needed
                var networkDriveMappings = new Dictionary<string, string>
                {
                    { @"\\cgfs0\data", "H:" },
                    { @"//cgfs0/data", "H:" },
                    { @"\\cgfs0\data\", "H:" },
                    { @"//cgfs0/data/", "H:" }
                };

                // First, normalize to backslashes for Windows
                var normalizedPath = networkPath.Replace('/', '\\');

                // Check for drive mappings
                foreach (var mapping in networkDriveMappings)
                {
                    var mappingKey = mapping.Key.Replace('/', '\\'); // Normalize mapping key too
                    if (normalizedPath.StartsWith(mappingKey, StringComparison.OrdinalIgnoreCase))
                    {
                        // Replace the UNC path with the drive letter
                        var remainder = normalizedPath.Substring(mappingKey.Length);
                        // Remove leading backslash if present
                        if (remainder.StartsWith("\\"))
                            remainder = remainder.Substring(1);
                        
                        return string.IsNullOrEmpty(remainder) ? mapping.Value : $"{mapping.Value}\\{remainder}";
                    }
                }

                // If no mapping found, just return with proper backslashes
                return normalizedPath;
            }
            catch
            {
                return networkPath; // Fallback to original path
            }
        }

        private string GetCurrentUsername()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var username = user.Identity.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    // Remove domain prefix if present (e.g., "DOMAIN\username" -> "username")
                    return username.Contains('\\') 
                        ? username.Split('\\').Last() 
                        : username;
                }
            }
            
            return "SYSTEM"; // Fallback for system operations
        }

        /// <summary>
        /// Generates a unique file name using Windows-style numbering (file (1).ext, file (2).ext, etc.)
        /// </summary>
        private string GenerateUniqueFileName(string destinationPath, string originalFileName)
        {
            var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(originalFileName);
            var extension = System.IO.Path.GetExtension(originalFileName);
            var filePath = System.IO.Path.Combine(destinationPath, originalFileName);
            
            // If file doesn't exist, use original name
            if (!File.Exists(filePath))
            {
                return originalFileName;
            }
            
            // Find next available number
            int counter = 1;
            string newFileName;
            do
            {
                newFileName = $"{nameWithoutExt} ({counter}){extension}";
                filePath = System.IO.Path.Combine(destinationPath, newFileName);
                counter++;
                
                // Safety check to prevent infinite loop
                if (counter > 9999)
                {
                    // Fall back to timestamp if somehow we have 10000 versions
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    newFileName = $"{nameWithoutExt}_{timestamp}{extension}";
                    _logger.LogWarning("Exceeded 9999 file versions, using timestamp fallback: {FileName}", newFileName);
                    break;
                }
            }
            while (File.Exists(filePath));
            
            return newFileName;
        }
    }
}