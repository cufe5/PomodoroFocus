#Requires -Version 5.1
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$Test,
    [switch]$UiTest,
    [switch]$Run,
    [switch]$Clean
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$slnPath = Join-Path $repoRoot 'PomodoroFocus.sln'
$msbuild = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild not found at $msbuild"
    exit 1
}

if ($Clean) {
    $dirs = @(
        'src\PomodoroFocus.Core\bin', 'src\PomodoroFocus.Core\obj',
        'src\PomodoroFocus.App\bin', 'src\PomodoroFocus.App\obj',
        'tests\PomodoroFocus.Tests\bin', 'tests\PomodoroFocus.Tests\obj',
        'tests\PomodoroFocus.UITests\bin', 'tests\PomodoroFocus.UITests\obj'
    )
    foreach ($d in $dirs) {
        $full = Join-Path $repoRoot $d
        if (Test-Path $full) {
            Remove-Item -Path $full -Recurse -Force
        }
    }
    Write-Output 'CLEAN OK'
    exit 0
}

& $msbuild $slnPath /t:Build "/p:Configuration=$Configuration" /p:Platform=x64 /nr:false /nologo /v:minimal
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
Write-Output 'BUILD OK'

if ($Test -or $UiTest) {
    $testExe = Join-Path $repoRoot ("tests\PomodoroFocus.Tests\bin\" + $Configuration + "\PomodoroFocus.Tests.exe")
    & $testExe
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Write-Output 'TESTS OK'
}

if ($UiTest) {
    $uiTestExe = Join-Path $repoRoot ("tests\PomodoroFocus.UITests\bin\" + $Configuration + "\PomodoroFocus.UITests.exe")
    $outDir = Join-Path $env:TEMP 'PomodoroFocus.UITests'
    & $uiTestExe --out $outDir
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Write-Output 'UI TESTS OK'
}

if ($Run) {
    $appExe = Join-Path $repoRoot ("src\PomodoroFocus.App\bin\" + $Configuration + "\PomodoroFocus.exe")
    Start-Process -FilePath $appExe
}
