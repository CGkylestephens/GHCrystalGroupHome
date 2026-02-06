using Blazorise;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components
{
    public class NetworkStorageInfoBase : ComponentBase
    {
        [Inject] protected INotificationService NotificationService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        [Parameter] public RMAFileStorageInfo? StorageInfo { get; set; }
        [Parameter] public string CssClass { get; set; } = "mb-3";
        [Parameter] public bool StartExpanded { get; set; } = false;

        protected bool IsExpanded { get; set; }

        // Network drive mappings - could be moved to configuration if needed
        private static readonly Dictionary<string, string> NetworkDriveMappings = new()
        {
            { @"\\cgfs0\data", "H:" },
            { @"//cgfs0/data", "H:" },
            { @"\\cgfs0\data\", "H:" },
            { @"//cgfs0/data/", "H:" }
        };

        protected override void OnInitialized()
        {
            IsExpanded = StartExpanded;
        }

        protected void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
            StateHasChanged();
        }

        protected async Task CopyToClipboardAndStopPropagation(MouseEventArgs e, string text)
        {
            // Prevent the click from bubbling up to the toggle
            await CopyToClipboard(text);
        }

        protected async Task CopyToClipboard(string text)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
                await NotificationService.Info("Network path copied to clipboard!");
            }
            catch (Exception ex)
            {
                await NotificationService.Warning($"Could not copy to clipboard. Path: {text}");
                Console.WriteLine($"Clipboard copy failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a path to use proper Windows backslashes and drive mappings
        /// </summary>
        private string FormatWindowsPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            // First, normalize to backslashes
            var normalizedPath = path.Replace('/', '\\');

            // Check for drive mappings
            foreach (var mapping in NetworkDriveMappings)
            {
                var mappingKey = mapping.Key.Replace('/', '\\'); // Normalize mapping key too
                if (normalizedPath.StartsWith(mappingKey, StringComparison.OrdinalIgnoreCase))
                {
                    // Replace the UNC path with the drive letter
                    var remainder = normalizedPath.Substring(mappingKey.Length);
                    // Remove leading backslash if present
                    if (remainder.StartsWith("\\"))
                        remainder = remainder.Substring(1);
                    
                    return string.IsNullOrEmpty(remainder) ? mapping.Value : $"{mapping.Value}\\{remainder}";
                }
            }

            // If no mapping found, just return with proper backslashes
            return normalizedPath;
        }

        /// <summary>
        /// Gets a safe path that is guaranteed to exist (RMA root level)
        /// </summary>
        protected string GetSafePath()
        {
            if (StorageInfo == null) return "";
            
            // Always return the RMA root directory which should exist
            var rmaRootPath = StorageInfo.RootNetworkPath;
            var displayPath = StorageInfo.DisplayPath;
            
            // Use backslashes for Windows paths
            var normalizedDisplayPath = displayPath.Replace('/', '\\');
            var segments = normalizedDisplayPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            
            // Find the RMA_XXXXXX segment
            var rmaSegment = segments.FirstOrDefault(s => s.StartsWith("RMA_", StringComparison.OrdinalIgnoreCase));
            
            if (!string.IsNullOrEmpty(rmaSegment))
            {
                // Build path up to and including the RMA directory
                var safePath = System.IO.Path.Combine(rmaRootPath, rmaSegment);
                return FormatWindowsPath(safePath);
            }
            
            // Fallback to root network path
            return FormatWindowsPath(rmaRootPath);
        }

        /// <summary>
        /// Gets the specific path with proper Windows formatting
        /// </summary>
        protected string GetSpecificPath()
        {
            if (StorageInfo == null) return "";
            return FormatWindowsPath(StorageInfo.DisplayPath);
        }

        /// <summary>
        /// Gets a shortened version of the safe path for compact display
        /// </summary>
        protected string GetShortPath()
        {
            var safePath = GetSafePath();
            
            // Show just the last 2-3 segments of the safe path for compact display
            var segments = safePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            
            if (segments.Length <= 3)
                return safePath;
                
            // Show "...\LastTwoSegments" (using Windows backslashes)
            return $@"...\{segments[segments.Length - 2]}\{segments[segments.Length - 1]}";
        }
    }
}