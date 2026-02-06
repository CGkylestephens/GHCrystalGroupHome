using CrystalGroupHome.External.Features.VendorSurvey.Models;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Data.Vendor;
using CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using static CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms.CMHub_VendorCommsSurveyDTOs;
using static CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms.CMHub_VendorCommsSurveyModels;

namespace CrystalGroupHome.External.Features.VendorSurvey.Data
{
    public interface IVendorSurveyService
    {
        Task<bool> ValidateSurveyTokenAsync(int batchId, string token);
        Task<bool> ValidateConfirmationCodeAsync(int batchId, string confirmationCode);
        Task<bool> IsConfirmationCodeRequiredAsync(int batchId);
        Task<CMHub_SurveyBatchViewModel?> GetSurveyBatchForResponseAsync(int batchId);
        Task<List<CMHub_SurveyQuestionDTO>> GetSurveyQuestionsAsync(int templateVersionId);
        Task<bool> IsSurveyCompletedAsync(int batchId);
        Task<List<CMHub_VendorCommsSurveyResponseModel>> GetExistingResponsesAsync(int batchId);
        Task SaveDraftResponsesAsync(int batchId, List<CMHub_VendorCommsSurveyResponseModel> responses);
        Task SubmitSurveyResponsesAsync(int batchId, List<CMHub_VendorCommsSurveyResponseModel> responses);
        
        Task<List<int>> GetSubmittedPartIdsAsync(int batchId);
        Task SubmitPartResponsesAsync(int batchId, int trackerId, List<CMHub_VendorCommsSurveyResponseModel> responses);
        Task<bool> IsPartSubmittedAsync(int batchId, int trackerId);
    }

    public class VendorSurveyService : IVendorSurveyService
    {
        private readonly string _connectionString;
        private readonly ILogger<VendorSurveyService> _logger;

        private const string SurveyBatchTable = "[dbo].[CMVendorComms_SurveyBatch]";
        private const string SurveyBatchPartTable = "[dbo].[CMVendorComms_SurveyBatchPart]";
        private const string SurveyResponseTable = "[dbo].[CMVendorComms_SurveyResponse]";
        private const string SurveyQuestionTable = "[dbo].[CMVendorComms_SurveyQuestion]";
        private const string SurveyTemplateVersionTable = "[dbo].[CMVendorComms_SurveyTemplateVersion]";
        private const string SurveyTemplateTable = "[dbo].[CMVendorComms_SurveyTemplate]";
        private const string PartStatusTrackerTable = "[dbo].[CMVendorComms_PartStatusTracker]";

        public VendorSurveyService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<VendorSurveyService> logger)
        {
            _connectionString = dbOptions.Value.CGIExtConnection;
            _logger = logger;
        }

        public async Task<bool> ValidateSurveyTokenAsync(int batchId, string token)
        {
            try
            {
                // Generate expected token for this batch ID
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"survey-{batchId}-crystal"));
                var expectedToken = Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

                // Compare tokens
                if (token != expectedToken)
                {
                    _logger.LogWarning("Invalid token for batch {BatchId}", batchId);
                    return false;
                }

                // Check if batch exists and is valid
                using var conn = new SqlConnection(_connectionString);
                var query = $@"
                    SELECT COUNT(*) 
                    FROM {SurveyBatchTable} 
                    WHERE Id = @BatchId 
                    AND Status IN ('Draft', 'Sent')";

                var count = await conn.ExecuteScalarAsync<int>(query, new { BatchId = batchId });
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating survey token for batch {BatchId}", batchId);
                return false;
            }
        }

        public async Task<bool> ValidateConfirmationCodeAsync(int batchId, string confirmationCode)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var query = $@"
                    SELECT ConfirmationCode 
                    FROM {SurveyBatchTable} 
                    WHERE Id = @BatchId 
                    AND Status IN ('Draft', 'Sent')";

                var storedCode = await conn.QueryFirstOrDefaultAsync<string>(query, new { BatchId = batchId });
                
                // Backwards compatibility: if no confirmation code exists in DB, allow access
                if (string.IsNullOrEmpty(storedCode))
                {
                    _logger.LogInformation("No confirmation code configured for batch {BatchId}, allowing access (backwards compatibility)", batchId);
                    return true;
                }

                // If a code is required but none was provided, deny access
                if (string.IsNullOrWhiteSpace(confirmationCode))
                {
                    _logger.LogWarning("Empty confirmation code provided for batch {BatchId} which requires a code", batchId);
                    return false;
                }

                // Case-insensitive comparison and trim whitespace
                var isValid = string.Equals(storedCode.Trim(), confirmationCode.Trim(), StringComparison.OrdinalIgnoreCase);
                
                if (!isValid)
                {
                    _logger.LogWarning("Invalid confirmation code provided for batch {BatchId}", batchId);
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating confirmation code for batch {BatchId}", batchId);
                return false;
            }
        }

        public async Task<bool> IsConfirmationCodeRequiredAsync(int batchId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var query = $@"
                    SELECT ConfirmationCode 
                    FROM {SurveyBatchTable} 
                    WHERE Id = @BatchId";

                var storedCode = await conn.QueryFirstOrDefaultAsync<string>(query, new { BatchId = batchId });
                
                // Code is required if one exists in the database
                return !string.IsNullOrEmpty(storedCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if confirmation code is required for batch {BatchId}", batchId);
                // Default to requiring code on error (fail secure)
                return true;
            }
        }

        public async Task<CMHub_SurveyBatchViewModel?> GetSurveyBatchForResponseAsync(int batchId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);

                // Get batch
                var batchQuery = $"SELECT * FROM {SurveyBatchTable} WHERE Id = @BatchId";
                var batch = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyBatchDTO>(batchQuery, new { BatchId = batchId });
                if (batch == null) return null;

                var model = new CMHub_SurveyBatchViewModel { Batch = batch };

                // Get template version
                var versionQuery = $"SELECT * FROM {SurveyTemplateVersionTable} WHERE Id = @VersionId";
                model.TemplateVersion = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyTemplateVersionDTO>(
                    versionQuery, new { VersionId = batch.SurveyTemplateVersionID });

                // Get template
                if (model.TemplateVersion != null)
                {
                    var templateQuery = $"SELECT * FROM {SurveyTemplateTable} WHERE Id = @TemplateId";
                    model.Template = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyTemplateDTO>(
                        templateQuery, new { TemplateId = model.TemplateVersion.SurveyTemplateID });
                }

                // Get parts from tracker table - no ERP joins needed
                // All required data is now cached in the external columns
                var partsQuery = $@"
                    SELECT 
                        t.Id AS TrackerId,
                        t.PartNum,
                        t.VendorNum,
                        t.ext_PartDesc AS PartDescription,
                        t.ext_VendorName AS VendorName,
                        t.ext_VendorPartNum AS VendorPartNum
                    FROM {SurveyBatchPartTable} bp
                    INNER JOIN {PartStatusTrackerTable} t ON bp.PartStatusTrackerID = t.Id
                    WHERE bp.SurveyBatchID = @BatchId";

                var partData = await conn.QueryAsync<dynamic>(partsQuery, new { BatchId = batchId });

                model.Parts = partData.Select(p => new CMHub_VendorCommsTrackerModel
                {
                    Tracker = new CMHub_VendorCommsTrackerDTO
                    {
                        Id = p.TrackerId,
                        PartNum = p.PartNum,
                        VendorNum = p.VendorNum,
                        ext_PartDesc = p.PartDescription,
                        ext_VendorName = p.VendorName,
                        ext_VendorPartNum = p.VendorPartNum
                    },
                    PartEolt = new PartEoltDTO
                    {
                        PartNum = p.PartNum,
                        PartDescription = p.PartDescription,
                        VendorPartNum = p.VendorPartNum
                    },
                    Vendor = !string.IsNullOrEmpty(p.VendorName) ? new VendorDTO_Base
                    {
                        VendorNum = p.VendorNum,
                        VendorName = p.VendorName
                    } : null
                }).ToList();

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading survey batch {BatchId} for response", batchId);
                return null;
            }
        }

        public async Task<List<CMHub_SurveyQuestionDTO>> GetSurveyQuestionsAsync(int templateVersionId)
        {
            using var conn = new SqlConnection(_connectionString);
            var query = $@"
                SELECT * FROM {SurveyQuestionTable} 
                WHERE SurveyTemplateVersionID = @TemplateVersionId 
                ORDER BY DisplayOrder";

            return (await conn.QueryAsync<CMHub_SurveyQuestionDTO>(query, new { TemplateVersionId = templateVersionId })).ToList();
        }

        public async Task<bool> IsSurveyCompletedAsync(int batchId)
        {
            using var conn = new SqlConnection(_connectionString);

            // Check if all parts are submitted
            var query = $@"
                SELECT COUNT(*) 
                FROM {SurveyBatchPartTable} 
                WHERE SurveyBatchID = @BatchId 
                AND ISNULL(SubmissionStatus, 'Draft') = 'Draft'";

            var draftCount = await conn.ExecuteScalarAsync<int>(query, new { BatchId = batchId });
            return draftCount == 0;
        }

        public async Task<List<CMHub_VendorCommsSurveyResponseModel>> GetExistingResponsesAsync(int batchId)
        {
            using var conn = new SqlConnection(_connectionString);

            var query = $@"
                SELECT 
                    r.Id,
                    bp.PartStatusTrackerID AS PartTrackerId,
                    r.QuestionID AS QuestionId,
                    r.ResponseValue
                FROM {SurveyResponseTable} r
                INNER JOIN {SurveyBatchPartTable} bp ON r.SurveyBatchPartID = bp.Id
                WHERE bp.SurveyBatchID = @BatchId";

            var results = await conn.QueryAsync<CMHub_VendorCommsSurveyResponseModel>(query, new { BatchId = batchId });
            return results.ToList();
        }

        public async Task<List<int>> GetSubmittedPartIdsAsync(int batchId)
        {
            using var conn = new SqlConnection(_connectionString);

            var query = $@"
                SELECT bp.PartStatusTrackerID
                FROM {SurveyBatchPartTable} bp
                WHERE bp.SurveyBatchID = @BatchId 
                AND bp.SubmissionStatus = 'Submitted'";

            var results = await conn.QueryAsync<int>(query, new { BatchId = batchId });
            return results.ToList();
        }

        public async Task<bool> IsPartSubmittedAsync(int batchId, int trackerId)
        {
            using var conn = new SqlConnection(_connectionString);

            var query = $@"
                SELECT COUNT(*) 
                FROM {SurveyBatchPartTable} 
                WHERE SurveyBatchID = @BatchId 
                AND PartStatusTrackerID = @TrackerId 
                AND SubmissionStatus = 'Submitted'";

            var count = await conn.ExecuteScalarAsync<int>(query, new { BatchId = batchId, TrackerId = trackerId });
            return count > 0;
        }

        public async Task SaveDraftResponsesAsync(int batchId, List<CMHub_VendorCommsSurveyResponseModel> responses)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            
            // Enable XACT_ABORT for proper transaction handling with remote tables
            await conn.ExecuteAsync("SET XACT_ABORT ON");
            
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (var response in responses)
                {
                    // Get the SurveyBatchPartID
                    var batchPartQuery = $@"
                        SELECT Id 
                        FROM {SurveyBatchPartTable} 
                        WHERE SurveyBatchID = @BatchId 
                        AND PartStatusTrackerID = @TrackerId";

                    var batchPartId = await conn.QueryFirstOrDefaultAsync<int>(
                        batchPartQuery,
                        new { BatchId = batchId, TrackerId = response.PartTrackerId },
                        transaction);

                    if (batchPartId > 0)
                    {
                        // Check if response exists
                        var existsQuery = $@"
                            SELECT Id 
                            FROM {SurveyResponseTable} 
                            WHERE SurveyBatchPartID = @BPartId 
                            AND QuestionID = @QId";

                        var existingId = await conn.QueryFirstOrDefaultAsync<int?>(existsQuery, new
                        {
                            BPartId = batchPartId,
                            QId = response.QuestionId
                        }, transaction);

                        if (existingId.HasValue)
                        {
                            // Update existing response
                            var updateQuery = $@"
                                UPDATE {SurveyResponseTable} 
                                SET ResponseValue = @Value, ResponseReceivedDate = GETDATE()
                                WHERE Id = @Id";

                            await conn.ExecuteAsync(updateQuery, new
                            {
                                Id = existingId.Value,
                                Value = response.ResponseValue
                            }, transaction);
                        }
                        else
                        {
                            // Insert new response
                            var insertQuery = $@"
                                INSERT INTO {SurveyResponseTable} (SurveyBatchPartID, QuestionID, ResponseValue, ResponseReceivedDate)
                                VALUES (@BPartId, @QId, @Value, GETDATE())";

                            await conn.ExecuteAsync(insertQuery, new
                            {
                                BPartId = batchPartId,
                                QId = response.QuestionId,
                                Value = response.ResponseValue
                            }, transaction);
                        }
                    }
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Saved draft responses for batch {BatchId}", batchId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error saving draft responses for batch {BatchId}", batchId);
                throw;
            }
        }

        public async Task SubmitPartResponsesAsync(int batchId, int trackerId, List<CMHub_VendorCommsSurveyResponseModel> responses)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            
            // Enable XACT_ABORT for proper transaction handling with remote tables
            await conn.ExecuteAsync("SET XACT_ABORT ON");
            
            using var transaction = conn.BeginTransaction();

            try
            {
                // Get the SurveyBatchPartID
                var batchPartQuery = $@"
                    SELECT Id 
                    FROM {SurveyBatchPartTable} 
                    WHERE SurveyBatchID = @BatchId 
                    AND PartStatusTrackerID = @TrackerId";

                var batchPartId = await conn.QueryFirstOrDefaultAsync<int>(
                    batchPartQuery,
                    new { BatchId = batchId, TrackerId = trackerId },
                    transaction);

                if (batchPartId > 0)
                {
                    // Save responses within the same transaction
                    foreach (var response in responses)
                    {
                        // Check if response exists
                        var existsQuery = $@"
                            SELECT Id 
                            FROM {SurveyResponseTable} 
                            WHERE SurveyBatchPartID = @BPartId 
                            AND QuestionID = @QId";

                        var existingId = await conn.QueryFirstOrDefaultAsync<int?>(existsQuery, new
                        {
                            BPartId = batchPartId,
                            QId = response.QuestionId
                        }, transaction);

                        if (existingId.HasValue)
                        {
                            // Update existing response
                            var updateQuery = $@"
                                UPDATE {SurveyResponseTable} 
                                SET ResponseValue = @Value, ResponseReceivedDate = GETDATE()
                                WHERE Id = @Id";

                            await conn.ExecuteAsync(updateQuery, new
                            {
                                Id = existingId.Value,
                                Value = response.ResponseValue
                            }, transaction);
                        }
                        else
                        {
                            // Insert new response
                            var insertQuery = $@"
                                INSERT INTO {SurveyResponseTable} (SurveyBatchPartID, QuestionID, ResponseValue, ResponseReceivedDate)
                                VALUES (@BPartId, @QId, @Value, GETDATE())";

                            await conn.ExecuteAsync(insertQuery, new
                            {
                                BPartId = batchPartId,
                                QId = response.QuestionId,
                                Value = response.ResponseValue
                            }, transaction);
                        }
                    }

                    // Mark this specific part as submitted
                    var updatePartStatusQuery = $@"
                        UPDATE {SurveyBatchPartTable} 
                        SET SubmissionStatus = 'Submitted'
                        WHERE SurveyBatchID = @BatchId 
                        AND PartStatusTrackerID = @TrackerId";

                    await conn.ExecuteAsync(updatePartStatusQuery, 
                        new { BatchId = batchId, TrackerId = trackerId }, transaction);

                    // Check if all parts are now submitted and update batch status if so
                    var remainingDraftCount = await conn.ExecuteScalarAsync<int>($@"
                        SELECT COUNT(*) 
                        FROM {SurveyBatchPartTable} 
                        WHERE SurveyBatchID = @BatchId 
                        AND ISNULL(SubmissionStatus, 'Draft') = 'Draft'", 
                        new { BatchId = batchId }, transaction);

                    if (remainingDraftCount == 0)
                    {
                        // All parts submitted, mark batch as completed
                        var updateBatchStatusQuery = $@"
                            UPDATE {SurveyBatchTable} 
                            SET Status = 'Completed' 
                            WHERE Id = @BatchId";

                        await conn.ExecuteAsync(updateBatchStatusQuery, new { BatchId = batchId }, transaction);
                    }
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Submitted part responses for batch {BatchId}, tracker {TrackerId}", batchId, trackerId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error submitting part responses for batch {BatchId}, tracker {TrackerId}", batchId, trackerId);
                throw;
            }
        }

        public async Task SubmitSurveyResponsesAsync(int batchId, List<CMHub_VendorCommsSurveyResponseModel> responses)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            
            // Enable XACT_ABORT for proper transaction handling with remote tables
            await conn.ExecuteAsync("SET XACT_ABORT ON");
            
            using var transaction = conn.BeginTransaction();

            try
            {
                // Save all responses first
                foreach (var response in responses)
                {
                    // Get the SurveyBatchPartID
                    var batchPartQuery = $@"
                        SELECT Id 
                        FROM {SurveyBatchPartTable} 
                        WHERE SurveyBatchID = @BatchId 
                        AND PartStatusTrackerID = @TrackerId";

                    var batchPartId = await conn.QueryFirstOrDefaultAsync<int>(
                        batchPartQuery,
                        new { BatchId = batchId, TrackerId = response.PartTrackerId },
                        transaction);

                    if (batchPartId > 0)
                    {
                        // Check if response exists
                        var existsQuery = $@"
                            SELECT Id 
                            FROM {SurveyResponseTable} 
                            WHERE SurveyBatchPartID = @BPartId 
                            AND QuestionID = @QId";

                        var existingId = await conn.QueryFirstOrDefaultAsync<int?>(existsQuery, new
                        {
                            BPartId = batchPartId,
                            QId = response.QuestionId
                        }, transaction);

                        if (existingId.HasValue)
                        {
                            // Update existing response
                            var updateQuery = $@"
                                UPDATE {SurveyResponseTable} 
                                SET ResponseValue = @Value, ResponseReceivedDate = GETDATE()
                                WHERE Id = @Id";

                            await conn.ExecuteAsync(updateQuery, new
                            {
                                Id = existingId.Value,
                                Value = response.ResponseValue
                            }, transaction);
                        }
                        else
                        {
                            // Insert new response
                            var insertQuery = $@"
                                INSERT INTO {SurveyResponseTable} (SurveyBatchPartID, QuestionID, ResponseValue, ResponseReceivedDate)
                                VALUES (@BPartId, @QId, @Value, GETDATE())";

                            await conn.ExecuteAsync(insertQuery, new
                            {
                                BPartId = batchPartId,
                                QId = response.QuestionId,
                                Value = response.ResponseValue
                            }, transaction);
                        }
                    }
                }

                // Update batch status to Completed
                var updateStatusQuery = $@"
                    UPDATE {SurveyBatchTable} 
                    SET Status = 'Completed' 
                    WHERE Id = @BatchId";

                await conn.ExecuteAsync(updateStatusQuery, new { BatchId = batchId }, transaction);

                // Mark all parts as submitted
                var updatePartsStatusQuery = $@"
                    UPDATE {SurveyBatchPartTable} 
                    SET SubmissionStatus = 'Submitted'
                    WHERE SurveyBatchID = @BatchId";

                await conn.ExecuteAsync(updatePartsStatusQuery, new { BatchId = batchId }, transaction);

                // Get questions with field mappings
                var mappedQuestionsQuery = $@"
                    SELECT q.*, bp.PartStatusTrackerID, t.PartNum, r.ResponseValue
                    FROM {SurveyResponseTable} r
                    INNER JOIN {SurveyBatchPartTable} bp ON r.SurveyBatchPartID = bp.Id
                    INNER JOIN {PartStatusTrackerTable} t ON bp.PartStatusTrackerID = t.Id
                    INNER JOIN {SurveyQuestionTable} q ON r.QuestionID = q.Id
                    WHERE bp.SurveyBatchID = @BatchId
                    AND q.MapsToField IS NOT NULL
                    AND q.AutoUpdateOnResponse = 1";

                var mappedResponses = await conn.QueryAsync<dynamic>(mappedQuestionsQuery, 
                    new { BatchId = batchId }, transaction);

                await transaction.CommitAsync();

                _logger.LogInformation("Submitted survey responses for batch {BatchId}", batchId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error submitting survey responses for batch {BatchId}", batchId);
                throw;
            }
        }
    }
}
