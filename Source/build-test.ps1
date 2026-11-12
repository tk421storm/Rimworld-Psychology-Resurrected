# Builds Psychology for one RimWorld version for an in-game test, and saves what changed since the last build that was
# in the game, for review after the test. Nothing is analysed here.
#
#   powershell -File Source/build-test.ps1               # RimWorld 1.6
#   powershell -File Source/build-test.ps1 -Version 1.7
#
# Writes Source/TestBuilds/<version>_<yyyy-MM-dd_HHmmss>/ (git-ignored):
#   decompiled.diff  the current DLL vs the one it replaced, both decompiled with ilspycmd (empty if nothing changed)
#   build.log        the build output
#   git.txt          HEAD, git status, and any uncommitted source changes
# The replaced DLL is the baseline (what you last ran), so the diff covers everything new in this test, committed or not.
param(
    [string]$Version = '1.6'
)

$ErrorActionPreference = 'Stop'
$modRoot = Split-Path $PSScriptRoot -Parent
$rimWorldRoot = Split-Path (Split-Path $modRoot -Parent) -Parent
$managed = Join-Path $rimWorldRoot 'RimWorldWin64_Data\Managed'
$dll = Join-Path $modRoot "$Version\Assemblies\Psychology.dll"

if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) {
    Write-Host 'RimWorld is running and may have the DLL locked. Close it and run this again.' -ForegroundColor Red
    exit 1
}
if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    Write-Host 'ilspycmd not found (dotnet tool install -g ilspycmd).' -ForegroundColor Red
    exit 1
}

$out = Join-Path $PSScriptRoot ("TestBuilds\{0}_{1}" -f $Version, (Get-Date -Format 'yyyy-MM-dd_HHmmss'))
New-Item -ItemType Directory -Force $out | Out-Null
$work = Join-Path $out 'work'
New-Item -ItemType Directory -Force "$work\before", "$work\after" | Out-Null

# Baseline: the DLL that's about to be replaced.
$hasBaseline = Test-Path $dll
if ($hasBaseline) {
    # Same file name as the new DLL, so the decompiled projects line up in the diff.
    New-Item -ItemType Directory -Force "$work\baseline" | Out-Null
    Copy-Item $dll "$work\baseline\Psychology.dll"
}

& (Join-Path $PSScriptRoot 'build.ps1') -Versions $Version *>&1 | Tee-Object -FilePath (Join-Path $out 'build.log')
$buildFailed = $LASTEXITCODE -ne 0

& {
    "HEAD: $(git -C $modRoot log --oneline -1)"
    ''
    '=== git status ==='
    git -C $modRoot status --short
    ''
    '=== uncommitted source changes ==='
    git -C $modRoot diff -- Source Common
} | Out-File -Encoding utf8 (Join-Path $out 'git.txt')

if ($buildFailed) {
    Write-Host "Build failed; see $out\build.log" -ForegroundColor Red
    exit 1
}

if ($hasBaseline) {
    Write-Host 'Decompiling...' -ForegroundColor Cyan
    ilspycmd -p -o "$work\before" "$work\baseline\Psychology.dll" -r $managed | Out-Null
    ilspycmd -p -o "$work\after" $dll -r $managed | Out-Null
    # Paths relative to the work folder keep the diff headers short (a/before/... b/after/...).
    Push-Location $work
    try {
        git diff --no-index --stat=150 before after | Out-File -Encoding utf8 (Join-Path $out 'decompiled.diff')
        git diff --no-index before after | Out-File -Encoding utf8 -Append (Join-Path $out 'decompiled.diff')
    }
    finally {
        Pop-Location
    }
}
else {
    'No previous DLL to compare against.' | Out-File -Encoding utf8 (Join-Path $out 'decompiled.diff')
}
Remove-Item $work -Recurse -Force

Write-Host "Built RimWorld $Version. Saved to $out" -ForegroundColor Green
