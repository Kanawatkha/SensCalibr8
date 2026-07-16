# P3-R1 — Engine contracts and state machine

## Scope

P3-R1 establishes the mode-independent lifecycle boundary for the production Test Engine. It contains no input capture, timer implementation, arena behavior, persistence, adaptation, mode-specific metric, or scoring logic.

## Shared contracts

Every production mode implements `ITestMode` with the same operations: `Prepare`, `Start`, `Capture`, `End`, `Report`, `Cancel`, and `Recover`. Immutable engine contexts carry the selected profile, cycle, protocol candidate, protocol battery, session run identity, Test Mode, and accepted calibration-configuration version.

Construction fails when profile/cycle/candidate/battery lineage disagrees, when candidate and battery phase or sensitivity differ, when the mode differs from the session, or when the accepted frozen configuration is absent, internally incomplete, or version-mismatched.

## State policy

The normal path is:

`Created -> Prepared -> Capturing -> Ending -> Completed`

`Capture` repeats only in `Capturing`. A callback exception or incomplete completion enters `Faulted`. `Recover` is permitted only from `Faulted` and returns to `Prepared`, requiring a fresh `Start`. Cancellation ends any nonterminal session in `Cancelled`. `Completed` and `Cancelled` are terminal states.

## Verification

Eight EditMode tests cover lifecycle callback order, invalid transitions, callback faults, recovery, cancellation, incomplete completion, lineage mismatch, configuration rejection, and mode mismatch. Completion verification passed Unity EditMode 81/81, production Python 11/11, Phase 0 regression 72/72, and the Windows production build.
