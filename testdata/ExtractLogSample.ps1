
# ExtractLogSample.ps1
# Generate a reduced-size log sample for Copilot use from a large Epicor MRP log file

param(
    [string]$InputFile = "AUTOMRPREGEN20260206.log",
    [string]$OutputFile = "mrp_log_sample_A.txt",
    [int]$ContextLines = 10
)

$ErrorKeywords = @(
    "cannot", "can not", "will not", "won't", "wont",
    "error", "exception", "defunct", "abandoned",
    "timeout", "deadlock", "cancel", "cancelling"
)

$EntityKeywords = @(
    "Part:", "Job", "Pegged", "Supply", "Demand"
)

# Read full file into memory
$lines = Get-Content -Path $InputFile -Raw -Encoding UTF8 -ErrorAction Stop | Out-String
$linesArray = $lines -split "\r?\n"

$Matches = @()
$UsedLineIndexes = @{}

# Function to safely extract context window
function Add-Window {
    param($center, $label)
    $start = [Math]::Max(0, $center - $ContextLines)
    $end = [Math]::Min($linesArray.Length - 1, $center + $ContextLines)
    $window = $linesArray[$start..$end]
    $Matches += "==== $label (Line $center) ===="
    $Matches += $window
    $Matches += ""
    for ($i = $start; $i -le $end; $i++) { $UsedLineIndexes[$i] = $true }
}

# Error context blocks
for ($i = 0; $i -lt $linesArray.Length; $i++) {
    foreach ($kw in $ErrorKeywords) {
        if ($linesArray[$i] -match [regex]::Escape($kw)) {
            if (-not $UsedLineIndexes.ContainsKey($i)) {
                Add-Window -center $i -label "ERROR"
            }
            break
        }
    }
}

# Add normal entity references (limited)
$EntityMatches = @()
foreach ($line in $linesArray) {
    foreach ($ekey in $EntityKeywords) {
        if ($line -match $ekey) {
            $EntityMatches += $line
            break
        }
    }
    if ($EntityMatches.Count -ge 100) { break }
}

# Write output
$Matches | Set-Content -Path $OutputFile -Encoding UTF8
"==== Normal Planning Entries ====" | Add-Content -Path $OutputFile
$EntityMatches | Add-Content -Path $OutputFile

Write-Host "✅ Sample log written to $OutputFile"
 
