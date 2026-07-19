param(
    [string]$BuildPath,
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ([string]::IsNullOrWhiteSpace($BuildPath)) { $BuildPath = Join-Path $repositoryRoot "app\Builds\Windows" }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repositoryRoot "artifacts\release-candidate\SensCalibr8-Windows-x86_64" }
$buildRoot = (Resolve-Path -LiteralPath $BuildPath).Path
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path -LiteralPath (Join-Path $buildRoot "SensCalibr8.exe") -PathType Leaf)) { throw "SensCalibr8.exe was not found in $buildRoot" }
if (-not (Test-Path -LiteralPath (Join-Path $buildRoot "SensCalibr8_Data") -PathType Container)) { throw "SensCalibr8_Data was not found in $buildRoot" }
if (Test-Path -LiteralPath $packageRoot) { throw "Release package directory already exists; choose a new OutputDirectory." }

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Get-ChildItem -LiteralPath $buildRoot -Force | Copy-Item -Destination $packageRoot -Recurse -Force
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot "SensCalibr8.exe") -PathType Leaf)) { throw "Release executable was not copied into the package." }
Copy-Item -LiteralPath (Join-Path $repositoryRoot "config") -Destination $packageRoot -Recurse -Force
$runtimeContractFiles = @(
    "calibration/plans/calibration-config-v1.json",
    "calibration/plans/p0-r7-calibration-config-accepted-v1.json",
    "calibration/plans/p0-r3-timing-contract-accepted-v1.json",
    "calibration/evidence/p0-r3/p0-r3-owner-waiver-closure.json",
    "calibration/plans/p0-r4-geometry-accepted-v1.json",
    "calibration/plans/p0-r5-signal-mode-accepted-v1.json",
    "calibration/plans/p0-r6-scoring-statistics-accepted-v1.json",
    "calibration/plans/p0-r6-scoring-statistics-candidate-v1.json"
)
foreach ($relativeFile in $runtimeContractFiles) {
    $source = Join-Path $repositoryRoot ($relativeFile -replace '/', '\')
    $destination = Join-Path $packageRoot ($relativeFile -replace '/', '\')
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Runtime contract is missing: $source" }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}
New-Item -ItemType Directory -Path (Join-Path $packageRoot "docs") -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\RELEASE_OPERATOR_GUIDE.md") -Destination (Join-Path $packageRoot "docs\RELEASE_OPERATOR_GUIDE.md") -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\RELEASE_DEPENDENCY_LICENSES.md") -Destination (Join-Path $packageRoot "docs\RELEASE_DEPENDENCY_LICENSES.md") -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\P7_R6_RELEASE_PACKAGING_CLEAN_MACHINE.md") -Destination (Join-Path $packageRoot "docs\P7_R6_RELEASE_PACKAGING_CLEAN_MACHINE.md") -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\FINAL_OPERATOR_RELEASE_HANDOFF.md") -Destination (Join-Path $packageRoot "docs\FINAL_OPERATOR_RELEASE_HANDOFF.md") -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "scripts\production\Smoke-Test-Release.ps1") -Destination (Join-Path $packageRoot "Smoke-Test-Release.ps1") -Force

$files = Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Where-Object { $_.Name -ne "RELEASE_MANIFEST.json" } | Sort-Object FullName
$checksums = foreach ($file in $files) {
    $relative = $file.FullName.Substring($packageRoot.Length).TrimStart('\').Replace('\', '/')
    [ordered]@{ path = $relative; size_bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifest = [ordered]@{
    schema_version = "sc8-release-manifest-v1"
    status = "release-candidate"
    platform = "Windows-x86_64"
    entrypoint = "SensCalibr8.exe"
    frozen_environment_manifest = "config/production-environment-v1.json"
    offline_runtime_required = $true
    import_restore_supported = $false
    generated_utc = [DateTime]::UtcNow.ToString("O")
    files = @($checksums)
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packageRoot "RELEASE_MANIFEST.json") -Encoding UTF8
[ordered]@{ status = "passed"; package = $packageRoot; file_count = $checksums.Count; manifest = (Join-Path $packageRoot "RELEASE_MANIFEST.json") } | ConvertTo-Json
