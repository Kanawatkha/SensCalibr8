param([string]$UnityPath)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
. (Join-Path $PSScriptRoot "Resolve-ProductionTools.ps1")
$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot "config\production-environment-v1.json") -Raw | ConvertFrom-Json
$unity = Resolve-SensCalibr8UnityEditor -ExplicitPath $UnityPath -Version $manifest.unity.editor_version
$null = Provision-SensCalibr8SqliteRuntime -UnityPath $unity -Manifest $manifest -RepositoryRoot $repositoryRoot
$artifactDirectory = Join-Path $repositoryRoot "artifacts\p1-r1"
$results = Join-Path $artifactDirectory "production-editmode-results.xml"
$log = Join-Path $artifactDirectory "production-editmode.log"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$project = Join-Path $repositoryRoot "app"
$arguments = @(
    "-batchmode", "-nographics",
    "-projectPath", "`"$project`"",
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", "`"$results`"",
    "-logFile", "`"$log`""
)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Unity EditMode tests failed; see $log" }
[xml]$document = Get-Content -LiteralPath $results -Raw
$run = $document.'test-run'
if ($run.result -ne "Passed" -or [int]$run.failed -ne 0) {
    throw "Unity EditMode result is not clean: result=$($run.result), failed=$($run.failed)."
}
[ordered]@{ result=$run.result; total=[int]$run.total; passed=[int]$run.passed; failed=[int]$run.failed; skipped=[int]$run.skipped; inconclusive=[int]$run.inconclusive } | ConvertTo-Json
