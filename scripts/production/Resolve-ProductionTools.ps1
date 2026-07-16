Set-StrictMode -Version Latest

function Resolve-SensCalibr8UnityEditor {
    param(
        [string]$ExplicitPath,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $candidates = @(
        $ExplicitPath,
        $env:SENSCALIBR8_UNITY_PATH,
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$Version\Editor\Unity.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw "Unity $Version was not found. Pass -UnityPath or set SENSCALIBR8_UNITY_PATH."
}

function Resolve-SensCalibr8Python {
    param([string]$ExplicitPath, [Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $local = Join-Path $RepositoryRoot ".venv\Scripts\python.exe"
    $candidates = @($ExplicitPath, $env:SENSCALIBR8_PYTHON, $local) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    $command = Get-Command python -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    throw "Pinned Python was not found. Pass -PythonPath or set SENSCALIBR8_PYTHON."
}

function Provision-SensCalibr8SqliteRuntime {
    param(
        [Parameter(Mandatory = $true)][string]$UnityPath,
        [Parameter(Mandatory = $true)]$Manifest,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $unityEditorDirectory = Split-Path -Parent $UnityPath
    $source = Join-Path $unityEditorDirectory $Manifest.sqlite.native_unity_relative_source
    $destination = Join-Path $RepositoryRoot $Manifest.sqlite.native_project_plugin_path
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Pinned Unity native SQLite runtime was not found: $source"
    }
    $actualHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $Manifest.sqlite.native_sha256) {
        throw "Pinned Unity native SQLite runtime hash mismatch."
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    return $destination
}
