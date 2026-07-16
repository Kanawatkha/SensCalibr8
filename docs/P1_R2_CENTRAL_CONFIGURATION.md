# P1-R2: Central Configuration and Domain Contracts

## Scope

P1-R2 supplies the production boundary for the accepted Phase 0 calibration configuration. It does not create a SQLite database, profile behavior, scoring behavior, target spawning, or input capture.

## Accepted configuration only

`FrozenCalibrationConfigurationLoader` reads only the P0-R7 acceptance envelope and the accepted `calibration_config_v1` bytes. It is fail-closed: it verifies the acceptance identity and SHA-256, `accepted`/immutable status, the explicit timing-waiver limitation, exactly 20 non-ID database projection fields, finite scalar values, canonical embedded JSON, identity/version agreement, duplicated scalar/embedded-contract agreement, and all six frozen source hashes.

The loader never rewrites either artifact. A future calibration change must create a new configuration version and acceptance envelope.

The non-Phase-0 research constants needed by later calculation rounds (PSA/eDPI, cm/360 conversion, Fitts multiplier, ergonomic references, and outlier multiplier) are similarly centralized in the accepted immutable `config/research-constants-v1.json` contract, through typed C# and Python readers. Phase 0-owned scoring, geometry, signal, and protocol constants remain in the SHA-pinned calibration configuration rather than being duplicated.

## Layer boundaries

- `SensCalibr8.Core` holds immutable value/domain contracts. It has no Unity or persistence reference.
- `SensCalibr8.Services` owns file/configuration loading and validation.
- Python uses an equivalent frozen dataclass reader for analysis-side validation.
- Both runtimes validate the shared `config/production-config-parity-v1.json` manifest against the common accepted configuration bytes.

## Deferred work

P1-R3 will create the SQLite schema and store the validated configuration projection transactionally. P2 onward will consume the configuration for calculations and user flows. No production Test Engine behavior is introduced in this round.
