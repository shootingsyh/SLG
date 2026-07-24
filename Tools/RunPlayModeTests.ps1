param([string]$UnityPath)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "UnityTestRunner.psm1") -Force
$exitCode = Invoke-UnityTests -TestPlatform PlayMode -UnityPath $UnityPath
exit $exitCode
