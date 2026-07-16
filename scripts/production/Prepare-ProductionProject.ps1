param([string]$UnityPath)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
. (Join-Path $PSScriptRoot "Resolve-ProductionTools.ps1")
$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot "config\production-environment-v1.json") -Raw | ConvertFrom-Json
$unity = Resolve-SensCalibr8UnityEditor -ExplicitPath $UnityPath -Version $manifest.unity.editor_version
$null = Provision-SensCalibr8SqliteRuntime -UnityPath $unity -Manifest $manifest -RepositoryRoot $repositoryRoot
$log = Join-Path $repositoryRoot "artifacts\p1-r1\prepare-unity.log"
New-Item -ItemType Directory -Path (Split-Path -Parent $log) -Force | Out-Null
$project = Join-Path $repositoryRoot "app"
$arguments = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", "`"$project`"",
    "-executeMethod", "SensCalibr8.Editor.ProductionBuild.PrepareProject",
    "-logFile", "`"$log`""
)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Unity production-project preparation failed; see $log" }
