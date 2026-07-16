# P1-R1 Production Project Foundation

## Scope

P1-R1 establishes the buildable production shell and toolchain contract. It intentionally contains no database schema, repository implementation, profile behavior, formulas, scoring, targets, input capture, mode behavior, or product UI.

## Accepted Environment

| Component | Pin |
|---|---|
| Environment contract | `sc8-production-environment-v1` |
| Unity | `6000.5.3f1` (`c2eb47b3a2a9`) |
| Target | Windows x86-64 |
| Input System | `1.19.0`, package-only handler |
| Test Framework | `1.7.0` |
| Python | `3.12.13` |
| pandas / NumPy / matplotlib | `3.0.1` / `2.3.5` / `3.11.0` |
| Runtime connectivity | Fully offline |
| Calibration dependency | `calibration_config_v1`, SHA-256 `c618a3e50473b072b107d2e2926f4d05e7bbafa33bc04af8beb5eb5f775b3b2e` |

The full Python transitive environment is frozen in `analysis/requirements.lock`. Matplotlib 3.11.0 is an official Production/Stable release with Python 3.12 support: <https://pypi.org/project/matplotlib/3.11.0/>.

## Production Boundaries

| Boundary | Assembly/path | Allowed dependencies |
|---|---|---|
| Core | `SensCalibr8.Core` | none; no Unity engine |
| Data | `SensCalibr8.Data` | Core; no Unity engine |
| Service | `SensCalibr8.Services` | Core, Data; no Unity engine |
| UI | `SensCalibr8.UI` | Core, Services |
| Test Logic | `SensCalibr8.TestLogic` | Core, Services, Unity Input System |
| Analysis | `analysis/src/senscalibr8_analysis` | future Data Layer exports only |

UI and Test Logic cannot reference Data directly. No production source references `SensCalibr8.Calibration` or `calibration/harness`.

## Repeatable Commands

All generated output goes to ignored `.venv`, `artifacts`, `app/Library`, and `app/Builds` locations.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Prepare-ProductionProject.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Verify-ProductionEnvironment.ps1 -OutputPath artifacts\p1-r1\environment-preflight.json
.\.venv\Scripts\python.exe -m unittest discover -s analysis\tests -p "test_*.py" -v
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Test-Production.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\production\Build-Production.ps1
```

`ExecutionPolicy Bypass` is process-local and does not modify system policy.

## Acceptance Result

| Check | Result |
|---|---|
| Environment preflight | Passed all Unity/Python/package/config-hash pins |
| Python P1-R1 tests | 6/6 passed |
| Phase 0 Python regression | 72/72 passed |
| Production Unity EditMode | 7/7 passed; 0 failed/skipped/inconclusive |
| Windows x86-64 shell build | Passed; `SensCalibr8.exe` produced |
| Layer dependency audit | Passed |
| Calibration-harness isolation | Passed |
| Product behavior added | None |

The first Unity test run exposed a self-scanning fixture: the guard found its own forbidden assertion literal. The scanner was restricted to non-test production source and the clean rerun passed 7/7. Direct GUI-executable invocation also did not expose `$LASTEXITCODE` under Windows PowerShell StrictMode; all Unity scripts now use hidden `Start-Process -Wait -PassThru` execution and verify the process exit code.

P1-R1 is complete. P1-R2 owns typed immutable configuration/domain contracts and must be authorized separately.
