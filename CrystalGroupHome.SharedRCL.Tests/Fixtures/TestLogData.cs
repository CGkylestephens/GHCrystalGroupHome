namespace CrystalGroupHome.SharedRCL.Tests.Fixtures;

/// <summary>
/// Provides static test log samples for testing.
/// </summary>
public static class TestLogData
{
    public static string SimpleRegenLog => @"Thursday, February 5, 2026 11:59:02
23:59:02 MRP Regeneration process begin
Site List -> MfgSys
Date: 2/5/2026
01:00:46 Building Pegging Demand Master...
01:05:07 Processing Part:ABC123, Attribute Set:''
02:30:15 MRP process complete";

    public static string SimpleNetChangeLog => @"Thursday, February 5, 2026 14:00:00
14:00:00 MRP Net Change process begin
Site List -> MfgSys
Date: 2/5/2026
14:01:00 Start Processing Part:XYZ789, Attribute Set:''
14:05:00 MRP process complete";

    public static string LogWithErrors => @"Thursday, February 5, 2026 01:00:00
01:00:00 MRP Regeneration process begin
Site List -> PLANT01
01:00:46 Building Pegging Demand Master...
01:05:07 Processing Part:ABC123, Attribute Set:''
01:10:23 ERROR: Job 14567 abandoned due to timeout
01:15:00 Processing continues";

    public static string LogWithMultipleHealthFlags => @"Thursday, February 5, 2026 01:00:00
01:00:00 MRP Regeneration process begin
Site List -> PLANT01
01:00:46 Building Pegging Demand Master...
01:05:07 ERROR: Database connection failed
01:10:23 Job 14567 abandoned due to timeout
01:15:00 Part ABC123 is defunct
01:20:00 Process failed";

    public static string IncompleteLog => @"Thursday, February 5, 2026 01:00:00
01:00:00 MRP Regeneration process begin
Site List -> PLANT01
01:00:46 Building Pegging Demand Master...
01:05:07 Processing Part:ABC123, Attribute Set:''";

    public static string EmptyLog => string.Empty;

    public static string LogWithContextualDate => @"System.Collections.Hashtable
==== Normal Planning Entries ====
Date: 6/15/2026
01:00:46 Building Pegging Demand Master...
01:05:07 Demand: S: 100516/1/1 Date: 6/15/2026 Quantity: 4.00000000
01:05:08 Supply: J: U0000000000273/0/0 Date: 6/15/2026 Quantity: 4.00000000";

    public static string LogWithMultipleParts => @"Thursday, February 5, 2026 01:00:00
01:00:00 MRP Regeneration process begin
Site List -> MfgSys
Date: 2/5/2026
01:00:46 Building Pegging Demand Master...
01:01:00 Processing Part:ABC123, Attribute Set:''
01:02:00 Processing Part:XYZ789, Attribute Set:''
01:03:00 Processing Part:DEF456, Attribute Set:''
01:30:00 MRP process complete";
}
