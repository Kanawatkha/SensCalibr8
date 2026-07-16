param(
    [string]$UnityPath,
    [string]$PythonPath,
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
. (Join-Path $PSScriptRoot "Resolve-ProductionTools.ps1")
$manifestPath = Join-Path $repositoryRoot "config\production-environment-v1.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$unity = Resolve-SensCalibr8UnityEditor -ExplicitPath $UnityPath -Version $manifest.unity.editor_version
$python = Resolve-SensCalibr8Python -ExplicitPath $PythonPath -RepositoryRoot $repositoryRoot
$sqliteRuntime = Provision-SensCalibr8SqliteRuntime -UnityPath $unity -Manifest $manifest -RepositoryRoot $repositoryRoot

$projectVersion = Get-Content -LiteralPath (Join-Path $repositoryRoot "app\ProjectSettings\ProjectVersion.txt") -Raw
if ($projectVersion -notmatch [regex]::Escape($manifest.unity.editor_version)) {
    throw "Unity ProjectVersion does not match the pinned environment."
}
$unityProductVersion = (Get-Item -LiteralPath $unity).VersionInfo.ProductVersion
if ($unityProductVersion -notmatch [regex]::Escape($manifest.unity.editor_version)) {
    throw "Installed Unity version does not match $($manifest.unity.editor_version): $unityProductVersion"
}

$pythonVersion = (& $python -c "import sys; print('.'.join(map(str, sys.version_info[:3])))").Trim()
if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne $manifest.python.version) {
    throw "Python version must be $($manifest.python.version); found $pythonVersion."
}
$dependencyJson = & $python -c "import importlib.metadata as m,json; print(json.dumps({n:m.version(n) for n in ('matplotlib','numpy','pandas')},sort_keys=True))"
if ($LASTEXITCODE -ne 0) { throw "Pinned Python dependencies are not installed." }
$dependencies = $dependencyJson | ConvertFrom-Json
foreach ($name in @("matplotlib", "numpy", "pandas")) {
    if ($dependencies.$name -ne $manifest.python.dependencies.$name) {
        throw "$name must be $($manifest.python.dependencies.$name); found $($dependencies.$name)."
    }
}

$configPath = Join-Path $repositoryRoot ($manifest.calibration.config_path -replace '/', '\')
$configHash = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($configHash -ne $manifest.calibration.config_sha256) {
    throw "Frozen calibration configuration SHA-256 mismatch."
}
$packageManifest = Get-Content -LiteralPath (Join-Path $repositoryRoot "app\Packages\manifest.json") -Raw | ConvertFrom-Json
if ($packageManifest.dependencies.'com.unity.inputsystem' -ne $manifest.unity.input_system_version) {
    throw "Unity Input System package version mismatch."
}
if ($packageManifest.dependencies.'com.unity.test-framework' -ne $manifest.unity.test_framework_version) {
    throw "Unity Test Framework package version mismatch."
}

$result = [ordered]@{
    status = "passed"
    schema_version = $manifest.schema_version
    unity_editor = $manifest.unity.editor_version
    unity_path = $unity
    python = $pythonVersion
    python_path = $python
    dependencies = $dependencies
    calibration_config_version = $manifest.calibration.config_version
    calibration_config_sha256 = $configHash
    sqlite_provider = $manifest.sqlite.provider
    sqlite_native_sha256 = (Get-FileHash -LiteralPath $sqliteRuntime -Algorithm SHA256).Hash.ToLowerInvariant()
    offline_runtime_required = $manifest.offline_runtime_required
}
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repositoryRoot $OutputPath }
    New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedOutput) -Force | Out-Null
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutput -Encoding UTF8
}
$result | ConvertTo-Json -Depth 8
