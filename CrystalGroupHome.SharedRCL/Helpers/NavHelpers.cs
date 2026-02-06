using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public static class NavHelpers
    {
        // General
        public const string HomeIcon = "fa fa-home";
        public const string BackCircleIcon = "fa fa-circle-left";
        public const string AdminSettingsIcon = "fa fa-screwdriver-wrench";
        public const string DataGridListIcon = "fa fa-list";
        public const string AddIcon = "fa fa-plus";
        public const string AddCircleIcon = "fa fa-circle-plus";
        public const string NotificationIcon = "fa fa-bell";
        public const string WarningIcon = "fa fa-triangle-exclamation";
        public const string MailIcon = "fa fa-envelope";
        public const string SearchIcon = "fa fa-magnifying-glass";
        public const string CheckIcon = "fa fa-check";
        public const string ThumbsUpIcon = "fa fa-thumbs-up";
        public const string XIcon = "fa fa-x";
        public const string TrashIcon = "fa fa-trash-can";
        public const string SaveIcon = "fa fa-save";
        public const string UndoIcon = "fa fa-rotate-left";
        public const string ClockIcon = "fa fa-clock";
        public const string FilterIcon = "fa fa-filter";
        public const string EditIcon = "fa fa-edit";
        public const string ViewIcon = "fa fa-eye";
        public const string InfoIcon = "fa fa-circle-info";
        public const string CloseIcon = "fa fa-times";
        public const string CloseSquareIcon = "fa fa-square-xmark";
        public const string OpenInNewTabIcon = "fa fa-arrow-up-right-from-square";
        public const string CopyIcon = "fa fa-copy";
        public const string CogIcon = "fa fa-cog";
        public const string ConfigurationManagedIcon = "fa fa-clipboard-list";
        public const string DeprecatedPartIcon = "fa fa-circle-exclamation";
        public const string ExpandedIcon = "fa fa-caret-down";
        public const string CollapsedIcon = "fa fa-caret-right";

        // First Time Yield
        public const string FirstTimeYieldRoot = "/first-time-yield";
        public const string FirstTimeYieldMainPage = $"{FirstTimeYieldRoot}";
        public const string FirstTimeYieldAddEntry = $"{FirstTimeYieldMainPage}/add";
        public const string FirstTimeYieldEditEntry = $"{FirstTimeYieldMainPage}/edit";
        public const string FirstTimeYieldEditEntryWithEditId = $"{FirstTimeYieldEditEntry}/{{EditId:int}}";
        public const string FirstTimeYieldAdmin = $"{FirstTimeYieldMainPage}/admin";

        public const string FirstTimeYieldIcon = CheckIcon;
        public const string FirstTimeYieldEntryHistoryIcon = DataGridListIcon;
        public const string FirstTimeYieldAddEntryIcon = AddCircleIcon;
        public const string FirstTimeYieldAdminToolsIcon = AdminSettingsIcon;

        // CM Hub
        public const string CMHubRoot = "/cmhub";
        public const string CMHubMainPage = $"{CMHubRoot}";

        public const string CMHubIcon = "fa fa-circle-nodes";
        public const string CMHub_CMDexIcon = "fa fa-address-book";
        public const string CMHub_VendorCommsIcon = MailIcon;
        public const string CMHub_CustCommsIcon = NotificationIcon;
        public const string CHHub_CMNotifIcon = WarningIcon;

        // CM Dex
        public const string CMHub_CMDexRoot = "/cmdex";
        public const string CMHub_CMDexMainPage = $"{CMHubMainPage}{CMHub_CMDexRoot}";
        public const string CMHub_CMDexPartDetails = $"{CMHub_CMDexMainPage}/part";
        public const string CMHub_CMDexPartDetailsWithPartNum = $"{CMHub_CMDexPartDetails}/{{PartNum}}";

        public const string CMHub_CMDexPartRelationsIcon = DataGridListIcon;

        // Cust Comms
        public const string CMHub_CustCommsRoot = "/custcomms";
        public const string CMHub_CustCommsMainPage = $"{CMHubMainPage}{CMHub_CustCommsRoot}";
        public const string CMHub_CustCommsTrackerDetails = $"{CMHub_CustCommsMainPage}/tracker";
        public const string CMHub_CustCommsTrackerDetailsWithPartNum = $"{CMHub_CustCommsTrackerDetails}/{{PartNum}}";
        public const string CMHub_CustCommsAddTracker = $"{CMHub_CustCommsMainPage}/add-tracker";
        public const string CMHub_CustCommsAddTrackerWithPartNum = $"{CMHub_CustCommsAddTracker}/{{PartNum}}";
        public const string CMHub_CustCommsTaskList = $"{CMHub_CustCommsMainPage}/tasks";
        public const string CMHub_CustCommsTaskDetails = $"{CMHub_CustCommsMainPage}/task";
        public const string CMHub_CustCommsTaskDetailsWithTaskId = $"{CMHub_CustCommsTaskDetails}/{{TaskId:int}}";

        public const string CMHub_CustCommsPartChangeTrackersIcon = DataGridListIcon;
        public const string CMHub_CustCommsAddPartChangeTrackerIcon = AddCircleIcon;
        public const string CMHub_CustCommsPartChangeTasksIcon = "fa fa-list-check";

        // CM Notifications
        public const string CMHub_CMNotifRoot = "/cmnotif";
        public const string CMHub_CMNotifMainPage = $"{CMHubMainPage}{CMHub_CMNotifRoot}";
        public const string CMHub_CMNotifRecordDetails = $"{CMHub_CMNotifMainPage}/record";
        public const string CMHub_CMNotifRecordDetailsWithECNNum = $"{CMHub_CMNotifRecordDetails}/{{ECNNumber}}";

        public const string CMHub_CMNotifRecordsIcon = DataGridListIcon;

        // Vendor Comms
        public const string CMHub_VendorCommsRoot = "/vendorcomms";
        public const string CMHub_VendorCommsMainPage = $"{CMHubMainPage}{CMHub_VendorCommsRoot}";
        public const string CMHub_VendorCommsTrackerDetails = $"{CMHub_VendorCommsMainPage}/tracker";
        public const string CMHub_VendorCommsTrackerDetailsWithPartNum = $"{CMHub_VendorCommsTrackerDetails}/{{PartNum}}";

        public const string CMHub_VendorCommsCommunicationTrackersIcon = DataGridListIcon;

        // Epicompare
        public const string EpicompareMainPage = "/epicompare";
        public const string EpicompareIcon = "fa fa-code-compare";

        // RMA Processing
        public const string RMAProcessingRoot = "/rma-processing";
        public const string RMAProcessingFiles = $"{RMAProcessingRoot}/files";
        public const string RMAProcessingIcon = "fa-solid fa-microchip";

        // File Serve
        public const string FileServe = "https://intranet.crystalrugged.com/Home/FileServe.ashx?path=";

        /// <summary>
        /// Updates the URL with the given query parameters, excluding null or empty values.
        /// </summary>
        /// <param name="navigationManager">The NavigationManager instance.</param>
        /// <param name="queryParameters">A dictionary of query parameters.</param>
        /// <param name="forceLoad">Whether to force a page reload.</param>
        public static void UpdateUrlWithQueryParameters(
            NavigationManager navigationManager,
            Dictionary<string, object?> queryParameters,
            bool forceLoad = false)
        {
            var newUri = navigationManager.GetUriWithQueryParameters(queryParameters);

            // Navigate to the new URI if it's different from the current one
            if (navigationManager.Uri != newUri)
            {
                try
                {
                    navigationManager.NavigateTo(newUri, forceLoad);
                }
                catch (Exception ex)
                {
                    // Log or handle the exception as needed
                    Console.WriteLine($"Failed to navigate to the new URI: {ex.Message}");
                }
            }
        }
    }
}
