using System.Text.RegularExpressions;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public class HtmlHelpers
    {
        private static readonly Regex UrlRegex = new Regex(
            @"(https?://[^\s]+|www\.[^\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex MarkdownLinkRegex = new Regex(
            @"\[([^\]]+)\]\(([^)]+)\)",
            RegexOptions.Compiled
        );

        public static string ConvertUrlsToLinks(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // First, HTML encode the entire text to prevent XSS
            var encodedText = System.Web.HttpUtility.HtmlEncode(text);

            // Then convert markdown-style links [text](url) to HTML links
            encodedText = MarkdownLinkRegex.Replace(encodedText, match =>
            {
                var linkText = match.Groups[1].Value;
                var url = match.Groups[2].Value;
                var href = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"http://{url}";
                return $"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer\">{linkText}</a>";
            });

            // Finally, convert plain URLs to links (avoiding already converted markdown links)
            encodedText = UrlRegex.Replace(encodedText, match =>
            {
                // Skip if this URL is already part of an anchor tag
                var index = match.Index;
                var beforeMatch = encodedText.Substring(0, index);
                if (beforeMatch.EndsWith("href=\"") || beforeMatch.EndsWith("\">"))
                    return match.Value;

                var url = match.Value;
                var href = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"http://{url}";
                return $"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer\">{url}</a>";
            });

            return encodedText;
        }
    }
}
