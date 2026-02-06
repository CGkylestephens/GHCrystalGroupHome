using CrystalGroupHome.Internal.Features.EnvironmentComparer.Models;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Data
{
    public interface IEnvironmentComparisonService
    {
        Task<ComparisonResult<BaqDefinition>> CompareBaqsAsync(string sourceEnvironment, string targetEnvironment);
        Task<ComparisonResult<BpmDirectiveDefinition>> CompareBpmsAsync(string sourceEnvironment, string targetEnvironment);
        Task<ComparisonResult<UDColumnDTO>> CompareUDColumnsAsync(string sourceEnvironment, string targetEnvironment);
        Task<ApplicationLayerComparisonResult> CompareApplicationLayersAsync(string sourceEnvironment, string targetEnvironment);
    }

    /// <summary>
    /// Extended comparison result for application layers that categorizes differences by type
    /// </summary>
    public class ApplicationLayerComparisonResult
    {
        /// <summary>
        /// Layers that are completely identical (same published AND same draft content)
        /// </summary>
        public List<ApplicationLayerDefinition> Identical { get; set; } = new();

        /// <summary>
        /// Layers where only published content differs
        /// </summary>
        public List<ComparisonDifference<ApplicationLayerDefinition>> PublishedDifferences { get; set; } = new();

        /// <summary>
        /// Layers where only draft content differs (one or both have unpublished changes)
        /// </summary>
        public List<ComparisonDifference<ApplicationLayerDefinition>> DraftDifferences { get; set; } = new();

        /// <summary>
        /// Layers where both published and draft content differ
        /// </summary>
        public List<ComparisonDifference<ApplicationLayerDefinition>> BothDifferences { get; set; } = new();

        /// <summary>
        /// Layers that exist only in the source environment
        /// </summary>
        public List<ApplicationLayerDefinition> ExistsOnlyInSource { get; set; } = new();

        /// <summary>
        /// Layers that exist only in the target environment
        /// </summary>
        public List<ApplicationLayerDefinition> ExistsOnlyInTarget { get; set; } = new();

        /// <summary>
        /// Total count of all differences
        /// </summary>
        public int TotalDifferenceCount => PublishedDifferences.Count + DraftDifferences.Count + BothDifferences.Count;

        /// <summary>
        /// All differences combined (for backward compatibility or simple iteration)
        /// </summary>
        public IEnumerable<ComparisonDifference<ApplicationLayerDefinition>> AllDifferences =>
            PublishedDifferences.Concat(DraftDifferences).Concat(BothDifferences);
    }

    public class EnvironmentComparisonService : IEnvironmentComparisonService
    {
        private readonly IEpicorCustomizationService _customizationService;
        private readonly ILogger<EnvironmentComparisonService> _logger;

        public EnvironmentComparisonService(IEpicorCustomizationService customizationService, ILogger<EnvironmentComparisonService> logger)
        {
            _customizationService = customizationService;
            _logger = logger;
        }

        public async Task<ComparisonResult<BaqDefinition>> CompareBaqsAsync(string sourceEnvironment, string targetEnvironment)
        {
            var result = new ComparisonResult<BaqDefinition>();

            try
            {
                _logger.LogInformation("Starting BAQ comparison between {Source} and {Target}", sourceEnvironment, targetEnvironment);

                // Fetch BAQs from both environments in parallel
                var sourceTask = _customizationService.GetBaqsAsync(sourceEnvironment);
                var targetTask = _customizationService.GetBaqsAsync(targetEnvironment);

                await Task.WhenAll(sourceTask, targetTask);

                var sourceBaqs = await sourceTask;
                var targetBaqs = await targetTask;

                _logger.LogInformation("Retrieved {SourceCount} BAQs from {Source} and {TargetCount} BAQs from {Target}", 
                    sourceBaqs.Count, sourceEnvironment, targetBaqs.Count, targetEnvironment);

                // Use concurrent collections for better performance with large datasets
                var sourceBaqDict = sourceBaqs.AsParallel().ToDictionary(b => b.QueryID);
                var targetBaqDict = targetBaqs.AsParallel().ToDictionary(b => b.QueryID);

                // Process comparisons in parallel for better performance
                var comparisonTasks = sourceBaqs.AsParallel().Select(sourceBaq =>
                {
                    if (targetBaqDict.TryGetValue(sourceBaq.QueryID, out var targetBaq))
                    {
                        // Item exists in both, check for modification
                        if (sourceBaq.ContentHash != targetBaq.ContentHash)
                        {
                            return new { Type = "Different", Source = sourceBaq, Target = (BaqDefinition?)targetBaq };
                        }
                        else
                        {
                            return new { Type = "Identical", Source = sourceBaq, Target = (BaqDefinition?)null };
                        }
                    }
                    else
                    {
                        // Item is in source but not target (removed)
                        return new { Type = "OnlyInSource", Source = sourceBaq, Target = (BaqDefinition?)null };
                    }
                }).ToList();

                // Process results
                foreach (var comparison in comparisonTasks)
                {
                    switch (comparison.Type)
                    {
                        case "Different":
                            result.Differences.Add(new ComparisonDifference<BaqDefinition>(comparison.Source, comparison.Target!));
                            targetBaqDict.Remove(comparison.Source.QueryID);
                            break;
                        case "Identical":
                            result.Identical.Add(comparison.Source);
                            targetBaqDict.Remove(comparison.Source.QueryID);
                            break;
                        case "OnlyInSource":
                            result.ExistsOnlyInSource.Add(comparison.Source);
                            break;
                    }
                }

                // Add remaining items (only in target)
                result.ExistsOnlyInTarget.AddRange(targetBaqDict.Values);

                _logger.LogInformation("BAQ comparison completed: {Identical} identical, {Different} different, {OnlySource} only in source, {OnlyTarget} only in target",
                    result.Identical.Count, result.Differences.Count, result.ExistsOnlyInSource.Count, result.ExistsOnlyInTarget.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while comparing BAQs between {Source} and {Target}", sourceEnvironment, targetEnvironment);
                throw;
            }

            return result;
        }

        public async Task<ComparisonResult<BpmDirectiveDefinition>> CompareBpmsAsync(string sourceEnvironment, string targetEnvironment)
        {
            var result = new ComparisonResult<BpmDirectiveDefinition>();

            try
            {
                _logger.LogInformation("Starting BPM Directive comparison between {Source} and {Target}", sourceEnvironment, targetEnvironment);

                // Fetch BPM Directives from both environments in parallel
                var sourceTask = _customizationService.GetBpmDirectivesAsync(sourceEnvironment);
                var targetTask = _customizationService.GetBpmDirectivesAsync(targetEnvironment);

                await Task.WhenAll(sourceTask, targetTask);

                var sourceDirectives = await sourceTask;
                var targetDirectives = await targetTask;

                _logger.LogInformation("Retrieved {SourceCount} BPM Directives from {Source} and {TargetCount} BPM Directives from {Target}", 
                    sourceDirectives.Count, sourceEnvironment, targetDirectives.Count, targetEnvironment);

                // Use concurrent collections for better performance with large datasets
                var sourceDirectiveDict = sourceDirectives.AsParallel().ToDictionary(d => d.GetUniqueIdentifier());
                var targetDirectiveDict = targetDirectives.AsParallel().ToDictionary(d => d.GetUniqueIdentifier());

                // Process comparisons in parallel for better performance
                var comparisonTasks = sourceDirectives.AsParallel().Select(sourceDirective =>
                {
                    var directiveId = sourceDirective.GetUniqueIdentifier();

                    if (targetDirectiveDict.TryGetValue(directiveId, out var targetDirective))
                    {
                        // Item exists in both, check for modification
                        if (sourceDirective.ContentHash != targetDirective.ContentHash)
                        {
                            return new { Type = "Different", Source = sourceDirective, Target = (BpmDirectiveDefinition?)targetDirective, Id = directiveId };
                        }
                        else
                        {
                            return new { Type = "Identical", Source = sourceDirective, Target = (BpmDirectiveDefinition?)null, Id = directiveId };
                        }
                    }
                    else
                    {
                        // Item is in source but not target (removed)
                        return new { Type = "OnlyInSource", Source = sourceDirective, Target = (BpmDirectiveDefinition?)null, Id = directiveId };
                    }
                }).ToList();

                // Process results
                foreach (var comparison in comparisonTasks)
                {
                    switch (comparison.Type)
                    {
                        case "Different":
                            result.Differences.Add(new ComparisonDifference<BpmDirectiveDefinition>(comparison.Source, comparison.Target!));
                            targetDirectiveDict.Remove(comparison.Id);
                            break;
                        case "Identical":
                            result.Identical.Add(comparison.Source);
                            targetDirectiveDict.Remove(comparison.Id);
                            break;
                        case "OnlyInSource":
                            result.ExistsOnlyInSource.Add(comparison.Source);
                            break;
                    }
                }

                // Add remaining items (only in target)
                result.ExistsOnlyInTarget.AddRange(targetDirectiveDict.Values);

                _logger.LogInformation("BPM Directive comparison completed: {Identical} identical, {Different} different, {OnlySource} only in source, {OnlyTarget} only in target",
                    result.Identical.Count, result.Differences.Count, result.ExistsOnlyInSource.Count, result.ExistsOnlyInTarget.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while comparing BPM Directives between {Source} and {Target}", sourceEnvironment, targetEnvironment);
                throw;
            }

            return result;
        }

        public async Task<ComparisonResult<UDColumnDTO>> CompareUDColumnsAsync(string sourceEnvironment, string targetEnvironment)
        {
            var result = new ComparisonResult<UDColumnDTO>();

            try
            {
                _logger.LogInformation("Starting UD Column comparison between {Source} and {Target}", sourceEnvironment, targetEnvironment);

                // Fetch UD Columns from both environments in parallel
                var sourceTask = _customizationService.GetUDColumnsAsync(sourceEnvironment);
                var targetTask = _customizationService.GetUDColumnsAsync(targetEnvironment);

                await Task.WhenAll(sourceTask, targetTask);

                var sourceColumns = await sourceTask;
                var targetColumns = await targetTask;

                _logger.LogInformation("Retrieved {SourceCount} UD Columns from {Source} and {TargetCount} UD Columns from {Target}", 
                    sourceColumns.Count, sourceEnvironment, targetColumns.Count, targetEnvironment);

                // Create dictionaries for comparison using Table.Column as the key
                var sourceColumnDict = sourceColumns.AsParallel().ToDictionary(c => $"{c.TableName}.{c.ColumnName}");
                var targetColumnDict = targetColumns.AsParallel().ToDictionary(c => $"{c.TableName}.{c.ColumnName}");

                // Process comparisons in parallel for better performance
                var comparisonTasks = sourceColumns.AsParallel().Select(sourceColumn =>
                {
                    var columnKey = $"{sourceColumn.TableName}.{sourceColumn.ColumnName}";

                    if (targetColumnDict.TryGetValue(columnKey, out var targetColumn))
                    {
                        // Column exists in both environments (identical)
                        return new { Type = "Identical", Source = sourceColumn, Target = (UDColumnDTO?)targetColumn, Key = columnKey };
                    }
                    else
                    {
                        // Column is in source but not target
                        return new { Type = "OnlyInSource", Source = sourceColumn, Target = (UDColumnDTO?)null, Key = columnKey };
                    }
                }).ToList();

                // Process results
                foreach (var comparison in comparisonTasks)
                {
                    switch (comparison.Type)
                    {
                        case "Identical":
                            result.Identical.Add(comparison.Source);
                            targetColumnDict.Remove(comparison.Key);
                            break;
                        case "OnlyInSource":
                            result.ExistsOnlyInSource.Add(comparison.Source);
                            break;
                    }
                }

                // Add remaining columns (only in target)
                result.ExistsOnlyInTarget.AddRange(targetColumnDict.Values);

                // UD Columns are structural - they're either present or not, so no differences category

                _logger.LogInformation("UD Column comparison completed: {Identical} identical, {OnlySource} only in source, {OnlyTarget} only in target",
                    result.Identical.Count, result.ExistsOnlyInSource.Count, result.ExistsOnlyInTarget.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while comparing UD Columns between {Source} and {Target}", sourceEnvironment, targetEnvironment);
                throw;
            }

            return result;
        }

        public async Task<ApplicationLayerComparisonResult> CompareApplicationLayersAsync(string sourceEnvironment, string targetEnvironment)
        {
            var result = new ApplicationLayerComparisonResult();

            try
            {
                _logger.LogInformation("Starting Application Layer comparison between {Source} and {Target}", sourceEnvironment, targetEnvironment);

                // Fetch Application Layers from both environments in parallel
                var sourceTask = _customizationService.GetApplicationLayersAsync(sourceEnvironment);
                var targetTask = _customizationService.GetApplicationLayersAsync(targetEnvironment);

                await Task.WhenAll(sourceTask, targetTask);

                var sourceLayers = await sourceTask;
                var targetLayers = await targetTask;

                _logger.LogInformation("Retrieved {SourceCount} Application Layers from {Source} and {TargetCount} Application Layers from {Target}", 
                    sourceLayers.Count, sourceEnvironment, targetLayers.Count, targetEnvironment);

                // Use dictionaries for efficient lookup
                var sourceLayerDict = sourceLayers.ToDictionary(l => l.GetUniqueIdentifier());
                var targetLayerDict = targetLayers.ToDictionary(l => l.GetUniqueIdentifier());
                var processedTargetIds = new HashSet<string>();

                // Compare each source layer
                foreach (var sourceLayer in sourceLayers)
                {
                    var layerId = sourceLayer.GetUniqueIdentifier();

                    if (targetLayerDict.TryGetValue(layerId, out var targetLayer))
                    {
                        processedTargetIds.Add(layerId);

                        // Determine the type of difference
                        var differenceType = sourceLayer.GetDifferenceType(targetLayer);

                        switch (differenceType)
                        {
                            case LayerDifferenceType.None:
                                result.Identical.Add(sourceLayer);
                                break;
                            case LayerDifferenceType.PublishedOnly:
                                result.PublishedDifferences.Add(new ComparisonDifference<ApplicationLayerDefinition>(sourceLayer, targetLayer));
                                break;
                            case LayerDifferenceType.DraftOnly:
                                result.DraftDifferences.Add(new ComparisonDifference<ApplicationLayerDefinition>(sourceLayer, targetLayer));
                                break;
                            case LayerDifferenceType.Both:
                                result.BothDifferences.Add(new ComparisonDifference<ApplicationLayerDefinition>(sourceLayer, targetLayer));
                                break;
                        }
                    }
                    else
                    {
                        // Layer exists only in source
                        result.ExistsOnlyInSource.Add(sourceLayer);
                    }
                }

                // Add layers that exist only in target
                foreach (var targetLayer in targetLayers)
                {
                    var layerId = targetLayer.GetUniqueIdentifier();
                    if (!processedTargetIds.Contains(layerId))
                    {
                        result.ExistsOnlyInTarget.Add(targetLayer);
                    }
                }

                _logger.LogInformation(
                    "Application Layer comparison completed: {Identical} identical, {Published} published differences, {Draft} draft differences, {Both} both, {OnlySource} only in source, {OnlyTarget} only in target",
                    result.Identical.Count, 
                    result.PublishedDifferences.Count, 
                    result.DraftDifferences.Count, 
                    result.BothDifferences.Count,
                    result.ExistsOnlyInSource.Count, 
                    result.ExistsOnlyInTarget.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while comparing Application Layers between {Source} and {Target}", sourceEnvironment, targetEnvironment);
                throw;
            }

            return result;
        }
    }
}