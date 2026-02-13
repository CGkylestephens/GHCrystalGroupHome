namespace CrystalGroupHome.SharedRCL.Core;

/// <summary>
/// Types of differences that can be detected between MRP runs
/// </summary>
public enum DifferenceType
{
    JobRemoved,
    JobAdded,
    DateShifted,
    QuantityChanged,
    ErrorAppeared,
    ErrorResolved,
    Other
}
