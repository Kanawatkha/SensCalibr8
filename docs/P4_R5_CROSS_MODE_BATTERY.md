# P4-R5 — Cross-mode battery integration

## Scope

P4-R5 composes the four already implemented production modes into one deterministic battery workflow. It introduces no Performance Score, candidate selection, adaptation rule, or mode-local numeric constant.

## Architectural boundary

`CrossModeBatteryWorkflow` belongs to the new `SensCalibr8.Integration` assembly. This is the only assembly added for composition that simultaneously needs Test Logic, Service orchestration, and Data contracts. `SensCalibr8.TestLogic` continues to reference only Core, Services, and the Unity Input System; it has no direct SQLite/Data dependency.

The production factory instantiates the existing `CloseFlickMode`, `FarFlickMode`, `TrackingMode`, and `MicroCorrectionMode`. The integration workflow owns no parallel mode implementation or scoring path.

## Battery contract

- A plan is bound to one consistent profile, cycle, candidate, battery, phase, and sensitivity lineage.
- The deterministic counterbalanced order contains every `TestMode` exactly once and rotates by battery repetition ordinal.
- The public plan reveals only the opaque blind candidate label and ordered mode identities. Candidate identity and numeric sensitivity remain internal.
- One mode may be active at a time. The next mode cannot begin until the current mode is completed and its capture persists. After four completions, the plan rejects any additional start.
- Each start creates one session attempt and carries a fresh per-mode sequence audit. The accepted frozen calibration configuration must match the database identity.

## Completion and evidence boundary

`CompleteActive` requires the shared state machine to be `Completed`, which means the mode has completed and reported before persistence begins. It then requires timing acceptance, exact session lineage, and raw/derived evidence appropriate to the active mode:

- Close Flick, Far Flick, and Micro-Correction: exactly 30 shot rows, with no Tracking trials or windows.
- Tracking: exactly 18 trials and 108 one-second windows, with no shot rows.
- Every row's sensitivity equals the selected candidate and every adaptation field remains null until the existing transactional finalization layer calculates it.

The workflow delegates the actual atomic write, adaptation finalization, and fourth-mode battery completion to `SessionBatteryPersistenceService`. It does not calculate a score or modify raw evidence.

## Verification

The EditMode suite covers opaque blind-label exposure, all-four-mode completion, transactional battery completion only at the fourth session, duplicate/start guards, mandatory completed-mode report, session-lineage rejection, exact mode sample contracts, failed timing rejection, and separation of the Integration assembly from Test Logic.
