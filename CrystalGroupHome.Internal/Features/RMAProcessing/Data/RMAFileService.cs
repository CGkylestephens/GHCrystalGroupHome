using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.SharedRCL.Data;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Data
{
    public interface IRMAFileService
    {
        Task<List<RMAFileUploadResult>> UploadFilesAsync(RMAFileUploadRequest request);
        Task<List<RMAFileInfo>> GetFilesAsync(RMAFileQuery query);
        Task<bool> DeleteFileAsync(string filePath, string? deletedByUsername = null);

        // Database tracking methods
        Task<List<RMAFileAttachmentDTO>> GetTrackedFilesAsync(RMAFileQuery query);
        Task<List<RMAFileAttachmentLogDTO>> GetFileLogsAsync(int fileAttachmentId);
        Task<RMAFileAttachmentDTO?> GetFileAttachmentAsync(int id);
        Task<int> CreateFileAttachmentAsync(RMAFileAttachmentDTO fileAttachment);
        Task<int> CreateFileLogAsync(RMAFileAttachmentLogDTO logEntry);
        Task<List<RMAFileHistoryModel>> GetRMAFileHistoryAsync(RMAFileQuery query);

        // File download method
        Task<(Stream? fileStream, string fileName, string contentType)> GetFileForDownloadAsync(string filePath);

        // Print test logs method
        Task<PrintTestLogsResponse> PrintTestLogsAsync(PrintTestLogsRequest request);

        // Content type method
        string GetContentType(string fileName);

        Task<List<RmaLineSummary>> GetRmaLinesAsync(string rmaNumber);

        // Get storage directory information
        RMAFileStorageInfo GetStorageInfo(string rmaNumber, string? rmaLineNumber = null);

        // Bulk download method
        Task<BulkDownloadResponse> CreateBulkDownloadAsync(BulkDownloadRequest request);

        // Category management methods
        Task<List<RMAFileCategoryDTO>> GetFileCategoriesAsync(bool? isDetailLevel = null);
        Task<RMAFileCategoryDTO?> GetFileCategoryAsync(int categoryId);
        Task<RMAFileCategoryDTO?> GetFileCategoryByShortNameAsync(string shortName, bool isDetailLevel);
        Task<int> CreateFileCategoryAsync(RMAFileCategoryDTO category);
        Task<bool> UpdateFileCategoryAsync(RMAFileCategoryDTO category);
        Task<bool> DeleteFileCategoryAsync(int categoryId);

        // ONLY async version - no more sync method
        Task<List<FileCategory>> GetAvailableCategoriesAsync(bool isDetailLevel);

        // File category change methods
        Task<bool> UpdateFileAttachmentCategoryAsync(int fileAttachmentId, int newCategoryId, string modifiedByUsername);
        Task<BulkCategoryUpdateResponse> UpdateMultipleFilesCategoryAsync(BulkCategoryUpdateRequest request);

        // File metadata update methods
        Task<FileMetadataUpdateResponse> UpdateFileMetadataAsync(FileMetadataUpdateRequest request);
        Task<BulkFileMetadataUpdateResponse> UpdateMultipleFilesMetadataAsync(BulkFileMetadataUpdateRequest request);

        // RMA and Line validation methods
        Task<bool> ValidateRMAExistsAsync(int rmaNumber);
        Task<bool> ValidateRMALineExistsAsync(int rmaNumber, int lineNumber);
        Task<RMAValidationResult> ValidateRMAAndLineAsync(int rmaNumber, int? lineNumber = null);

        // RMA Summary methods
        Task<RMASummaryResponse> GetRMASummariesAsync(RMASummaryQuery query);
        Task<RMASummaryModel?> GetRMASummaryAsync(int rmaNumber);
    }
    public class RMAFileService : IRMAFileService
    {
        private readonly ILogger<RMAFileService> _logger;
        private readonly IRMAFileStorageService _storageService;
        private readonly IRMAFileDataService _dataService;
        private readonly IRMAFileCategoryService _categoryService;
        private readonly IRMAFileProcessingService _processingService;
        private readonly IRMAValidationService _validationService;

        public RMAFileService(
            ILogger<RMAFileService> logger,
            IRMAFileStorageService storageService,
            IRMAFileDataService dataService,
            IRMAFileCategoryService categoryService,
            IRMAFileProcessingService processingService,
            IRMAValidationService validationService)
        {
            _logger = logger;
            _storageService = storageService;
            _dataService = dataService;
            _categoryService = categoryService;
            _processingService = processingService;
            _validationService = validationService;
        }

        // File Storage Operations - delegate to storage service
        public async Task<List<RMAFileUploadResult>> UploadFilesAsync(RMAFileUploadRequest request)
            => await _storageService.UploadFilesAsync(request);

        public async Task<List<RMAFileInfo>> GetFilesAsync(RMAFileQuery query)
            => await _storageService.GetFilesAsync(query);

        public async Task<bool> DeleteFileAsync(string filePath, string? deletedByUsername = null)
            => await _storageService.DeleteFileAsync(filePath, deletedByUsername);

        public async Task<(Stream? fileStream, string fileName, string contentType)> GetFileForDownloadAsync(string filePath)
            => await _storageService.GetFileForDownloadAsync(filePath);

        public string GetContentType(string fileName)
            => _storageService.GetContentType(fileName);

        public RMAFileStorageInfo GetStorageInfo(string rmaNumber, string? rmaLineNumber = null)
            => _storageService.GetStorageInfo(rmaNumber, rmaLineNumber);

        // Database Operations - delegate to data service
        public async Task<List<RMAFileAttachmentDTO>> GetTrackedFilesAsync(RMAFileQuery query)
            => await _dataService.GetTrackedFilesAsync(query);

        public async Task<List<RMAFileAttachmentLogDTO>> GetFileLogsAsync(int fileAttachmentId)
            => await _dataService.GetFileLogsAsync(fileAttachmentId);

        public async Task<RMAFileAttachmentDTO?> GetFileAttachmentAsync(int id)
            => await _dataService.GetFileAttachmentAsync(id);

        public async Task<int> CreateFileAttachmentAsync(RMAFileAttachmentDTO fileAttachment)
            => await _dataService.CreateFileAttachmentAsync(fileAttachment);

        public async Task<int> CreateFileLogAsync(RMAFileAttachmentLogDTO logEntry)
            => await _dataService.CreateFileLogAsync(logEntry);

        public async Task<List<RMAFileHistoryModel>> GetRMAFileHistoryAsync(RMAFileQuery query)
            => await _dataService.GetRMAFileHistoryAsync(query);

        public async Task<List<RmaLineSummary>> GetRmaLinesAsync(string rmaNumber)
            => await _dataService.GetRmaLinesAsync(rmaNumber);

        // Category Management Operations - delegate to category service
        public async Task<List<RMAFileCategoryDTO>> GetFileCategoriesAsync(bool? isDetailLevel = null)
            => await _categoryService.GetFileCategoriesAsync(isDetailLevel);

        public async Task<RMAFileCategoryDTO?> GetFileCategoryAsync(int categoryId)
            => await _categoryService.GetFileCategoryAsync(categoryId);

        public async Task<RMAFileCategoryDTO?> GetFileCategoryByShortNameAsync(string shortName, bool isDetailLevel)
            => await _categoryService.GetFileCategoryByShortNameAsync(shortName, isDetailLevel);

        public async Task<int> CreateFileCategoryAsync(RMAFileCategoryDTO category)
            => await _categoryService.CreateFileCategoryAsync(category);

        public async Task<bool> UpdateFileCategoryAsync(RMAFileCategoryDTO category)
            => await _categoryService.UpdateFileCategoryAsync(category);

        public async Task<bool> DeleteFileCategoryAsync(int categoryId)
            => await _categoryService.DeleteFileCategoryAsync(categoryId);

        public async Task<List<FileCategory>> GetAvailableCategoriesAsync(bool isDetailLevel)
            => await _categoryService.GetAvailableCategoriesAsync(isDetailLevel);

        // Processing Operations - delegate to processing service
        public async Task<PrintTestLogsResponse> PrintTestLogsAsync(PrintTestLogsRequest request)
            => await _processingService.PrintTestLogsAsync(request);

        public async Task<BulkDownloadResponse> CreateBulkDownloadAsync(BulkDownloadRequest request)
            => await _processingService.CreateBulkDownloadAsync(request);

        public async Task<bool> UpdateFileAttachmentCategoryAsync(int fileAttachmentId, int newCategoryId, string modifiedByUsername)
            => await _processingService.UpdateFileAttachmentCategoryAsync(fileAttachmentId, newCategoryId, modifiedByUsername);

        public async Task<BulkCategoryUpdateResponse> UpdateMultipleFilesCategoryAsync(BulkCategoryUpdateRequest request)
            => await _processingService.UpdateMultipleFilesCategoryAsync(request);

        public async Task<FileMetadataUpdateResponse> UpdateFileMetadataAsync(FileMetadataUpdateRequest request)
            => await _processingService.UpdateFileMetadataAsync(request);

        public async Task<BulkFileMetadataUpdateResponse> UpdateMultipleFilesMetadataAsync(BulkFileMetadataUpdateRequest request)
            => await _processingService.UpdateMultipleFilesMetadataAsync(request);

        // Validation Operations - delegate to validation service
        public async Task<bool> ValidateRMAExistsAsync(int rmaNumber)
            => await _validationService.ValidateRMAExistsAsync(rmaNumber);

        public async Task<bool> ValidateRMALineExistsAsync(int rmaNumber, int lineNumber)
            => await _validationService.ValidateRMALineExistsAsync(rmaNumber, lineNumber);

        public async Task<RMAValidationResult> ValidateRMAAndLineAsync(int rmaNumber, int? lineNumber = null)
            => await _validationService.ValidateRMAAndLineAsync(rmaNumber, lineNumber);

        public async Task<RMASummaryResponse> GetRMASummariesAsync(RMASummaryQuery query)
            => await _validationService.GetRMASummariesAsync(query);

        public async Task<RMASummaryModel?> GetRMASummaryAsync(int rmaNumber)
            => await _validationService.GetRMASummaryAsync(rmaNumber);
    }
}