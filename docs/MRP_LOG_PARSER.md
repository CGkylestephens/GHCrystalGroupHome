# MRP Log Parser

## Overview

The MRP Log Parser is a service for extracting metadata from Epicor MRP (Material Requirements Planning) log files. It parses log files and extracts key run information for analysis and reporting.

## Components

### MrpRunMetadata Model

Located in `CrystalGroupHome.SharedRCL/Models/MrpRunMetadata.cs`

A strongly-typed class containing:
- **Site**: The site name where the MRP run occurred (e.g., "PLANT01", "MfgSys")
- **StartTime**: The timestamp when the MRP run started
- **EndTime**: The timestamp when the MRP run ended (may be null if incomplete)
- **RunType**: The type of MRP run - "regen" (regeneration), "net change", or "unknown"
- **Status**: The run status - "success", "failed", "incomplete", or "uncertain"
- **HealthFlags**: List of health indicators like "error", "timeout", "abandoned", "defunct", "failed"

### MrpLogParser Service

Located in `CrystalGroupHome.SharedRCL/Services/MrpLogParser.cs`

Provides methods to parse MRP log files:
- `ParseLogFileAsync(string filePath)`: Parses a log file from disk
- `ParseLogContent(IEnumerable<string> lines)`: Parses log content from lines

## Usage

### Basic Example

```csharp
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Models;

var parser = new MrpLogParser();

// Parse a log file
MrpRunMetadata metadata = await parser.ParseLogFileAsync("path/to/mrp_log.txt");

// Access the metadata
Console.WriteLine($"Site: {metadata.Site}");
Console.WriteLine($"Start Time: {metadata.StartTime}");
Console.WriteLine($"End Time: {metadata.EndTime}");
Console.WriteLine($"Run Type: {metadata.RunType}");
Console.WriteLine($"Status: {metadata.Status}");
Console.WriteLine($"Health Flags: {string.Join(", ", metadata.HealthFlags)}");
```

### Parse from String Content

```csharp
var parser = new MrpLogParser();
string[] logLines = File.ReadAllLines("path/to/log.txt");

MrpRunMetadata metadata = parser.ParseLogContent(logLines);
```

## Supported Log Formats

The parser supports multiple MRP log formats:

1. **Simple format with explicit markers**:
   ```
   Start Time: 2024-02-01 02:00 UTC
   Site: PLANT01
   ...
   End Time: 2024-02-01 03:10 UTC
   ```

2. **Epicor MRP logs with date header**:
   ```
   Thursday, February 5, 2026 11:59:02
   23:59:02 MRP Regeneration process begin
   ...
   Site List -> MfgSys
   ```

3. **Pegging logs with contextual dates**:
   ```
   System.Collections.Hashtable
   ==== Normal Planning Entries ====
   01:00:46 Building Pegging Demand Master...
   ...
   Date: 6/15/2026
   ```

## Detection Rules

### Run Type Detection
- **Regen**: Detects "MRP Regen", "Regeneration", or "Building Pegging" operations
- **Net Change**: Detects "Net Change" keywords or "Start Processing Part" without pegging
- **Unknown**: When run type cannot be determined

### Status Detection
- **Failed**: When health flags indicate errors, failures, or abandoned runs
- **Incomplete**: When start time exists but no end time is found
- **Success**: When both start and end times exist with no error flags
- **Uncertain**: When status cannot be determined

### Health Flags
The parser automatically detects issues in the logs:
- **error**: Found "ERROR" keyword in logs
- **timeout**: Found "timeout" keyword
- **abandoned**: Found "abandoned" keyword
- **defunct**: Found "defunct" keyword
- **failed**: Found "failed" keyword

## Testing

Test files are located in `testdata/`:
- `mrp_log_sample_A.txt` - Failed regen run with errors
- `mrp_log_sample_B.txt` - Successful net change run
- `MRPRegenSample.txt` - Regen run with pegging operations
- `MRPNETChangeSample.txt` - Net change run
- `AUTOMRPREGEN2026_20260205_2359.log` - Real Epicor MRP log

## Integration

To use in your application:

1. Add a reference to `CrystalGroupHome.SharedRCL`
2. Inject or instantiate `MrpLogParser`
3. Call the appropriate parse method
4. Process the returned `MrpRunMetadata`

Example with dependency injection:

```csharp
// In Startup.cs or Program.cs
services.AddScoped<MrpLogParser>();

// In your component or service
public class MyService
{
    private readonly MrpLogParser _parser;

    public MyService(MrpLogParser parser)
    {
        _parser = parser;
    }

    public async Task<MrpRunMetadata> GetLogMetadata(string filePath)
    {
        return await _parser.ParseLogFileAsync(filePath);
    }
}
```
