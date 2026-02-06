---
name: Bootstrap MRP.Assistant Class Library Project
about: Create foundational project structure for MRP Log Investigation Assistant
title: "[Agent Task] Bootstrap MRP.Assistant Class Library Project"
labels: [infra, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Bootstrap the foundational .NET 9 class library and console application projects for the Epicor MRP Log Investigation Assistant. Set up the project structure that will support parsing, analysis, and reporting components.

## 🔍 Scope / Input
**Reference**: `/docs/MRP_ASSISTANT_PROJECT_PLAN.md` (full project context)

**Existing Resources**:
- Solution: `CrystalGroupHome.sln` (existing solution file)
- Test Data: `testdata/*.log`, `testdata/*.txt` (MRP log samples)
- Instructions: `copilot-instructions.md` (behavior guidelines)

**Projects to Create**:
1. **MRP.Assistant** - Core class library (.NET 9)
2. **MRP.Assistant.CLI** - Console application (.NET 9)
3. **MRP.Assistant.Tests** - xUnit test project (.NET 9)

## ✅ Expected Output

### 1. Project Structure
```
MRP.Assistant/
├── MRP.Assistant.csproj
├── Core/
│   └── .gitkeep
├── Parsers/
│   └── .gitkeep
├── Analysis/
│   └── .gitkeep
└── Reporting/
    └── .gitkeep

MRP.Assistant.CLI/
├── MRP.Assistant.CLI.csproj
└── Program.cs (with basic Main method)

MRP.Assistant.Tests/
├── MRP.Assistant.Tests.csproj
└── _README.md (test structure placeholder)
```

### 2. Solution Integration
- Add all 3 projects to `CrystalGroupHome.sln`
- MRP.Assistant.CLI references MRP.Assistant
- MRP.Assistant.Tests references MRP.Assistant
- All projects target .NET 9

### 3. Project Files
**MRP.Assistant.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

**MRP.Assistant.CLI.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\MRP.Assistant\MRP.Assistant.csproj" />
  </ItemGroup>
</Project>
```

**MRP.Assistant.Tests.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xUnit" Version="2.9.2" />
    <PackageReference Include="xUnit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\MRP.Assistant\MRP.Assistant.csproj" />
  </ItemGroup>
</Project>
```

### 4. README Documentation
Create `MRP.Assistant/README.md`:
```markdown
# MRP.Assistant - Epicor MRP Log Investigation Assistant

## Purpose
A .NET class library for parsing, analyzing, and comparing Epicor Kinetic MRP log files. 
Helps planners and IT teams understand what changed between MRP runs and why.

## Project Structure
- **/Core** - Domain models (MrpLogEntry, MrpLogDocument, etc.)
- **/Parsers** - Log parsing logic
- **/Analysis** - Comparison and explanation engines
- **/Reporting** - Report generation

## Usage
See MRP.Assistant.CLI for command-line interface.

## Dependencies
- .NET 9.0
- No external dependencies in core library

## Test Data
Sample MRP logs available in `/testdata/`
```

### 5. Basic Program.cs
```csharp
namespace MRP.Assistant.CLI;

public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("MRP.Assistant CLI - Epicor MRP Log Investigation Assistant");
        Console.WriteLine("Version 0.1.0");
        Console.WriteLine();
        Console.WriteLine("Run 'mrp-assistant --help' for usage information.");
        return 0;
    }
}
```

## 🧪 Acceptance Criteria
- [ ] All 3 projects created with correct structure
- [ ] Projects added to CrystalGroupHome.sln
- [ ] Solution builds successfully: `dotnet build CrystalGroupHome.sln`
- [ ] Folder structure matches specification (Core, Parsers, Analysis, Reporting)
- [ ] README.md created with project description
- [ ] Program.cs compiles and runs: `dotnet run --project MRP.Assistant.CLI`
- [ ] No build warnings or errors
- [ ] .gitignore entries added for bin/, obj/, *.user files in MRP projects

## 🧪 Validation Commands
```bash
# Build solution
dotnet build CrystalGroupHome.sln

# Run CLI (should show version message)
dotnet run --project MRP.Assistant.CLI/MRP.Assistant.CLI.csproj

# Verify test project structure
dotnet test MRP.Assistant.Tests/MRP.Assistant.Tests.csproj --list-tests
```

## 📝 Notes
- This is the foundation for all subsequent issues
- Keep dependencies minimal at this stage
- Ensure .NET 9 SDK is available
- No actual functionality yet - just scaffolding
- Next issue will implement the log parser
