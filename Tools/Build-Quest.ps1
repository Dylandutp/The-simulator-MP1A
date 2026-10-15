param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity 6000.5.6f1 not found: $UnityEditor" }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets/Kitchen/Scenes/KitchenEntry.unity'))) {
    throw 'Run this script from the Kitchen repository.'
}

# Android rejects non-ASCII project paths. Keep the source checkout in place and
# build an isolated copy, without stale Library/Temp data or linked directories.
$buildProject = $projectRoot
if ($projectRoot -match '[^\x00-\x7F]') {
    $buildProject = Join-Path $env:TEMP ('KitchenQuest-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    if ($buildProject -match '[^\x00-\x7F]') { throw 'TEMP must have an ASCII path for Android builds.' }
    New-Item -ItemType Directory -Path $buildProject | Out-Null
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path $projectRoot $part) (Join-Path $buildProject $part) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -gt 7) { throw "Could not copy $part into the build workspace." }
    }
    Set-Content -LiteralPath (Join-Path $buildProject 'KITCHEN_BUILD_SOURCE.txt') -Value $projectRoot
    Write-Host "Isolated Android build workspace: $buildProject"
}

$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$buildLog = Join-Path $logDirectory 'Kitchen-QuestBuild.log'
$arguments = '-batchmode -quit -buildTarget Android -projectPath "{0}" -executeMethod HouseSim.Kitchen.Editor.KitchenBuildTools.BuildAndroid -logFile "{1}"' -f $buildProject,$buildLog
Write-Host "Building Quest APK. Progress log: $buildLog"
$buildProcess = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -PassThru -WindowStyle Hidden
$buildProcess.WaitForExit()
if ($buildProcess.ExitCode -ne 0) { throw "Quest build failed (exit $($buildProcess.ExitCode)). Read $buildLog" }
$apk = Join-Path $buildProject 'Builds/Quest/Kitchen.apk'
if (-not (Test-Path -LiteralPath $apk)) { throw 'Unity exited without producing Kitchen.apk.' }
$outputDirectory = Join-Path $projectRoot 'Builds/Quest'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$output = Join-Path $outputDirectory 'Kitchen.apk'
if ($apk -ne $output) { Copy-Item -LiteralPath $apk -Destination $output -Force }
Write-Host "Quest APK ready: $output"
Get-FileHash -LiteralPath $output -Algorithm SHA256
