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
$internalSource = $nugetConfig.configuration.packageSources.add |
    Where-Object {
        $_.key -eq "Project.Reunion.nuget.internal" -or
        $_.value -like "*/Project.Reunion.nuget.internal/*"
    } |
    Select-Object -First 1

if (-not $internalSource) {
    throw "The Project.Reunion.nuget.internal source was not found in $nugetConfigPath."
}

$resolvedRepoRoot = (Resolve-Path $WinUIRepoRoot).Path
$escapedRepoRoot = [System.Security.SecurityElement]::Escape($resolvedRepoRoot)
$escapedInternalFeed = [System.Security.SecurityElement]::Escape($internalSource.value)

$props = @"
<Project>
  <PropertyGroup>
    <WinUIRepoRoot>$escapedRepoRoot</WinUIRepoRoot>
    <RestoreAdditionalProjectSources>$escapedInternalFeed</RestoreAdditionalProjectSources>
    <RestoreAdditionalProjectSources Condition="Exists('`$(WinUIRepoRoot)\PackageStore')">`$(WinUIRepoRoot)\PackageStore;`$(RestoreAdditionalProjectSources)</RestoreAdditionalProjectSources>
  </PropertyGroup>
</Project>
"@

[System.IO.File]::WriteAllText(
    $propsPath,
    $props,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Created $propsPath"
Write-Host "Internal feed: $($internalSource.value)"
Write-Host "Local package store: $resolvedRepoRoot\PackageStore"
Write-Host "Select the Windows App SDK package version in Visual Studio, then build normally."
