using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms;
using CrystalGroupHome.SharedRCL.Data.Parts;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using static CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms.CMHub_VendorCommsSurveyDTOs;
using static CrystalGroupHome.SharedRCL.Data.Vendor.VendorComms.CMHub_VendorCommsSurveyModels;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.Internal.Common.Data._Epicor;
using CrystalGroupHome.Internal.Common.Data.Vendors;

namespace CrystalGroupHome.Internal.Features.CMHub.VendorComms.Data
{
    public class CMHub_PartSurveyHistoryModel
    {
        public int BatchId { get; set; }
        public int VendorNum { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? SentDate { get; set; }
        public DateTime? ResponseDueDate { get; set; }
        public string BatchStatus { get; set; } = string.Empty;
        public string PartSubmissionStatus { get; set; } = string.Empty;
        public bool IsPartSubmitted => PartSubmissionStatus == "Submitted";
        public string TemplateName { get; set; } = string.Empty;
    }

    public class CMHub_SurveyResponseViewModel
    {
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string? ResponseValue { get; set; }
        public DateTime? ResponseDate { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class SurveyProcessingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int UpdatedFieldsCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public interface ICMHub_VendorCommsSurveyService
    {
        Task<List<CMHub_SurveyTemplateDTO>> GetSurveyTemplatesAsync();
        Task<CMHub_SurveyTemplateVersionDTO?> GetActiveTemplateVersionAsync(int templateId);
        Task<List<CMHub_SurveyQuestionDTO>> GetSurveyQuestionsAsync(int templateVersionId);
        Task<int> CreateSurveyBatchAsync(CMHub_SurveyBatchCreateModel model, string createdByEmpId);
        Task<CMHub_SurveyBatchViewModel?> GetSurveyBatchAsync(int batchId);
        string GenerateSurveyLink(int batchId);
        Task SendSurveyEmailAsync(int batchId, string sentByEmpId);
        Task<int> CreateOrUpdateSurveyQuestionAsync(CMHub_SurveyQuestionDTO question);
        Task<List<string>> GetAvailableEpicorFieldsAsync();
        Task<List<CMHub_PartSurveyHistoryModel>> GetPartSurveyHistoryAsync(int trackerId);
        Task<List<CMHub_SurveyResponseViewModel>> GetPartSurveyResponsesAsync(int trackerId, int batchId);
        Task<DateTime?> GetLatestResponseDateForTrackerAsync(int trackerId);
        Task<Dictionary<int, DateTime?>> GetLatestResponseDatesForTrackersAsync(IEnumerable<int> trackerIds);
        Task<SurveyProcessingResult> ProcessSurveyResponsesAsync(int batchId, int trackerId);
        Task<bool> HasProcessableResponsesAsync(int batchId, int trackerId);
        bool IsSurveySendingEnabled(int? allowedVendorExceptionNum);
        string? GetSurveyDisabledMessage();
        Task<Dictionary<int, bool>> GetProcessableResponsesForTrackersAsync(IEnumerable<int> trackerIds);
        int GetAllowedVendorExceptionNum();
        Task CloseSurveyAsync(int batchId, int trackerId, string closedByEmpId, string? notes = null);
        Task CloseSurveyBatchAsync(int batchId, string closedByEmpId, string? notes = null);
    }

    public class CMHub_VendorCommsSurveyService : ICMHub_VendorCommsSurveyService
    {
        private readonly string _connectionString;
        private readonly ILogger<CMHub_VendorCommsSurveyService> _logger;
        private readonly IVendorService _vendorService;
        private readonly EmailHelpers _emailHelpers;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly VendorSurveyFeatureOptions _featureOptions;

        private const string SurveyTemplateTable = "[CGIExt].[dbo].[CMVendorComms_SurveyTemplate]";
        private const string SurveyTemplateVersionTable = "[CGIExt].[dbo].[CMVendorComms_SurveyTemplateVersion]";
        private const string SurveyQuestionTable = "[CGIExt].[dbo].[CMVendorComms_SurveyQuestion]";
        private const string SurveyBatchTable = "[CGIExt].[dbo].[CMVendorComms_SurveyBatch]";
        private const string SurveyBatchPartTable = "[CGIExt].[dbo].[CMVendorComms_SurveyBatchPart]";
        private const string SurveyResponseTable = "[CGIExt].[dbo].[CMVendorComms_SurveyResponse]";
        private const string PartStatusTrackerTable = "[CGIExt].[dbo].[CMVendorComms_PartStatusTracker]";

        public const int AllowedVendorExceptionNum = 3294; //SourceDayTest in Production
        public int GetAllowedVendorExceptionNum() => AllowedVendorExceptionNum;

        // List of commonly used Epicor Part fields for EOLT tracking
        private static readonly List<string> AvailableEpicorFields = new()
        {
            "EOLT_EolDate_c",
            "EOLTLastTimeBuyDate_c",
            "EOLTReplacementPartNum_c",
            "EOLTNotes_c",
            "EOLTLastResponseDate_c",
            "EOLT_IsObsolete_c",
            "EOLT_InventoryAvailable_c",
            "EOLT_EstimatedStockDuration_c"
        };

        // Maximum parameters SQL Server allows per query (leaving some buffer)
        private const int MaxSqlParameters = 2000;

        public CMHub_VendorCommsSurveyService(
            IOptions<DatabaseOptions> dbOptions,
            ILogger<CMHub_VendorCommsSurveyService> logger,
            IVendorService vendorService,
            EmailHelpers emailHelpers,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            IOptions<VendorSurveyFeatureOptions> featureOptions)
        {
            _connectionString = dbOptions.Value.CGIExtConnection;
            _logger = logger;
            _vendorService = vendorService;
            _emailHelpers = emailHelpers;
            _environment = environment;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _featureOptions = featureOptions.Value;
        }

        public async Task<List<CMHub_SurveyTemplateDTO>> GetSurveyTemplatesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            var query = $"SELECT * FROM {SurveyTemplateTable} ORDER BY Name";
            return (await conn.QueryAsync<CMHub_SurveyTemplateDTO>(query)).ToList();
        }

        public async Task<CMHub_SurveyTemplateVersionDTO?> GetActiveTemplateVersionAsync(int templateId)
        {
            using var conn = new SqlConnection(_connectionString);
            var query = $@"
                SELECT * FROM {SurveyTemplateVersionTable} 
                WHERE SurveyTemplateID = @TemplateId AND IsActive = 1
                ORDER BY VersionNumber DESC";
            return await conn.QueryFirstOrDefaultAsync<CMHub_SurveyTemplateVersionDTO>(query, new { TemplateId = templateId });
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

        public async Task<int> CreateOrUpdateSurveyQuestionAsync(CMHub_SurveyQuestionDTO question)
        {
            using var conn = new SqlConnection(_connectionString);
            
            if (question.Id > 0)
            {
                // Update existing question
                var updateQuery = $@"
                    UPDATE {SurveyQuestionTable}
                    SET QuestionText = @QuestionText,
                        QuestionType = @QuestionType,
                        IsRequired = @IsRequired,
                        DisplayOrder = @DisplayOrder,
                        MapsToField = @MapsToField,
                        FieldDataType = @FieldDataType,
                        AutoUpdateOnResponse = @AutoUpdateOnResponse
                    WHERE Id = @Id";
                
                await conn.ExecuteAsync(updateQuery, question);
                return question.Id;
            }
            else
            {
                // Insert new question
                var insertQuery = $@"
                    INSERT INTO {SurveyQuestionTable}
                    (SurveyTemplateVersionID, QuestionText, QuestionType, IsRequired, DisplayOrder, 
                     MapsToField, FieldDataType, AutoUpdateOnResponse)
                    VALUES
                    (@SurveyTemplateVersionID, @QuestionText, @QuestionType, @IsRequired, @DisplayOrder,
                     @MapsToField, @FieldDataType, @AutoUpdateOnResponse);
                    SELECT CAST(SCOPE_IDENTITY() as int);";
                
                return await conn.QuerySingleAsync<int>(insertQuery, question);
            }
        }

        public async Task<List<string>> GetAvailableEpicorFieldsAsync()
        {
            // In a real implementation, you might query the Epicor metadata or maintain a configuration table
            return await Task.FromResult(AvailableEpicorFields);
        }

        public async Task<int> CreateSurveyBatchAsync(CMHub_SurveyBatchCreateModel model, string createdByEmpId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Get vendor comms service lazily to avoid circular dependency
                var vendorCommsService = _serviceProvider.GetRequiredService<ICMHub_VendorCommsService>();
                
                // First, we need to ensure all tracker IDs exist in the database
                // Get all trackers to check which ones exist
                var allTrackers = await vendorCommsService.GetTrackersAsync();
                var existingTrackerIds = allTrackers
                    .Where(t => t.Tracker.Id > 0)
                    .Select(t => t.Tracker.Id)
                    .ToHashSet();

                // Build a dictionary for quick lookup of trackers by ID
                var trackersByIdDict = allTrackers
                    .Where(t => t.Tracker.Id > 0)
                    .GroupBy(t => t.Tracker.Id)
                    .ToDictionary(g => g.Key, g => g.First());

                // Separate valid IDs from those that need creation
                var validTrackerIds = new List<int>();
                var trackersNeedingCreation = new List<CMHub_VendorCommsTrackerModel>();

                foreach (var trackerId in model.SelectedTrackerIds)
                {
                    if (trackerId > 0 && existingTrackerIds.Contains(trackerId))
                    {
                        // This is an existing tracker
                        validTrackerIds.Add(trackerId);
                    }
                    else if (trackerId <= 0)
                    {
                        // This is a placeholder for a new tracker
                        // We need to find the tracker model from the full list
                        // Since we're working with IDs, we need to get the tracker by matching vendor
                        var newTracker = allTrackers.FirstOrDefault(t => 
                            t.Tracker.Id == 0 && 
                            t.Vendor?.VendorNum == model.VendorNum);
                        
                        if (newTracker != null)
                        {
                            trackersNeedingCreation.Add(newTracker);
                        }
                    }
                }

                // Create any missing trackers (these need individual calls due to DB identity)
                foreach (var tracker in trackersNeedingCreation)
                {
                    try
                    {
                        var newTrackerId = await vendorCommsService.CreateOrUpdateTrackerAsync(tracker);
                        validTrackerIds.Add(newTrackerId);
                        
                        // Update our lookup dictionary with the newly created tracker
                        tracker.Tracker.Id = newTrackerId;
                        trackersByIdDict[newTrackerId] = tracker;
                        
                        _logger.LogInformation("Created new tracker ID {TrackerId} for part {PartNum} during survey batch creation", 
                            newTrackerId, tracker.Tracker.PartNum);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create tracker for part {PartNum}", tracker.Tracker.PartNum);
                        // Continue with other trackers
                    }
                }

                if (validTrackerIds.Count == 0)
                {
                    throw new InvalidOperationException("No valid tracker IDs available. Cannot create survey batch.");
                }

                // Collect trackers that need syncing - use our dictionary instead of individual queries
                var trackersToSync = new List<CMHub_VendorCommsTrackerModel>();
                foreach (var trackerId in validTrackerIds)
                {
                    if (trackersByIdDict.TryGetValue(trackerId, out var tracker))
                    {
                        trackersToSync.Add(tracker);
                    }
                }

                // Sync external columns for all trackers with latest ERP data
                // Batch the updates - sync all trackers then save
                foreach (var tracker in trackersToSync)
                {
                    try
                    {
                        // The tracker already has PartEolt and Vendor populated from GetTrackersAsync
                        // Just update to sync the external columns
                        await vendorCommsService.CreateOrUpdateTrackerAsync(tracker);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync external columns for tracker {TrackerId} before batch creation", tracker.Tracker.Id);
                        // Don't fail the entire batch if sync fails for one tracker
                    }
                }

                // Generate a 6-digit confirmation code for survey access
                var confirmationCode = GenerateConfirmationCode();

                // Create the batch
                var batchQuery = $@"
                    INSERT INTO {SurveyBatchTable} 
                    (VendorNum, SurveyTemplateVersionID, Status, ResponseDueDate, ConfirmationCode) 
                    VALUES 
                    (@VendorNum, @TemplateVersionId, 'Draft', @ResponseDueDate, @ConfirmationCode);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                var batchId = await conn.QuerySingleAsync<int>(batchQuery, new
                {
                    model.VendorNum,
                    model.TemplateVersionId,
                    model.ResponseDueDate,
                    ConfirmationCode = confirmationCode
                }, transaction);

                // Generate the survey link now that we have the batch ID
                var surveyLink = GenerateSurveyLink(batchId);

                // Batch insert all batch parts in one operation using a single multi-row INSERT
                var trackerIdList = validTrackerIds.ToList();

                if (trackerIdList.Count > 0)
                {
                    var partInsertSqlBuilder = new StringBuilder();
                    partInsertSqlBuilder.AppendLine($@"
                    INSERT INTO {SurveyBatchPartTable} 
                    (SurveyBatchID, PartStatusTrackerID) 
                    VALUES");

                    var partInsertParameters = new DynamicParameters();
                    partInsertParameters.Add("@BatchId", batchId);

                    for (int i = 0; i < trackerIdList.Count; i++)
                    {
                        var parameterName = $"@TrackerId{i}";
                        var valueClausePrefix = i == 0 ? "                        " : "                        ,";
                        partInsertSqlBuilder.AppendLine($"{valueClausePrefix}(@BatchId, {parameterName})");
                        partInsertParameters.Add(parameterName, trackerIdList[i]);
                    }

                    await conn.ExecuteAsync(partInsertSqlBuilder.ToString(), partInsertParameters, transaction);
                }
                // Prepare log entries for batch creation
                var logEntries = new List<CMHub_VendorCommsTrackerLogModel>();
                var trackersToUpdateContactDate = new List<CMHub_VendorCommsTrackerModel>();
                var logMessage = $"Added to survey batch #{batchId}\nSurvey Link: {surveyLink}\nResponse Due: {model.ResponseDueDate?.ToString("MM/dd/yyyy") ?? "No due date"}";

                foreach (var trackerId in validTrackerIds)
                {
                    if (trackersByIdDict.TryGetValue(trackerId, out var tracker))
                    {
                        // Update the Last Contact Date in memory
                        tracker.PartEolt.LastContactDate = DateTime.Today;
                        trackersToUpdateContactDate.Add(tracker);
                        
                        // Prepare log entry
                        logEntries.Add(new CMHub_VendorCommsTrackerLogModel
                        {
                            TrackerId = trackerId,
                            LogMessage = logMessage,
                            LoggedByUser = createdByEmpId,
                            ManualLogEntry = false
                        });
                    }
                }

                // Commit the transaction first to ensure batch parts are created
                await transaction.CommitAsync();

                // Now update trackers and create logs outside the transaction
                // (These operations update external data and logs, not critical for batch creation)
                foreach (var tracker in trackersToUpdateContactDate)
                {
                    try
                    {
                        await vendorCommsService.CreateOrUpdateTrackerAsync(tracker);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update LastContactDate for tracker {TrackerId}", tracker.Tracker.Id);
                    }
                }

                // Create all log entries
                foreach (var logEntry in logEntries)
                {
                    try
                    {
                        await vendorCommsService.CreateTrackerLogAsync(logEntry, createdByEmpId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create log entry for tracker {TrackerId}", logEntry.TrackerId);
                    }
                }
                
                _logger.LogInformation("Created survey batch {BatchId} with {PartCount} parts for vendor {VendorNum}", 
                    batchId, validTrackerIds.Count, model.VendorNum);
                
                return batchId;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating survey batch for vendor {VendorNum}", model.VendorNum);
                throw;
            }
        }

        public async Task<CMHub_SurveyBatchViewModel?> GetSurveyBatchAsync(int batchId)
        {
            using var conn = new SqlConnection(_connectionString);

            // Get batch
            var batchQuery = $"SELECT * FROM {SurveyBatchTable} WHERE Id = @BatchId";
            var batch = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyBatchDTO>(batchQuery, new { BatchId = batchId });
            if (batch == null) return null;

            var model = new CMHub_SurveyBatchViewModel { Batch = batch };

            // Get template version and template separately
            var versionQuery = $"SELECT * FROM {SurveyTemplateVersionTable} WHERE Id = @VersionId";
            model.TemplateVersion = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyTemplateVersionDTO>(versionQuery, new { VersionId = batch.SurveyTemplateVersionID });

            if (model.TemplateVersion != null)
            {
                var templateQuery = $"SELECT * FROM {SurveyTemplateTable} WHERE Id = @TemplateId";
                model.Template = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyTemplateDTO>(templateQuery, new { TemplateId = model.TemplateVersion.SurveyTemplateID });
            }

            // Get vendor
            var vendors = await _vendorService.GetVendorsByNumbersAsync(new[] { batch.VendorNum });
            model.Vendor = vendors.FirstOrDefault();

            // Get parts
            var partsQuery = $@"
                SELECT bp.* 
                FROM {SurveyBatchPartTable} bp
                WHERE bp.SurveyBatchID = @BatchId";

            var batchParts = await conn.QueryAsync<CMHub_SurveyBatchPartDTO>(partsQuery, new { BatchId = batchId });
            var vendorCommsService = _serviceProvider.GetRequiredService<ICMHub_VendorCommsService>();

            var trackerIds = batchParts.Select(p => p.PartStatusTrackerID).ToList();
            if (trackerIds.Any())
            {
                var trackers = await vendorCommsService.GetTrackersAsync();
                model.Parts = trackers.Where(t => trackerIds.Contains(t.Tracker.Id)).ToList();
            }

            // Generate survey link
            model.SurveyLink = GenerateSurveyLink(batchId);

            return model;
        }

        public string GenerateSurveyLink(int batchId)
        {
            // Generate a secure token for the survey
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            // Store token in database (you might want to add a token column to SurveyBatch table)
            // For now, we'll use a simple hash of batchId as token
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"survey-{batchId}-crystal"));
            token = Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

            var baseUrl = _configuration["ExternalSiteUrl"] ?? "https://vendor.crystalrugged.com";
            return $"{baseUrl}/survey/{batchId}/{token}";
        }

        public async Task SendSurveyEmailAsync(int batchId, string sentByEmpId)
        {
            var surveyBatch = await GetSurveyBatchAsync(batchId);

            // Check if survey sending is enabled
            if (!IsSurveySendingEnabled(surveyBatch?.Vendor?.VendorNum))
            {
                var message = GetSurveyDisabledMessage() ?? "Vendor survey sending is currently disabled.";
                _logger.LogWarning("Attempt to send survey batch {BatchId} blocked: {Message}", batchId, message);
                throw new InvalidOperationException(message);
            }
            
            if (surveyBatch == null || surveyBatch.Vendor == null)
            {
                throw new InvalidOperationException("Survey batch or vendor not found");
            }

            // Build the parts list HTML
            var partsHtml = "";
            if (surveyBatch.Parts != null && surveyBatch.Parts.Any())
            {
                partsHtml = @"
                    <h3>Parts requiring status update:</h3>
                    <table style='border-collapse: collapse; width: 100%; margin: 20px 0;'>
                        <thead>
                            <tr style='background-color: #f2f2f2;'>
                                <th style='border: 1px solid #ddd; padding: 12px; text-align: left;'>Part Number</th>
                                <th style='border: 1px solid #ddd; padding: 12px; text-align: left;'>Description</th>
                                <th style='border: 1px solid #ddd; padding: 12px; text-align: left;'>Current EOL Date</th>
                                <th style='border: 1px solid #ddd; padding: 12px; text-align: left;'>Current LTB Date</th>
                            </tr>
                        </thead>
                        <tbody>";

                foreach (var part in surveyBatch.Parts)
                {
                    // Determine which part number to display
                    string partNumberDisplay;
                    if (!string.IsNullOrEmpty(part.PartEolt.VendorPartNum))
                    {
                        // Use Vendor PartNum with Crystal PartNum for context
                        partNumberDisplay = $"{part.PartEolt.VendorPartNum}<br/><small style='color: #666;'>(Crystal Group: {part.Tracker.PartNum})</small>";
                    }
                    else if (!string.IsNullOrEmpty(part.PartEolt.MfgPartNum))
                    {
                        // Use Mfg PartNum with Crystal PartNum for context
                        partNumberDisplay = $"{part.PartEolt.MfgPartNum}<br/><small style='color: #666;'>(Crystal Group: {part.Tracker.PartNum})</small>";
                    }
                    else
                    {
                        // Use Crystal PartNum only, labeled as such
                        partNumberDisplay = $"Crystal: {part.Tracker.PartNum}";
                    }

                    partsHtml += $@"
                            <tr>
                                <td style='border: 1px solid #ddd; padding: 8px;'>{partNumberDisplay}</td>
                                <td style='border: 1px solid #ddd; padding: 8px;'>{part.PartEolt.PartDescription ?? "N/A"}</td>
                                <td style='border: 1px solid #ddd; padding: 8px;'>{part.PartEolt.EolDate?.ToString("MM/dd/yyyy") ?? "Not Set"}</td>
                                <td style='border: 1px solid #ddd; padding: 8px;'>{part.PartEolt.LastTimeBuyDate?.ToString("MM/dd/yyyy") ?? "Not Set"}</td>
                            </tr>";
                }

                partsHtml += @"
                        </tbody>
                    </table>";
            }

            var emailBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Part Status Update Request</h2>
                    <p>Dear {surveyBatch.Vendor.VendorName},</p>
                    <p>We are requesting a product lifecycle update on the status of the following parts. Please review the parts listed below and click the link to complete the survey:</p>
                    {partsHtml}
                    <div style='text-align: center; margin: 30px 0; padding: 20px; background-color: #f8f9fa; border: 2px solid #0d6efd; border-radius: 8px;'>
                        <p style='margin: 0 0 15px 0; font-size: 18px; font-weight: bold; color: #333;'>
                            📋 Click the link below to complete your survey:
                        </p>
                        <p style='margin: 0;'>
                            <a href='{surveyBatch.SurveyLink}' 
                               style='color: #0d6efd; font-size: 20px; font-weight: bold; text-decoration: underline;'>
                                Complete Part Status Survey
                            </a>
                        </p>
                    </div>
                    <div style='text-align: center; margin: 20px 0; padding: 20px; background-color: #fff3cd; border: 2px solid #ffc107; border-radius: 8px;'>
                        <p style='margin: 0 0 10px 0; font-size: 16px; font-weight: bold; color: #856404;'>
                            🔐 Your Confirmation Code:
                        </p>
                        <p style='margin: 0; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #333; font-family: monospace;'>
                            {surveyBatch.Batch.ConfirmationCode}
                        </p>
                        <p style='margin: 10px 0 0 0; font-size: 14px; color: #856404;'>
                            You will need to enter this code to access the survey.
                        </p>
                    </div>
                    <p><strong>Response due date: {surveyBatch.Batch.ResponseDueDate?.ToString("MM/dd/yyyy") ?? "N/A"}</strong></p>
                    <p>If you have any questions, please don't hesitate to contact us at <a href=""mailto:purchasing@crystalrugged.com"">purchasing@crystalrugged.com</a>.</p>
                    <p>Thank you for your cooperation.</p>
                    <p>Crystal Group Inc.</p>
                </body>
                </html>";

            var toRecipients = new List<string>();
            if (!string.IsNullOrEmpty(surveyBatch.Vendor.EmailAddress))
            {
                toRecipients.Add(surveyBatch.Vendor.EmailAddress);
            }
            else
            {
                var testEmail = _configuration["Email:TestEmailRecipientAddress"] ?? "appemails@crystalgroup.com";
                toRecipients.Add(testEmail);
            }

            _emailHelpers.SendEmail(
                subject: $"Part Status Update Request - {surveyBatch.Vendor.VendorName}",
                messageHtml: emailBody,
                toRecipients: toRecipients,
                environmentName: _environment.EnvironmentName,
                fromAddress: null,
                ccRecipients: null,
                msAttachment: null,
                attachmentFileName: null,
                bccRecipients: null,
                featureEmailEnabled: IsSurveySendingEnabled(surveyBatch.Vendor.VendorNum),
                previewRecipients: new List<string> { "purchasing@crystalrugged.com" }
            );

            // Update batch status to Sent
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(
                $"UPDATE {SurveyBatchTable} SET Status = 'Sent', SentDate = GETDATE() WHERE Id = @BatchId",
                new { BatchId = batchId });

            // Get vendor comms service lazily to avoid circular dependency
            var vendorCommsService = _serviceProvider.GetRequiredService<ICMHub_VendorCommsService>();

            // Log the email sent to each tracker
            if (surveyBatch.Parts != null)
            {
                foreach (var part in surveyBatch.Parts)
                {
                    await vendorCommsService.CreateTrackerLogAsync(new CMHub_VendorCommsTrackerLogModel
                    {
                        TrackerId = part.Tracker.Id,
                        LogMessage = $"Survey email sent to {surveyBatch.Vendor.VendorName} ({string.Join(", ", toRecipients)})\nBatch #{batchId}",
                        LoggedByUser = sentByEmpId,
                        ManualLogEntry = false
                    }, sentByEmpId);
                }
            }
        }

        public async Task<List<CMHub_PartSurveyHistoryModel>> GetPartSurveyHistoryAsync(int trackerId)
        {
            using var conn = new SqlConnection(_connectionString);

            var query = $@"
                SELECT 
                    b.Id as BatchId,
                    b.VendorNum,
                    pst.ext_VendorName as VendorName,
                    b.CreatedDate,
                    b.SentDate,
                    b.ResponseDueDate,
                    b.Status as BatchStatus,
                    ISNULL(bp.SubmissionStatus, 'Draft') as PartSubmissionStatus,
                    t.Name as TemplateName
                FROM {SurveyBatchPartTable} bp
                INNER JOIN {SurveyBatchTable} b ON bp.SurveyBatchID = b.Id
                INNER JOIN {SurveyTemplateVersionTable} tv ON b.SurveyTemplateVersionID = tv.Id
                INNER JOIN {SurveyTemplateTable} t ON tv.SurveyTemplateID = t.Id
                INNER JOIN {PartStatusTrackerTable} pst ON bp.PartStatusTrackerID = pst.Id
                WHERE bp.PartStatusTrackerID = @TrackerId
                ORDER BY b.CreatedDate DESC";

            var results = await conn.QueryAsync<CMHub_PartSurveyHistoryModel>(query, new { TrackerId = trackerId });
            return results.ToList();
        }

        public async Task<List<CMHub_SurveyResponseViewModel>> GetPartSurveyResponsesAsync(int trackerId, int batchId)
        {
            using var conn = new SqlConnection(_connectionString);
            
            var query = $@"
                SELECT 
                    q.QuestionText,
                    q.QuestionType,
                    q.DisplayOrder,
                    r.ResponseValue,
                    r.ResponseReceivedDate as ResponseDate
                FROM {SurveyBatchPartTable} bp
                INNER JOIN {SurveyResponseTable} r ON r.SurveyBatchPartID = bp.Id
                INNER JOIN {SurveyQuestionTable} q ON r.QuestionID = q.Id
                WHERE bp.PartStatusTrackerID = @TrackerId 
                AND bp.SurveyBatchID = @BatchId
                ORDER BY q.DisplayOrder";
            
            var results = await conn.QueryAsync<CMHub_SurveyResponseViewModel>(query, 
                new { TrackerId = trackerId, BatchId = batchId });
            return results.ToList();
        }

        public async Task<DateTime?> GetLatestResponseDateForTrackerAsync(int trackerId)
        {
            using var conn = new SqlConnection(_connectionString);

            var query = $@"
                SELECT MAX(r.ResponseReceivedDate)
                FROM {SurveyResponseTable} r
                INNER JOIN {SurveyBatchPartTable} bp ON r.SurveyBatchPartID = bp.Id
                WHERE bp.PartStatusTrackerID = @TrackerId";

            var result = await conn.QuerySingleOrDefaultAsync<DateTime?>(query, new { TrackerId = trackerId });
            return result;
        }

        public async Task<Dictionary<int, DateTime?>> GetLatestResponseDatesForTrackersAsync(IEnumerable<int> trackerIds)
        {
            var trackerIdsList = trackerIds.ToList();
            
            // Handle empty collection
            if (!trackerIdsList.Any())
            {
                return new Dictionary<int, DateTime?>();
            }

            // If within limits, execute single query
            if (trackerIdsList.Count <= MaxSqlParameters)
            {
                return await GetLatestResponseDatesForTrackersBatchAsync(trackerIdsList);
            }

            // Otherwise, batch the queries to avoid exceeding SQL parameter limit
            var resultDict = new Dictionary<int, DateTime?>();
            
            foreach (var batch in trackerIdsList.Chunk(MaxSqlParameters))
            {
                var batchResults = await GetLatestResponseDatesForTrackersBatchAsync(batch.ToList());
                foreach (var kvp in batchResults)
                {
                    resultDict[kvp.Key] = kvp.Value;
                }
            }
            
            return resultDict;
        }

        private async Task<Dictionary<int, DateTime?>> GetLatestResponseDatesForTrackersBatchAsync(List<int> trackerIdsList)
        {
            using var conn = new SqlConnection(_connectionString);

            // Use Dapper's built-in support for IN clauses with collections
            // This safely parameterizes the query without dynamic SQL string building
            var query = $@"
                SELECT 
                    bp.PartStatusTrackerID as Id, 
                    MAX(r.ResponseReceivedDate) as LatestResponseDate
                FROM {SurveyBatchPartTable} bp
                LEFT JOIN {SurveyResponseTable} r ON bp.Id = r.SurveyBatchPartID
                WHERE bp.PartStatusTrackerID IN @TrackerIds
                GROUP BY bp.PartStatusTrackerID";

            var results = await conn.QueryAsync<LatestResponseResult>(query, new { TrackerIds = trackerIdsList });
            
            // Build result dictionary, including trackers with no responses (null dates)
            var resultDict = results.ToDictionary(r => r.Id, r => r.LatestResponseDate);
            
            // Ensure all requested tracker IDs are in the result (with null for those without responses)
            foreach (var trackerId in trackerIdsList)
            {
                if (!resultDict.ContainsKey(trackerId))
                {
                    resultDict[trackerId] = null;
                }
            }
            
            return resultDict;
        }

        public async Task<SurveyProcessingResult> ProcessSurveyResponsesAsync(int batchId, int trackerId)
        {
            var result = new SurveyProcessingResult();
            
            try
            {
                using var conn = new SqlConnection(_connectionString);
                
                // Get questions with field mappings for this specific part's responses
                var mappedQuestionsQuery = $@"
                    SELECT q.*, bp.PartStatusTrackerID, t.PartNum, r.ResponseValue
                    FROM {SurveyResponseTable} r
                    INNER JOIN {SurveyBatchPartTable} bp ON r.SurveyBatchPartID = bp.Id
                    INNER JOIN {PartStatusTrackerTable} t ON bp.PartStatusTrackerID = t.Id
                    INNER JOIN {SurveyQuestionTable} q ON r.QuestionID = q.Id
                    WHERE bp.SurveyBatchID = @BatchId
                    AND bp.PartStatusTrackerID = @TrackerId
                    AND q.MapsToField IS NOT NULL
                    AND q.AutoUpdateOnResponse = 1
                    AND r.ResponseValue IS NOT NULL";

                var mappedResponses = await conn.QueryAsync<dynamic>(mappedQuestionsQuery, 
                    new { BatchId = batchId, TrackerId = trackerId });

                if (!mappedResponses.Any())
                {
                    result.Success = true;
                    result.Message = "No mapped fields found for processing";
                    return result;
                }

                // Try to get part service - if not available, log warning but continue
                IPartService? partService;
                IEpicorPartService? epicorPartService;
                
                try
                {
                    partService = _serviceProvider.GetRequiredService<IPartService>();
                    epicorPartService = _serviceProvider.GetRequiredService<IEpicorPartService>();
                }
                catch (InvalidOperationException ex)
                {
                    result.Errors.Add($"Required services not available: {ex.Message}");
                    _logger.LogError(ex, "Part services not available for survey response processing");
                    return result;
                }
                
                var partNum = mappedResponses.First().PartNum as string;
                if (string.IsNullOrEmpty(partNum))
                {
                    result.Errors.Add("Part number not found");
                    return result;
                }

                // Get current part data from Epicor
                var parts = await partService.GetPartsByPartNumbersAsync<PartEoltDTO>(new[] { partNum });
                if (!parts.Any())
                {
                    result.Errors.Add($"Part {partNum} not found in Epicor");
                    return result;
                }

                var updateDto = parts.First();
                var originalDto = ClonePartDto(updateDto); // Create a copy for comparison
                updateDto.LastProcessedSurveyResponseDate = DateTime.Now;

                var updatedFields = new List<string>();

                // Dynamically set fields based on mappings
                foreach (var field in mappedResponses)
                {
                    try
                    {
                        var fieldName = field.MapsToField as string;
                        var responseValue = field.ResponseValue as string;
                        var dataType = field.FieldDataType as string;

                        if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(responseValue))
                            continue;

                        var convertedValue = ConvertResponseValue(responseValue, dataType);
                        var originalValue = GetDtoPropertyValue(originalDto, fieldName);

                        if (SetDtoProperty(updateDto, fieldName, convertedValue))
                        {
                            updatedFields.Add($"{fieldName}: '{originalValue}' → '{convertedValue}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Failed to process field {field.MapsToField}: {ex.Message}");
                    }
                }

                if (updatedFields.Any())
                {
                    // Update part via Epicor API
                    var updateSuccess = await epicorPartService.UpdatePartAsync(updateDto);
                    if (!updateSuccess)
                    {
                        result.Errors.Add("Failed to update part in Epicor");
                        return result;
                    }
                    
                    result.UpdatedFieldsCount = updatedFields.Count;
                    
                    // Log the processing action
                    try
                    {
                        var vendorCommsService = _serviceProvider.GetRequiredService<ICMHub_VendorCommsService>();
                        await vendorCommsService.CreateTrackerLogAsync(new CMHub_VendorCommsTrackerLogModel
                        {
                            TrackerId = trackerId,
                            LogMessage = $"Survey responses processed and applied to Epicor:\n{string.Join("\n", updatedFields)}\nBatch #{batchId}",
                            LoggedByUser = "System", // Could pass current user if available
                            ManualLogEntry = false
                        }, "System");
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Failed to log processing action: {ex.Message}");
                    }

                    _logger.LogInformation("Processed survey responses for part {PartNum}, batch {BatchId}: {UpdatedFields}", 
                        partNum, batchId, string.Join(", ", updatedFields));
                }

                result.Success = true;
                result.Message = updatedFields.Any() 
                    ? $"Successfully updated {updatedFields.Count} field(s)"
                    : "No changes were needed";

                if (result.Warnings.Any())
                {
                    result.Message += $" with {result.Warnings.Count} warning(s)";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing survey responses for batch {BatchId}, tracker {TrackerId}", batchId, trackerId);
                result.Success = false;
                result.Errors.Add($"Processing failed: {ex.Message}");
            }

            return result;
        }

        public async Task<bool> HasProcessableResponsesAsync(int batchId, int trackerId)
        {
            using var conn = new SqlConnection(_connectionString);
            
            var query = $@"
                SELECT COUNT(*)
                FROM {SurveyResponseTable} r
                INNER JOIN {SurveyBatchPartTable} bp ON r.SurveyBatchPartID = bp.Id
                INNER JOIN {SurveyQuestionTable} q ON r.QuestionID = q.Id
                WHERE bp.SurveyBatchID = @BatchId
                AND bp.PartStatusTrackerID = @TrackerId
                AND q.MapsToField IS NOT NULL
                AND q.AutoUpdateOnResponse = 1
                AND r.ResponseValue IS NOT NULL
                AND r.ResponseValue != ''";
            
            var count = await conn.ExecuteScalarAsync<int>(query, new { BatchId = batchId, TrackerId = trackerId });
            return count > 0;
        }

        public async Task<Dictionary<int, bool>> GetProcessableResponsesForTrackersAsync(IEnumerable<int> trackerIds)
        {
            var trackerIdsList = trackerIds.ToList();
            
            // Handle empty collection
            if (!trackerIdsList.Any())
            {
                return new Dictionary<int, bool>();
            }

            // If within limits, execute single query
            if (trackerIdsList.Count <= MaxSqlParameters)
            {
                return await GetProcessableResponsesForTrackersBatchAsync(trackerIdsList);
            }

            // Otherwise, batch the queries to avoid exceeding SQL parameter limit
            var resultDict = new Dictionary<int, bool>();
            
            foreach (var batch in trackerIdsList.Chunk(MaxSqlParameters))
            {
                var batchResults = await GetProcessableResponsesForTrackersBatchAsync(batch.ToList());
                foreach (var kvp in batchResults)
                {
                    resultDict[kvp.Key] = kvp.Value;
                }
            }
            
            return resultDict;
        }

        private async Task<Dictionary<int, bool>> GetProcessableResponsesForTrackersBatchAsync(List<int> trackerIdsList)
        {
            using var conn = new SqlConnection(_connectionString);
            
            var query = $@"
                SELECT DISTINCT bp.PartStatusTrackerID
                FROM {SurveyResponseTable} r
                INNER JOIN {SurveyBatchPartTable} bp ON r.SurveyBatchPartID = bp.Id
                INNER JOIN {SurveyQuestionTable} q ON r.QuestionID = q.Id
                WHERE bp.PartStatusTrackerID IN @TrackerIds
                AND bp.SubmissionStatus = 'Submitted'
                AND q.MapsToField IS NOT NULL
                AND q.AutoUpdateOnResponse = 1
                AND r.ResponseValue IS NOT NULL
                AND r.ResponseValue != ''";
            
            var trackersWithProcessableResponses = await conn.QueryAsync<int>(query, new { TrackerIds = trackerIdsList });
            var processableSet = trackersWithProcessableResponses.ToHashSet();
            
            // Return a dictionary with true for trackers that have processable responses
            return trackerIdsList.ToDictionary(
                id => id, 
                id => processableSet.Contains(id)
            );
        }

        private object? ConvertResponseValue(string responseValue, string? dataType)
        {
            if (string.IsNullOrEmpty(responseValue) || string.IsNullOrEmpty(dataType))
                return responseValue;

            return dataType?.ToLower() switch
            {
                "datetime" => DateTime.TryParse(responseValue, out var dt) ? dt : null,
                "date" => DateTime.TryParse(responseValue, out var d) ? d.Date : null,
                "boolean" or "bool" => bool.TryParse(responseValue, out var b) ? b : null,
                "int" or "integer" => int.TryParse(responseValue, out var i) ? i : null,
                "decimal" => decimal.TryParse(responseValue, out var dec) ? dec : null,
                "double" => double.TryParse(responseValue, out var dbl) ? dbl : null,
                _ => responseValue
            };
        }

        private bool SetDtoProperty(PartEoltDTO dto, string fieldName, object? value)
        {
            try
            {
                var property = dto.GetType().GetProperty(fieldName);
                if (property != null && property.CanWrite)
                {
                    // Get current value to check if it's actually changing
                    var currentValue = property.GetValue(dto);
                    
                    // Convert null to appropriate type if needed
                    if (value == null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
                    {
                        // Don't set non-nullable value types to null
                        return false;
                    }
                    
                    // Check if values are different
                    if (!Equals(currentValue, value))
                    {
                        property.SetValue(dto, value);
                        return true;
                    }
                }
                else
                {
                    _logger.LogWarning("Field {FieldName} not found or not writable on PartEoltDTO", fieldName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting property {FieldName} to value {Value}", fieldName, value);
            }
            
            return false;
        }

        private object? GetDtoPropertyValue(PartEoltDTO dto, string fieldName)
        {
            try
            {
                var property = dto.GetType().GetProperty(fieldName);
                return property?.GetValue(dto);
            }
            catch
            {
                return null;
            }
        }

        private PartEoltDTO ClonePartDto(PartEoltDTO original)
        {
            // Simple cloning - in a real implementation you might use AutoMapper or similar
            return new PartEoltDTO
            {
                PartNum = original.PartNum,
                PartDescription = original.PartDescription,
                RevisionNum = original.RevisionNum,
                EolDate = original.EolDate,
                NotifyIntervalDays = original.NotifyIntervalDays,
                LastContactDate = original.LastContactDate,
                LastProcessedSurveyResponseDate = original.LastProcessedSurveyResponseDate,
                LastTimeBuyDate = original.LastTimeBuyDate,
                LastTimeBuyDateConfirmed = original.LastTimeBuyDateConfirmed,
                ExcludeVendorComms = original.ExcludeVendorComms,
                ReplacementPartNum = original.ReplacementPartNum,
                TechNotes = original.TechNotes,
                VendorPartNum = original.VendorPartNum,
                MfgPartNum = original.MfgPartNum,
                MfgName = original.MfgName
            };
        }

        public bool IsSurveySendingEnabled(int? allowedVendorNum)
        {
            return allowedVendorNum == AllowedVendorExceptionNum || _featureOptions.EnableSurveySending;
        }

        public string? GetSurveyDisabledMessage()
        {
            return _featureOptions.DisabledMessage;
        }

        /// <summary>
        /// Manually closes a survey for a specific part, marking it as complete without vendor submission
        /// </summary>
        public async Task CloseSurveyAsync(int batchId, int trackerId, string closedByEmpId, string? notes = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            
            try
            {
                // Get the batch part record
                var batchPartQuery = $@"
                    SELECT bp.* 
                    FROM {SurveyBatchPartTable} bp
                    WHERE bp.SurveyBatchID = @BatchId 
                    AND bp.PartStatusTrackerID = @TrackerId";
                
                var batchPart = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyBatchPartDTO>(
                    batchPartQuery, 
                    new { BatchId = batchId, TrackerId = trackerId },
                    transaction);
                
                if (batchPart == null)
                {
                    throw new InvalidOperationException($"Survey batch part not found for batch {batchId} and tracker {trackerId}");
                }
                
                // Update the submission status to Submitted (marking as complete)
                var updateQuery = $@"
                    UPDATE {SurveyBatchPartTable} 
                    SET SubmissionStatus = 'Submitted'
                    WHERE SurveyBatchID = @BatchId 
                    AND PartStatusTrackerID = @TrackerId";
                
                await conn.ExecuteAsync(updateQuery, new { BatchId = batchId, TrackerId = trackerId }, transaction);
                
                // Get the first question from this survey template to create a dummy response
                // This ensures the system recognizes the survey as "responded to"
                var batch = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyBatchDTO>(
                    $"SELECT * FROM {SurveyBatchTable} WHERE Id = @BatchId",
                    new { BatchId = batchId },
                    transaction);
                
                if (batch != null)
                {
                    // Get any question from the survey template (we just need one to create a response record)
                    var questionQuery = $@"
                        SELECT TOP 1 * FROM {SurveyQuestionTable} 
                        WHERE SurveyTemplateVersionID = @TemplateVersionId 
                        ORDER BY DisplayOrder";
                    
                    var question = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyQuestionDTO>(
                        questionQuery,
                        new { TemplateVersionId = batch.SurveyTemplateVersionID },
                        transaction);
                    
                    if (question != null)
                    {
                        // Create a dummy response to mark the survey as responded
                        // This allows the LatestSurveyResponseDate to be populated
                        var insertResponseQuery = $@"
                            INSERT INTO {SurveyResponseTable}
                            (SurveyBatchPartID, QuestionID, ResponseValue, ResponseReceivedDate)
                            VALUES
                            (@BatchPartId, @QuestionId, @ResponseValue, GETDATE())";
                        
                        await conn.ExecuteAsync(
                            insertResponseQuery,
                            new 
                            { 
                                BatchPartId = batchPart.Id,
                                QuestionId = question.Id,
                                ResponseValue = "[Manually closed by internal user]"
                            },
                            transaction);
                    }
                }
                
                await transaction.CommitAsync();
                
                // Log the manual closure
                var vendorCommsService = _serviceProvider.GetRequiredService<ICMHub_VendorCommsService>();
                var logMessage = $"Survey manually closed by internal user (Batch #{batchId})";
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    logMessage += $"\nNotes: {notes}";
                }
                logMessage += "\nThis survey was marked as complete without vendor submission.";
                
                await vendorCommsService.CreateTrackerLogAsync(new CMHub_VendorCommsTrackerLogModel
                {
                    TrackerId = trackerId,
                    LogMessage = logMessage,
                    LoggedByUser = closedByEmpId,
                    ManualLogEntry = true
                }, closedByEmpId);
                
                _logger.LogInformation(
                    "Survey manually closed for batch {BatchId}, tracker {TrackerId} by {EmployeeId}", 
                    batchId, trackerId, closedByEmpId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error closing survey for batch {BatchId}, tracker {TrackerId}", batchId, trackerId);
                throw;
            }
        }

        /// <summary>
        /// Closes an entire survey batch and marks all parts as complete
        /// </summary>
        public async Task CloseSurveyBatchAsync(int batchId, string closedByEmpId, string? notes = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            
            try
            {
                // Get the batch
                var batch = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyBatchDTO>(
                    $"SELECT * FROM {SurveyBatchTable} WHERE Id = @BatchId",
                    new { BatchId = batchId },
                    transaction);
                
                if (batch == null)
                {
                    throw new InvalidOperationException($"Survey batch {batchId} not found");
                }
                
                // Get all parts in this batch
                var partsQuery = $@"
                    SELECT bp.* 
                    FROM {SurveyBatchPartTable} bp
                    WHERE bp.SurveyBatchID = @BatchId";
                
                var batchParts = await conn.QueryAsync<CMHub_SurveyBatchPartDTO>(
                    partsQuery, 
                    new { BatchId = batchId }, 
                    transaction);
                
                if (!batchParts.Any())
                {
                    throw new InvalidOperationException($"No parts found in survey batch {batchId}");
                }
                
                // Update all parts to Submitted status
                var updatePartsQuery = $@"
                    UPDATE {SurveyBatchPartTable} 
                    SET SubmissionStatus = 'Submitted'
                    WHERE SurveyBatchID = @BatchId";
                
                await conn.ExecuteAsync(updatePartsQuery, new { BatchId = batchId }, transaction);
                
                // Update batch status to Closed
                var updateBatchQuery = $@"
                    UPDATE {SurveyBatchTable} 
                    SET Status = 'Closed'
                    WHERE Id = @BatchId";
                
                await conn.ExecuteAsync(updateBatchQuery, new { BatchId = batchId }, transaction);
                
                // Get any question from the survey template (we just need one to create response records)
                var questionQuery = $@"
                    SELECT TOP 1 * FROM {SurveyQuestionTable} 
                    WHERE SurveyTemplateVersionID = @TemplateVersionId 
                    ORDER BY DisplayOrder";
                
                var question = await conn.QueryFirstOrDefaultAsync<CMHub_SurveyQuestionDTO>(
                    questionQuery,
                    new { TemplateVersionId = batch.SurveyTemplateVersionID },
                    transaction);
                
                if (question != null)
                {
                    // Create dummy responses for all parts to mark them as responded
                    foreach (var part in batchParts)
                    {
                        var insertResponseQuery = $@"
                            INSERT INTO {SurveyResponseTable}
                            (SurveyBatchPartID, QuestionID, ResponseValue, ResponseReceivedDate)
                            VALUES
                            (@BatchPartId, @QuestionId, @ResponseValue, GETDATE())";
                        
                        await conn.ExecuteAsync(
                            insertResponseQuery,
                            new 
                            { 
                                BatchPartId = part.Id,
                                QuestionId = question.Id,
                                ResponseValue = "[Manually closed by internal user - entire batch]"
                            },
                            transaction);
                    }
                }
                
                await transaction.CommitAsync();
                
                // Log the closure for each part
                var vendorCommsService = _serviceProvider.GetRequiredService<ICMHub_VendorCommsService>();
                foreach (var part in batchParts)
                {
                    var logMessage = $"Survey batch #{batchId} manually closed by internal user";
                    if (!string.IsNullOrWhiteSpace(notes))
                    {
                        logMessage += $"\nNotes: {notes}";
                    }
                    logMessage += "\nAll parts in this batch were marked as complete without vendor submission.";
                    
                    await vendorCommsService.CreateTrackerLogAsync(new CMHub_VendorCommsTrackerLogModel
                    {
                        TrackerId = part.PartStatusTrackerID,
                        LogMessage = logMessage,
                        LoggedByUser = closedByEmpId,
                        ManualLogEntry = true
                    }, closedByEmpId);
                }
                
                _logger.LogInformation(
                    "Survey batch {BatchId} manually closed by {EmployeeId}. {PartCount} parts marked as complete", 
                    batchId, closedByEmpId, batchParts.Count());
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error closing survey batch {BatchId}", batchId);
                throw;
            }
        }

        private class LatestResponseResult
        {
            public int Id { get; set; }
            public DateTime? LatestResponseDate { get; set; }
        }

        /// <summary>
        /// Generates a random 6-digit confirmation code for survey access verification.
        /// </summary>
        private static string GenerateConfirmationCode()
        {
            // Generate a cryptographically secure random 6-digit number
            var bytes = RandomNumberGenerator.GetBytes(4);
            var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
            return number.ToString("D6");
        }
    }
}