# Builds release artifacts into dist:
#   ScreenTranslator.exe                     single-file app (framework-dependent by default)
#   ScreenTranslator-<ver>-portable.zip      with -Zip
#   installer\Output\ScreenTranslator-Setup-<ver>.exe   with -Installer (needs Inno Setup 6: winget install JRSoftware.InnoSetup)
#
#   -SelfContained  bundles the .NET runtime (~150 MB) so it runs on machines without .NET 10
#   -Run            launches the built exe afterwards
param(
    [switch]$SelfContained,
    [switch]$Zip,
    [switch]$Installer,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src\ScreenTranslator\ScreenTranslator.csproj'
$dist = Join-Path $root 'dist'

Get-Process ScreenTranslator -ErrorAction SilentlyContinue | Stop-Process -Force

$publishArgs = @(
    'publish', $proj,
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $dist,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=none',
    "-p:SelfContained=$($SelfContained.IsPresent)"
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$exe = Join-Path $dist 'ScreenTranslator.exe'
$version = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''
Write-Host ""
Write-Host "Built $exe  (v$version)" -ForegroundColor Green

if ($Zip) {
    $zipPath = Join-Path $dist "ScreenTranslator-$version-portable.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Compress-Archive -Path $exe, (Join-Path $root 'README.md') -DestinationPath $zipPath
    Write-Host "Zip:   $zipPath" -ForegroundColor Green
}

if ($Installer) {
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

    & $iscc "/DAppVersion=$version" (Join-Path $root 'installer\ScreenTranslator.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }
    Write-Host "Setup: $(Join-Path $root "installer\Output\ScreenTranslator-Setup-$version.exe")" -ForegroundColor Green
}

if ($Run) { Start-Process $exe }
