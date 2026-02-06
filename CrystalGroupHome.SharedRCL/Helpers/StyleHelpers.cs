using Blazorise;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public class StyleHelpers
    {
        public static string GetSearchButtonIcon(bool currentItemHasBeenSearched, bool currentItemIsValid)
        {
            if (currentItemHasBeenSearched)
            {
                if (currentItemIsValid)
                {
                    return NavHelpers.CheckIcon;
                }
                else
                {
                    return NavHelpers.XIcon;
                }
            }
            else
            {
                return NavHelpers.SearchIcon;
            }
        }

        public static Color GetSearchButtonColor(bool currentItemHasBeenSearched, bool currentItemIsValid)
        {
            if (currentItemHasBeenSearched)
            {
                if (currentItemIsValid)
                {
                    return Color.Success;
                }
                else
                {
                    return Color.Danger;
                }
            }
            else
            {
                return Color.Primary;
            }
        }

        public static string GetExpandableToggleIcon(bool isExpanded)
        {
            return isExpanded ? NavHelpers.ExpandedIcon : NavHelpers.CollapsedIcon;
        }
    }
}
