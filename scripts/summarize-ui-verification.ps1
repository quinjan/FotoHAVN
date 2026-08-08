[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RunRoot,

    [int] $ExpectedResults = 48,

    [string] $MarkdownPath = (Join-Path $RunRoot "summary.md"),

    [string] $JsonPath = (Join-Path $RunRoot "summary.json")
)

$ErrorActionPreference = "Stop"
$resolvedRunRoot = (Resolve-Path -LiteralPath $RunRoot).Path
$runPath = Join-Path $resolvedRunRoot "run.json"
$environmentPath = Join-Path $resolvedRunRoot "environment.json"
if (-not (Test-Path -LiteralPath $runPath) -or -not (Test-Path -LiteralPath $environmentPath)) {
    throw "The verification run is incomplete: run.json or environment.json is missing."
}

$run = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json
$environment = Get-Content -LiteralPath $environmentPath -Raw | ConvertFrom-Json
$resultFiles = @(Get-ChildItem -LiteralPath $resolvedRunRoot -Recurse -File -Filter result.json)
$results = @($resultFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$semanticFindings = @($results | ForEach-Object { @($_.uiAutomation.violations) })
$missingEvidence = @()
foreach ($result in $results) {
    foreach ($relativePath in @($result.evidenceFiles.actual, $result.evidenceFiles.diff)) {
        if (-not (Test-Path -LiteralPath (Join-Path $resolvedRunRoot $relativePath))) {
            $missingEvidence += "$($result.fixtureId): $relativePath"
        }
    }
}

$ratios = @($results | ForEach-Object {
    if ($_.image.totalPixels -gt 0) { $_.image.changedPixels / $_.image.totalPixels } else { 0 }
})
$minimumRatio = if ($ratios.Count -gt 0) { ($ratios | Measure-Object -Minimum).Minimum } else { 0 }
$averageRatio = if ($ratios.Count -gt 0) { ($ratios | Measure-Object -Average).Average } else { 0 }
$maximumRatio = if ($ratios.Count -gt 0) { ($ratios | Measure-Object -Maximum).Maximum } else { 0 }
$statusCounts = [ordered]@{}
foreach ($group in $results | Group-Object status | Sort-Object Name) {
    $statusCounts[$group.Name] = $group.Count
}

$summary = [ordered]@{
    schemaVersion = 1
    gitCommit = $run.gitCommit
    applicationSha256 = $run.applicationSha256
    completedThroughBatch = $run.completedThroughBatch
    pinnedEnvironmentMatched = [bool]$run.pinnedEnvironmentMatched
    environmentManifestSha256 = $environment.manifestSha256
    expectedResults = $ExpectedResults
    totalResults = $results.Count
    statusCounts = $statusCounts
    semanticViolations = $semanticFindings.Count
    missingEvidenceFiles = $missingEvidence.Count
    changedPixelRatio = [ordered]@{
        minimum = $minimumRatio
        average = $averageRatio
        maximum = $maximumRatio
    }
    visualReviewRequired = @($results | Where-Object { $_.image.changedPixels -gt 0 }).Count
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $JsonPath -Encoding utf8

$markdown = @(
    "# Batch 3 UI verification summary"
    ""
    "- Commit: ``$($summary.gitCommit)``"
    "- Application SHA-256: ``$($summary.applicationSha256)``"
    "- Pinned environment matched: **$($summary.pinnedEnvironmentMatched)**"
    "- Results: **$($summary.totalResults) / $ExpectedResults**"
    "- Semantic violations: **$($summary.semanticViolations)**"
    "- Missing evidence files: **$($summary.missingEvidenceFiles)**"
    "- Changed-pixel ratio: $($minimumRatio.ToString('P2')) minimum, $($averageRatio.ToString('P2')) average, $($maximumRatio.ToString('P2')) maximum"
    "- Fixtures requiring recorded visual review: **$($summary.visualReviewRequired)**"
    ""
    "Exact pixel differences remain reviewable evidence; this script does not apply a tolerance or approve them."
)
$markdown | Set-Content -LiteralPath $MarkdownPath -Encoding utf8

$failures = @()
if (-not $run.pinnedEnvironmentMatched -or -not $environment.isPinned) {
    $failures += "The pinned environment did not match."
}
if ($run.totalResults -ne $ExpectedResults -or $results.Count -ne $ExpectedResults) {
    $failures += "Expected $ExpectedResults results, found run=$($run.totalResults), evidence=$($results.Count)."
}
if ($semanticFindings.Count -gt 0) {
    $failures += "Found $($semanticFindings.Count) semantic violations."
}
if ($missingEvidence.Count -gt 0) {
    $failures += "Found $($missingEvidence.Count) missing actual/diff evidence files."
}
if ($failures.Count -gt 0) {
    throw ($failures -join " ")
}

Write-Output "Verified $($results.Count) pinned fixtures with zero semantic violations."
