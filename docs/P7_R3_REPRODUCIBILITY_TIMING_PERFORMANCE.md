# P7-R3 Reproducibility, Timing, and Performance

## Purpose

This acceptance round verifies that the frozen calibration configuration produces reproducible target conditions and that a complete frozen shot contract can persist and be read without losing raw input evidence. It records an environment-specific baseline; it does not introduce a new performance threshold or alter any frozen scientific configuration.

## Reproducibility controls verified

- Two fresh `DeterministicTargetSequencer` instances receive the same `SequenceSeedContext` for each of the four test modes. Their SHA-256 seed audit values and complete condition signatures must match.
- `FrozenInputTimingContract` is read from `calibration_config_v1`; the fixture verifies the sampling rate and resampling tolerance against the persisted frozen configuration.
- `FrozenFramePolicy` remains the documented target-environment policy: 144 Hz target frame rate, VSync count 0, with adaptive sync disabled by the calibration procedure.

## Persistence and raw-data check

The fixture uses `FrozenSequenceContract.ShotTrials` rather than a local trial constant, creates a 30-shot close-flick capture, and writes one raw mouse sample for every shot through `SessionCaptureRepository`'s transaction boundary. It then reads the profile through `AnalysisDatasetService` and verifies:

- exactly 30 `shots` and 30 `mouse_samples` rows are present;
- the first persisted `raw_delta_x` is unchanged;
- the session is visible to the analysis dataset; and
- the SQLite database is non-empty.

## Observed baseline

Unity EditMode run on the frozen target environment, 2026-07-19:

| Measurement | Observed value |
| --- | ---: |
| Frozen shot contract | 30 shots |
| Raw mouse samples | 30 samples |
| SQLite file size after fixture | 380,928 bytes |
| Transactional persistence elapsed time | 8.017 ms |
| Analysis dataset read elapsed time | 1.990 ms |

These values are diagnostic baselines only. Hardware load, filesystem cache, and editor state can vary; the acceptance requirement is preservation and successful repeated execution, not a new pass/fail latency limit.

## Automated evidence

- `P7R3ReproducibilityTimingPerformanceTests.RepeatedFreshSequencersProduceTheSameFrozenConditionsForEveryMode`
- `P7R3ReproducibilityTimingPerformanceTests.FullFrozenShotContractPreservesRawRowsAndReportsObservedPersistenceReadCost`
- Unity EditMode: 226/226 passed, 0 failed, skipped, or inconclusive.
