[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$CoreOnly,
    [ValidateSet('installed-local', 'reference-assembly-smoke')][string]$ReferenceMode = 'installed-local',
    [switch]$RegenerateAssets,
    [string]$DotNet = 'dotnet',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    if ($RegenerateAssets) {
        & $Python scripts/generate-assets.py
    } else {
        & $Python scripts/generate-assets.py --check
    }
    if ($LASTEXITCODE -ne 0) { throw 'Asset generation or validation failed.' }
    & $Python scripts/validate-content.py
    if ($LASTEXITCODE -ne 0) { throw 'Source content validation failed.' }
    & $Python scripts/test-packaging.py
    if ($LASTEXITCODE -ne 0) { throw 'Packaging guard tests failed.' }
    & $DotNet run --project tests/DarkFogSynthesis.Core.Tests/DarkFogSynthesis.Core.Tests.csproj --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Pure logic tests failed.' }
    if ($CoreOnly) {
        Write-Host 'Pure logic and content checks passed. Runtime compilation and all game acceptance checks were NOT EXECUTED.'
    } else {
        # The project validates lawful local reference paths. No assemblies are downloaded or copied into the game.
        & $DotNet build src/DarkFogSynthesis/DarkFogSynthesis.csproj --configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed. Do not create an installable package; source-only packaging remains available.' }
        & $Python scripts/package.py --record-build --configuration $Configuration --reference-mode $ReferenceMode
        if ($LASTEXITCODE -ne 0) { throw 'Runtime build provenance could not be recorded.' }
        Write-Host 'Runtime compilation passed. Game behavior, safe removal and integrity remain unverified until target-game acceptance.'
    }
} finally {
    Pop-Location
}
