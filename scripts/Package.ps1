[CmdletBinding()]
param(
    [ValidateSet('source-only', 'experimental', 'release')][string]$Channel = 'source-only',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$AcceptancePath = '',
    [string]$OutputDirectory = '',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Arguments = @((Join-Path $PSScriptRoot 'package.py'), '--channel', $Channel, '--configuration', $Configuration)
if ($AcceptancePath) { $Arguments += @('--acceptance', $AcceptancePath) }
if ($OutputDirectory) { $Arguments += @('--output-dir', $OutputDirectory) }
& $Python @Arguments
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed or was refused. No package should be treated as a release.' }
