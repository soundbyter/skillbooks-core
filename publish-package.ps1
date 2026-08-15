<#
.SYNOPSIS
    Packs Skillbooks.Core and publishes it to GitHub Packages so addon mods
    (Stats, Archivist) can reference it as a normal NuGet dependency.

.DESCRIPTION
    Run this locally whenever core's public API surface changes and you want
    addon mods to be able to pick up the new version. Requires:
      - VINTAGE_STORY environment variable set (same as for a normal build).
      - gh CLI logged in with the write:packages scope
        (gh auth refresh -h github.com -s write:packages,read:packages).

.PARAMETER Version
    The package version to publish, e.g. "0.1.0". Follow semver: bump the
    patch number for fixes, the minor number for additive API changes, the
    major number for breaking changes to the public surface.

.EXAMPLE
    ./publish-package.ps1 -Version 0.1.0
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

if (-not $env:VINTAGE_STORY) {
    Write-Error "VINTAGE_STORY environment variable is not set -- needed to resolve the game's own DLLs during the build."
    exit 1
}

$repoRoot = $PSScriptRoot
$outputDir = Join-Path $repoRoot "nupkg"
if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }

Write-Host "Packing Skillbooks.Core $Version..."
dotnet pack (Join-Path $repoRoot "Skillbooks\Skillbooks.csproj") -c Release -p:PackageVersion=$Version -o $outputDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$package = Join-Path $outputDir "Skillbooks.Core.$Version.nupkg"
if (-not (Test-Path $package)) {
    Write-Error "Expected package not found at $package"
    exit 1
}

$token = gh auth token
if (-not $token) {
    Write-Error "Could not get a token from gh CLI. Run 'gh auth login' first."
    exit 1
}

Write-Host "Publishing to GitHub Packages..."
dotnet nuget push $package `
    --source "https://nuget.pkg.github.com/soundbyter/index.json" `
    --api-key $token `
    --skip-duplicate

if ($LASTEXITCODE -eq 0) {
    Write-Host "Published Skillbooks.Core $Version to GitHub Packages."
}
