namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Models
{
    public class ComparisonResult<T>
    {
        public List<T> ExistsOnlyInTarget { get; set; } = new();
        public List<T> ExistsOnlyInSource { get; set; } = new();
        public List<ComparisonDifference<T>> Differences { get; set; } = new();
        public List<T> Identical { get; set; } = new();
    }

    public class ComparisonDifference<T>
    {
        public T SourceItem { get; set; }
        public T TargetItem { get; set; }

        public ComparisonDifference(T sourceItem, T targetItem)
        {
            SourceItem = sourceItem;
            TargetItem = targetItem;
        }
    }
}