# RULES.md

## Purpose

This file defines two categories of hard rules: Scientific Rigor and Confound Control mechanisms that protect the validity of every test result, and Input Validation Rules that define how the system must behave when it receives invalid or edge-case user input. Both categories must be enforced without exception — they are not optional quality improvements, they are correctness requirements.

---

## 1. Scientific Rigor & Confound Control

| Mechanism | Purpose |
|---|---|
| Blind Testing | Prevents placebo effect from the user knowing which sensitivity value is currently being tested |
| Counterbalancing | Prevents order effect (fatigue or warm-up bias accumulating across the test sequence) |
| Adaptation Period (50% discard) | Prevents cold-start performance at the beginning of testing a new value from contaminating the aggregate result — see `RESEARCH.md`, Section 8 |
| Outlier Detection | Applies the metric-specific 3-SD rule after adaptation, preserves raw data, and flags without automatic exclusion; only a separately documented data-quality error may be excluded from the authoritative score (see `RESEARCH.md`, Section 12) |
| Fatigue Detection | Compares first-half versus second-half Performance Score after adaptation; a decline greater than 15% is flagged but never excluded from Winner selection (see `RESEARCH.md`, Section 17.2) |
| Statistical Significance Test | Uses exactly 10 fresh matched complete-battery pairs, 5/5 order counterbalancing, and exhaustive 1024-assignment two-sided paired sign-flip randomization at `alpha = 0.05`; a non-significant result is a statistical tie carried into Phase 2 (see `RESEARCH.md`, Section 11.1) |

Every one of these mechanisms must be implemented before a Phase (see `FEATURES.md`, Section 3) can be considered functionally complete. None of them are optional add-ons — skipping any one of them invalidates the scientific basis of the entire testing protocol.

## 2. Injury Risk & Ergonomic Safety Notice

The system must display general, non-diagnostic warning messages when it detects a configuration pattern that may increase ergonomic risk. These are informational flags only — never a medical diagnosis, and never a blocker to continued use.

Warning conditions:

The numeric wrist-strain condition below is defined in `RESEARCH.md`, Section 16, from SensCalibr8 Project Proposal V3.0, Section 9. It is informational and non-diagnostic only.

```
IF edpi < 200 AND movement_strategy == "wrist":
    SHOW warning_flag("low_edpi_wrist_strain")

IF cm_360 > mousepad_width_cm:
    SHOW warning_flag("mousepad_constraint_violation")
```

Every triggered flag must be recorded in `injury_risk_flags` (see `ARCHITECTURE.md`) with a timestamp and the eDPI value at the time of triggering. Flags must never block test execution or alter calculated results — they are surfaced to the user as a simple banner (see `DESIGN.md`, Section 2.4), and the user may acknowledge them. These warnings are general information only and must never be presented as replacing professional medical advice.

## 3. Input Validation Rules

- Hardware DPI must be a positive integer. Reject and prompt again on non-positive or non-integer input before allowing any PSA Baseline calculation to run.
- Current in-game sensitivity must be a positive numeric value. Reject missing, non-numeric, zero, or negative input before comparing it with the PSA Baseline.
- Configured mouse polling rate must be a positive numeric value in Hz. Reject missing, non-numeric, zero, or negative input; store it as supporting metadata and never substitute it for measured session cadence.
- If a calculated eDPI falls below the floor of 160 (see `RESEARCH.md`, Section 2), the system must auto-adjust it upward to 160 and clearly notify the user of the adjustment. Never silently apply the floor without informing the user.
- Profile names must be unique within the local installation. Reject profile creation with a duplicate name and prompt the user to choose a different one.
- The system must prevent deletion of a profile that is currently active/selected. The user must exit that profile before it can be deleted (see `FEATURES.md`, Section 1.3).
- Mousepad width and height must be positive numeric values greater than zero; reject non-positive input before allowing Mousepad Constraint Validation to run.

## 4. Data Integrity & Data Export Strategy

### 4.1 Data Export

The system supports profile-separated JSON and CSV Data Export for portability and external analysis. Import/Restore is explicitly Out of Scope (see `CONTEXT.md`), so exports must not be presented as restorable backups or as a guaranteed recovery mechanism for a corrupted SQLite file.

### 4.2 Formula Versioning

Every result computed from the Performance Score formula must be tagged with the `formula_version` and immutable calibration configuration active at the time of calculation (see `ARCHITECTURE.md`, `sensitivity_tests`). The configuration identifies normalization bounds, signal pipeline, geometry, tier cutpoints, and protocol contracts. Historical data must never be silently recalculated under a newer version.

Production may load only the SHA-pinned accepted `calibration_config_v1` contract. It must verify the acceptance hash and complete 20-field schema projection before use, and fail closed on draft/incomplete status, mutation, source-hash drift, missing/non-finite values, or disagreement between scalar columns and embedded JSON contracts. Accepted configuration bytes are immutable; changes require a new version and acceptance envelope.

Under accepted contract `sc8-p0-r6-scoring-statistics-contract-v1`, production scoring must preserve the source formula without a final clamp, use only the pinned fixed bounds, fail closed at Submovement Penalty 1.0 when a complete authoritative shot mode has zero hits, and treat `abs(mean Performance Score) <= 1e-9` as undefined for CV stabilization. Exploratory batteries must never be reused in confirmatory pairs; incomplete pairs do not count and early stopping is prohibited.
