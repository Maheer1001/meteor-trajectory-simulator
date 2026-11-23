<#
.SYNOPSIS
    Runs every EditMode test (unit tests plus the setup checks) in batch mode and prints a pass/fail summary.

.EXAMPLE
    .\run-tests.ps1                       # all EditMode tests
    .\run-tests.ps1 -Filter SetupChecks   # only the setup checks (any NUnit name filter Unity accepts)

.NOTES
    Needs Unity 2022.3.7f1 (see ProjectSettings/ProjectVersion.txt). Set UNITY_EDITOR to Unity.exe if it is not in
    the usual Unity Hub or standalone install folder. Close the editor on this project first: Unity will not open a
    project twice. Results: TestResults/editmode-results.xml (NUnit 3 XML) and TestResults/editmode.log.
    Exit code: 0 all passed, 1 failures or the run could not start.
#>
param(
    [string]$Filter
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = ((Get-Content (Join-Path $root 'ProjectSettings/ProjectVersion.txt') | Select-String 'm_EditorVersion:') -split ':\s*')[1].Trim()

function Find-Unity {
    $candidates = @(
        $env:UNITY_EDITOR,
        "$env:ProgramFiles\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "$env:ProgramFiles\Unity $version\Editor\Unity.exe",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor\$version\Editor\Unity.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }
    if (-not $candidates) {
        throw "Unity $version not found. Install it with Unity Hub (Installs > Install Editor, or the download archive), or set UNITY_EDITOR to the path of Unity.exe."
    }
    return @($candidates)[0]
}

$unity = Find-Unity
$outDir = Join-Path $root 'TestResults'
New-Item -ItemType Directory -Force $outDir | Out-Null
$xml = Join-Path $outDir 'editmode-results.xml'
$log = Join-Path $outDir 'editmode.log'
Remove-Item $xml -ErrorAction SilentlyContinue

$unityArgs = @('-batchmode', '-nographics', '-projectPath', "`"$root`"", '-runTests', '-testPlatform', 'EditMode',
    '-testResults', "`"$xml`"", '-logFile', "`"$log`"")
if ($Filter) { $unityArgs += @('-testFilter', "`"$Filter`"") }

Write-Host "Running EditMode tests with Unity $version (the first run imports the project and takes several minutes)..."
$watch = [Diagnostics.Stopwatch]::StartNew()
# WaitForExit rather than -Wait: -Wait also waits for helper processes Unity leaves running after it quits.
$p = Start-Process -FilePath $unity -PassThru -NoNewWindow -ArgumentList $unityArgs
$p.WaitForExit()
$watch.Stop()

if (-not (Test-Path $xml)) {
    Get-Content $log -Tail 40 -ErrorAction SilentlyContinue | Write-Host
    Write-Host ""
    Write-Host "No results written (Unity exit code $($p.ExitCode)). Common causes: compile errors (search the log for 'error CS'), the editor already open on this project, or no Unity licence. Log: $log" -ForegroundColor Red
    exit 1
}

[xml]$doc = Get-Content $xml
$run = $doc.'test-run'
$failedCases = @($doc.SelectNodes("//test-case[@result='Failed']"))
foreach ($case in $failedCases) {
    Write-Host ("FAIL  " + $case.fullname) -ForegroundColor Red
    $message = $case.failure.message.'#cdata-section'
    if ($message) { Write-Host ("      " + ($message.Trim() -replace "`n", "`n      ")) }
}

# Per-assembly counts so it is obvious which layer failed.
foreach ($suite in $doc.SelectNodes("//test-suite[@type='Assembly']")) {
    Write-Host ("  {0,-24} {1,4} passed  {2,3} failed  {3,3} skipped" -f $suite.name, $suite.passed, $suite.failed, $suite.skipped)
}

$summary = "{0} tests: {1} passed, {2} failed, {3} skipped ({4:N0} s, wall {5:N0} s). Results: {6}" -f `
    $run.total, $run.passed, $run.failed, $run.skipped, [double]$run.duration, $watch.Elapsed.TotalSeconds, $xml
if ([int]$run.failed -gt 0 -or $run.result -notlike 'Passed*') {
    Write-Host "FAILED  $summary" -ForegroundColor Red
    exit 1
}
Write-Host "PASSED  $summary" -ForegroundColor Green
exit 0
