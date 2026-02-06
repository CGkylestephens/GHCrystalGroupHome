using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

public class EmailHelpers
{
    private readonly EmailOptions _options;

    public EmailHelpers(IOptions<EmailOptions> settings)
    {
        _options = settings.Value;
    }

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new MailAddress(email);
            // This ensures the address is in a valid format and not just a valid domain.
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends an email with the specified parameters.
    /// </summary>
    /// <param name="subject">Email subject</param>
    /// <param name="messageHtml">HTML body of the email</param>
    /// <param name="toRecipients">List of recipient email addresses</param>
    /// <param name="isProduction">Whether this is a production environment</param>
    /// <param name="fromAddress">Optional sender address (defaults to configured From address)</param>
    /// <param name="ccRecipients">Optional CC recipients</param>
    /// <param name="msAttachment">Optional attachment stream</param>
    /// <param name="attachmentFileName">Filename for the attachment</param>
    /// <param name="bccRecipients">Optional BCC recipients</param>
    /// <param name="featureEmailEnabled">
    /// Optional per-feature flag. When provided and false in production, emails are redirected based on previewRecipients.
    /// In non-production environments, this flag is ignored (emails always go to test inbox).
    /// When null, uses the legacy RestrictEmailsInProd behavior.
    /// </param>
    /// <param name="previewRecipients">
    /// Optional list of email addresses that should receive the email even when featureEmailEnabled is false.
    /// This allows designated reviewers (e.g., Primary PMs) to preview emails before enabling the feature.
    /// These recipients are NOT used when GlobalEmailShutoff is true - in that case, all emails go to test inbox.
    /// </param>
    public void SendEmail(
        string subject, 
        string messageHtml, 
        List<string> toRecipients,
        string environmentName,
        string? fromAddress = null, 
        List<string>? ccRecipients = null, 
        MemoryStream? msAttachment = null, 
        string? attachmentFileName = null, 
        List<string>? bccRecipients = null,
        bool? featureEmailEnabled = null,
        List<string>? previewRecipients = null)
    {
        string messageSuffix = string.Empty;

        if (toRecipients == null || toRecipients.Count == 0)
        {
            toRecipients = new List<string> { _options.TestEmailRecipientAddress };
            messageSuffix += "<br/><br/><b>Email recipient list was blank! Sent to default test inbox.</b>";
        }

        string toDelimited = string.Join(", ", toRecipients);
        string ccDelimited = "";
        if (ccRecipients != null && ccRecipients.Count > 0)
        {
            ccDelimited = string.Join(", ", ccRecipients);
        }
        string bccDelimited = "";
        if (bccRecipients != null && bccRecipients.Count > 0)
        {
            bccDelimited = string.Join(", ", bccRecipients);
        }

        using var msg = new MailMessage
        {
            From = new MailAddress(fromAddress ?? _options.From),
            Subject = subject,
            IsBodyHtml = true
        };

        if (environmentName == Environments.Production)
        {
            // Determine the email routing based on settings
            var routingResult = DetermineEmailRouting(featureEmailEnabled, previewRecipients);

            switch (routingResult.Mode)
            {
                case EmailRoutingMode.RealRecipients:
                    // Send to real recipients
                    msg.To.Add(toDelimited);
                    if (ccRecipients != null && ccRecipients.Count > 0)
                    {
                        msg.CC.Add(ccDelimited);
                    }
                    if (bccRecipients != null && bccRecipients.Count > 0)
                    {
                        msg.Bcc.Add(bccDelimited);
                    }
                    break;

                case EmailRoutingMode.PreviewRecipients:
                    // Send to preview recipients only (e.g., Primary PM for review)
                    var validPreviewRecipients = previewRecipients!
                        .Where(e => !string.IsNullOrWhiteSpace(e) && IsValidEmail(e))
                        .Distinct()
                        .ToList();
                    
                    if (validPreviewRecipients.Count > 0)
                    {
                        msg.To.Add(string.Join(", ", validPreviewRecipients));
                        // Always send a copy to test inbox when in preview mode
                        msg.Bcc.Add(_options.TestEmailRecipientAddress);
                    }
                    else
                    {
                        // Fallback to test inbox if no valid preview recipients
                        msg.To.Add(_options.TestEmailRecipientAddress);
                    }
                    
                    messageSuffix += @$"
                        <br/><br/><b>FEATURE EMAIL SENDING IS DISABLED - PREVIEW MODE</b>
                        <br/><br/><i>This email was sent to designated preview recipients for review.</i>
                        <br/><br/><i>Originally intended for: {toDelimited}</i>
                        <br/><br/><i>CC intended for: {ccDelimited}</i>
                        <br/><br/><i>BCC intended for: {bccDelimited}</i>
                    ";
                    break;

                case EmailRoutingMode.TestInbox:
                default:
                    // Redirect to test inbox
                    msg.To.Add(_options.TestEmailRecipientAddress);
                    messageSuffix += @$"
                        <br/><br/><b>{routingResult.Reason}</b>
                        <br/><br/><i>Originally intended for: {toDelimited}</i>
                        <br/><br/><i>CC intended for: {ccDelimited}</i>
                        <br/><br/><i>BCC intended for: {bccDelimited}</i>
                    ";
                    break;
            }
        }
        else if (environmentName == Environments.Staging)
        {
            // Only send to preview recipients in staging
            var validPreviewRecipients = previewRecipients?
                .Where(e => !string.IsNullOrWhiteSpace(e) && IsValidEmail(e))
                .Distinct()
                .ToList() ?? new List<string>();

            if (validPreviewRecipients.Count > 0)
            {
                msg.To.Add(string.Join(", ", validPreviewRecipients));
                // Always send a copy to test inbox when in preview mode
                msg.Bcc.Add(_options.TestEmailRecipientAddress);
                messageSuffix += @$"
                    <br/><br/><b>PILOT ENVIRONMENT - SENDING TO PREVIEW RECIPIENTS ONLY</b>
                    <br/><br/><i>This email was sent to designated preview recipients for review.</i>
                    <br/><br/><i>Originally intended for: {toDelimited}</i>
                    <br/><br/><i>CC intended for: {ccDelimited}</i>
                    <br/><br/><i>BCC intended for: {bccDelimited}</i>
                ";
            }
            else
            {
                // Fallback to test inbox if no valid preview recipients
                msg.To.Add(_options.TestEmailRecipientAddress);
            }
        }
        else
        {
            // Test environment: ALWAYS send to test inbox regardless of any settings
            msg.To.Add(_options.TestEmailRecipientAddress);
            messageSuffix += @$"
                <br/><br/><i>Originally intended for: {toDelimited}</i>
                <br/><br/><i>CC intended for: {ccDelimited}</i>
                <br/><br/><i>BCC intended for: {bccDelimited}</i>
            ";
        }

        msg.Body = $"{messageHtml} {messageSuffix}";

        if (msAttachment != null && !string.IsNullOrWhiteSpace(attachmentFileName))
        {
            string mimeType = GetMimeType(attachmentFileName);
            msAttachment.Position = 0;
            msg.Attachments.Add(new Attachment(msAttachment, attachmentFileName, mimeType));
        }

        using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
        {
            smtpClient.Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword);
        }

        smtpClient.Send(msg);
    }

    private enum EmailRoutingMode
    {
        RealRecipients,
        PreviewRecipients,
        TestInbox
    }

    private record EmailRoutingResult(EmailRoutingMode Mode, string Reason);

    /// <summary>
    /// Determines how emails should be routed in production.
    /// </summary>
    private EmailRoutingResult DetermineEmailRouting(bool? featureEmailEnabled, List<string>? previewRecipients)
    {
        // Global shutoff takes precedence over everything - no exceptions
        if (_options.GlobalEmailShutoff)
        {
            return new EmailRoutingResult(EmailRoutingMode.TestInbox, "GLOBAL EMAIL SHUTOFF IS ACTIVE");
        }

        // If a feature-specific flag is provided
        if (featureEmailEnabled.HasValue)
        {
            if (featureEmailEnabled.Value)
            {
                // Feature is enabled - send to real recipients
                return new EmailRoutingResult(EmailRoutingMode.RealRecipients, string.Empty);
            }
            else
            {
                // Feature is disabled - check for preview recipients
                if (previewRecipients != null && previewRecipients.Count > 0)
                {
                    return new EmailRoutingResult(EmailRoutingMode.PreviewRecipients, "FEATURE EMAIL SENDING IS DISABLED - PREVIEW MODE");
                }
                else
                {
                    return new EmailRoutingResult(EmailRoutingMode.TestInbox, "FEATURE EMAIL SENDING IS DISABLED");
                }
            }
        }

        // Fall back to legacy RestrictEmailsInProd behavior
        if (_options.RestrictEmailsInProd)
        {
            return new EmailRoutingResult(EmailRoutingMode.TestInbox, "PRODUCTION EMAIL SENDING IS RESTRICTED");
        }

        return new EmailRoutingResult(EmailRoutingMode.TestInbox, "EMAIL ROUTING ERROR - FALLTHROUGH TO TEST INBOX");
    }

    private static string GetMimeType(string fileName)
    {
        return FileHelpers.GetContentType(fileName);
    }
}