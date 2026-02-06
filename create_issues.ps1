# PowerShell script to create all 8 MRP Assistant GitHub issues
# Usage: .\create_issues.ps1

$ErrorActionPreference = "Stop"

$repo = "CGkylestephens/GHCrystalGroupHome"
$issuesDir = "docs/issues"

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Creating MRP Assistant GitHub Issues" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Repository: $repo"
Write-Host "Issues to create: 8"
Write-Host ""

# Check if gh CLI is installed
try {
    $ghVersion = gh --version 2>&1
    Write-Host "✓ GitHub CLI is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: GitHub CLI (gh) is not installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install it from: https://cli.github.com/"
    Write-Host "Or use the manual creation method in MANUAL_ISSUE_CREATION.md"
    exit 1
}

# Check if authenticated
try {
    gh auth status 2>&1 | Out-Null
    Write-Host "✓ GitHub CLI is authenticated" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: Not authenticated with GitHub CLI." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run: gh auth login"
    exit 1
}

Write-Host ""

# Function to extract title from markdown file
function Get-IssueTitle {
    param($FilePath)
    $content = Get-Content $FilePath -Raw
    if ($content -match 'title:\s*"([^"]+)"') {
        return $matches[1]
    }
    return $null
}

# Function to extract labels from markdown file
function Get-IssueLabels {
    param($FilePath)
    $content = Get-Content $FilePath -Raw
    if ($content -match 'labels:\s*\[([^\]]+)\]') {
        return $matches[1]
    }
    return $null
}

# Create each issue
for ($i = 1; $i -le 8; $i++) {
    $issuePattern = "$issuesDir/ISSUE_0$i`_*.md"
    $issuePath = Get-ChildItem $issuePattern | Select-Object -First 1
    
    if (-not $issuePath) {
        Write-Host "❌ Issue file not found: $issuePattern" -ForegroundColor Red
        continue
    }
    
    $title = Get-IssueTitle $issuePath.FullName
    $labels = Get-IssueLabels $issuePath.FullName
    
    Write-Host "Creating Issue #$i`: $title" -ForegroundColor Yellow
    
    try {
        # Create the issue
        gh issue create `
            --repo $repo `
            --title $title `
            --body-file $issuePath.FullName `
            --label $labels `
            --assignee "copilot"
        
        Write-Host "  ✓ Created successfully" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Failed to create: $_" -ForegroundColor Red
    }
    
    Write-Host ""
    
    # Small delay to avoid rate limiting
    Start-Sleep -Seconds 1
}

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Issue creation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Visit: https://github.com/$repo/issues"
Write-Host "  2. Verify all 8 issues were created"
Write-Host "  3. Issues will be assigned to @copilot"
Write-Host "  4. Issues should be completed sequentially (1→2→3→4→5→6→7→8)"
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
