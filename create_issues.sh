#!/bin/bash

# Script to create all 8 MRP Assistant GitHub issues
# Usage: ./create_issues.sh

set -e

REPO="CGkylestephens/GHCrystalGroupHome"
ISSUES_DIR="docs/issues"

echo "════════════════════════════════════════════════════════════"
echo "  Creating MRP Assistant GitHub Issues"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "Repository: $REPO"
echo "Issues to create: 8"
echo ""

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ ERROR: GitHub CLI (gh) is not installed."
    echo ""
    echo "Please install it from: https://cli.github.com/"
    echo "Or use the manual creation method in MANUAL_ISSUE_CREATION.md"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ ERROR: Not authenticated with GitHub CLI."
    echo ""
    echo "Please run: gh auth login"
    exit 1
fi

echo "✓ GitHub CLI is installed and authenticated"
echo ""

# Function to extract title from markdown file
get_title() {
    local file=$1
    grep '^title:' "$file" | sed 's/title: "\(.*\)"/\1/' | tr -d '"'
}

# Function to extract labels from markdown file
get_labels() {
    local file=$1
    grep '^labels:' "$file" | sed 's/labels: \[\(.*\)\]/\1/' | tr -d '[]'
}

# Create each issue
for i in {1..8}; do
    ISSUE_FILE="$ISSUES_DIR/ISSUE_0${i}_*.md"
    ISSUE_PATH=$(ls $ISSUE_FILE 2>/dev/null | head -1)
    
    if [ -z "$ISSUE_PATH" ]; then
        echo "❌ Issue file not found: $ISSUE_FILE"
        continue
    fi
    
    TITLE=$(get_title "$ISSUE_PATH")
    LABELS=$(get_labels "$ISSUE_PATH")
    
    echo "Creating Issue #$i: $TITLE"
    
    # Create the issue
    gh issue create \
        --repo "$REPO" \
        --title "$TITLE" \
        --body-file "$ISSUE_PATH" \
        --label "$LABELS" \
        --assignee "copilot" \
        2>&1
    
    if [ $? -eq 0 ]; then
        echo "  ✓ Created successfully"
    else
        echo "  ❌ Failed to create"
    fi
    
    echo ""
    
    # Small delay to avoid rate limiting
    sleep 1
done

echo "════════════════════════════════════════════════════════════"
echo "✅ Issue creation complete!"
echo ""
echo "Next steps:"
echo "  1. Visit: https://github.com/$REPO/issues"
echo "  2. Verify all 8 issues were created"
echo "  3. Issues will be assigned to @copilot"
echo "  4. Issues should be completed sequentially (1→2→3→4→5→6→7→8)"
echo "════════════════════════════════════════════════════════════"
