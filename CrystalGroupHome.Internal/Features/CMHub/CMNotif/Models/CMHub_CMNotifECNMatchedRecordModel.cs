using CrystalGroupHome.Internal.Features.CMHub.CMDex.Models;
using CrystalGroupHome.Internal.Features.CMHub.CMNotif.Data;
using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.CMHub.CMNotif.Models
{
    public enum CMHub_CMNotifStatus
    {
        Unknown = 0,
        PartNoLongerAssociated = 1,
        NotSent = 2,
        SentNotAccepted = 3,
        SentOverdue = 4,
        Accepted = 5,
        AcceptedViaOverride = 6,
        ConfirmationSent = 7,
        NoNotificationRecord = 8
    }

    public class CMHub_CMNotifECNGroupedRecordModel
    {
        public string ECNNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public List<CMHub_CMNotifECNMatchedRecordModel> AllRecords { get; set; } = new();

        public bool IsDetailExpanded { get; set; } = false;
    }

    public class CMHub_CMNotifECNMatchedRecordModel
    {
        public string ECNNumber { get; set; } = string.Empty;
        public int ECNId { get; set; } = 0;
        public string ReasonForChange { get; set; } = string.Empty;
        public string PrimaryPMName { get; set; } = string.Empty;
        //public string CustomerOwnerName { get; set; } = string.Empty; // TODO: Add this like was done with PrimaryPMName. Will need to modify the original SQL query to include necessary data.
        public List<CMHub_CMNotifECNPartDTO> ECNParts { get; set; } = new();
        public CMHub_CMNotifRecordModel? Record { get; set; }
        public CMHub_CMNotifRecordModel? TempRecord { get; set; }
        public List<CMHub_CMDexPartModel> TempCMDexParts { get; set; } = new();

        public int NotificationsSentPartsCount
        {
            get
            {
                return Record?.RecordedParts.Count(p => p.IsNotifSent && p.MatchedToECNPart) ?? 0;
            }
        }

        public float PartsNotificationsSentCompletion
        {
            get
            {
                return TotalPartsCount == 0 ? 0 : (float)NotificationsSentPartsCount / TotalPartsCount;
            }
        }

        public int AcceptedPartsCount
        {
            get
            {
                return Record?.RecordedParts.Count(p => (p.HasCustAcceptance || p.HasCustAcceptanceOverride) && p.MatchedToECNPart) ?? 0;
            }
        }

        public float PartsCustAcceptedCompletion
        {
            get
            {
                return TotalPartsCount == 0 ? 0 : (float)AcceptedPartsCount / TotalPartsCount;
            }
        }

        public int ConfirmationsSentPartsCount
        {
            get
            {
                return Record?.RecordedParts.Count(p => p.IsConfirmSent && p.MatchedToECNPart) ?? 0;
            }
        }

        public float PartsConfirmationsSentCompletion
        {
            get
            {
                return TotalPartsCount == 0 ? 0 : (float)ConfirmationsSentPartsCount / TotalPartsCount;
            }
        }

        public int TotalPartsCount => TempCMDexParts.Count > 0 ? TempCMDexParts.Count : (Record?.RecordedParts.Count ?? 0);

        public float TotalCompletion
        {
            get
            {
                return TotalPartsCount == 0 ? 0 : (float)(NotificationsSentPartsCount + AcceptedPartsCount + ConfirmationsSentPartsCount) / (TotalPartsCount * 3);
            }
        }

        public string QuickStatus
        {
            get
            {
                if (TotalPartsCount == 0)
                {
                    return "New";
                }
                else if (Record?.Completed ?? false)
                {
                    return "Completed";
                }
                else
                {
                    return $"In Progress";
                }
            }
        }

        public string TrueStatus
        {
            get
            {
                if (Record?.Completed ?? false)
                {
                    return "Completed";
                }
                else if (NotificationsSentPartsCount > 0)
                {
                    return "In Progress";
                }
                else
                {
                    return "New";
                }
            }
        }

        public MarkupString ReasonForChangeAsHtml => !string.IsNullOrEmpty(ReasonForChange)
            ? new MarkupString(ReasonForChange.Replace("\n", "<br />"))
            : new MarkupString(string.Empty);
    }
}