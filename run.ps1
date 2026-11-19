<#
.SYNOPSIS
    Runs the Meteor Trajectory Simulator: builds the Windows player the first time (or when asked), then launches it.

.EXAMPLE
    .\run.ps1              # build if needed, then play
    .\run.ps1 -Rebuild     # force a fresh build, then play
    .\run.ps1 -Editor      # open the project in the Unity Editor on the main menu scene
    .\run.ps1 -Windowed    # play in a 1600x900 window instead of full screen

.NOTES
    Needs Unity 2022.3.7f1 (see ProjectSettings/ProjectVersion.txt). The script looks in the usual Unity Hub and
    standalone install folders; set the UNITY_EDITOR environment variable to Unity.exe to point it elsewhere.
#>
param(
    [switch]$Rebuild,
    [switch]$Editor,
    [switch]$Windowed,
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = ((Get-Content (Join-Path $root 'ProjectSettings/ProjectVersion.txt') | Select-String 'm_EditorVersion:') -split ':\s*')[1].Trim()
$exe = Join-Path $root 'Builds/Windows/MeteorTrajectorySimulator.exe'

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

$stamp = Join-Path $root 'Builds/Windows/build.stamp'

function Test-BuildStale {
    if (-not (Test-Path $exe) -or -not (Test-Path $stamp)) { return $true }
    $built = (Get-Item $stamp).LastWriteTimeUtc
    # TextMesh Pro font atlases are re-saved by Unity on every run, so they don't count as changes.
    $newer = Get-ChildItem -Path (Join-Path $root 'Assets'), (Join-Path $root 'Packages'), (Join-Path $root 'ProjectSettings') -Recurse -File |
        Where-Object { $_.LastWriteTimeUtc -gt $built -and $_.Name -notmatch 'SDF.*\.asset$' -and $_.FullName -notmatch 'codecoverage' } |
        Select-Object -First 1
    return [bool]$newer
}

if ($Editor) {
    $unity = Find-Unity
    Write-Host "Opening the project in Unity $version (the first open imports assets and takes a few minutes)..."
    Start-Process -FilePath $unity -ArgumentList @('-projectPath', "`"$root`"", '-executeMethod', 'MTS.Launcher.Launcher.OpenMainMenu')
    Write-Host "When the editor is ready, press Play (or use Tools > Meteor Simulator > Play from main menu)."
    return
}

if ($Rebuild -or (Test-BuildStale)) {
    $unity = Find-Unity
    $log = Join-Path $root 'Logs/build.log'
    New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
    Write-Host "Building the Windows player with Unity $version. The first build takes several minutes; log: $log"
    $p = Start-Process -FilePath $unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        '-batchmode', '-quit', '-projectPath', "`"$root`"",
        '-executeMethod', 'MTS.Launcher.Launcher.BuildWindows',
        '-logFile', "`"$log`"")
    if ($p.ExitCode -ne 0 -or -not (Test-Path $exe)) {
        Get-Content $log -Tail 30 | Write-Host
        throw "Build failed (exit code $($p.ExitCode)). See $log. If the editor is open on this project, close it and try again."
    }
    Set-Content -Path $stamp -Value (Get-Date -Format o)
    Write-Host "Build finished: $exe"
}

if ($BuildOnly) {
    Write-Host "Up to date: $exe"
    return
}

$playerArgs = @()
if ($Windowed) { $playerArgs = @('-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900') }
Write-Host 'Starting the simulator...'
if ($playerArgs.Count -gt 0) { Start-Process -FilePath $exe -ArgumentList $playerArgs } else { Start-Process -FilePath $exe }
