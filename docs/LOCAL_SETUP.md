# SensCalibr8 Local Production Setup

## Supported Environment

The authoritative machine-readable contract is `config/production-environment-v1.json`.

- Windows x86-64
- Unity `6000.5.3f1` (`c2eb47b3a2a9`)
- Unity Input System `1.19.0`, package-only input handler
- Unity Test Framework `1.7.0`
- Python `3.12.13`
- pandas `3.0.1`, NumPy `2.3.5`, matplotlib `3.11.0`
- Native SQLite `3.49.1` C API runtime, SHA-256 pinned in `config/production-environment-v1.json`
- Offline application runtime; network access is not a product dependency

Matplotlib 3.11.0 is pinned because the official PyPI package is marked Production/Stable, requires Python 3.11 or newer, and publishes Python 3.12 support: <https://pypi.org/project/matplotlib/3.11.0/>.

## Repository Boundaries

- `app/` — production Unity project only
- `analysis/` — offline Python report/analysis package only
- `config/` — machine-readable production environment contract
- `scripts/production/` — repeatable preflight, prepare, test, and build commands
- `calibration/` — isolated Phase 0 harness/contracts/evidence; production code must not copy its harness classes
- `artifacts/` — ignored local logs/test results

Production assembly direction is fixed:

```
Core -> Data -> Services -> UI
                         -> TestLogic (+ Unity Input System)
```

`UI` and `TestLogic` never reference `Data` directly. Only `Data` may implement SQLite access. Python Analysis consumes future Data Layer exports and never accesses Unity classes.

## First-Time Setup

1. Install the pinned Unity version through Unity Hub.
2. Create a project-local Python environment using Python 3.12.13:

   ```powershell
   <python-3.12.13> -m venv .venv
   .\.venv\Scripts\python.exe -m pip install --upgrade pip
   .\.venv\Scripts\python.exe -m pip install -r analysis\requirements.lock
   ```

3. Prepare the Unity project and deterministic empty bootstrap scene:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Prepare-ProductionProject.ps1
   ```

   This command also copies the SHA-256-pinned native SQLite runtime from the pinned Unity editor into the ignored local path `app/Assets/Plugins/sqlite3.dll`. Do not add that generated binary or its `.meta` file to Git. Preflight, test, and build commands repeat the same verification/provisioning step automatically.

4. Verify all pins and the frozen Phase 0 configuration hash:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Verify-ProductionEnvironment.ps1 -OutputPath artifacts\p1-r1\environment-preflight.json
   ```

## Repeatable Validation

```powershell
.\.venv\Scripts\python.exe -m unittest discover -s analysis\tests -p "test_*.py" -v
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Test-Production.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Build-Production.ps1
```

The bypass applies only to the spawned PowerShell process and does not change the machine-wide execution policy.

Generated Unity `Library`, logs, local test results, `.venv`, `artifacts`, and builds are ignored. Keep `Assets`, `.meta`, `Packages`, and `ProjectSettings` under source control.

P1-R3 provides the versioned schema, migration/bootstrap boundary, and real SQLite integration tests. Repository behavior, profiles, scoring, targets, input capture, and the Test Engine belong to later authorized rounds.
