using CrystalGroupHome.Internal.Common.Data._Epicor;
using CrystalGroupHome.Internal.Features.EnvironmentComparer.Data;
using CrystalGroupHome.Internal.Features.EnvironmentComparer.Models;
using CrystalGroupHome.SharedRCL.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer
{
    public partial class EnvironmentComparer : ComponentBase
    {
        [Inject] public IEpicorEnvironmentService EnvironmentService { get; set; } = default!;
        [Inject] public IEnvironmentComparisonService ComparisonService { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

        // Change protected to public for Razor accessibility
        public List<EpicorRestSettings> _environments = new();
        public string _sourceEnvironment = string.Empty;
        public string _targetEnvironment = string.Empty;

        // Track the actual environments used in the last comparison
        public string _lastComparedSourceEnvironment = string.Empty;
        public string _lastComparedTargetEnvironment = string.Empty;

        public bool _isLoading = false;
        public string _loadingStatus = string.Empty;

        // Content loading state
        public bool _isContentLoading = false;
        public string _loadingContentType = string.Empty;

        // Authorization state
        public bool _isAuthorized = false;
        public bool _authorizationChecked = false;

        // Comparison type selection - all unchecked by default
        public bool _compareBaqs = false;
        public bool _compareBpms = false;
        public bool _compareUdColumns = false;
        public bool _compareAppLayers = false;

        // Track what was actually compared in the last run
        public bool _lastComparedBaqs = false;
        public bool _lastComparedBpms = false;
        public bool _lastComparedUdColumns = false;
        public bool _lastComparedAppLayers = false;

        // Performance tracking
        public PerformanceStats? _performanceStats;

        // Caching for formatted content
        public readonly Dictionary<string, string> _xmlFormatCache = new();
        public readonly Dictionary<string, string> _jsonFormatCache = new();
        public readonly Dictionary<string, IEnumerable<(string line, int index)>> _contentLinesCache = new();

        // Main tab selection
        public string _selectedMainTab = "baqs";

        // BAQ related fields
        public ComparisonResult<BaqDefinition>? _baqComparisonResult;
        public ComparisonDifference<BaqDefinition>? _selectedBaqDifference;
        public BaqDefinition? _selectedIdenticalBaq;
        public BaqDefinition? _selectedOnlyInSourceBaq;
        public BaqDefinition? _selectedOnlyInTargetBaq;
        public string _selectedBaqTab = "identical";

        // BPM Directive related fields
        public ComparisonResult<BpmDirectiveDefinition>? _bpmComparisonResult;
        public ComparisonDifference<BpmDirectiveDefinition>? _selectedBpmDifference;
        public BpmDirectiveDefinition? _selectedIdenticalBpm;
        public BpmDirectiveDefinition? _selectedOnlyInSourceBpm;
        public BpmDirectiveDefinition? _selectedOnlyInTargetBpm;
        public string _selectedBpmTab = "identical";

        // UD Column related fields
        public ComparisonResult<UDColumnDTO>? _udColumnComparisonResult;
        public string _selectedUdColumnTab = "identical";

        // Application Layer related fields - using new extended result type
        public ApplicationLayerComparisonResult? _appLayerComparisonResult;
        public ComparisonDifference<ApplicationLayerDefinition>? _selectedAppLayerDifference;
        public ApplicationLayerDefinition? _selectedIdenticalAppLayer;
        public ApplicationLayerDefinition? _selectedOnlyInSourceAppLayer;
        public ApplicationLayerDefinition? _selectedOnlyInTargetAppLayer;
        public string _selectedAppLayerTab = "identical";

        // Move the enum to the code-behind file to avoid duplication
        public enum EnabledFilter
        {
            All,
            Yes,
            No
        }

        // Helper to check if at least one comparison type is selected
        public bool HasAnyComparisonSelected => _compareBaqs || _compareBpms || _compareUdColumns || _compareAppLayers;

        // Helper to check if all comparison types are selected
        public bool HasAllComparisonsSelected => _compareBaqs && _compareBpms && _compareUdColumns && _compareAppLayers;

        // Count of selected comparisons
        public int SelectedComparisonCount => (_compareBaqs ? 1 : 0) + (_compareBpms ? 1 : 0) + (_compareUdColumns ? 1 : 0) + (_compareAppLayers ? 1 : 0);

        // Select all comparison types
        public void SelectAllComparisons()
        {
            _compareBaqs = true;
            _compareBpms = true;
            _compareUdColumns = true;
            _compareAppLayers = true;
        }

        // Clear all comparison type selections
        public void ClearAllComparisons()
        {
            _compareBaqs = false;
            _compareBpms = false;
            _compareUdColumns = false;
            _compareAppLayers = false;
        }

        protected override async Task OnInitializedAsync()
        {
            // Check authorization
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            _isAuthorized = AccessOverrides.IsIT(authState.User);
            _authorizationChecked = true;

            if (_isAuthorized)
            {
                _environments = EnvironmentService.GetConfiguredEnvironments();
            }

            StateHasChanged();
        }

        // Custom filter methods for IsEnabled columns
        public bool OnIsEnabledCustomFilter(object itemValue, object searchValue) => (itemValue, searchValue) switch
        {
            (_, null) => true,
            (bool isEnabled, EnabledFilter filter) => filter switch
            {
                EnabledFilter.All => true,
                EnabledFilter.Yes => isEnabled,
                EnabledFilter.No => !isEnabled,
                _ => true
            },
            _ => true
        };

        public bool OnSourceIsEnabledCustomFilter(object itemValue, object searchValue) => (itemValue, searchValue) switch
        {
            (_, null) => true,
            (ComparisonDifference<BpmDirectiveDefinition> comparison, EnabledFilter filter) => filter switch
            {
                EnabledFilter.All => true,
                EnabledFilter.Yes => comparison.SourceItem.IsEnabled,
                EnabledFilter.No => !comparison.SourceItem.IsEnabled,
                _ => true
            },
            _ => true
        };

        public bool OnTargetIsEnabledCustomFilter(object itemValue, object searchValue) => (itemValue, searchValue) switch
        {
            (_, null) => true,
            (ComparisonDifference<BpmDirectiveDefinition> comparison, EnabledFilter filter) => filter switch
            {
                EnabledFilter.All => true,
                EnabledFilter.Yes => comparison.TargetItem.IsEnabled,
                EnabledFilter.No => !comparison.TargetItem.IsEnabled,
                _ => true
            },
            _ => true
        };

        public async Task CompareEnvironments()
        {
            if (string.IsNullOrEmpty(_sourceEnvironment) || string.IsNullOrEmpty(_targetEnvironment))
            {
                return;
            }

            if (!HasAnyComparisonSelected)
            {
                return;
            }

            var startTime = DateTime.UtcNow;
            _isLoading = true;
            _baqComparisonResult = null;
            _bpmComparisonResult = null;
            _udColumnComparisonResult = null;
            _appLayerComparisonResult = null;
            _performanceStats = null;
            ClearSelections();
            ClearCaches();

            // Track what we're comparing this run
            _lastComparedBaqs = _compareBaqs;
            _lastComparedBpms = _compareBpms;
            _lastComparedUdColumns = _compareUdColumns;
            _lastComparedAppLayers = _compareAppLayers;

            try
            {
                var tasks = new List<Task>();
                Task<ComparisonResult<BaqDefinition>>? baqTask = null;
                Task<ComparisonResult<BpmDirectiveDefinition>>? bpmTask = null;
                Task<ComparisonResult<UDColumnDTO>>? udColumnTask = null;
                Task<ApplicationLayerComparisonResult>? appLayerTask = null;

                var baqStartTime = DateTime.UtcNow;
                var bpmStartTime = DateTime.UtcNow;
                var udColumnStartTime = DateTime.UtcNow;
                var appLayerStartTime = DateTime.UtcNow;

                if (_compareBaqs)
                {
                    _loadingStatus = "Loading BAQs from both environments...";
                    StateHasChanged();
                    baqStartTime = DateTime.UtcNow;
                    baqTask = ComparisonService.CompareBaqsAsync(_sourceEnvironment, _targetEnvironment);
                    tasks.Add(baqTask);
                }

                if (_compareBpms)
                {
                    _loadingStatus = "Loading BPM Directives from both environments...";
                    StateHasChanged();
                    bpmStartTime = DateTime.UtcNow;
                    bpmTask = ComparisonService.CompareBpmsAsync(_sourceEnvironment, _targetEnvironment);
                    tasks.Add(bpmTask);
                }

                if (_compareUdColumns)
                {
                    _loadingStatus = "Loading UD Columns from both environments...";
                    StateHasChanged();
                    udColumnStartTime = DateTime.UtcNow;
                    udColumnTask = ComparisonService.CompareUDColumnsAsync(_sourceEnvironment, _targetEnvironment);
                    tasks.Add(udColumnTask);
                }

                if (_compareAppLayers)
                {
                    _loadingStatus = "Loading Application Layers from both environments...";
                    StateHasChanged();
                    appLayerStartTime = DateTime.UtcNow;
                    appLayerTask = ComparisonService.CompareApplicationLayersAsync(_sourceEnvironment, _targetEnvironment);
                    tasks.Add(appLayerTask);
                }

                _loadingStatus = "Processing comparison results...";
                StateHasChanged();

                // Wait for all selected tasks to complete
                await Task.WhenAll(tasks);

                var baqEndTime = DateTime.UtcNow;
                var bpmEndTime = DateTime.UtcNow;
                var udColumnEndTime = DateTime.UtcNow;
                var appLayerEndTime = DateTime.UtcNow;
                var endTime = DateTime.UtcNow;

                // Collect results
                ComparisonResult<BaqDefinition>? baqResult = null;
                ComparisonResult<BpmDirectiveDefinition>? bpmResult = null;
                ComparisonResult<UDColumnDTO>? udColumnResult = null;
                ApplicationLayerComparisonResult? appLayerResult = null;

                if (baqTask != null)
                {
                    baqResult = await baqTask;
                    baqEndTime = DateTime.UtcNow;
                    _baqComparisonResult = baqResult;
                }

                if (bpmTask != null)
                {
                    bpmResult = await bpmTask;
                    bpmEndTime = DateTime.UtcNow;
                    _bpmComparisonResult = bpmResult;
                }

                if (udColumnTask != null)
                {
                    udColumnResult = await udColumnTask;
                    udColumnEndTime = DateTime.UtcNow;
                    _udColumnComparisonResult = udColumnResult;
                }

                if (appLayerTask != null)
                {
                    appLayerResult = await appLayerTask;
                    appLayerEndTime = DateTime.UtcNow;
                    _appLayerComparisonResult = appLayerResult;
                }

                // Store the environments that were actually compared
                _lastComparedSourceEnvironment = _sourceEnvironment;
                _lastComparedTargetEnvironment = _targetEnvironment;

                // Calculate performance statistics
                var baqCount = baqResult != null 
                    ? baqResult.Identical.Count + baqResult.Differences.Count + baqResult.ExistsOnlyInSource.Count + baqResult.ExistsOnlyInTarget.Count 
                    : 0;
                var bpmCount = bpmResult != null 
                    ? bpmResult.Identical.Count + bpmResult.Differences.Count + bpmResult.ExistsOnlyInSource.Count + bpmResult.ExistsOnlyInTarget.Count 
                    : 0;
                var udColumnCount = udColumnResult != null 
                    ? udColumnResult.Identical.Count + udColumnResult.ExistsOnlyInSource.Count + udColumnResult.ExistsOnlyInTarget.Count 
                    : 0;
                var appLayerCount = appLayerResult != null 
                    ? appLayerResult.Identical.Count + appLayerResult.TotalDifferenceCount + appLayerResult.ExistsOnlyInSource.Count + appLayerResult.ExistsOnlyInTarget.Count 
                    : 0;

                _performanceStats = new PerformanceStats
                {
                    BaqCount = baqCount,
                    BpmCount = bpmCount,
                    UdColumnCount = udColumnCount,
                    AppLayerCount = appLayerCount,
                    BaqLoadTime = _compareBaqs ? baqEndTime - baqStartTime : TimeSpan.Zero,
                    BpmLoadTime = _compareBpms ? bpmEndTime - bpmStartTime : TimeSpan.Zero,
                    UdColumnLoadTime = _compareUdColumns ? udColumnEndTime - udColumnStartTime : TimeSpan.Zero,
                    AppLayerLoadTime = _compareAppLayers ? appLayerEndTime - appLayerStartTime : TimeSpan.Zero,
                    TotalTime = endTime - startTime,
                    // Track what was compared for display purposes
                    CompareBaqs = _compareBaqs,
                    CompareBpms = _compareBpms,
                    CompareUdColumns = _compareUdColumns,
                    CompareAppLayers = _compareAppLayers
                };

                // Auto-select the first available tab
                SelectFirstAvailableTab();
            }
            finally
            {
                _loadingStatus = string.Empty;
                _isLoading = false;
                StateHasChanged();
            }
        }

        private void SelectFirstAvailableTab()
        {
            if (_lastComparedBaqs && _baqComparisonResult != null)
            {
                _selectedMainTab = "baqs";
            }
            else if (_lastComparedBpms && _bpmComparisonResult != null)
            {
                _selectedMainTab = "bpms";
            }
            else if (_lastComparedUdColumns && _udColumnComparisonResult != null)
            {
                _selectedMainTab = "udcolumns";
            }
            else if (_lastComparedAppLayers && _appLayerComparisonResult != null)
            {
                _selectedMainTab = "applayers";
            }
        }

        // BAQ selection handlers with loading indicators
        public async Task OnBaqDifferenceSelected(ComparisonDifference<BaqDefinition>? difference)
        {
            if (difference == null) return;

            _isContentLoading = true;
            _loadingContentType = "baq_difference";
            _selectedBaqDifference = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedBaqDifference = difference;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnBaqIdenticalSelected(BaqDefinition? baq)
        {
            if (baq == null) return;

            _isContentLoading = true;
            _loadingContentType = "baq_identical";
            _selectedIdenticalBaq = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedIdenticalBaq = baq;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnBaqOnlyInSourceSelected(BaqDefinition? baq)
        {
            if (baq == null) return;

            _isContentLoading = true;
            _loadingContentType = "baq_onlyInSource";
            _selectedOnlyInSourceBaq = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedOnlyInSourceBaq = baq;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnBaqOnlyInTargetSelected(BaqDefinition? baq)
        {
            if (baq == null) return;

            _isContentLoading = true;
            _loadingContentType = "baq_onlyInTarget";
            _selectedOnlyInTargetBaq = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedOnlyInTargetBaq = baq;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        // BPM selection handlers with loading indicators
        public async Task OnBpmDifferenceSelected(ComparisonDifference<BpmDirectiveDefinition>? difference)
        {
            if (difference == null) return;

            _isContentLoading = true;
            _loadingContentType = "bpm_difference";
            _selectedBpmDifference = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedBpmDifference = difference;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnBpmIdenticalSelected(BpmDirectiveDefinition? bpm)
        {
            if (bpm == null) return;

            _isContentLoading = true;
            _loadingContentType = "bpm_identical";
            _selectedIdenticalBpm = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedIdenticalBpm = bpm;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnBpmOnlyInSourceSelected(BpmDirectiveDefinition? bpm)
        {
            if (bpm == null) return;

            _isContentLoading = true;
            _loadingContentType = "bpm_onlyInSource";
            _selectedOnlyInSourceBpm = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedOnlyInSourceBpm = bpm;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnBpmOnlyInTargetSelected(BpmDirectiveDefinition? bpm)
        {
            if (bpm == null) return;

            _isContentLoading = true;
            _loadingContentType = "bpm_onlyInTarget";
            _selectedOnlyInTargetBpm = null;
            StateHasChanged();

            await Task.Delay(1); // Allow UI to update
            _selectedOnlyInTargetBpm = bpm;

            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        // Application Layer selection handlers - use longer delay for heavy diff content
        public async Task OnAppLayerDifferenceSelected(ComparisonDifference<ApplicationLayerDefinition>? difference)
        {
            if (difference == null) return;

            // Clear previous selection and show loading
            _selectedAppLayerDifference = null;
            _isContentLoading = true;
            _loadingContentType = "applayer_difference";
            StateHasChanged();

            // Give the UI time to render the loading state before heavy diff work
            await Task.Yield();
            await Task.Delay(50);

            // Now set the selection which will trigger the diff component
            _selectedAppLayerDifference = difference;
            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnAppLayerIdenticalSelected(ApplicationLayerDefinition? layer)
        {
            if (layer == null) return;

            _selectedIdenticalAppLayer = null;
            _isContentLoading = true;
            _loadingContentType = "applayer_identical";
            StateHasChanged();

            await Task.Yield();
            await Task.Delay(50);

            _selectedIdenticalAppLayer = layer;
            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnAppLayerOnlyInSourceSelected(ApplicationLayerDefinition? layer)
        {
            if (layer == null) return;

            _selectedOnlyInSourceAppLayer = null;
            _isContentLoading = true;
            _loadingContentType = "applayer_onlyInSource";
            StateHasChanged();

            await Task.Yield();
            await Task.Delay(50);

            _selectedOnlyInSourceAppLayer = layer;
            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public async Task OnAppLayerOnlyInTargetSelected(ApplicationLayerDefinition? layer)
        {
            if (layer == null) return;

            _selectedOnlyInTargetAppLayer = null;
            _isContentLoading = true;
            _loadingContentType = "applayer_onlyInTarget";
            StateHasChanged();

            await Task.Yield();
            await Task.Delay(50);

            _selectedOnlyInTargetAppLayer = layer;
            _isContentLoading = false;
            _loadingContentType = string.Empty;
            StateHasChanged();
        }

        public void OnMainTabChanged(string tabName)
        {
            _selectedMainTab = tabName;
            ClearSelections();
        }

        public void OnTabChanged(string tabName, string type)
        {
            if (type == "baq")
            {
                _selectedBaqTab = tabName;
            }
            else if (type == "bpm")
            {
                _selectedBpmTab = tabName;
            }
            else if (type == "udcolumn")
            {
                _selectedUdColumnTab = tabName;
            }
            else if (type == "applayer")
            {
                _selectedAppLayerTab = tabName;
            }
            ClearSelections();
        }

        public void ClearSelections()
        {
            // Clear content loading state
            _isContentLoading = false;
            _loadingContentType = string.Empty;

            // Clear BAQ selections
            _selectedBaqDifference = null;
            _selectedIdenticalBaq = null;
            _selectedOnlyInSourceBaq = null;
            _selectedOnlyInTargetBaq = null;

            // Clear BPM selections
            _selectedBpmDifference = null;
            _selectedIdenticalBpm = null;
            _selectedOnlyInSourceBpm = null;
            _selectedOnlyInTargetBpm = null;

            // Clear Application Layer selections
            _selectedAppLayerDifference = null;
            _selectedIdenticalAppLayer = null;
            _selectedOnlyInSourceAppLayer = null;
            _selectedOnlyInTargetAppLayer = null;
        }

        public void ClearCaches()
        {
            _xmlFormatCache.Clear();
            _jsonFormatCache.Clear();
            _contentLinesCache.Clear();
        }

        // Optimized XML formatting with caching
        public string GetFormattedXml(string xml, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return string.Empty;

            if (_xmlFormatCache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                var formatted = doc.ToString();
                _xmlFormatCache[cacheKey] = formatted;
                return formatted;
            }
            catch
            {
                // If XML parsing fails, return the original string
                _xmlFormatCache[cacheKey] = xml;
                return xml;
            }
        }

        // Optimized JSON formatting with caching
        public string GetFormattedJson(string json, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            if (_jsonFormatCache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                var formatted = System.Text.Json.JsonSerializer.Serialize(jsonDoc, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                _jsonFormatCache[cacheKey] = formatted;
                return formatted;
            }
            catch
            {
                // If JSON parsing fails, return the original string
                _jsonFormatCache[cacheKey] = json;
                return json;
            }
        }

        // Optimized line splitting with caching for XML content
        public IEnumerable<(string line, int index)> GetFormattedXmlLines(string xml, string cacheKey)
        {
            if (_contentLinesCache.TryGetValue(cacheKey, out var cachedLines))
                return cachedLines;

            var formattedXml = GetFormattedXml(xml, cacheKey + "_xml");
            var lines = formattedXml.Split('\n').Select((line, index) => (line, index));
            _contentLinesCache[cacheKey] = lines.ToList(); // Materialize to avoid re-computation
            return _contentLinesCache[cacheKey];
        }

        // Optimized line splitting with caching for JSON content
        public IEnumerable<(string line, int index)> GetFormattedJsonLines(string json, string cacheKey)
        {
            if (_contentLinesCache.TryGetValue(cacheKey, out var cachedLines))
                return cachedLines;

            var formattedJson = GetFormattedJson(json, cacheKey + "_json");
            var lines = formattedJson.Split('\n').Select((line, index) => (line, index));
            _contentLinesCache[cacheKey] = lines.ToList(); // Materialize to avoid re-computation
            return _contentLinesCache[cacheKey];
        }

        // Optimized line splitting with caching for regular content
        public IEnumerable<(string line, int index)> GetFormattedContentLines(string content, string cacheKey)
        {
            if (_contentLinesCache.TryGetValue(cacheKey, out var cachedLines))
                return cachedLines;

            var lines = content.Split('\n').Select((line, index) => (line, index));
            _contentLinesCache[cacheKey] = lines.ToList(); // Materialize to avoid re-computation
            return _contentLinesCache[cacheKey];
        }

        /// <summary>
        /// Gets a descriptive label for the difference type
        /// </summary>
        public static string GetDifferenceTypeLabel(LayerDifferenceType differenceType) => differenceType switch
        {
            LayerDifferenceType.PublishedOnly => "Published",
            LayerDifferenceType.DraftOnly => "Draft Only",
            LayerDifferenceType.Both => "Both",
            _ => "None"
        };

        /// <summary>
        /// Gets a CSS class for styling the difference type badge
        /// </summary>
        public static string GetDifferenceTypeBadgeClass(LayerDifferenceType differenceType) => differenceType switch
        {
            LayerDifferenceType.PublishedOnly => "bg-danger",
            LayerDifferenceType.DraftOnly => "bg-warning text-dark",
            LayerDifferenceType.Both => "bg-danger",
            _ => "bg-secondary"
        };

        // Performance statistics class
        public class PerformanceStats
        {
            public int BaqCount { get; set; }
            public int BpmCount { get; set; }
            public int UdColumnCount { get; set; }
            public int AppLayerCount { get; set; }
            public TimeSpan BaqLoadTime { get; set; }
            public TimeSpan BpmLoadTime { get; set; }
            public TimeSpan UdColumnLoadTime { get; set; }
            public TimeSpan AppLayerLoadTime { get; set; }
            public TimeSpan TotalTime { get; set; }
            
            // Track what was compared
            public bool CompareBaqs { get; set; }
            public bool CompareBpms { get; set; }
            public bool CompareUdColumns { get; set; }
            public bool CompareAppLayers { get; set; }
        }
    }
}
