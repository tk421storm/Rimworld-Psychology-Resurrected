# Builds Psychology (and whichever compatibility assemblies can be built) for every supported RimWorld version.
#
#   pwsh Source/build.ps1                 # all versions, Release
#   pwsh Source/build.ps1 -Versions 1.6   # just one
#
# Output goes straight into <mod root>/<version>/Assemblies and <mod root>/<version>/Mods/<Mod>/Assemblies.
# RimWorld 1.7 targets .NET 10, so building it needs the .NET 10 SDK (dotnet --list-sdks).
# Compatibility projects whose third-party DLL isn't installed are skipped with a warning (see Compat.targets).
param(
    [string[]]$Versions = @('1.6', '1.7'),
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot 'Psychology.sln'
$failed = @()

foreach ($v in $Versions) {
    Write-Host "=== RimWorld $v ===" -ForegroundColor Cyan
    dotnet build $solution -c $Configuration "-p:RimWorldVersion=$v" -nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { $failed += $v }
}

if ($failed.Count -gt 0) {
    Write-Host "Build failed for: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
