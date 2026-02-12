using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Core;

/// <summary>
/// Represents a complete MRP log document with metadata and entries
/// </summary>
public class MrpLogDocument
{
    public MrpRunMetadata Metadata { get; set; } = new();
    public List<MrpLogEntry> Entries { get; set; } = new();
}
