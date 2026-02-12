# Epicor MRP Assistant - Project Documentation

This directory contains the complete implementation plan and issue specifications for the **Epicor MRP Log Investigation & Comparison Assistant**.

## 📂 Directory Structure

```
docs/
├── README.md (this file)
├── MRP_ASSISTANT_PROJECT_PLAN.md         # Master technical plan
├── MRP_ASSISTANT_ISSUES_QUICK_REFERENCE.md  # Issue creation guide
└── issues/                                # Individual issue specifications
    ├── ISSUE_01_Bootstrap_Project.md
    ├── ISSUE_02_Implement_Parser.md
    ├── ISSUE_03_Build_Comparison_Engine.md
    ├── ISSUE_04_Create_Explanation_Engine.md
    ├── ISSUE_05_Implement_Report_Generator.md
    ├── ISSUE_06_Add_CLI_Interface.md
    ├── ISSUE_07_Create_Unit_Tests.md
    └── ISSUE_08_Add_Integration_Tests.md
```

## 🎯 What This Is

This is a **complete project breakdown** for building the MRP Assistant as a .NET class library and CLI tool. The project has been divided into **8 agent-executable issues** following the WRAP pattern (Well-written, Atomic, Paired with agent).

## 📖 Key Documents

### 1. Master Project Plan
**File**: `MRP_ASSISTANT_PROJECT_PLAN.md`

Comprehensive technical specification including:
- Project overview and goals
- Complete task breakdown (8 issues)
- Execution order and dependencies
- Detailed specifications for each component
- Code structure examples
- Acceptance criteria
- Success metrics

**Read this first** to understand the complete vision.

### 2. Issues Quick Reference
**File**: `MRP_ASSISTANT_ISSUES_QUICK_REFERENCE.md`

Practical guide for creating GitHub issues:
- Issue summary table
- File locations
- Execution order diagram
- Multiple creation methods (manual, CLI, API)
- Validation steps
- Success criteria

**Use this** to actually create the GitHub issues.

### 3. Individual Issue Files
**Directory**: `issues/`

Each issue is a complete, standalone specification that can be:
- Copy-pasted into GitHub issue creation form
- Used as input for GitHub CLI/API
- Assigned directly to GitHub Copilot Agent

Each issue includes:
- Clear task intent
- Required input files
- Expected output (with code examples)
- Acceptance criteria
- Validation commands
- Dependencies
- Estimated effort

## 🚀 Quick Start Guide

### For Project Managers

1. **Review the plan**: Read `MRP_ASSISTANT_PROJECT_PLAN.md`
2. **Create issues**: Follow `MRP_ASSISTANT_ISSUES_QUICK_REFERENCE.md`
3. **Assign to agent**: Use `@copilot` as assignee
4. **Monitor progress**: Track via GitHub Projects board

### For Developers

1. **Understand scope**: Read `MRP_ASSISTANT_PROJECT_PLAN.md`
2. **Pick an issue**: Start with Issue #1 (Bootstrap)
3. **Follow specs**: Each issue in `issues/` folder is detailed
4. **Validate**: Run tests and validation commands
5. **Move to next**: Complete issues sequentially

### For GitHub Copilot Agent

1. **Receive assignment**: Issue will reference this documentation
2. **Read issue spec**: Full details in `issues/ISSUE_XX_*.md`
3. **Access resources**: Test data in `/testdata/`, guidelines in `/copilot-instructions.md`
4. **Execute task**: Follow acceptance criteria
5. **Validate**: Run specified validation commands

## 📋 The 8 Issues at a Glance

| # | Title | What It Does | Time |
|---|-------|--------------|------|
| 1 | Bootstrap Project | Create .NET projects, folder structure | 30m |
| 2 | Implement Parser | Extract data from MRP log files | 2-3h |
| 3 | Build Comparer | Detect differences between logs | 2-3h |
| 4 | Create Explainer | Generate FACT vs INFERENCE explanations | 2-3h |
| 5 | Build Reporter | Format output as planner-friendly reports | 2h |
| 6 | Add CLI | Command-line interface (compare, parse, demo) | 1-2h |
| 7 | Unit Tests | Test parser, comparer, explainer | 2-3h |
| 8 | Integration Tests | End-to-end pipeline validation | 1-2h |

**Total**: 12-18 hours of development work

## 🔗 Dependencies

```
Issue 1 → Issue 2 → Issue 3 → Issue 5 → Issue 6 → Issue 7 → Issue 8
                  ↘ Issue 4 ↗
```

- Issues must be completed **sequentially** (each builds on previous)
- After Issue 2, Issues 3-5 have some parallel potential
- Issues 7-8 require all core components complete

## ✅ How to Validate Success

After all 8 issues complete:

```bash
# Build solution
dotnet build CrystalGroupHome.sln

# Run tests
dotnet test

# Generate demo report
dotnet run --project MRP.Assistant.CLI -- demo

# Compare two logs
dotnet run --project MRP.Assistant.CLI -- compare \
  --run-a testdata/mrp_log_sample_A.txt \
  --run-b testdata/mrp_log_sample_B.txt \
  --output my_report.md
```

**Success criteria**:
- ✅ All commands succeed (exit code 0)
- ✅ Tests pass (green output)
- ✅ Demo report generated
- ✅ Report contains 5 sections (A-E)
- ✅ FACT and INFERENCE clearly separated

## 📚 Related Resources

In this repository:
- `/testdata/` - Sample MRP log files for testing
- `/copilot-instructions.md` - Agent behavior guidelines
- `CrystalGroupHome.sln` - Existing solution file

External:
- [MRP Troubleshooter.docx](https://github.com/user-attachments/files/25140287/MRP.Troubleshooter.docx) - Original requirements document
- Epicor Kinetic documentation (if needed for domain context)

## 🎯 Project Goals

**Primary Goal**: Build a tool that helps planners understand MRP log differences

**Key Features**:
- Parse noisy, unstructured MRP logs
- Compare two MRP runs (regen vs net change)
- Detect meaningful differences (jobs, dates, quantities, errors)
- Explain *why* changes occurred
- Distinguish FACT (log evidence) from INFERENCE (likely cause)
- Generate readable, actionable reports

**Target Users**: Production planners and IT teams (non-developers)

## 💡 Design Principles

1. **Planner-First**: Output must be readable by non-technical users
2. **Evidence-Based**: Always cite log sources (FACT vs INFERENCE)
3. **Actionable**: Provide "Next Steps in Epicor" guidance
4. **Robust**: Handle malformed logs gracefully
5. **Testable**: Comprehensive unit and integration tests
6. **Maintainable**: Clear code structure, good documentation

## 🛠️ Technology Stack

- **.NET 9** - Target framework
- **C#** - Primary language
- **xUnit** - Testing framework
- **FluentAssertions** - Readable test assertions
- **McMaster.Extensions.CommandLineUtils** - CLI framework
- **Console application** - Initial delivery (Blazor backend optional future)

## 📝 Notes

- **Sequential execution recommended**: Each issue builds on the previous
- **Test data included**: Real MRP log samples in `/testdata/`
- **Agent-ready**: All issues designed for GitHub Copilot Agent
- **Extensible**: Architecture supports future enhancements (web UI, database, etc.)

## 🙋 Questions?

- **Technical details**: See `MRP_ASSISTANT_PROJECT_PLAN.md`
- **Issue creation**: See `MRP_ASSISTANT_ISSUES_QUICK_REFERENCE.md`
- **Specific issue**: See `issues/ISSUE_XX_*.md`
- **Domain context**: See `/copilot-instructions.md`

---

**Created**: 2026-02-06  
**Version**: 1.0  
**Status**: Ready for Implementation  
**Maintainer**: GitHub Copilot Agent + Human Project Manager
