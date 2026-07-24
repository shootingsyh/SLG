param([string]$UnityPath)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "UnityTestRunner.psm1") -Force
$exitCode = Invoke-UnityTests -TestPlatform EditMode -UnityPath $UnityPath
exit $exitCode
