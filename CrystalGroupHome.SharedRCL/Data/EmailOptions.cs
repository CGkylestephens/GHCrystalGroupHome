namespace CrystalGroupHome.SharedRCL.Data
{
    public class EmailOptions
    {
        public string From { get; set; } = string.Empty;
        public string TestEmailRecipientAddress { get; set; } = string.Empty;
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 25;
        public bool UseSsl { get; set; } = false;
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        
        /// <summary>
        /// Legacy setting - use GlobalEmailShutoff instead for emergency shutoff.
        /// When true in Production, emails are redirected to the sender or test inbox.
        /// </summary>
        public bool RestrictEmailsInProd { get; set; } = false;

        /// <summary>
        /// Emergency global shutoff - when true, ALL emails in production go to test inbox
        /// regardless of individual feature settings. Use this for emergency situations.
        /// </summary>
        public bool GlobalEmailShutoff { get; set; } = false;
    }

}
