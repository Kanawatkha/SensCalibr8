param([string]$PackagePath)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$packageRoot = if ([string]::IsNullOrWhiteSpace($PackagePath)) { (Resolve-Path (Split-Path -Parent $MyInvocation.MyCommand.Path)).Path } else { (Resolve-Path -LiteralPath $PackagePath).Path }
$entrypoint = Join-Path $packageRoot "SensCalibr8.exe"
$dataDirectory = Join-Path $packageRoot "SensCalibr8_Data"
$manifestPath = Join-Path $packageRoot "RELEASE_MANIFEST.json"
foreach ($required in @($entrypoint, $dataDirectory, (Join-Path $packageRoot "calibration"), (Join-Path $packageRoot "config"), $manifestPath)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Release package item is missing: $required" }
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.status -ne "release-candidate" -or $manifest.offline_runtime_required -ne $true -or $manifest.import_restore_supported -ne $false) { throw "Release manifest scope flags are invalid." }
foreach ($entry in @($manifest.files)) {
    $relativePath = [string]$entry.path
    $filePath = Join-Path $packageRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Manifest file is missing: $relativePath" }
    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne ([string]$entry.sha256).ToLowerInvariant()) { throw "Manifest checksum mismatch: $relativePath" }
}
$smokeRoot = Join-Path ([IO.Path]::GetTempPath()) ("senscalibr8-clean-smoke-" + [Guid]::NewGuid().ToString("N"))
$log = Join-Path $packageRoot "SMOKE_TEST.log"
$startupTimeoutSeconds = 15
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
$process = $null
try {
    $process = Start-Process -FilePath $entrypoint -WorkingDirectory $packageRoot -ArgumentList @("-batchmode", "-nographics", "-userDataPath", $smokeRoot, "-logFile", $log) -PassThru -WindowStyle Hidden
    $deadline = (Get-Date).AddSeconds($startupTimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 250
        if ($process.HasExited) { throw "Clean-machine smoke exited with code $($process.ExitCode). See $log" }
    } while (-not (Test-Path -LiteralPath $log) -and (Get-Date) -lt $deadline)
    if (-not (Test-Path -LiteralPath $log)) { throw "Clean-machine smoke did not produce a player log within the startup window." }
    $errors = Select-String -LiteralPath $log -Pattern "InvalidOperationException|NullReferenceException|MissingMethodException|startup error" -ErrorAction SilentlyContinue
    if ($null -ne $errors) { throw "Clean-machine smoke reported a startup exception. See $log" }
    Stop-Process -Id $process.Id -Force
    [ordered]@{ status = "passed"; package = $packageRoot; startup_observed = $true; stopped_by_runner = $true; log = $log } | ConvertTo-Json
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $smokeRoot) { Remove-Item -LiteralPath $smokeRoot -Recurse -Force }
}
