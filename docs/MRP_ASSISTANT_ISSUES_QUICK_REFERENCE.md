# MRP Assistant - GitHub Issues Quick Reference

This document provides ready-to-use GitHub issue content for the Epicor MRP Log Investigation Assistant project.

## 📋 Issue List

All issues follow the WRAP pattern (Well-written, Atomic, Paired with agent) and are designed to be executed by GitHub Copilot Agent.

| # | Title | Labels | Dependencies | Est. Time |
|---|-------|--------|--------------|-----------|
| 1 | Bootstrap MRP.Assistant Class Library Project | `infra`, `agent` | None | 30 min |
| 2 | Implement MRP Log Parser | `parser`, `agent` | #1 | 2-3 hrs |
| 3 | Build Log Comparison Engine | `diff`, `agent` | #2 | 2-3 hrs |
| 4 | Create Explanation Engine (FACT vs INFERENCE) | `analysis`, `agent` | #2, #3 | 2-3 hrs |
| 5 | Implement Planner-Friendly Report Generator | `reporter`, `agent` | #2, #3, #4 | 2 hrs |
| 6 | Add CLI Interface for File-Based Comparison | `cli`, `agent` | #2, #3, #4, #5 | 1-2 hrs |
| 7 | Create Unit Tests for Core Components | `testing`, `agent` | #2, #3, #4 | 2-3 hrs |
| 8 | Add Integration Tests with Sample Logs | `testing`, `agent` | #6, #7 | 1-2 hrs |

**Total Estimated Effort**: 12-18 hours

## 🔗 Issue Files Location

Each issue is fully documented in:
- `/docs/issues/ISSUE_01_Bootstrap_Project.md`
- `/docs/issues/ISSUE_02_Implement_Parser.md`
- `/docs/issues/ISSUE_03_Build_Comparison_Engine.md`
- `/docs/issues/ISSUE_04_Create_Explanation_Engine.md`
- `/docs/issues/ISSUE_05_Implement_Report_Generator.md`
- `/docs/issues/ISSUE_06_Add_CLI_Interface.md`
- `/docs/issues/ISSUE_07_Create_Unit_Tests.md`
- `/docs/issues/ISSUE_08_Add_Integration_Tests.md`

## 📖 Master Plan

Comprehensive project plan with all technical details:
- `/docs/MRP_ASSISTANT_PROJECT_PLAN.md`

## 🎯 How to Use This

### Option 1: Create Issues Manually (Recommended)
1. Navigate to GitHub repository → Issues → New Issue
2. Copy content from each issue file in `/docs/issues/`
3. Paste into GitHub issue form
4. Set appropriate labels (`infra`, `parser`, `diff`, `analysis`, `reporter`, `cli`, `testing`, `agent`)
5. Assign to `@copilot`
6. Create issue

### Option 2: Use GitHub CLI
```bash
# Create all 8 issues at once
for i in {1..8}; do
  gh issue create \
    --title "$(grep '^title:' docs/issues/ISSUE_0${i}_*.md | cut -d'"' -f2)" \
    --body-file docs/issues/ISSUE_0${i}_*.md \
    --label agent \
    --assignee copilot
done
```

### Option 3: Use GitHub API
```bash
# Example for Issue 1
curl -X POST \
  -H "Authorization: token YOUR_TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/CGkylestephens/GHCrystalGroupHome/issues \
  -d @- << EOF
{
  "title": "[Agent Task] Bootstrap MRP.Assistant Class Library Project",
  "body": "$(cat docs/issues/ISSUE_01_Bootstrap_Project.md)",
  "labels": ["infra", "agent"],
  "assignees": ["copilot"]
}
EOF
```

## 📊 Execution Order

Issues should be tackled in sequential order due to dependencies:

```
Issue 1 (Bootstrap)
    ↓
Issue 2 (Parser)
    ↓
    ├─→ Issue 3 (Comparison)
    │       ↓
    ├─→ Issue 4 (Explanation)
    │       ↓
    └─→ Issue 5 (Report Generator)
            ↓
        Issue 6 (CLI)
            ↓
        Issue 7 (Unit Tests)
            ↓
        Issue 8 (Integration Tests)
```

**Sequential Execution**: Complete each issue before starting the next.

**Parallel Options**: After Issue 2 is complete, Issues 3, 4, and 5 *could* be developed in parallel if multiple agents are available, though sequential is recommended for clarity.

## ✅ Success Criteria

The project is complete when:
1. ✅ All 8 issues closed
2. ✅ Solution builds: `dotnet build CrystalGroupHome.sln`
3. ✅ All tests pass: `dotnet test`
4. ✅ CLI works: `dotnet run --project MRP.Assistant.CLI -- demo`
5. ✅ Sample report generated in `testdata/`
6. ✅ README documentation complete

## 🧪 Quick Validation

After all issues are complete, run these commands to validate:

```bash
# 1. Build solution
dotnet build CrystalGroupHome.sln

# 2. Run all tests
dotnet test

# 3. Generate demo report
dotnet run --project MRP.Assistant.CLI/MRP.Assistant.CLI.csproj -- demo

# 4. Verify demo report exists
cat demo_report.md
```

## 📝 Key Files Referenced

All issues reference these existing files:
- `/testdata/mrp_log_sample_A.txt` - Simple sample log (Run A)
- `/testdata/mrp_log_sample_B.txt` - Simple sample log (Run B)
- `/testdata/MRPRegenSample.txt` - Real regeneration log
- `/testdata/MRPNETChangeSample.txt` - Real net change log
- `/testdata/ExtractLogSample.ps1` - Log sampling script
- `/copilot-instructions.md` - Agent behavior guidelines

## 🎨 Visual Hierarchy

Issues are color-coded by area:
- 🔵 **Infrastructure** (Issue 1): Project setup
- 🟢 **Core Development** (Issues 2-5): Parser, comparer, explainer, reporter
- 🟡 **Interface** (Issue 6): CLI
- 🟣 **Quality** (Issues 7-8): Tests

## 🚀 Getting Started

To begin implementation:
1. Create Issue #1 in GitHub
2. Assign to @copilot
3. Wait for completion
4. Verify build succeeds
5. Create Issue #2
6. Repeat until all 8 issues complete

## 📧 Support

Questions or issues? Reference:
- **Master Plan**: `/docs/MRP_ASSISTANT_PROJECT_PLAN.md`
- **Project Context**: Issue description in repository
- **Test Data**: `/testdata/` folder
- **Guidelines**: `/copilot-instructions.md`

---

## 🎯 Next Steps for Human

**Immediate Actions**:
1. ✅ Review the 8 issue files in `/docs/issues/`
2. ✅ Validate issue content matches requirements
3. ⏭️ Create GitHub issues (manually or via CLI)
4. ⏭️ Assign first issue to @copilot
5. ⏭️ Monitor progress and address questions

**Optional Enhancements**:
- Create GitHub Project board with columns: Backlog, In Progress, Review, Done
- Add milestone: "MRP Assistant v1.0"
- Set up CI/CD pipeline for automated testing
- Create release checklist

---

**Generated**: 2026-02-06  
**Version**: 1.0  
**Status**: Ready for Issue Creation
