using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models; // Add this line
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Controllers
{
    [Authorize]
    [Route("api/rma/files")]
    [ApiController]
    public class RMAFileDownloadController : ControllerBase
    {
        private readonly IRMAFileService _rmaFileService;
        private readonly ITempFileService _tempFileService; // ADD this
        private readonly ILogger<RMAFileDownloadController> _logger;

        public RMAFileDownloadController(
            IRMAFileService rmaFileService,
            ITempFileService tempFileService, // ADD this parameter
            ILogger<RMAFileDownloadController> logger)
        {
            _rmaFileService = rmaFileService;
            _tempFileService = tempFileService; // ADD this line
            _logger = logger;
        }

        // Endpoint for PDF downloads
        [HttpPost("print-test-logs")]
        public async Task<IActionResult> PrintTestLogs([FromBody] PrintTestLogsRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RmaNumber) || !request.SelectedFileIds.Any())
                {
                    return BadRequest("RMA number and selected files are required");
                }

                var response = await _rmaFileService.PrintTestLogsAsync(request);

                if (!response.Success)
                {
                    return BadRequest(response.ErrorMessage);
                }

                var fileName = $"RMA_{request.RmaNumber}_L{request.RmaLineNumber}_SN_{request.SerialNumber}_TestLogs_{DateTime.Now:yyyyMMdd}.pdf";
                
                // Return the response with success info
                return Ok(new { 
                    success = true, 
                    fileName = fileName,
                    filesProcessed = response.FilesProcessed,
                    totalPages = response.TotalPages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PDF for RMA {RmaNumber}", request.RmaNumber);
                return StatusCode(500, "An error occurred while creating the PDF");
            }
        }

        [HttpGet("bulk-download/{token}")]
        public async Task<IActionResult> BulkDownload(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest("Download token is required");
                }

                var (filePath, originalFileName) = await _tempFileService.GetTempFileByTokenAsync(token);
                
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    return NotFound("Download not found or has expired");
                }

                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                // Schedule cleanup after a delay (but don't wait for it)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Wait a bit to ensure download completes
                        await Task.Delay(TimeSpan.FromMinutes(5));
                        await _tempFileService.DeleteTempFileAsync(token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not clean up temp file for token: {Token}", token);
                    }
                });

                return File(fileStream, "application/zip", originalFileName ?? "download.zip", enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading bulk archive for token: {Token}", token);
                return StatusCode(500, "An error occurred while downloading the archive");
            }
        }

        // Cleanup endpoint for download temp files
        [HttpPost("cleanup-temp-files")]
        [Authorize(Roles = "SystemAdmin")] // Restrict to system administrators
        public async Task<IActionResult> CleanupTempFiles()
        {
            try
            {
                var cleanedCount = await _tempFileService.CleanupExpiredFilesAsync();
                
                _logger.LogInformation("Temp file cleanup completed. Cleaned {Count} files.", cleanedCount);
                
                return Ok(new { 
                    success = true, 
                    message = $"Cleanup completed successfully. Removed {cleanedCount} expired files.",
                    filesRemoved = cleanedCount 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temp file cleanup");
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred during cleanup",
                    error = ex.Message 
                });
            }
        }
    }
}