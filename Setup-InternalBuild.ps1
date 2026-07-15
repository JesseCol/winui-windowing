[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$WinUIRepoRoot,

    [switch]$Force
)

$propsPath = Join-Path $PSScriptRoot "MyApp.local.props"
$nugetConfigPath = Join-Path $WinUIRepoRoot "NuGet.config"

if ((Test-Path $propsPath) -and -not $Force) {
    throw "$propsPath already exists. Use -Force to replace it."
}

if (-not (Test-Path $WinUIRepoRoot -PathType Container)) {
    throw "WinUI repository root not found at $WinUIRepoRoot."
}

if (-not (Test-Path $nugetConfigPath -PathType Leaf)) {
    throw "NuGet.config not found at $nugetConfigPath."
}

[xml]$nugetConfig = Get-Content $nugetConfigPath -Raw
$internalSources = @($nugetConfig.configuration.packageSources.add |
    Where-Object {
        $_.value -match "^https?://" -and
        $_.value -notmatch "^https://api\.nuget\.org/"
    })

if ($internalSources.Count -eq 0) {
    throw "No non-nuget.org remote package sources were found in $nugetConfigPath."
}

$resolvedRepoRoot = (Resolve-Path $WinUIRepoRoot).Path
$escapedRepoRoot = [System.Security.SecurityElement]::Escape($resolvedRepoRoot)
$internalFeedValues = $internalSources | ForEach-Object { $_.value }
$escapedInternalFeeds = [System.Security.SecurityElement]::Escape(
    ($internalFeedValues -join ";"))

$props = @"
<Project>
  <PropertyGroup>
    <WinUIRepoRoot>$escapedRepoRoot</WinUIRepoRoot>
    <RestoreAdditionalProjectSources>$escapedInternalFeeds</RestoreAdditionalProjectSources>
    <RestoreAdditionalProjectSources Condition="Exists('`$(WinUIRepoRoot)\PackageStore')">`$(WinUIRepoRoot)\PackageStore;`$(RestoreAdditionalProjectSources)</RestoreAdditionalProjectSources>
  </PropertyGroup>
</Project>
"@

[System.IO.File]::WriteAllText(
    $propsPath,
    $props,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Created $propsPath"
Write-Host "WinUI package source(s):"
$internalFeedValues | ForEach-Object { Write-Host "  $_" }
Write-Host "Local package store: $resolvedRepoRoot\PackageStore"
Write-Host "Select the Windows App SDK package version in Visual Studio, then build normally."
