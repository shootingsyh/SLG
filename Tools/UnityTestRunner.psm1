function Get-ProjectRoot {
    $scriptDir = Split-Path -Parent $PSScriptRoot
    return (Resolve-Path -LiteralPath $scriptDir).Path
}

function Get-UnityVersion {
    param([string]$ProjectPath)

    $versionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "ProjectVersion.txt not found at $versionFile"
    }

    $line = Get-Content -LiteralPath $versionFile | Where-Object { $_ -like "m_EditorVersion:*" } | Select-Object -First 1
    if (-not $line) {
        throw "Could not read Unity version from $versionFile"
    }

    return ($line -replace "m_EditorVersion:\s*", "").Trim()
}

function Resolve-UnityExecutable {
    param(
        [string]$UnityPath,
        [string]$UnityVersion
    )

    if ($UnityPath) {
        if (-not (Test-Path -LiteralPath $UnityPath)) {
            throw "Unity executable was not found: $UnityPath"
        }

        return (Resolve-Path -LiteralPath $UnityPath).Path
    }

    $candidates = @(
        "${env:ProgramFiles}\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $hubRoot = "${env:ProgramFiles}\Unity\Hub\Editor"
    if (Test-Path -LiteralPath $hubRoot) {
        $match = Get-ChildItem -LiteralPath $hubRoot -Directory | Where-Object { $_.Name -eq $UnityVersion } | Select-Object -First 1
        if ($match) {
            $exe = Join-Path $match.FullName "Editor\Unity.exe"
            if (Test-Path -LiteralPath $exe) {
                return $exe
            }
        }
    }

    throw "Could not discover Unity $UnityVersion. Pass -UnityPath <path-to-Unity.exe>."
}

function Invoke-UnityTests {
    param(
        [Parameter(Mandatory = $true)][ValidateSet("EditMode", "PlayMode")][string]$TestPlatform,
        [string]$UnityPath
    )

    $projectPath = Get-ProjectRoot
    $unityVersion = Get-UnityVersion -ProjectPath $projectPath
    $unityExe = Resolve-UnityExecutable -UnityPath $UnityPath -UnityVersion $unityVersion
    $resultsDir = Join-Path $projectPath "TestResults"
    if (-not (Test-Path -LiteralPath $resultsDir)) {
        New-Item -ItemType Directory -Path $resultsDir | Out-Null
    }

    $lower = $TestPlatform.ToLowerInvariant()
    $resultsPath = Join-Path $resultsDir "$lower-results.xml"
    $logPath = Join-Path $resultsDir "$lower.log"

    if (Test-Path -LiteralPath $resultsPath) { Remove-Item -LiteralPath $resultsPath -Force }
    if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }

    if ($TestPlatform -eq "PlayMode") {
        $arguments = @(
            "-batchmode",
            "-nographics",
            "-projectPath", $projectPath,
            "-runTests",
            "-testPlatform", $TestPlatform,
            "-testResults", $resultsPath,
            "-logFile", $logPath
        )
    }
    else {
        $arguments = @(
            "-batchmode",
            "-nographics",
            "-projectPath", $projectPath,
            "-executeMethod", "SLG.Tests.SLGCommandLineTestRunner.Run",
            "-slgTestPlatform", $TestPlatform,
            "-slgTestResults", $resultsPath,
            "-logFile", $logPath
        )
    }

    Write-Host "Unity: $unityExe"
    Write-Host "Project: $projectPath"
    Write-Host "Running $TestPlatform tests"
    Write-Host "Results: $resultsPath"
    Write-Host "Log: $logPath"

    $argumentLine = ($arguments | ForEach-Object { ConvertTo-UnityArgument $_ }) -join " "
    Write-Host "Command: `"$unityExe`" $argumentLine"
    $process = Start-Process -FilePath $unityExe -ArgumentList $argumentLine -Wait -PassThru -NoNewWindow
    $exitCode = $process.ExitCode

    if (-not (Test-Path -LiteralPath $resultsPath)) {
        Write-Host "Result XML was not generated: $resultsPath"
        if ($exitCode -eq 0) { $exitCode = 1 }
    }

    if (-not (Test-Path -LiteralPath $logPath)) {
        Write-Host "Unity log was not generated: $logPath"
        if ($exitCode -eq 0) { $exitCode = 1 }
    }

    return $exitCode
}

Export-ModuleMember -Function Invoke-UnityTests, Get-ProjectRoot, Get-UnityVersion, Resolve-UnityExecutable

function ConvertTo-UnityArgument {
    param([string]$Value)

    if ($Value -match '[\s"]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }

    return $Value
}
