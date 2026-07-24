param([string]$UnityPath)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "UnityTestRunner.psm1") -Force

$editExit = Invoke-UnityTests -TestPlatform EditMode -UnityPath $UnityPath
if ($editExit -ne 0) {
    exit $editExit
}

$playExit = Invoke-UnityTests -TestPlatform PlayMode -UnityPath $UnityPath
exit $playExit
