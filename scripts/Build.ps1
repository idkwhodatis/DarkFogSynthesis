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
    & $DotNet run --project scripts/ResourceAudit/ResourceAudit.csproj --configuration $Configuration -- --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Compiled-resource audit self-tests failed.' }
    & $DotNet run --project tests/DarkFogSynthesis.Core.Tests/DarkFogSynthesis.Core.Tests.csproj --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Pure logic tests failed.' }
    if ($CoreOnly) {
        Write-Host 'Pure logic and content checks passed. Runtime compilation and all game acceptance checks were NOT EXECUTED.'
    } else {
        # The project validates lawful local reference paths. No assemblies are downloaded or copied into the game.
        # Remove stale localization satellites left by older builds before checking the complete output tree.
        & $DotNet clean src/DarkFogSynthesis/DarkFogSynthesis.csproj --configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Runtime build-output cleanup failed.' }
        & $DotNet build src/DarkFogSynthesis/DarkFogSynthesis.csproj --configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed. Do not create an installable package; source-only packaging remains available.' }
        # Resolve imported Local.Build.props and configuration values through MSBuild, not a second XML parser.
        $PropertyOutput = & $DotNet msbuild src/DarkFogSynthesis/DarkFogSynthesis.csproj -nologo "-property:Configuration=$Configuration" '-getProperty:DSPManagedDir,BepInExDir,CommonApiDir,LdbToolDir,TargetPath'
        if ($LASTEXITCODE -ne 0) { throw 'Could not resolve runtime output/reference paths through MSBuild (SDK 8+ is required).' }
        $Properties = ($PropertyOutput -join "`n" | ConvertFrom-Json).Properties
        foreach ($Name in @('DSPManagedDir', 'BepInExDir', 'CommonApiDir', 'LdbToolDir', 'TargetPath')) {
            if ([string]::IsNullOrWhiteSpace($Properties.$Name)) { throw "MSBuild returned an empty $Name." }
        }
        $RuntimeDll = [System.IO.Path]::GetFullPath($Properties.TargetPath)
        $ManagedDir = [System.IO.Path]::GetFullPath($Properties.DSPManagedDir)
        $GameDll = Join-Path $ManagedDir 'Assembly-CSharp.dll'
        New-Item -ItemType Directory -Path artifacts -Force | Out-Null
        & $DotNet run --project scripts/ResourceAudit/ResourceAudit.csproj --configuration $Configuration -- $RuntimeDll src/DarkFogSynthesis/Localization --json-report artifacts/resource-audit.json
        if ($LASTEXITCODE -ne 0) { throw 'Runtime localization resources failed audit. Build provenance was not recorded.' }
        $ApiOutput = & $DotNet run --project scripts/PublicApiAudit/PublicApiAudit.csproj --configuration $Configuration -- $RuntimeDll $GameDll $ManagedDir $Properties.BepInExDir $Properties.CommonApiDir $Properties.LdbToolDir --json-report artifacts/public-api-audit.json 2>&1
        $ApiExitCode = $LASTEXITCODE
        $ApiOutput | Tee-Object -FilePath artifacts/public-api-audit.txt | Write-Host
        if ($ApiExitCode -ne 0) { throw 'Runtime public API accessibility audit failed. Build provenance was not recorded.' }
        & $Python scripts/package.py --record-build --configuration $Configuration --reference-mode $ReferenceMode
        if ($LASTEXITCODE -ne 0) { throw 'Runtime build provenance could not be recorded.' }
        Write-Host 'Runtime compilation passed. Game behavior, safe removal and integrity remain unverified until target-game acceptance.'
    }
} finally {
    Pop-Location
}
