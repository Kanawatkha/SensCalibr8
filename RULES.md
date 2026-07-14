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
| Outlier Detection | Flags shots beyond 3 standard deviations as accidental, not reflective of the tested sensitivity |
| Fatigue Detection | Flags within-session performance decline caused by tiredness, distinguishing it from a genuine sensitivity problem |
| Statistical Significance Test | Prevents declaring a "Winner" between candidate values based on statistical noise alone |

Every one of these mechanisms must be implemented before a Phase (see `FEATURES.md`, Section 3) can be considered functionally complete. None of them are optional add-ons — skipping any one of them invalidates the scientific basis of the entire testing protocol.

## 2. Injury Risk & Ergonomic Safety Notice

The system must display general, non-diagnostic warning messages when it detects a configuration pattern that may increase ergonomic risk. These are informational flags only — never a medical diagnosis, and never a blocker to continued use.

Warning conditions:

```
IF edpi < 200 AND movement_strategy == "wrist":
    SHOW warning_flag("low_edpi_wrist_strain")

IF cm_360 > mousepad_width_cm:
    SHOW warning_flag("mousepad_constraint_violation")
```

Every triggered flag must be recorded in `injury_risk_flags` (see `ARCHITECTURE.md`) with a timestamp and the eDPI value at the time of triggering. Flags must never block test execution or alter calculated results — they are surfaced to the user as a simple banner (see `DESIGN.md`, Section 2.4), and the user may acknowledge them. These warnings are general information only and must never be presented as replacing professional medical advice.

## 3. Input Validation Rules

- Hardware DPI must be a positive integer. Reject and prompt again on non-positive or non-integer input before allowing any PSA Baseline calculation to run.
- If a calculated eDPI falls below the floor of 160 (see `RESEARCH.md`, Section 2), the system must auto-adjust it upward to 160 and clearly notify the user of the adjustment. Never silently apply the floor without informing the user.
- Profile names must be unique within the local installation. Reject profile creation with a duplicate name and prompt the user to choose a different one.
- The system must prevent deletion of a profile that is currently active/selected. The user must exit that profile before it can be deleted (see `FEATURES.md`, Section 1.3).
- Mousepad width and height must be positive numeric values greater than zero; reject non-positive input before allowing Mousepad Constraint Validation to run.

## 4. Data Integrity & Backup Strategy

### 4.1 Export / Backup

The system supports exporting data as JSON and CSV, separated by profile, to protect against data loss in the event that the SQLite file becomes corrupted.

### 4.2 Formula Versioning

Every result computed from the Performance Score formula must be tagged with the `formula_version` value active at the time of calculation (see `ARCHITECTURE.md`, `sensitivity_tests.formula_version`). This prevents ambiguity if formula weights are revised in the future, and preserves the ability to meaningfully compare historical results against current ones.
