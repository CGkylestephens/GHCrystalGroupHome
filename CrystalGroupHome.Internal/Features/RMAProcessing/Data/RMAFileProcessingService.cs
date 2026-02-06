using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.Internal.Features.RMAProcessing.Pages;
using CrystalGroupHome.SharedRCL.Data;
using System.IO.Compression;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Layout.Properties;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.IO.Image;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.Pdf.Extgstate;
using Dapper;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Data
{
    public interface IRMAFileProcessingService
    {
        Task<PrintTestLogsResponse> PrintTestLogsAsync(PrintTestLogsRequest request);
        Task<BulkDownloadResponse> CreateBulkDownloadAsync(BulkDownloadRequest request);
        Task<bool> UpdateFileAttachmentCategoryAsync(int fileAttachmentId, int newCategoryId, string modifiedByUsername);
        Task<BulkCategoryUpdateResponse> UpdateMultipleFilesCategoryAsync(BulkCategoryUpdateRequest request);
        Task<FileMetadataUpdateResponse> UpdateFileMetadataAsync(FileMetadataUpdateRequest request);
        Task<BulkFileMetadataUpdateResponse> UpdateMultipleFilesMetadataAsync(BulkFileMetadataUpdateRequest request);
    }

    public class RMAFileProcessingService : IRMAFileProcessingService
    {
        private readonly string _connectionString;
        private readonly ILogger<RMAFileProcessingService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ITempFileService _tempFileService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IRMAFileDataService _dataService;
        private readonly IRMAFileCategoryService _categoryService;
        private readonly IRMAFileStorageService _storageService;
        private readonly IRMAValidationService _validationService;

        // Table name for direct updates
        private const string FileAttachmentsTable = "dbo.RMA_FileAttachments";

        public RMAFileProcessingService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<RMAFileProcessingService> logger,
            IWebHostEnvironment webHostEnvironment,
            ITempFileService tempFileService,
            IHttpContextAccessor httpContextAccessor,
            IRMAFileDataService dataService,
            IRMAFileCategoryService categoryService,
            IRMAFileStorageService storageService,
            IRMAValidationService validationService)
        {
            _connectionString = dbOptions.Value.CgiConnection;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _tempFileService = tempFileService;
            _httpContextAccessor = httpContextAccessor;
            _dataService = dataService;
            _categoryService = categoryService;
            _storageService = storageService;
            _validationService = validationService;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        private bool CheckEditPermission(string operation)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (!RMAProcessingBase.HasFileUploadEditPermission(user))
            {
                _logger.LogWarning("{Operation} attempt denied for user {Username} - insufficient permissions", 
                    operation, GetCurrentUsername());
                return false;
            }
            return true;
        }

        public async Task<PrintTestLogsResponse> PrintTestLogsAsync(PrintTestLogsRequest request)
        {
            try
            {
                var currentUsername = GetCurrentUsername();
                _logger.LogInformation("Starting print test logs for RMA {RmaNumber} by user {Username}",
                    request.RmaNumber, currentUsername);

                var selectedFiles = new List<RMAFileAttachmentDTO>();
                foreach (var fileId in request.SelectedFileIds)
                {
                    var file = await _dataService.GetFileAttachmentAsync(fileId);
                    if (file != null && File.Exists(file.FilePath))
                        selectedFiles.Add(file);
                }

                if (!selectedFiles.Any())
                {
                    return new PrintTestLogsResponse
                    {
                        Success = false,
                        ErrorMessage = "No valid files found for printing",
                        FilesProcessed = 0,
                        TotalPages = 0
                    };
                }

                var pdfBytes = await CreateCombinedTestLogsPdfAsync(selectedFiles, request.PrintOptions);

                var fileName = $"RMA_{request.RmaNumber}_L{request.RmaLineNumber}_TestLogs_{DateTime.Now:yyyyMMdd}.pdf";

                return new PrintTestLogsResponse
                {
                    Success = true,
                    FilesProcessed = selectedFiles.Count,
                    TotalPages = GetPageCountFromPdf(pdfBytes),
                    PdfData = pdfBytes,
                    FileName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating print job for RMA {RmaNumber}", request.RmaNumber);
                return new PrintTestLogsResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    FilesProcessed = 0,
                    TotalPages = 0
                };
            }
        }

        public async Task<BulkDownloadResponse> CreateBulkDownloadAsync(BulkDownloadRequest request)
        {
            try
            {
                var files = new List<RMAFileAttachmentDTO>();
                
                foreach (var fileId in request.FileIds)
                {
                    var file = await _dataService.GetFileAttachmentAsync(fileId);
                    if (file != null && File.Exists(file.FilePath))
                        files.Add(file);
                }

                if (!files.Any())
                {
                    return new BulkDownloadResponse
                    {
                        Success = false,
                        ErrorMessage = "No valid files found for download"
                    };
                }

                var tempPath = System.IO.Path.GetTempPath();
                var zipFileName = $"{request.ArchiveName}.zip";
                var zipFilePath = System.IO.Path.Combine(tempPath, zipFileName);

                using (var zip = new ZipArchive(File.Create(zipFilePath), ZipArchiveMode.Create))
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            var entry = zip.CreateEntry(file.FileName);
                            using var entryStream = entry.Open();
                            using var fileStream = File.OpenRead(file.FilePath);
                            await fileStream.CopyToAsync(entryStream);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not add file {FileName} to ZIP archive", file.FileName);
                        }
                    }
                }

                var zipInfo = new FileInfo(zipFilePath);
                
                var token = await _tempFileService.CreateTempFileTokenAsync(
                    zipFilePath, 
                    request.ArchiveName, 
                    TimeSpan.FromHours(2));
                
                return new BulkDownloadResponse
                {
                    Success = true,
                    DownloadUrl = $"/api/rma/files/bulk-download/{token}",
                    ArchiveSize = zipInfo.Length,
                    FilesProcessed = files.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bulk download for RMA {RmaNumber}", request.RmaNumber);
                return new BulkDownloadResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> UpdateFileAttachmentCategoryAsync(int fileAttachmentId, int newCategoryId, string modifiedByUsername)
        {
            if (!CheckEditPermission("Category Update"))
                return false;

            try
            {
                var currentFile = await _dataService.GetFileAttachmentAsync(fileAttachmentId);
                if (currentFile == null)
                {
                    _logger.LogWarning("File attachment {FileAttachmentId} not found for category update", fileAttachmentId);
                    return false;
                }

                var newCategory = await _categoryService.GetFileCategoryAsync(newCategoryId);
                if (newCategory == null)
                {
                    _logger.LogWarning("Category {CategoryId} not found for file attachment {FileAttachmentId}", newCategoryId, fileAttachmentId);
                    return false;
                }

                var sql = $@"
                    UPDATE {FileAttachmentsTable}
                    SET CategoryId = @NewCategoryId
                    WHERE Id = @FileAttachmentId";

                using var conn = Connection;
                var rowsAffected = await conn.ExecuteAsync(sql, new 
                { 
                    NewCategoryId = newCategoryId,
                    FileAttachmentId = fileAttachmentId
                });

                if (rowsAffected > 0)
                {
                    var oldCategoryName = currentFile.Category?.DisplayValue ?? "Unknown";
                    var logEntry = new RMAFileAttachmentLogDTO
                    {
                        FileAttachmentId = fileAttachmentId,
                        Action = "Category Change",
                        ActionDetails = $"Category changed from '{oldCategoryName}' to '{newCategory.DisplayValue}'",
                        ActionByUsername = modifiedByUsername,
                        ActionDate = DateTime.UtcNow,
                        IsSystemAction = false
                    };

                    await _dataService.CreateFileLogAsync(logEntry);
                    
                    _logger.LogInformation("Updated category for file attachment {FileAttachmentId} from {OldCategory} to {NewCategory}", 
                        fileAttachmentId, oldCategoryName, newCategory.DisplayValue);
                    
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category for file attachment {FileAttachmentId}", fileAttachmentId);
                return false;
            }
        }

        public async Task<BulkCategoryUpdateResponse> UpdateMultipleFilesCategoryAsync(BulkCategoryUpdateRequest request)
        {
            if (!CheckEditPermission("Bulk Category Update"))
            {
                return new BulkCategoryUpdateResponse
                {
                    Success = false,
                    ErrorMessage = "You do not have permission to edit file metadata. This feature is restricted to Technical Services users."
                };
            }

            var response = new BulkCategoryUpdateResponse { Success = true };
            
            try
            {
                var newCategory = await _categoryService.GetFileCategoryAsync(request.NewCategoryId);
                if (newCategory == null)
                {
                    return new BulkCategoryUpdateResponse
                    {
                        Success = false,
                        ErrorMessage = "Selected category is not valid."
                    };
                }

                foreach (var fileId in request.FileAttachmentIds)
                {
                    try
                    {
                        var success = await UpdateFileAttachmentCategoryAsync(fileId, request.NewCategoryId, request.ModifiedByUsername);
                        if (success)
                        {
                            response.FilesUpdated++;
                        }
                        else
                        {
                            response.FilesSkipped++;
                            response.Errors.Add($"Failed to update file ID {fileId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        response.FilesSkipped++;
                        response.Errors.Add($"Error updating file ID {fileId}: {ex.Message}");
                        _logger.LogError(ex, "Error updating category for file ID {FileId} in bulk operation", fileId);
                    }
                }

                var bulkLogEntry = new RMAFileAttachmentLogDTO
                {
                    FileAttachmentId = request.FileAttachmentIds.FirstOrDefault(),
                    Action = "Bulk Category Change",
                    ActionDetails = $"Bulk category change operation: {response.FilesUpdated} files updated to '{newCategory.DisplayValue}', {response.FilesSkipped} skipped",
                    ActionByUsername = request.ModifiedByUsername,
                    ActionDate = DateTime.UtcNow,
                    IsSystemAction = false
                };

                if (request.FileAttachmentIds.Any())
                {
                    await _dataService.CreateFileLogAsync(bulkLogEntry);
                }

                if (response.FilesSkipped > 0 && response.FilesUpdated == 0)
                {
                    response.Success = false;
                    response.ErrorMessage = "No files were updated successfully.";
                }
                else if (response.FilesSkipped > 0)
                {
                    response.ErrorMessage = $"Some files could not be updated. {response.FilesUpdated} updated, {response.FilesSkipped} skipped.";
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk category update operation");
                return new BulkCategoryUpdateResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<FileMetadataUpdateResponse> UpdateFileMetadataAsync(FileMetadataUpdateRequest request)
        {
            if (!CheckEditPermission("File Metadata Update"))
            {
                return new FileMetadataUpdateResponse
                {
                    Success = false,
                    ErrorMessage = "You do not have permission to edit file metadata. This feature is restricted to Technical Services users."
                };
            }

            try
            {
                var validationResult = await _validationService.ValidateRMAAndLineAsync(request.NewRMANumber, request.NewRMALineNumber);
                if (!validationResult.IsValid)
                {
                    return new FileMetadataUpdateResponse
                    {
                        Success = false,
                        ErrorMessage = validationResult.ErrorMessage,
                        ValidationResult = validationResult
                    };
                }

                var currentFile = await _dataService.GetFileAttachmentAsync(request.FileAttachmentId);
                if (currentFile == null)
                {
                    return new FileMetadataUpdateResponse
                    {
                        Success = false,
                        ErrorMessage = "File attachment not found."
                    };
                }

                var changes = new List<string>();
                var needsFileMove = false;
                string? newFilePath = null;

                if (currentFile.RMANumber != request.NewRMANumber || 
                    currentFile.RMALineNumber != request.NewRMALineNumber)
                {
                    needsFileMove = true;
                    
                    var storageInfo = _storageService.GetStorageInfo(request.NewRMANumber.ToString(), request.NewRMALineNumber?.ToString());
                    var fileName = System.IO.Path.GetFileName(currentFile.FilePath);
                    
                    string categoryDir = "Unknown";
                    if (request.NewCategoryId.HasValue)
                    {
                        var category = await _categoryService.GetFileCategoryAsync(request.NewCategoryId.Value);
                        categoryDir = category?.ShortName ?? "Unknown";
                    }
                    else if (currentFile.Category != null)
                    {
                        categoryDir = currentFile.Category.ShortName;
                    }
                    
                    newFilePath = System.IO.Path.Combine(storageInfo.RmaDirectory, categoryDir, fileName);
                }

                if (needsFileMove && newFilePath != null)
                {
                    try
                    {
                        var destDir = System.IO.Path.GetDirectoryName(newFilePath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        if (File.Exists(currentFile.FilePath))
                        {
                            File.Move(currentFile.FilePath, newFilePath);
                            changes.Add($"File moved from {currentFile.FilePath} to {newFilePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to move file from {OldPath} to {NewPath}", currentFile.FilePath, newFilePath);
                        return new FileMetadataUpdateResponse
                        {
                            Success = false,
                            ErrorMessage = $"Failed to move file: {ex.Message}"
                        };
                    }
                }

                var sql = $@"
                    UPDATE {FileAttachmentsTable}
                    SET RMANumber = @NewRMANumber,
                        RMALineNumber = @NewRMALineNumber,
                        CategoryId = COALESCE(@NewCategoryId, CategoryId),
                        FilePath = COALESCE(@NewFilePath, FilePath)
                    WHERE Id = @FileAttachmentId";

                using var conn = Connection;
                var rowsAffected = await conn.ExecuteAsync(sql, new 
                {
                    NewRMANumber = request.NewRMANumber,
                    NewRMALineNumber = request.NewRMALineNumber,
                    NewCategoryId = request.NewCategoryId,
                    NewFilePath = newFilePath,
                    FileAttachmentId = request.FileAttachmentId
                });

                if (rowsAffected > 0)
                {
                    if (currentFile.RMANumber != request.NewRMANumber)
                        changes.Add($"RMA Number changed from {currentFile.RMANumber} to {request.NewRMANumber}");
                    
                    if (currentFile.RMALineNumber != request.NewRMALineNumber)
                    {
                        var oldLine = currentFile.RMALineNumber?.ToString() ?? "Header";
                        var newLine = request.NewRMALineNumber?.ToString() ?? "Header";
                        changes.Add($"Line Number changed from {oldLine} to {newLine}");
                    }

                    if (request.NewCategoryId.HasValue && currentFile.CategoryId != request.NewCategoryId)
                    {
                        var oldCategory = currentFile.Category?.DisplayValue ?? "Unknown";
                        var newCategory = await _categoryService.GetFileCategoryAsync(request.NewCategoryId.Value);
                        changes.Add($"Category changed from {oldCategory} to {newCategory?.DisplayValue ?? "Unknown"}");
                    }

                    var logEntry = new RMAFileAttachmentLogDTO
                    {
                        FileAttachmentId = request.FileAttachmentId,
                        Action = "Metadata Update",
                        ActionDetails = string.Join("; ", changes),
                        ActionByUsername = request.ModifiedByUsername,
                        ActionDate = DateTime.UtcNow,
                        IsSystemAction = false
                    };

                    await _dataService.CreateFileLogAsync(logEntry);

                    return new FileMetadataUpdateResponse
                    {
                        Success = true,
                        FileRenamed = needsFileMove,
                        NewFilePath = newFilePath,
                        OldFilePath = currentFile.FilePath,
                        ValidationResult = validationResult
                    };
                }

                return new FileMetadataUpdateResponse
                {
                    Success = false,
                    ErrorMessage = "No records were updated."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file metadata for attachment {FileAttachmentId}", request.FileAttachmentId);
                return new FileMetadataUpdateResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<BulkFileMetadataUpdateResponse> UpdateMultipleFilesMetadataAsync(BulkFileMetadataUpdateRequest request)
        {
            if (!CheckEditPermission("Bulk File Metadata Update"))
            {
                return new BulkFileMetadataUpdateResponse
                {
                    Success = false,
                    ErrorMessage = "You do not have permission to edit file metadata. This feature is restricted to Technical Services users."
                };
            }

            var response = new BulkFileMetadataUpdateResponse { Success = true };

            try
            {
                foreach (var fileId in request.FileAttachmentIds)
                {
                    try
                    {
                        var individualRequest = new FileMetadataUpdateRequest
                        {
                            FileAttachmentId = fileId,
                            NewRMANumber = request.NewRMANumber ?? 0,
                            NewRMALineNumber = request.NewRMALineNumber,
                            NewCategoryId = request.NewCategoryId,
                            ModifiedByUsername = request.ModifiedByUsername
                        };

                        if (!request.NewRMANumber.HasValue)
                        {
                            var currentFile = await _dataService.GetFileAttachmentAsync(fileId);
                            if (currentFile != null)
                            {
                                individualRequest.NewRMANumber = currentFile.RMANumber;
                            }
                        }

                        var result = await UpdateFileMetadataAsync(individualRequest);
                        if (result.Success)
                        {
                            response.FilesUpdated++;
                            if (result.FileRenamed && result.OldFilePath != null && result.NewFilePath != null)
                            {
                                response.FilesMoved.Add((result.OldFilePath, result.NewFilePath));
                            }
                        }
                        else
                        {
                            response.FilesSkipped++;
                            response.Errors.Add($"File ID {fileId}: {result.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        response.FilesSkipped++;
                        response.Errors.Add($"File ID {fileId}: {ex.Message}");
                        _logger.LogError(ex, "Error updating metadata for file ID {FileId} in bulk operation", fileId);
                    }
                }

                if (response.FilesSkipped > 0 && response.FilesUpdated == 0)
                {
                    response.Success = false;
                    response.ErrorMessage = "No files were updated successfully.";
                }
                else if (response.FilesSkipped > 0)
                {
                    response.ErrorMessage = $"Some files could not be updated. {response.FilesUpdated} updated, {response.FilesSkipped} skipped.";
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk file metadata update operation");
                return new BulkFileMetadataUpdateResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<byte[]> CreateCombinedTestLogsPdfAsync(List<RMAFileAttachmentDTO> files, PrintTestLogsOptions options)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new PdfWriter(memoryStream);
            using var pdfDoc = new PdfDocument(writer);
            using var document = new Document(pdfDoc, PageSize.LETTER);

            // Match legacy margins: left=50, right=50, top=25, bottom=25
            // iText 7 SetMargins order: top, right, bottom, left
            document.SetMargins(25, 50, 25, 50);

            var contentFont = PdfFontFactory.CreateFont(StandardFonts.COURIER);
            var sections = new List<SectionInfo>();
            var blankPages = new HashSet<int>();
            var fileRanges = new List<(string fileName, int startPage, int endPage)>();

            int fileIndex = 0;
            foreach (var file in files.OrderBy(f => f.FileName))
            {
                fileIndex++;

                if (fileIndex > 1)
                {
                    int pagesBefore = pdfDoc.GetNumberOfPages();

                    if (options.InsertBlankPagesForDuplex && pagesBefore % 2 == 1)
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                        int blankPageNum = pdfDoc.GetNumberOfPages();
                        blankPages.Add(blankPageNum);

                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                        _logger.LogInformation("Inserted blank duplex separator page {BlankPage} then advanced to content page {ContentPageStart}",
                            blankPageNum, pdfDoc.GetNumberOfPages());
                    }
                    else
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                        _logger.LogInformation("Inserted standard page break before file {FileName}", file.FileName);
                    }
                }

                int pagesNow = pdfDoc.GetNumberOfPages();
                int startPage = pagesNow == 0 ? 1 : pagesNow;

                if (File.Exists(file.FilePath))
                {
                    try
                    {
                        var fileContent = await File.ReadAllTextAsync(file.FilePath);
                        
                        // Split content into lines to preserve exact line breaks
                        var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        
                        // Build paragraph with preserved formatting
                        // Font size 8 with fixed leading of 12 to match legacy line spacing
                        var paragraph = new Paragraph()
                            .SetFont(contentFont)
                            .SetFontSize(8)
                            .SetFixedLeading(12f);
                        
                        for (int i = 0; i < lines.Length; i++)
                        {
                            // Replace tabs with 3 spaces (matching legacy tab rendering)
                            var processedLine = lines[i].Replace("\t", "   ");
                            
                            // Replace multiple consecutive spaces with non-breaking spaces to preserve alignment
                            // But keep single spaces as regular spaces to allow natural word wrapping
                            processedLine = PreserveMultipleSpaces(processedLine);
                            
                            paragraph.Add(new Text(processedLine));
                            
                            // Add explicit line break after each line except the last
                            if (i < lines.Length - 1)
                            {
                                paragraph.Add(new Text("\n"));
                            }
                        }
                        
                        document.Add(paragraph);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not read file {FilePath}", file.FilePath);
                        var errorParagraph = new Paragraph($"Error reading file: {file.FileName}\n{ex.Message}")
                            .SetFont(contentFont)
                            .SetFontSize(8)
                            .SetFixedLeading(12f);
                        document.Add(errorParagraph);
                    }
                }
                else
                {
                    var missingParagraph = new Paragraph($"File missing: {file.FileName}")
                        .SetFont(contentFont)
                        .SetFontSize(8)
                        .SetFixedLeading(12f);
                    document.Add(missingParagraph);
                }

                int endPage = pdfDoc.GetNumberOfPages();
                fileRanges.Add((file.FileName, startPage, endPage));
            }

            int totalPages = pdfDoc.GetNumberOfPages();

            foreach (var (fileName, startPage, endPage) in fileRanges)
            {
                sections.Add(new SectionInfo
                {
                    StartPage = startPage,
                    EndPage = endPage,
                    SectionName = fileName
                });
            }

            document.Close();

            var firstPassBytes = memoryStream.ToArray();
            using var stampingMemoryStream = new MemoryStream();

            using (var reader = new PdfReader(new MemoryStream(firstPassBytes)))
            using (var stampWriter = new PdfWriter(stampingMemoryStream))
            using (var stampDoc = new PdfDocument(reader, stampWriter))
            {
                PdfImageXObject? watermark = null;
                try
                {
                    var watermarkPath = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "images", "logos", "diagonal.gif");
                    if (File.Exists(watermarkPath))
                    {
                        watermark = new PdfImageXObject(ImageDataFactory.Create(watermarkPath));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to load watermark image.");
                }

                var footerFont = PdfFontFactory.CreateFont(StandardFonts.COURIER);

                for (int pageNum = 1; pageNum <= totalPages; pageNum++)
                {
                    if (blankPages.Contains(pageNum))
                        continue;

                    var page = stampDoc.GetPage(pageNum);
                    var section = sections.FirstOrDefault(s => pageNum >= s.StartPage && pageNum <= s.EndPage);
                    if (section == null)
                        continue;

                    var canvas = new PdfCanvas(page);

                    if (watermark != null)
                    {
                        canvas.SaveState();
                        var gs = new PdfExtGState();
                        gs.SetFillOpacity(0.1f);
                        gs.SetStrokeOpacity(0.1f);
                        canvas.SetExtGState(gs);

                        var pageSize = page.GetPageSize();
                        float watermarkWidth = Math.Min(500f, pageSize.GetWidth() - 100f);
                        float watermarkHeight = Math.Min(500f, pageSize.GetHeight() - 300f);
                        canvas.AddXObjectFittedIntoRectangle(watermark, new Rectangle(50, 150, watermarkWidth, watermarkHeight));
                        canvas.RestoreState();
                    }

                    if (options.IncludePageNumbers)
                    {
                        int relativePage = pageNum - section.StartPage + 1;
                        int sectionTotal = section.EndPage - section.StartPage + 1;

                        string leftText = section.SectionName;
                        string rightText = $"Page {relativePage} of {sectionTotal}";

                        var pageSize = page.GetPageSize();
                        float leftX = pageSize.GetLeft() + 40;
                        float rightX = pageSize.GetRight() - 40;
                        float yFooter = pageSize.GetBottom() + 10;

                        canvas.BeginText()
                              .SetFontAndSize(footerFont, 8)
                              .MoveText(leftX, yFooter)
                              .ShowText(leftText)
                              .MoveText(rightX - leftX - footerFont.GetWidth(rightText, 8), 0)
                              .ShowText(rightText)
                              .EndText();
                    }
                }
            }

            return stampingMemoryStream.ToArray();
        }

        private int GetPageCountFromPdf(byte[] pdfBytes)
        {
            try
            {
                using var reader = new PdfReader(new MemoryStream(pdfBytes));
                using var pdfDoc = new PdfDocument(reader);
                return pdfDoc.GetNumberOfPages();
            }
            catch
            {
                return 0;
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
                    return username.Contains('\\') 
                        ? username.Split('\\').Last() 
                        : username;
                }
            }
            
            return "SYSTEM";
        }

        // Helper class for section tracking
        private class SectionInfo
        {
            public int StartPage { get; set; }
            public int EndPage { get; set; }
            public string SectionName { get; set; } = default!;
        }

        /// <summary>
        /// Preserves multiple consecutive spaces by converting them to non-breaking spaces,
        /// while keeping single spaces as regular spaces to allow natural word wrapping.
        /// This matches the behavior of the legacy iTextSharp 5.x implementation.
        /// </summary>
        private static string PreserveMultipleSpaces(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new System.Text.StringBuilder(input.Length);
            int i = 0;
            
            while (i < input.Length)
            {
                if (input[i] == ' ')
                {
                    // Count consecutive spaces
                    int spaceStart = i;
                    while (i < input.Length && input[i] == ' ')
                    {
                        i++;
                    }
                    
                    int spaceCount = i - spaceStart;
                    
                    if (spaceCount == 1)
                    {
                        // Single space - keep as regular space for word wrapping
                        result.Append(' ');
                    }
                    else
                    {
                        // Multiple spaces - convert ALL to non-breaking spaces to preserve exact alignment
                        result.Append('\u00A0', spaceCount);
                    }
                }
                else
                {
                    result.Append(input[i]);
                    i++;
                }
            }
            
            return result.ToString();
        }
    }
}