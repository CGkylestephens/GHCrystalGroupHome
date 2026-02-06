using CrystalGroupHome.Internal.Features.EnvironmentComparer.Data;
using CrystalGroupHome.Internal.Features.EnvironmentComparer.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Components;

/// <summary>
/// Represents a search result found in layer content
/// </summary>
public class ContentSearchResult
{
    public ApplicationLayerDefinition Layer { get; set; } = default!;
    public string Environment { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> MatchLocations { get; set; } = [];
    public int MatchCount { get; set; }
}

public partial class ApplicationLayerComparisonPanel : ComponentBase
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter, EditorRequired]
    public ApplicationLayerComparisonResult ComparisonResult { get; set; } = default!;

    [Parameter, EditorRequired]
    public string SourceEnvironmentName { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string TargetEnvironmentName { get; set; } = string.Empty;

    [Parameter]
    public bool IsContentLoading { get; set; }

    [Parameter]
    public string LoadingContentType { get; set; } = string.Empty;

    [Parameter]
    public ComparisonDifference<ApplicationLayerDefinition>? SelectedDifference { get; set; }

    [Parameter]
    public ApplicationLayerDefinition? SelectedIdentical { get; set; }

    [Parameter]
    public ApplicationLayerDefinition? SelectedOnlyInSource { get; set; }

    [Parameter]
    public ApplicationLayerDefinition? SelectedOnlyInTarget { get; set; }

    [Parameter]
    public EventCallback<ComparisonDifference<ApplicationLayerDefinition>?> OnDifferenceSelectedCallback { get; set; }

    [Parameter]
    public EventCallback<ApplicationLayerDefinition?> OnIdenticalSelectedCallback { get; set; }

    [Parameter]
    public EventCallback<ApplicationLayerDefinition?> OnOnlyInSourceSelectedCallback { get; set; }

    [Parameter]
    public EventCallback<ApplicationLayerDefinition?> OnOnlyInTargetSelectedCallback { get; set; }

    [Parameter]
    public EventCallback<string> OnTabChangedCallback { get; set; }

    private string _selectedTab = "identical";

    // Local references to avoid null reference issues during tab transitions
    private ComparisonDifference<ApplicationLayerDefinition>? _localSelectedDifference;

    // Content search functionality (main search panel)
    private string _searchText = string.Empty;
    private bool _searchCaseSensitive = false;
    private bool _isSearching = false;
    private bool _hasSearched = false;
    private List<ContentSearchResult> _searchResults = [];

    // Content viewer modal
    private bool _showContentModal = false;
    private ApplicationLayerDefinition? _viewingLayer = null;
    private string _viewingEnvironment = string.Empty;
    private string _viewingCategory = string.Empty;
    private string _selectedContentType = "Published";
    private string _displayedContent = string.Empty;
    
    // Modal search functionality (search within modal)
    private string _modalSearchText = string.Empty;
    private bool _modalSearchCaseSensitive = false;
    private int _currentMatchIndex = 0;
    private int _totalMatchesInContent = 0;
    private bool _pendingScrollToMatch = false;
    private int _pendingScrollMatchNumber = 1;
    private bool _hasModalSearched = false;

    // Constants for content types
    private const string ContentTypePublished = "Published";
    private const string ContentTypeDraft = "Draft";

    protected override void OnParametersSet()
    {
        _localSelectedDifference = SelectedDifference;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingScrollToMatch && _showContentModal)
        {
            _pendingScrollToMatch = false;
            await ScrollToMatchWithRetry(_pendingScrollMatchNumber);
        }
    }

    private async Task OnTabChanged(string tabName)
    {
        _selectedTab = tabName;
        _localSelectedDifference = null;
        await OnTabChangedCallback.InvokeAsync(tabName);
    }

    private async Task OnDifferenceSelected(ComparisonDifference<ApplicationLayerDefinition>? difference)
    {
        _localSelectedDifference = difference;
        await OnDifferenceSelectedCallback.InvokeAsync(difference);
    }

    private async Task OnIdenticalSelected(ApplicationLayerDefinition? layer)
    {
        await OnIdenticalSelectedCallback.InvokeAsync(layer);
    }

    private async Task OnOnlyInSourceSelected(ApplicationLayerDefinition? layer)
    {
        await OnOnlyInSourceSelectedCallback.InvokeAsync(layer);
    }

    private async Task OnOnlyInTargetSelected(ApplicationLayerDefinition? layer)
    {
        await OnOnlyInTargetSelectedCallback.InvokeAsync(layer);
    }

    #region Main Search Panel Methods

    /// <summary>
    /// Called when the search text changes (user is typing).
    /// Resets the search state so the "no results" message doesn't show prematurely.
    /// </summary>
    private void OnSearchTextChanged()
    {
        // Reset the searched flag when user starts typing a new search
        _hasSearched = false;
    }

    private async Task ExecuteSearch()
    {
        if (string.IsNullOrWhiteSpace(_searchText) || _searchText.Length < 2)
        {
            _searchResults = [];
            return;
        }

        _isSearching = true;
        _hasSearched = false;
        StateHasChanged();

        await Task.Delay(1);

        try
        {
            _searchResults = SearchAllLayers(_searchText, _searchCaseSensitive);
            _hasSearched = true;
        }
        finally
        {
            _isSearching = false;
            StateHasChanged();
        }
    }

    private void ClearSearch()
    {
        _searchText = string.Empty;
        _searchResults = [];
        _hasSearched = false;
    }

    private List<ContentSearchResult> SearchAllLayers(string searchTerm, bool caseSensitive)
    {
        var results = new List<ContentSearchResult>();
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Helper to search a single layer
        void SearchLayer(ApplicationLayerDefinition layer, string environment, string category)
        {
            var matchLocations = new List<string>();

            // Search published content
            if (!string.IsNullOrEmpty(layer.PublishedContent) && 
                layer.PublishedContent.Contains(searchTerm, comparison))
            {
                matchLocations.Add(ContentTypePublished);
            }

            // Search draft content
            if (!string.IsNullOrEmpty(layer.DraftContent) && 
                layer.DraftContent.Contains(searchTerm, comparison))
            {
                matchLocations.Add(ContentTypeDraft);
            }

            if (matchLocations.Count > 0)
            {
                results.Add(new ContentSearchResult
                {
                    Layer = layer,
                    Environment = environment,
                    Category = category,
                    MatchLocations = matchLocations,
                    MatchCount = CountOccurrences(layer.PublishedContent, searchTerm, comparison) +
                                 CountOccurrences(layer.DraftContent, searchTerm, comparison)
                });
            }
        }

        // Search identical layers (both environments have the same content, so just search source)
        foreach (var layer in ComparisonResult.Identical)
        {
            SearchLayer(layer, "Both", "Identical");
        }

        // Search published differences
        foreach (var diff in ComparisonResult.PublishedDifferences)
        {
            SearchLayer(diff.SourceItem, SourceEnvironmentName, "Published Diff");
            SearchLayer(diff.TargetItem, TargetEnvironmentName, "Published Diff");
        }

        // Search draft differences
        foreach (var diff in ComparisonResult.DraftDifferences)
        {
            SearchLayer(diff.SourceItem, SourceEnvironmentName, "Pending Changes");
            SearchLayer(diff.TargetItem, TargetEnvironmentName, "Pending Changes");
        }

        // Search both differences
        foreach (var diff in ComparisonResult.BothDifferences)
        {
            SearchLayer(diff.SourceItem, SourceEnvironmentName, "Both Diff");
            SearchLayer(diff.TargetItem, TargetEnvironmentName, "Both Diff");
        }

        // Search only in source
        foreach (var layer in ComparisonResult.ExistsOnlyInSource)
        {
            SearchLayer(layer, SourceEnvironmentName, $"Only in {SourceEnvironmentName}");
        }

        // Search only in target
        foreach (var layer in ComparisonResult.ExistsOnlyInTarget)
        {
            SearchLayer(layer, TargetEnvironmentName, $"Only in {TargetEnvironmentName}");
        }

        // Sort by match count descending, then by layer name
        return results
            .OrderByDescending(r => r.MatchCount)
            .ThenBy(r => r.Layer.Id)
            .ThenBy(r => r.Layer.LayerName)
            .ToList();
    }

    private static int CountOccurrences(string? text, string searchTerm, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(searchTerm, index, comparison)) != -1)
        {
            count++;
            index += searchTerm.Length;
        }
        return count;
    }

    private string GetContentPreview(ApplicationLayerDefinition layer, string searchTerm)
    {
        var comparison = _searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        
        // Try to find a preview in published content first
        var preview = GetPreviewFromContent(layer.PublishedContent, searchTerm, comparison);
        if (!string.IsNullOrEmpty(preview))
            return preview;

        // Fall back to draft content
        return GetPreviewFromContent(layer.DraftContent, searchTerm, comparison);
    }

    private static string GetPreviewFromContent(string? content, string searchTerm, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var index = content.IndexOf(searchTerm, comparison);
        if (index == -1)
            return string.Empty;

        // Get context around the match (50 chars before and after)
        const int contextLength = 50;
        var start = Math.Max(0, index - contextLength);
        var end = Math.Min(content.Length, index + searchTerm.Length + contextLength);

        var preview = content[start..end]
            .Replace("\r", "")
            .Replace("\n", " ")
            .Replace("  ", " ");

        // Add ellipsis if truncated
        if (start > 0) 
            preview = "..." + preview;
        if (end < content.Length) 
            preview += "...";

        return preview;
    }

    #endregion

    #region Content Viewer Modal Methods

    /// <summary>
    /// Opens the content viewer from a search result
    /// </summary>
    private async Task OpenContentViewerFromSearch(ContentSearchResult result, string contentType)
    {
        _viewingLayer = result.Layer;
        _viewingEnvironment = result.Environment;
        _viewingCategory = result.Category;
        _selectedContentType = contentType;
        
        // Initialize modal search with the current search term
        _modalSearchText = _searchText;
        _modalSearchCaseSensitive = _searchCaseSensitive;
        
        await OpenContentViewerInternal();
    }

    /// <summary>
    /// Opens the content viewer for a layer (without search context)
    /// </summary>
    private async Task OpenContentViewerForLayer(ApplicationLayerDefinition layer, string environment, string category, string contentType = ContentTypePublished)
    {
        _viewingLayer = layer;
        _viewingEnvironment = environment;
        _viewingCategory = category;
        _selectedContentType = contentType;
        
        // Clear modal search when opening without search context
        _modalSearchText = string.Empty;
        _modalSearchCaseSensitive = false;
        
        await OpenContentViewerInternal();
    }

    // Grid button click handlers (avoid inline lambda issues with quotes)
    private Task ViewIdenticalLayer(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, "Both", "Identical");

    private Task ViewPublishedDiffSource(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, SourceEnvironmentName, "Published Diff");

    private Task ViewPublishedDiffTarget(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, TargetEnvironmentName, "Published Diff");

    private Task ViewPendingChangesSource(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, SourceEnvironmentName, "Pending Changes");

    private Task ViewPendingChangesTarget(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, TargetEnvironmentName, "Pending Changes");

    private Task ViewBothDiffSource(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, SourceEnvironmentName, "Both Diff");

    private Task ViewBothDiffTarget(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, TargetEnvironmentName, "Both Diff");

    private Task ViewOnlyInSourceLayer(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, SourceEnvironmentName, $"Only in {SourceEnvironmentName}");

    private Task ViewOnlyInTargetLayer(ApplicationLayerDefinition layer) 
        => OpenContentViewerForLayer(layer, TargetEnvironmentName, $"Only in {TargetEnvironmentName}");

    private async Task OpenContentViewerInternal()
    {
        if (_viewingLayer == null) return;

        var content = _selectedContentType == ContentTypePublished 
            ? _viewingLayer.PublishedContent 
            : _viewingLayer.DraftContent;
        
        _displayedContent = content ?? string.Empty;
        UpdateModalSearchMatches();
        
        _showContentModal = true;
        
        if (_totalMatchesInContent > 0)
        {
            _pendingScrollToMatch = true;
            _pendingScrollMatchNumber = 1;
        }
        
        StateHasChanged();
    }

    private void CloseContentViewer()
    {
        _showContentModal = false;
        _viewingLayer = null;
        _displayedContent = string.Empty;
        _modalSearchText = string.Empty;
        _currentMatchIndex = 0;
        _totalMatchesInContent = 0;
        _pendingScrollToMatch = false;
        _hasModalSearched = false;
    }

    private async Task SwitchToPublished() => await SwitchContentType(ContentTypePublished);
    private async Task SwitchToDraft() => await SwitchContentType(ContentTypeDraft);

    private async Task SwitchContentType(string contentType)
    {
        if (_viewingLayer == null || _selectedContentType == contentType) return;
        
        _selectedContentType = contentType;
        _displayedContent = contentType == ContentTypePublished 
            ? _viewingLayer.PublishedContent ?? string.Empty
            : _viewingLayer.DraftContent ?? string.Empty;
        
        UpdateModalSearchMatches();
        
        if (_totalMatchesInContent > 0)
        {
            _currentMatchIndex = 1;
            _pendingScrollToMatch = true;
            _pendingScrollMatchNumber = 1;
        }
        
        StateHasChanged();
    }

    #endregion

    #region Modal Search Methods

    /// <summary>
    /// Called when the modal search text changes (user is typing).
    /// Resets the search state so the "no results" message doesn't show prematurely.
    /// </summary>
    private void OnModalSearchTextChanged()
    {
        // Reset the searched flag when user starts typing a new search
        _hasModalSearched = false;
    }

    private void UpdateModalSearchMatches()
    {
        if (string.IsNullOrWhiteSpace(_modalSearchText) || _modalSearchText.Length < 2)
        {
            _totalMatchesInContent = 0;
            _currentMatchIndex = 0;
            return;
        }

        var comparison = _modalSearchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        _totalMatchesInContent = CountOccurrences(_displayedContent, _modalSearchText, comparison);
        _currentMatchIndex = _totalMatchesInContent > 0 ? 1 : 0;
    }

    private async Task ExecuteModalSearch()
    {
        _hasModalSearched = true;
        UpdateModalSearchMatches();
        StateHasChanged();
        
        if (_totalMatchesInContent > 0)
        {
            await ScrollToMatchWithRetry(1);
        }
    }

    private void ClearModalSearch()
    {
        _modalSearchText = string.Empty;
        _totalMatchesInContent = 0;
        _currentMatchIndex = 0;
        _hasModalSearched = false;
        StateHasChanged();
    }

    private async Task JumpToCurrentMatch()
    {
        if (_totalMatchesInContent > 0 && _currentMatchIndex > 0)
        {
            await ScrollToMatchWithRetry(_currentMatchIndex);
        }
    }

    private async Task GoToFirstMatch()
    {
        if (_totalMatchesInContent > 0)
        {
            _currentMatchIndex = 1;
            await ScrollToMatchWithRetry(_currentMatchIndex);
        }
    }

    private async Task GoToPreviousMatch()
    {
        if (_totalMatchesInContent > 0 && _currentMatchIndex > 1)
        {
            _currentMatchIndex--;
            await ScrollToMatchWithRetry(_currentMatchIndex);
        }
    }

    private async Task GoToNextMatch()
    {
        if (_totalMatchesInContent > 0 && _currentMatchIndex < _totalMatchesInContent)
        {
            _currentMatchIndex++;
            await ScrollToMatchWithRetry(_currentMatchIndex);
        }
    }

    private async Task GoToLastMatch()
    {
        if (_totalMatchesInContent > 0)
        {
            _currentMatchIndex = _totalMatchesInContent;
            await ScrollToMatchWithRetry(_currentMatchIndex);
        }
    }

    private async Task ScrollToMatchWithRetry(int matchNumber, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Wait a bit for DOM to be ready
                await Task.Delay(50 + (attempt * 100));
                
                var success = await JSRuntime.InvokeAsync<bool>("scrollToSearchMatch", matchNumber);
                if (success)
                {
                    StateHasChanged();
                    return;
                }
            }
            catch
            {
                // Continue retrying
            }
        }
    }

    #endregion

    #region Helper Methods

    private bool HasPublishedContent => _viewingLayer?.HasPublishedContent ?? false;
    private bool HasDraftContent => _viewingLayer?.HasDraft ?? false;
    private bool IsPublishedSelected => _selectedContentType == ContentTypePublished;
    private bool IsDraftSelected => _selectedContentType == ContentTypeDraft;

    private string GetHighlightedHtml(string content, string searchTerm)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;
            
        if (string.IsNullOrEmpty(searchTerm) || searchTerm.Length < 2)
            return System.Net.WebUtility.HtmlEncode(content);

        var comparison = _modalSearchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var result = new System.Text.StringBuilder();
        int lastIndex = 0;
        int matchIndex;
        int matchCount = 0;

        while ((matchIndex = content.IndexOf(searchTerm, lastIndex, comparison)) != -1)
        {
            matchCount++;
            
            if (matchIndex > lastIndex)
            {
                result.Append(System.Net.WebUtility.HtmlEncode(content[lastIndex..matchIndex]));
            }

            var actualMatch = content.Substring(matchIndex, searchTerm.Length);
            var cssClass = matchCount == _currentMatchIndex ? "search-match-current" : "search-match";
            result.Append($"<span class=\"{cssClass}\">{System.Net.WebUtility.HtmlEncode(actualMatch)}</span>");
            
            lastIndex = matchIndex + searchTerm.Length;
        }

        if (lastIndex < content.Length)
        {
            result.Append(System.Net.WebUtility.HtmlEncode(content[lastIndex..]));
        }

        return result.ToString();
    }

    #endregion
}
