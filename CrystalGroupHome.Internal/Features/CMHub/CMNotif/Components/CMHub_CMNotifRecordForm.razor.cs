using CrystalGroupHome.Internal.Authorization;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.Internal.Features.CMHub.CMNotif.Data;
using CrystalGroupHome.Internal.Features.CMHub.CMNotif.Models;
using CrystalGroupHome.SharedRCL.Components;
using CrystalGroupHome.SharedRCL.Data.Labor;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Helpers;
using CrystalGroupHome.SharedRCL.Data;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using static CrystalGroupHome.Internal.Features.CMHub.CMNotif.Components.CMHub_CMNotifPDFFormModal;

namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Components
{
    public class CMHub_CMNotifRecordFormBase : ComponentBase
    {
        private static readonly string CMNSPDFExportLocation = "\\\\cgfs0\\Data\\QDMS\\CMNS\\";
        private static readonly string CMNSTemplatePDFLocation = $"{CMNSPDFExportLocation}FRM-00013_TEST_02.pdf";
        
        [Inject] public ICMHub_CMNotifService CMNotifService { get; set; } = default!;
        [Inject] public ICMHub_CMDexService CMDexService { get; set; } = default!;
        [Inject] public IADUserService ADUserService { get; set; } = default!;
        [Inject] public IPartService PartService { get; set; } = default!;
        [Inject] public EmailHelpers EmailHelpers { get; set; } = default!;
        [Inject] public IWebHostEnvironment Environment { get; set; } = default!;
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
        [Inject] protected IJSRuntime JS { get; set; } = default!;
        [Inject] protected ECNHelpers ECNHelpers { get; set; } = default!;
        [Inject] public IOptions<CMNotificationsFeatureOptions> CMNotificationsOptions { get; set; } = default!;
        [Inject] public IAuthorizationService AuthorizationService { get; set; } = default!;
        [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

        [Parameter] public string? ECNNumber { get; set; }
        [CascadingParameter] public ADUserModel? CurrentUser { get; set; }

        protected bool IsLoading = true;
        protected bool HasDocumentEditPermission { get; set; } = false;
        protected bool HasCreateLogPermission { get; set; } = false;

        protected CMHub_CMNotifECNMatchedRecordModel? ECNRecord;

        protected CMHub_CMNotifRecordLogListModal? LogListModal { get; set; }

        protected CMHub_CMNotifNewLogFormModal? LogFormModal;

        protected CMHub_CMNotifPDFFormModal? PdfFormModal;

        protected ConfirmationModal? SendNotifDocConfirmationModal;
        private PDFFormDataModel? _pendingSendFormData;

        protected ConfirmationModal? MarkAcceptedConfirmationModal;
        protected ConfirmationModal? MarkAcceptedOverrideConfirmationModal;
        protected ConfirmationModal? UploadAdditionalEvidenceModal;
        private CMHub_CMNotifRecordPartModel? _partRecordToMark;
        private IReadOnlyList<IBrowserFile> _evidenceFiles = new List<IBrowserFile>();
        protected string _evidenceLogMessage = string.Empty;

        protected ConfirmationModal? SendConfirmImplementedConfirmationModal;

        public string SendNotifDocConfirmationMessage { get; set; } = string.Empty;

        protected Dictionary<string, int> PdfFieldLimits = new();

        protected override async Task OnInitializedAsync()
        {
            await CheckAuthorizationAsync();
            await LoadData();
            PdfFieldLimits = GetPdfFieldCharacterLimits(CMNSTemplatePDFLocation);
        }

        private async Task CheckAuthorizationAsync()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            
            var documentEditResult = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubCMNotifDocumentEdit);
            HasDocumentEditPermission = documentEditResult.Succeeded;

            var createLogResult = await AuthorizationService.AuthorizeAsync(
                authState.User,
                AuthorizationPolicies.CMHubCMNotifCreateLog);
            HasCreateLogPermission = createLogResult.Succeeded;
        }

        public static Dictionary<string, int> GetPdfFieldCharacterLimits(string pdfTemplatePath)
        {
            var fieldLimits = new Dictionary<string, int>();

            using var pdfReader = new PdfReader(pdfTemplatePath);
            using var pdfDoc = new PdfDocument(pdfReader);
            PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, false);
            if (form == null) return fieldLimits;

            var fields = form.GetAllFormFields(); // includes child fields

            foreach (var kvp in fields)
            {
                string name = kvp.Key;
                PdfFormField field = kvp.Value;

                // Only text fields support character limits
                if (field is PdfTextFormField textField)
                {
                    int maxLen = textField.GetMaxLen(); // returns -1 if unlimited
                    fieldLimits[name] = maxLen > 0 ? maxLen : int.MaxValue;
                }
            }

            return fieldLimits;
        }

        protected async Task LoadData()
        {
            IsLoading = true;
            try
            {
                var results = await CMNotifService.GetHeldECNPartsAsync([ECNNumber]);
                ECNRecord = results.FirstOrDefault();

                if (ECNRecord?.Record != null)
                {
                    // If a Record is found, we have previously done SOMETHING with this ECN
                    List<string> recordPartNumbers = ECNRecord.Record.RecordedParts
                        .Where(p => !p.Deleted)
                        .Select(p => p.PartNum)
                        .ToList();

                    var recordedCMDexParts = await CMDexService.GetCMDexPartsByPartNumbersAsync(recordPartNumbers);

                    foreach (var part in ECNRecord.Record.RecordedParts)
                    {
                        part.CMDexPart = recordedCMDexParts.FirstOrDefault(p => p.Part.PartNum == part.PartNum);
                        if (part.CMDexPart != null)
                        {
                            await CMDexService.FillCMPartDisplayData(part.CMDexPart);
                        }
                    }

                    // But we need to compare the recorded part logs/notifications 
                    // with the parts that are actually on the ECN
                    List<string> ecnPartNumbers = ECNRecord.ECNParts
                        .Select(p => p.PartNum)
                        .ToList();

                    var ecnCMDexParts = await CMDexService.GetCMDexPartsByPartNumbersAsync(ecnPartNumbers);
                    var cmManagedPartsOnly = ecnCMDexParts.Where(p => p.Part.CMManaged_c);

                    foreach (var cmPart in cmManagedPartsOnly)
                    {
                        // Add to temp parts so we can compare the parts on the ECN to the recorded parts
                        await CMDexService.FillCMPartDisplayData(cmPart);
                        ECNRecord.TempCMDexParts.Add(cmPart);
                    }

                    // We will also need to get any employee info for ApprovedByEmpIds and LoggedByEmpId and match them to the records
                    var allApprovedByEmpIds = ECNRecord.Record.RecordedParts
                        .Where(p => !string.IsNullOrWhiteSpace(p.ApprovedByEmpId))
                        .Select(p => p.ApprovedByEmpId)
                        .Distinct()
                        .ToList();

                    var allLoggedByEmpIds = ECNRecord.Record.RecordedLogs
                        .Where(p => !string.IsNullOrWhiteSpace(p.LoggedByEmpId))
                        .Select(p => p.LoggedByEmpId)
                        .Distinct()
                        .ToList();

                    if (allApprovedByEmpIds.Count > 0)
                    {
                        var approvedByEmployees = await ADUserService
                            .GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(allApprovedByEmpIds);

                        var approvedByLookup = approvedByEmployees
                            .ToDictionary(emp => emp.EmployeeNumber, emp => emp);

                        foreach (var part in ECNRecord.Record.RecordedParts)
                        {
                            if (!string.IsNullOrWhiteSpace(part.ApprovedByEmpId)
                                && approvedByLookup.TryGetValue(part.ApprovedByEmpId, out var employee))
                            {
                                part.ApprovedByEmployee = employee;
                            }
                        }
                    }

                    if (allLoggedByEmpIds.Count > 0)
                    {
                        var allLoggedByEmployees = await ADUserService
                            .GetADUsersByEmployeeNumbersAsync<ADUserDTO_Base>(allLoggedByEmpIds);

                        var loggedByLookup = allLoggedByEmployees
                            .ToDictionary(emp => emp.EmployeeNumber, emp => emp);

                        foreach (var part in ECNRecord.Record.RecordedLogs)
                        {
                            if (!string.IsNullOrWhiteSpace(part.LoggedByEmpId)
                                && loggedByLookup.TryGetValue(part.LoggedByEmpId, out var employee))
                            {
                                part.LoggedByEmployee = employee;
                            }
                        }
                    }
                }
                else if (ECNRecord?.TempRecord != null)
                {
                    // If only a Temp Record is returned that means nothing actually exists in the CMNS database tables for this ECN yet
                    // And if we do something with it (send a Notification, etc) we will eventually create some stuff there
                    List<string> partNumbers = ECNRecord.ECNParts
                        .Select(p => p.PartNum)
                        .ToList();

                    var cmDexParts = await CMDexService.GetCMDexPartsByPartNumbersAsync(partNumbers);
                    var cmManagedPartsOnly = cmDexParts.Where(p => p.Part.CMManaged_c);

                    foreach (var cmPart in cmManagedPartsOnly)
                    {
                        await CMDexService.FillCMPartDisplayData(cmPart);
                        ECNRecord.TempCMDexParts.Add(cmPart);
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Helper method to refresh part logs efficiently
        private async Task RefreshPartLogs()
        {
            if (ECNRecord?.Record == null) return;

            ECNRecord.Record.RecordedLogs = await CMNotifService.GetLogsByRecordIdAsync(ECNRecord.Record.Id) ?? new List<CMHub_CMNotifRecordLogModel>();
        }

        protected Task ShowPartLogsAsync(string? partNum = null)
        {
            List<CMHub_CMNotifRecordLogModel>? partLogs = null;
            if (partNum == null)
            {
                partLogs = ECNRecord?.Record?.RecordedLogs;
            }
            else
            {
                partLogs = ECNRecord?.Record?.RecordedLogs.Where(p => p.LogAssociatedWithPartNum == partNum).ToList();
            }

            if (partLogs != null)
            {
                // Clone logs before modifying properties to avoid side effects
                var clonedPartLogs = partLogs
                    .Select(log => {
                        var clonedLog = log;
                        return clonedLog;
                    })
                    .OrderByDescending(l => l.LogDate)
                    .ToList();

                LogListModal?.Show(
                    title: $"Part Logs for {partNum}",
                    recordLogs: clonedPartLogs
                );
            }

            return Task.CompletedTask;
        }

        protected async Task HandleLogSubmitted(CMHub_CMNotifRecordLogModel log)
        {
            if (CurrentUser == null) return;

            log.LoggedByEmpId = CurrentUser.DBUser.EmployeeNumber;

            int logId = await CMNotifService.CreateRecordLogAsync(log);

            await RefreshPartLogs();
        }

        protected async Task HandleFormSaved(CMHub_CMNotifPDFFormModal.PDFFormDataModel form)
        {
            if (form.RecordId == 0)
            {
                // Find or create the record
                var record = await CMNotifService.CreateRecordByECNNumberAsync(form.ECNNum);
                form.RecordId = record.Id;

                // Part does not exist yet, create it
                var partRecords = await CMNotifService.GetPartsByRecordIdAsync(form.RecordId);
                var partRecordFound = partRecords.Any(_ => _.PartNum == form.PartNum);
                if (!partRecordFound)
                {
                    var newPart = CMHub_CMNotifPDFFormModal.ConvertPdfFormToNotifPart(form);
                    newPart.DateCreated = DateTime.UtcNow;
                    var newPartRecordId = await CMNotifService.CreatePartAssociatedWithRecordAsync(newPart);
                }
            }
            else
            {
                if (CurrentUser?.DBUser != null)
                {
                    // Log the document change
                    var log = new CMHub_CMNotifRecordLogModel
                    {
                        RecordId = form.RecordId,
                        LogAssociatedWithPartNum = form.PartNum,
                        LogDate = DateTime.UtcNow,
                        LoggedByEmpId = CurrentUser.DBUser.EmployeeNumber,
                        LogMessage = $"Notification details modified by {CurrentUser.DBUser.DisplayName}."
                    };
                    await CMNotifService.CreateRecordLogAsync(log);
                }

                // Update existing part
                await CMNotifService.UpdatePartByPartIdAsync(CMHub_CMNotifPDFFormModal.ConvertPdfFormToNotifPart(form));
            }
            await LoadData();
        }

        protected async Task MarkCustomerAccepted(CMHub_CMNotifRecordPartModel? partRecord)
        {
            if (partRecord != null && MarkAcceptedConfirmationModal != null)
            {
                _partRecordToMark = partRecord;
                _evidenceFiles = new List<IBrowserFile>();
                _evidenceLogMessage = string.Empty;
                await MarkAcceptedConfirmationModal.ShowAsync();
            }
        }

        protected async Task MarkCustomerAcceptedOverride(CMHub_CMNotifRecordPartModel? partRecord)
        {
            if (partRecord != null && MarkAcceptedOverrideConfirmationModal != null)
            {
                _partRecordToMark = partRecord;
                _evidenceFiles = new List<IBrowserFile>();
                _evidenceLogMessage = string.Empty;
                await MarkAcceptedOverrideConfirmationModal.ShowAsync();
            }
        }

        public async Task ConfirmMarkAccepted(bool confirm)
        {
            await ConfirmMarkAccepted(confirm, false);
        }

        public async Task ConfirmMarkAcceptedOverride(bool confirm)
        {
            await ConfirmMarkAccepted(confirm, true);
        }

        public async Task ConfirmMarkAccepted(bool confirm, bool confirmViaOverride = false)
        {
            if (confirm && _partRecordToMark != null && CurrentUser?.DBUser != null)
            {
                // Log the customer acceptance
                var log = new CMHub_CMNotifRecordLogModel
                {
                    RecordId = _partRecordToMark.RecordId,
                    LogAssociatedWithPartNum = _partRecordToMark.PartNum,
                    LogDate = DateTime.UtcNow,
                    LoggedByEmpId = CurrentUser.DBUser.EmployeeNumber,
                    LogMessage = confirmViaOverride ? $"Customer acceptance was overridden by {CurrentUser.DBUser.DisplayName}." : $"Changes accepted by the customer. {CurrentUser.DBUser.DisplayName} marked the acceptance."
                };
                await CMNotifService.CreateRecordLogAsync(log);

                // Handle evidence file uploads
                if (_evidenceFiles.Any())
                {
                    string environment = Environment.EnvironmentName;
                    var folderPath = Path.Combine($"{CMNSPDFExportLocation}{environment}\\", $"{ECNNumber}\\Part_{_partRecordToMark.PartNum}_ID_{_partRecordToMark.Id}\\Evidence");
                    Directory.CreateDirectory(folderPath); // ensure folder exists

                    foreach (var file in _evidenceFiles)
                    {
                        try
                        {
                            var filePath = Path.Combine(folderPath, file.Name);
                            await using FileStream fs = new(filePath, FileMode.Create);
                            await file.OpenReadStream(file.Size).CopyToAsync(fs);

                            var fileLog = new CMHub_CMNotifRecordLogModel
                            {
                                RecordId = _partRecordToMark.RecordId,
                                LogAssociatedWithPartNum = _partRecordToMark.PartNum,
                                LogDate = DateTime.UtcNow,
                                LoggedByEmpId = CurrentUser.DBUser.EmployeeNumber,
                                LogMessage = $"Evidence of acceptance uploaded: {file.Name}" + (string.IsNullOrWhiteSpace(_evidenceLogMessage) ? "" : $" - {_evidenceLogMessage}"), // Don't change the text before the : or it will break logic... yes, I know this is not the best way to do it.
                                LogFileLocation = filePath
                            };
                            await CMNotifService.CreateRecordLogAsync(fileLog);
                        }
                        catch (Exception ex)
                        {
                            // Handle file save error, maybe log it
                        }
                    }
                }

                // Update existing part
                if (confirmViaOverride)
                {
                    _partRecordToMark.HasCustAcceptanceOverride = true;
                }
                else
                {
                    _partRecordToMark.HasCustAcceptance = true;
                }
                _partRecordToMark.DateCustAccepted = DateTime.UtcNow;
                await CMNotifService.UpdatePartByPartIdAsync(_partRecordToMark);

                await LoadData();
            }

            _partRecordToMark = null;
            _evidenceFiles = new List<IBrowserFile>();
            _evidenceLogMessage = string.Empty;
        }

        protected async Task ShowUploadAdditionalEvidenceModalAsync(CMHub_CMNotifRecordPartModel? partRecord)
        {
            if (partRecord != null && UploadAdditionalEvidenceModal != null)
            {
                _partRecordToMark = partRecord;
                _evidenceFiles = new List<IBrowserFile>();
                _evidenceLogMessage = string.Empty;
                await UploadAdditionalEvidenceModal.ShowAsync();
            }
        }

        public async Task ConfirmUploadAdditionalEvidence(bool confirm)
        {
            if (confirm && _partRecordToMark != null && CurrentUser?.DBUser != null)
            {
                if (_evidenceFiles.Any())
                {
                    string environment = Environment.EnvironmentName;
                    var folderPath = Path.Combine($"{CMNSPDFExportLocation}{environment}\\", $"{ECNNumber}\\Part_{_partRecordToMark.PartNum}_ID_{_partRecordToMark.Id}\\Evidence");
                    Directory.CreateDirectory(folderPath); // ensure folder exists

                    foreach (var file in _evidenceFiles)
                    {
                        try
                        {
                            var filePath = Path.Combine(folderPath, file.Name);
                            await using FileStream fs = new(filePath, FileMode.Create);
                            await file.OpenReadStream(file.Size).CopyToAsync(fs);

                            var fileLog = new CMHub_CMNotifRecordLogModel
                            {
                                RecordId = _partRecordToMark.RecordId,
                                LogAssociatedWithPartNum = _partRecordToMark.PartNum,
                                LogDate = DateTime.UtcNow,
                                LoggedByEmpId = CurrentUser.DBUser.EmployeeNumber,
                                LogMessage = $"Additional evidence uploaded: {file.Name}" + (string.IsNullOrWhiteSpace(_evidenceLogMessage) ? "" : $" - {_evidenceLogMessage}"), // Don't change the text before the : or it will break logic... yes, I know this is not the best way to do it.
                                LogFileLocation = filePath
                            };
                            await CMNotifService.CreateRecordLogAsync(fileLog);
                        }
                        catch (Exception ex)
                        {
                            // Handle file save error, maybe log it
                        }
                    }
                    await LoadData();
                }
            }
            _partRecordToMark = null;
            _evidenceFiles = new List<IBrowserFile>();
            _evidenceLogMessage = string.Empty;
        }

        protected void OnEvidenceFileChange(InputFileChangeEventArgs e)
        {
            _evidenceFiles = e.GetMultipleFiles();
        }

        protected async Task OnModalClosedAsync()
        {
            await LoadData();
        }

        protected async Task HandleFormSend(CMHub_CMNotifPDFFormModal.PDFFormDataModel form)
        {
            if (SendNotifDocConfirmationModal != null)
            {
                // Save the current form to the field so we can use it after confirm
                _pendingSendFormData = form;

                SendNotifDocConfirmationMessage =
                    @$"Are you sure you want to send a Change Notification to the Customer Owner for Part {form.PartNum}?<br/><br/>{form.Company}<br/>{form.ContactName}<br/>{form.ContactEmail}";

                var partRecord = ECNRecord?.Record?.RecordedParts.FirstOrDefault(p => p.PartNum == form.PartNum && !p.Deleted);
                if (partRecord != null && partRecord.IsNotifSent)
                {
                    string warningMessage = "<br/><br/><strong class='text-danger'>WARNING: A notification for this part has already been sent.";
                    if (partRecord.HasCustAcceptance || partRecord.HasCustAcceptanceOverride)
                    {
                        warningMessage += " The customer has already provided approval. Sending a new notification will reset the acceptance status.</strong>";
                    }
                    else
                    {
                        warningMessage += "</strong>";
                    }
                    SendNotifDocConfirmationMessage += warningMessage;
                }

                if (form.CMDexPart != null)
                {
                    if (form.CMDexPart.CustConsAssociatedWithPrimaryPM.Count > 1)
                    {
                        // Insert a paragraph break using HTML <br/> tags before the additional customers section
                        SendNotifDocConfirmationMessage += "<br/><br/>Additional Customers listed below will be CC'd on the notification:";
                        
                        // Append each line for non-owner customer contacts with HTML line breaks
                        foreach (var custCon in form.CMDexPart.CustConsAssociatedWithPrimaryPM)
                        {
                            var additionalContact = form.CMDexPart.GetCustomerContactFor(custCon);
                            if (!(custCon.IsOwner ?? false))
                            {
                                string addLine = $"<br/><br/>{additionalContact?.DisplayContact.CustName}<br/>{additionalContact?.DisplayContact.ConName}<br/>{additionalContact?.DisplayContact.EMailAddress}";
                                SendNotifDocConfirmationMessage += addLine;
                            }
                        }
                    }
                }

                await SendNotifDocConfirmationModal.ShowAsync();
            }
        }

        // Get a list of all the non-owner customer contact email addresses and the primary PM's email address for the purposes of using in the CC line for the sent notifications
        protected List<string> GetCCEmailAddresses()
        {
            var ccEmails = new List<string>();
            foreach (var custCon in _pendingSendFormData?.CMDexPart?.PartCustomerContacts ?? [])
            {
                var custConModel = _pendingSendFormData?.CMDexPart?.GetCustomerContactFor(custCon);
                if (custConModel != null
                    && custConModel.DisplayContact.EMailAddress != null
                    && !string.IsNullOrWhiteSpace(custConModel.DisplayContact.EMailAddress)
                    && !(custCon.IsOwner ?? false))
                {
                    ccEmails.Add(custConModel.DisplayContact.EMailAddress);
                }
            }
            ccEmails.Add(_pendingSendFormData?.CMDexPart?.PrimaryPMEmployee?.Mail ?? "");
            return ccEmails;
        }

        protected async Task ConfirmSendNotifDoc(bool confirm)
        {
            if (!confirm || _pendingSendFormData == null || ECNRecord?.Record == null || CurrentUser == null)
                return;

            StateHasChanged();

            try
            {
                if (PdfFormModal != null)
                {
                    PdfFormModal.IsSaving = true;
                }

                if (_pendingSendFormData.RecordId == 0)
                {
                    // Find or create the record
                    var record = await CMNotifService.CreateRecordByECNNumberAsync(_pendingSendFormData.ECNNum);
                    _pendingSendFormData.RecordId = record.Id;

                    // Part does not exist yet, create it
                    var partRecords = await CMNotifService.GetPartsByRecordIdAsync(_pendingSendFormData.RecordId);
                    var partRecordFound = partRecords.Any(_ => _.PartNum == _pendingSendFormData.PartNum);
                    if (!partRecordFound)
                    {
                        var newPart = CMHub_CMNotifPDFFormModal.ConvertPdfFormToNotifPart(_pendingSendFormData);
                        newPart.DateCreated = DateTime.UtcNow;
                        newPart.IsNotifSent = true;
                        newPart.DateNotifSent = DateTime.UtcNow;
                        newPart.IsApproved = true;
                        newPart.ApprovedByEmpId = CurrentUser.DBUser.EmployeeNumber;
                        var newPartRecordId = await CMNotifService.CreatePartAssociatedWithRecordAsync(newPart);
                    }
                }
                else
                {
                    // Update the part record
                    var updatedPart = CMHub_CMNotifPDFFormModal.ConvertPdfFormToNotifPart(_pendingSendFormData);
                    updatedPart.IsNotifSent = true;
                    updatedPart.DateNotifSent = DateTime.UtcNow;
                    updatedPart.IsApproved = true;
                    updatedPart.ApprovedByEmpId = CurrentUser.DBUser.EmployeeNumber;

                    // Reset some flags/dates when a notification is resent
                    updatedPart.HasCustAcceptance = false;
                    updatedPart.HasCustAcceptanceOverride = false;
                    updatedPart.DateCustAccepted = null;
                    updatedPart.IsConfirmSent = false;
                    updatedPart.DateConfirmSent = null;

                    await CMNotifService.UpdatePartByPartIdAsync(updatedPart);
                }

                // Create the log of the notification first (because we need a log ID before we can continue)
                var log = new CMHub_CMNotifRecordLogModel
                {
                    RecordId = ECNRecord.Record.Id,
                    LogAssociatedWithPartNum = _pendingSendFormData.PartNum,
                    LogDate = DateTime.UtcNow,
                    LoggedByEmpId = CurrentUser.DBUser.EmployeeNumber,
                    LogMessage = $"Notification Document sent to {_pendingSendFormData.ContactName} ({_pendingSendFormData.ContactEmail}) with {_pendingSendFormData.Company} for Part {_pendingSendFormData.PartNum}."
                };
                int logId = await CMNotifService.CreateRecordLogAsync(log);

                var parts = await PartService.GetPartsByPartNumbersAsync<PartDTO_Base>(new List<string> { _pendingSendFormData.PartNum });
                var part = parts.FirstOrDefault();

                // Create the notification document
                string environment = Environment.EnvironmentName;
                string outputPath = SaveFilledNotificationPdf(_pendingSendFormData, logId, ECNNumber, CMNSTemplatePDFLocation, $"{CMNSPDFExportLocation}{environment}\\", part);

                // Read the PDF into memory so that it can be attached to the email sent to the customer
                MemoryStream? attachmentStream = null;
                if (File.Exists(outputPath))
                {
                    attachmentStream = new MemoryStream(await File.ReadAllBytesAsync(outputPath));
                }

                // Add the document file location back to the log entry
                bool success = await CMNotifService.UpdateRecordLogNotifLocationAsync(outputPath, logId);

                if (success)
                {
                    // Open in new tab
                    string fileServeUrl = $"{NavHelpers.FileServe}{Uri.EscapeDataString(outputPath)}&ct=application/pdf";
                    await JS.InvokeVoidAsync("open", fileServeUrl, "_blank");
                }

                if (attachmentStream != null)
                {
                    var customEmailHtml = string.IsNullOrWhiteSpace(_pendingSendFormData.EmailBodyCustomHtml)
                        ? ""
                        : _pendingSendFormData.EmailBodyCustomHtml
                            .Trim()
                            .Replace("\r\n", "<br/>")
                            .Replace("\n", "<br/>") + "<br/><br/>";

                    // Get the Primary PM email for preview mode (when feature is disabled but not global shutoff)
                    var primaryPmEmail = _pendingSendFormData.CMDexPart?.PrimaryPMEmployee?.Mail;
                    var previewRecipients = !string.IsNullOrWhiteSpace(primaryPmEmail) 
                        ? new List<string> { primaryPmEmail } 
                        : null;

                    // Send email with feature flag - in non-production, emails always go to test inbox
                    // In production with feature disabled, emails go to Primary PM for preview
                    // In production with GlobalEmailShutoff, emails go to test inbox
                    EmailHelpers.SendEmail(
                        subject: $"Action Required Change Notice {_pendingSendFormData.PartNum} - {ECNNumber}",
                        messageHtml: @$"
<span style=""color: red;"">
Please review the attached Configuration Management Change Notice (CMCN).<br />
To ensure prompt processing of configuration changes and to avoid delays in pending shipments, please approve upon receipt, or respond within 14 days with any questions or concerns.<br />
If no response is received in 14 days, approval will be assumed.<br />
</span><br />
{customEmailHtml}
{_pendingSendFormData.CMDexPart?.PrimaryPMName ?? ""}<br />
Crystal Group Inc.<br />
+1 800-378-1636<br /><br />
<a href=""https://www.crystalrugged.com/"" target=""_blank"">www.crystalrugged.com</a>
",
                        toRecipients: new List<string> { _pendingSendFormData.CMDexPart?.OwnerConEmail ?? "AppErrors@crystalrugged.com" },
                        environmentName: Environment.EnvironmentName,
                        fromAddress: _pendingSendFormData.CMDexPart?.PrimaryPMEmployee?.Mail.ToLower(),
                        ccRecipients: GetCCEmailAddresses(),
                        msAttachment: attachmentStream,
                        attachmentFileName: $"CMCN {_pendingSendFormData.PartNum} {ECNNumber} {DateTime.Now.ToLocalTime().ToLongDateString()}.pdf",
                        bccRecipients: null,
                        featureEmailEnabled: IsNotificationSendingEnabled(),
                        previewRecipients: previewRecipients
                    );
                }

                if (PdfFormModal != null)
                {
                    PdfFormModal.IsSaving = false;
                    await PdfFormModal.Close();
                }
            }
            finally
            {
                _pendingSendFormData = null;
                await LoadData(); // reload UI
                StateHasChanged();
            }
        }

        protected async Task SendImplemented(CMHub_CMNotifRecordPartModel? partRecord)
        {
            if (partRecord != null && SendConfirmImplementedConfirmationModal != null)
            {
                _partRecordToMark = partRecord;

                SendNotifDocConfirmationMessage =
                    @$"Are you sure you want to send a Confirmation of Implementation to the Customer Owner for Part {partRecord.PartNum}?<br/><br/>{partRecord.CMDexPart?.OwnerCustName}<br/>{partRecord.CMDexPart?.OwnerConName}<br/>{partRecord.CMDexPart?.OwnerConEmail}";

                if (partRecord.CMDexPart != null)
                {
                    if (partRecord.CMDexPart.CustConsAssociatedWithPrimaryPM.Count > 1)
                    {
                        // Insert a paragraph break using HTML <br/> tags before the additional customers section
                        SendNotifDocConfirmationMessage += "<br/><br/>Additional Customers listed below will be CC'd on the notification:";

                        // Append each line for non-owner customer contacts with HTML line breaks
                        foreach (var custCon in partRecord.CMDexPart.CustConsAssociatedWithPrimaryPM)
                        {
                            var additionalContact = partRecord.CMDexPart.GetCustomerContactFor(custCon);
                            if (!(custCon.IsOwner ?? false))
                            {
                                string addLine = $"<br/><br/>{additionalContact?.DisplayContact.CustName}<br/>{additionalContact?.DisplayContact.ConName}<br/>{additionalContact?.DisplayContact.EMailAddress}";
                                SendNotifDocConfirmationMessage += addLine;
                            }
                        }
                    }
                }

                await SendConfirmImplementedConfirmationModal.ShowAsync();
            }
        }

        protected async Task ConfirmSendImplementedDoc(bool confirm)
        {
            if (!confirm || _partRecordToMark == null || ECNRecord?.Record == null || CurrentUser == null)
                return;

            StateHasChanged();

            try
            {
                if (PdfFormModal != null)
                {
                    PdfFormModal.IsSaving = true;
                }

                // Update the part record
                var partToUpdate = _partRecordToMark;
                partToUpdate.IsConfirmSent = true;
                partToUpdate.DateConfirmSent = DateTime.UtcNow;

                await CMNotifService.UpdatePartByPartIdAsync(partToUpdate);

                // Create the log of the notification first (because we need a log ID before we can continue)
                var log = new CMHub_CMNotifRecordLogModel
                {
                    RecordId = ECNRecord.Record.Id,
                    LogAssociatedWithPartNum = partToUpdate.PartNum,
                    LogDate = DateTime.UtcNow,
                    LoggedByEmpId = CurrentUser.DBUser.EmployeeNumber,
                    LogMessage = $"Confirmation of Implementation Document sent to {partToUpdate.CMDexPart?.OwnerConName} ({partToUpdate.CMDexPart?.OwnerConEmail}) with {partToUpdate.CMDexPart?.OwnerCustName} for Part {partToUpdate.PartNum}."
                };
                int logId = await CMNotifService.CreateRecordLogAsync(log);

                var parts = await PartService.GetPartsByPartNumbersAsync<PartDTO_Base>(new List<string> { partToUpdate.PartNum });
                var part = parts.FirstOrDefault();

                // Create the notification document
                string environment = Environment.EnvironmentName;
                string outputPath = SaveFilledConfirmationPdf(partToUpdate, logId, ECNNumber, CMNSTemplatePDFLocation, $"{CMNSPDFExportLocation}{environment}\\", part);

                // Read the PDF into memory so that it can be attached to the email sent to the customer
                MemoryStream? attachmentStream = null;
                if (File.Exists(outputPath))
                {
                    attachmentStream = new MemoryStream(await File.ReadAllBytesAsync(outputPath));
                }

                // Add the document file location back to the log entry
                bool success = await CMNotifService.UpdateRecordLogNotifLocationAsync(outputPath, logId);

                if (success)
                {
                    // Open in new tab
                    string fileServeUrl = $"{NavHelpers.FileServe}{Uri.EscapeDataString(outputPath)}&ct=application/pdf";
                    await JS.InvokeVoidAsync("open", fileServeUrl, "_blank");
                }

                if (attachmentStream != null)
                {
                    // Get the Primary PM email for preview mode (when feature is disabled but not global shutoff)
                    var primaryPmEmail = partToUpdate.CMDexPart?.PrimaryPMEmployee?.Mail;
                    var previewRecipients = !string.IsNullOrWhiteSpace(primaryPmEmail) 
                        ? new List<string> { primaryPmEmail } 
                        : null;

                    // Send email with feature flag - in non-production, emails always go to test inbox
                    // In production, they go to real recipients only if EnableNotificationSending is true
                    EmailHelpers.SendEmail(
                        subject: $"Confirmation of Implementation Notice (CMIN) {partToUpdate.PartNum} - {ECNNumber}",
                        messageHtml: $"{ECNNumber} for {partToUpdate.PartNum} has been implemented to revision {partToUpdate.CMDexPart?.Part.RevisionNum}. Please find the attached notification for your files.",
                        toRecipients: new List<string> { partToUpdate.CMDexPart?.OwnerConEmail ?? "AppErrors@crystalrugged.com" },
                        environmentName: Environment.EnvironmentName,
                        fromAddress: partToUpdate.CMDexPart?.PrimaryPMEmployee?.Mail.ToLower(),
                        ccRecipients: GetCCEmailAddresses(),
                        msAttachment: attachmentStream,
                        attachmentFileName: $"COI {partToUpdate.PartNum} {ECNNumber} {DateTime.Now.ToLocalTime().ToLongDateString()}.pdf",
                        bccRecipients: null,
                        featureEmailEnabled: IsNotificationSendingEnabled(),
                        previewRecipients: previewRecipients
                    );
                }

                if (PdfFormModal != null)
                {
                    PdfFormModal.IsSaving = false;
                    await PdfFormModal.Close();
                }
            }
            finally
            {
                _pendingSendFormData = null;
                await LoadData(); // reload UI
                StateHasChanged();
            }
        }

        public async Task HandlePreviewNotifDoc(PDFFormDataModel form)
        {
            if (string.IsNullOrWhiteSpace(ECNNumber)) return;

            var parts = await PartService.GetPartsByPartNumbersAsync<PartDTO_Base>(new List<string> { form.PartNum });
            var part = parts.FirstOrDefault();

            string environment = Environment.EnvironmentName;
            string outputPath = SavePreviewNotificationPdf(form, ECNNumber, CMNSTemplatePDFLocation, $"{CMNSPDFExportLocation}{environment}\\", part);

            if (File.Exists(outputPath))
            {
                string fileServeUrl = $"{NavHelpers.FileServe}{Uri.EscapeDataString(outputPath)}&ct=application/pdf";
                await JS.InvokeVoidAsync("open", fileServeUrl, "_blank");
            }
        }

        public static string SavePreviewNotificationPdf(PDFFormDataModel form, string ecnNum, string sourceTemplatePath, string rootSavePath, PartDTO_Base? part = null)
        {
            return SavePdf(MapFormDataToPdfFields(form), ecnNum, sourceTemplatePath, rootSavePath, "Notification_Preview.pdf", $"PREVIEW - Part Change Notification for {form.PartNum}", part);
        }

        public static string SaveFilledNotificationPdf(PDFFormDataModel form, int logId, string? ecnNum, string sourceTemplatePath, string rootSavePath, PartDTO_Base? part = null)
        {
            return SavePdf(MapFormDataToPdfFields(form), ecnNum, sourceTemplatePath, rootSavePath, $"Notification_Log_{logId}.pdf", $"Part Change Notification for {form.PartNum}", part);
        }

        public static string SaveFilledConfirmationPdf(CMHub_CMNotifRecordPartModel partModel, int logId, string? ecnNum, string sourceTemplatePath, string rootSavePath, PartDTO_Base? part = null)
        {
            partModel.PDFFormType = PDFFormType.ConfirmationOfImplementation;
            return SavePdf(MapPartRecordDataToPdfFields(partModel, (ecnNum ?? ""), partModel.Id), ecnNum, sourceTemplatePath, rootSavePath, $"Confirmation_Log_{logId}.pdf", $"Confirmation of Implementation for {part?.PartNum}", part, isConfirmOfImp: true);
        }

        private static string SavePdf(Dictionary<string, string> pdfFields, string? ecnNum, string sourceTemplatePath, string rootSavePath, string fileName, string? title = null, PartDTO_Base? part = null, bool isConfirmOfImp = false)
        {
            var folderPath = Path.Combine(rootSavePath, $"{ecnNum}\\Part_{part?.PartNum}_ID_{pdfFields[NotifDocPdfFields.ID]}");
            Directory.CreateDirectory(folderPath); // ensure folder exists

            var outputPath = Path.Combine(folderPath, fileName);

            PDFHelpers.FillPdfForm00013(sourceTemplatePath, outputPath, pdfFields, title: title, removeSecondPage: string.IsNullOrWhiteSpace(pdfFields[NotifDocPdfFields.NOTES]), isConfirmOfImp: isConfirmOfImp, part: part);
            return outputPath;
        }

        protected IEnumerable<(string PartNum, object? CMDexPart, CMHub_CMNotifRecordPartModel? RecordPart)> GetUnifiedParts()
        {
            // Recorded Parts
            if (ECNRecord?.Record?.RecordedParts != null)
            {
                foreach (var part in ECNRecord.Record.RecordedParts.Where(p => !p.Deleted))
                {
                    var found = false;
                    if (ECNRecord?.TempCMDexParts != null)
                    {
                        var cmDexPartInECN = ECNRecord.TempCMDexParts.FirstOrDefault(_ => _.Part.PartNum == part.PartNum);
                        found = cmDexPartInECN != null;
                        part.MatchedToECNPart = found;
                    }
                    yield return (part.PartNum, part.CMDexPart, part);
                }
            }

            // ECN Parts
            if (ECNRecord?.TempCMDexParts != null)
            {
                foreach (var temp in ECNRecord.TempCMDexParts)
                {
                    var recordedParts = ECNRecord?.Record?.RecordedParts.FirstOrDefault(_ => _.PartNum == temp.Part.PartNum);
                    var foundInRecordedParts = recordedParts != null;

                    if (!foundInRecordedParts)
                    {
                        yield return (temp.Part.PartNum, temp, null);
                    }
                }
            }
        }

        public string LinkToMostRecentNotificationDocumentForPart(string partNum)
        {
            if (ECNRecord?.Record?.RecordedLogs == null)
            {
                return string.Empty;
            }

            var logs = ECNRecord.Record.RecordedLogs
                        .Where(p => p.LogAssociatedWithPartNum == partNum)
                        .ToList();

            if (logs.Count == 0)
            {
                return string.Empty;
            }

            var mostRecentLog = logs
                        .OrderByDescending(log => log.LogDate)
                        .FirstOrDefault(log =>
                            !string.IsNullOrWhiteSpace(log.LogFileLocation) 
                            && log.LogFileLocation.EndsWith(".pdf")
                            && log.LogMessage != null
                            && log.LogMessage.StartsWith("Notification Document sent to"));

            return (mostRecentLog == null || string.IsNullOrWhiteSpace(mostRecentLog.LogFileLocation))
                        ? string.Empty
                        : mostRecentLog.LogFileLocation;
        }

        protected T? GetPropValue<T>(object? obj, string propertyName)
        {
            if (obj == null) return default;
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop == null) return default;

            var value = prop.GetValue(obj);
            return value is T typed ? typed : default;
        }

        protected string GetHighlightClass(CMHub_CMNotifStatus status) => status switch
        {
            CMHub_CMNotifStatus.PartNoLongerAssociated => "gray-highlight",
            CMHub_CMNotifStatus.NoNotificationRecord => "white-highlight",
            CMHub_CMNotifStatus.NotSent => "white-highlight",
            CMHub_CMNotifStatus.SentNotAccepted => "crystal-blue-highlight",
            CMHub_CMNotifStatus.SentOverdue => "crystal-blue-highlight",
            CMHub_CMNotifStatus.Accepted => "warning-highlight",
            CMHub_CMNotifStatus.AcceptedViaOverride => "warning-highlight",
            CMHub_CMNotifStatus.ConfirmationSent => "success-highlight",
            CMHub_CMNotifStatus.Unknown => string.Empty,
            _ => string.Empty
        };

        /// <summary>
        /// Checks if CM Notification sending is enabled in the current environment
        /// </summary>
        protected bool IsNotificationSendingEnabled()
        {
            return CMNotificationsOptions?.Value?.EnableNotificationSending ?? true;
        }

        /// <summary>
        /// Gets the disabled message for CM Notifications when sending is disabled
        /// </summary>
        protected string? GetNotificationDisabledMessage()
        {
            return CMNotificationsOptions?.Value?.DisabledMessage;
        }
    }
}