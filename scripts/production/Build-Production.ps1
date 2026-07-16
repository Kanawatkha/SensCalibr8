param([string]$UnityPath)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
. (Join-Path $PSScriptRoot "Resolve-ProductionTools.ps1")
$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot "config\production-environment-v1.json") -Raw | ConvertFrom-Json
$unity = Resolve-SensCalibr8UnityEditor -ExplicitPath $UnityPath -Version $manifest.unity.editor_version
$null = Provision-SensCalibr8SqliteRuntime -UnityPath $unity -Manifest $manifest -RepositoryRoot $repositoryRoot
$artifactDirectory = Join-Path $repositoryRoot "artifacts\p1-r1"
$log = Join-Path $artifactDirectory "production-build.log"
$build = Join-Path $repositoryRoot "app\Builds\Windows\SensCalibr8.exe"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$env:SENSCALIBR8_BUILD_PATH = $build
try {
    $project = Join-Path $repositoryRoot "app"
    $arguments = @(
        "-batchmode", "-nographics", "-quit",
        "-projectPath", "`"$project`"",
        "-executeMethod", "SensCalibr8.Editor.ProductionBuild.BuildWindows",
        "-logFile", "`"$log`""
    )
    $process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $build)) {
        throw "Unity production build failed; see $log"
    }
} finally {
    Remove-Item Env:SENSCALIBR8_BUILD_PATH -ErrorAction SilentlyContinue
}
[ordered]@{ status="passed"; executable=$build; size_bytes=(Get-Item -LiteralPath $build).Length } | ConvertTo-Json
