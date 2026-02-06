using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.AspNetCore.Components;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Components;

public partial class CollapsedDiff : ComponentBase
{
    [Parameter] public string OldText { get; set; } = string.Empty;
    [Parameter] public string NewText { get; set; } = string.Empty;
    [Parameter] public string OldTextLabel { get; set; } = "Source";
    [Parameter] public string NewTextLabel { get; set; } = "Target";
    [Parameter] public int ContextLines { get; set; } = 3;

    private List<DiffChunk> _diffChunks = new();
    private List<SideBySideChunk> _sideBySideChunks = new();
    private bool _isProcessing = false;
    private bool _showFullDiff = false;
    private bool _isExpandingFullDiff = false; // Track when we're loading full diff
    private int _totalChanges = 0;
    private DiffViewMode _viewMode = DiffViewMode.Unified;

    private enum DiffViewMode
    {
        Unified,
        SideBySide
    }

    protected override async Task OnParametersSetAsync()
    {
        _isProcessing = true;
        _showFullDiff = false;
        _isExpandingFullDiff = false;
        StateHasChanged();

        await Task.Yield();
        await Task.Delay(10);

        ComputeDiff();
        ComputeSideBySideDiff();

        _isProcessing = false;
        StateHasChanged();
    }

    private void SetViewMode(DiffViewMode mode)
    {
        _viewMode = mode;
        
        // If switching to unified view while expanded, collapse first
        // since BlazorTextDiff only shows side-by-side
        if (mode == DiffViewMode.Unified && _showFullDiff)
        {
            _showFullDiff = false;
            _isExpandingFullDiff = false;
        }
        
        StateHasChanged();
    }

    private async Task ToggleFullDiff()
    {
        if (_showFullDiff)
        {
            // Collapsing - do immediately
            _showFullDiff = false;
            _isExpandingFullDiff = false;
        }
        else
        {
            // Expanding - show loading state first, then render
            _isExpandingFullDiff = true;
            // Switch to side-by-side mode since BlazorTextDiff only shows side-by-side
            _viewMode = DiffViewMode.SideBySide;
            StateHasChanged();
            
            // Give the UI time to show the loading indicator
            await Task.Delay(50);
            
            _showFullDiff = true;
            _isExpandingFullDiff = false;
        }
        StateHasChanged();
    }

    private void ComputeDiff()
    {
        _diffChunks.Clear();
        _totalChanges = 0;

        if (string.IsNullOrEmpty(OldText) && string.IsNullOrEmpty(NewText))
            return;

        var diffBuilder = new InlineDiffBuilder(new Differ());
        var diff = diffBuilder.BuildDiffModel(OldText ?? string.Empty, NewText ?? string.Empty);

        var allLines = new List<DiffLine>();
        int oldLineNum = 0;
        int newLineNum = 0;

        foreach (var line in diff.Lines)
        {
            var diffLine = new DiffLine
            {
                Text = line.Text,
                Type = line.Type
            };

            switch (line.Type)
            {
                case ChangeType.Unchanged:
                    oldLineNum++;
                    newLineNum++;
                    diffLine.OldLineNumber = oldLineNum;
                    diffLine.NewLineNumber = newLineNum;
                    break;
                case ChangeType.Deleted:
                    oldLineNum++;
                    diffLine.OldLineNumber = oldLineNum;
                    diffLine.NewLineNumber = 0;
                    _totalChanges++;
                    break;
                case ChangeType.Inserted:
                    newLineNum++;
                    diffLine.OldLineNumber = 0;
                    diffLine.NewLineNumber = newLineNum;
                    _totalChanges++;
                    break;
                case ChangeType.Modified:
                    oldLineNum++;
                    newLineNum++;
                    diffLine.OldLineNumber = oldLineNum;
                    diffLine.NewLineNumber = newLineNum;
                    _totalChanges++;
                    break;
            }

            allLines.Add(diffLine);
        }

        // Now extract chunks with context
        var changedIndices = new HashSet<int>();
        for (int i = 0; i < allLines.Count; i++)
        {
            if (allLines[i].Type != ChangeType.Unchanged)
            {
                // Mark this line and surrounding context lines
                for (int j = Math.Max(0, i - ContextLines); j <= Math.Min(allLines.Count - 1, i + ContextLines); j++)
                {
                    changedIndices.Add(j);
                }
            }
        }

        if (changedIndices.Count == 0)
            return;

        // Group consecutive indices into chunks
        var sortedIndices = changedIndices.OrderBy(x => x).ToList();
        var currentChunk = new DiffChunk();
        int? lastIndex = null;

        foreach (var idx in sortedIndices)
        {
            if (lastIndex.HasValue && idx > lastIndex.Value + 1)
            {
                // Gap detected, start new chunk
                if (currentChunk.Lines.Count > 0)
                {
                    FinalizeChunk(currentChunk);
                    _diffChunks.Add(currentChunk);
                }
                currentChunk = new DiffChunk();
            }

            currentChunk.Lines.Add(allLines[idx]);
            lastIndex = idx;
        }

        // Add the last chunk
        if (currentChunk.Lines.Count > 0)
        {
            FinalizeChunk(currentChunk);
            _diffChunks.Add(currentChunk);
        }
    }

    private void ComputeSideBySideDiff()
    {
        _sideBySideChunks.Clear();

        if (string.IsNullOrEmpty(OldText) && string.IsNullOrEmpty(NewText))
            return;

        var diffBuilder = new SideBySideDiffBuilder(new Differ());
        var diff = diffBuilder.BuildDiffModel(OldText ?? string.Empty, NewText ?? string.Empty);

        var allRows = new List<SideBySideRow>();
        int maxLines = Math.Max(diff.OldText.Lines.Count, diff.NewText.Lines.Count);

        for (int i = 0; i < maxLines; i++)
        {
            var oldLine = i < diff.OldText.Lines.Count ? diff.OldText.Lines[i] : null;
            var newLine = i < diff.NewText.Lines.Count ? diff.NewText.Lines[i] : null;

            allRows.Add(new SideBySideRow
            {
                OldLineNumber = oldLine?.Position ?? 0,
                OldText = oldLine?.Text ?? string.Empty,
                OldType = oldLine?.Type ?? ChangeType.Imaginary,
                NewLineNumber = newLine?.Position ?? 0,
                NewText = newLine?.Text ?? string.Empty,
                NewType = newLine?.Type ?? ChangeType.Imaginary
            });
        }

        // Find indices with changes
        var changedIndices = new HashSet<int>();
        for (int i = 0; i < allRows.Count; i++)
        {
            var row = allRows[i];
            if (row.OldType != ChangeType.Unchanged || row.NewType != ChangeType.Unchanged)
            {
                for (int j = Math.Max(0, i - ContextLines); j <= Math.Min(allRows.Count - 1, i + ContextLines); j++)
                {
                    changedIndices.Add(j);
                }
            }
        }

        if (changedIndices.Count == 0)
            return;

        // Group into chunks
        var sortedIndices = changedIndices.OrderBy(x => x).ToList();
        var currentChunk = new SideBySideChunk();
        int? lastIndex = null;

        foreach (var idx in sortedIndices)
        {
            if (lastIndex.HasValue && idx > lastIndex.Value + 1)
            {
                if (currentChunk.Rows.Count > 0)
                {
                    FinalizeSbsChunk(currentChunk);
                    _sideBySideChunks.Add(currentChunk);
                }
                currentChunk = new SideBySideChunk();
            }

            currentChunk.Rows.Add(allRows[idx]);
            lastIndex = idx;
        }

        if (currentChunk.Rows.Count > 0)
        {
            FinalizeSbsChunk(currentChunk);
            _sideBySideChunks.Add(currentChunk);
        }
    }

    private void FinalizeChunk(DiffChunk chunk)
    {
        var firstLine = chunk.Lines.FirstOrDefault();

        if (firstLine != null)
        {
            chunk.OldStartLine = firstLine.OldLineNumber > 0 ? firstLine.OldLineNumber :
                chunk.Lines.FirstOrDefault(l => l.OldLineNumber > 0)?.OldLineNumber ?? 0;
            chunk.NewStartLine = firstLine.NewLineNumber > 0 ? firstLine.NewLineNumber :
                chunk.Lines.FirstOrDefault(l => l.NewLineNumber > 0)?.NewLineNumber ?? 0;
        }

        chunk.OldLineCount = chunk.Lines.Count(l => l.OldLineNumber > 0);
        chunk.NewLineCount = chunk.Lines.Count(l => l.NewLineNumber > 0);
    }

    private void FinalizeSbsChunk(SideBySideChunk chunk)
    {
        var firstRow = chunk.Rows.FirstOrDefault();
        if (firstRow != null)
        {
            chunk.OldStartLine = firstRow.OldLineNumber > 0 ? firstRow.OldLineNumber :
                chunk.Rows.FirstOrDefault(r => r.OldLineNumber > 0)?.OldLineNumber ?? 0;
            chunk.NewStartLine = firstRow.NewLineNumber > 0 ? firstRow.NewLineNumber :
                chunk.Rows.FirstOrDefault(r => r.NewLineNumber > 0)?.NewLineNumber ?? 0;
        }

        chunk.OldLineCount = chunk.Rows.Count(r => r.OldLineNumber > 0);
        chunk.NewLineCount = chunk.Rows.Count(r => r.NewLineNumber > 0);
    }

    private static string GetLineClass(ChangeType type) => type switch
    {
        ChangeType.Deleted => "diff-line-deleted",
        ChangeType.Inserted => "diff-line-inserted",
        ChangeType.Modified => "diff-line-modified",
        _ => "diff-line-context"
    };

    private static string GetLinePrefix(ChangeType type) => type switch
    {
        ChangeType.Deleted => "-",
        ChangeType.Inserted => "+",
        ChangeType.Modified => "~",
        _ => " "
    };

    private static string GetSbsLineClass(ChangeType type) => type switch
    {
        ChangeType.Deleted => "sbs-deleted",
        ChangeType.Inserted => "sbs-inserted",
        ChangeType.Modified => "sbs-deleted", // Modified shows as change on both sides
        ChangeType.Imaginary => "sbs-empty",
        _ => "sbs-unchanged"
    };

    private class DiffChunk
    {
        public List<DiffLine> Lines { get; set; } = new();
        public int OldStartLine { get; set; }
        public int NewStartLine { get; set; }
        public int OldLineCount { get; set; }
        public int NewLineCount { get; set; }
    }

    private class DiffLine
    {
        public string Text { get; set; } = string.Empty;
        public ChangeType Type { get; set; }
        public int OldLineNumber { get; set; }
        public int NewLineNumber { get; set; }
    }

    private class SideBySideChunk
    {
        public List<SideBySideRow> Rows { get; set; } = new();
        public int OldStartLine { get; set; }
        public int NewStartLine { get; set; }
        public int OldLineCount { get; set; }
        public int NewLineCount { get; set; }
    }

    private class SideBySideRow
    {
        public int OldLineNumber { get; set; }
        public string OldText { get; set; } = string.Empty;
        public ChangeType OldType { get; set; }
        public int NewLineNumber { get; set; }
        public string NewText { get; set; } = string.Empty;
        public ChangeType NewType { get; set; }
    }
}
