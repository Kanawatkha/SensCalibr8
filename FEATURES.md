# FEATURES.md

## Purpose

This file specifies the exact behavior of every feature in SensCalibr8: the Multi-Profile System, the four Test Modes, the full Testing Protocol execution flow, and the Visualization/Reporting layer. `ARCHITECTURE.md` describes what the system is built from; this file describes what the system must do.

---

## 1. Multi-Profile System

Because this application may be used by multiple people on the same machine (e.g. friends testing on one PC), profiles must be fully isolated from one another.

### 1.1 Application Flow

```
Launch Application
  -> Slot Selection Screen (list of existing profiles + "Create New Slot")
  -> Select / Create Profile
  -> Main Dashboard (scoped to selected profile_id)
  -> Optional: Comparison Page (cross-profile analytics)
```

### 1.2 Physical Profile Setup

During profile creation, the user provides:

- Hardware DPI (manually entered, or derived via the Physical Ruler Test fallback described in `CONTEXT.md`)
- Current in-game sensitivity, stored for comparison with the calculated PSA baseline
- Dominant hand
- Crosshair color (selected once during profile creation and locked for the lifetime of the profile — see Section 2.2 below). Dot style and dot size are fixed by the application and are not user-configurable.
- Grip style (Fingertip / Palm / Claw / Hybrid) — descriptive field only, stored for the user's own reference, never used in any calculation
- Movement strategy (Wrist / Arm / Hybrid) — descriptive field only, same restriction as above
- Mousepad width and height in centimeters — used only for Mousepad Constraint Validation (see `RESEARCH.md`, Section 9)
- ADS multiplier — reference field only, stored for the user's own information, never tested via a dedicated test mode

### 1.3 Key Features

- Unlimited profile creation. Each profile stores its own DPI, dominant hand, crosshair configuration, physical profile data, and full test history, completely isolated from other profiles.
- Comparison Page: compares Consistency, Reaction Time Tier, and Performance Score across profiles. Comparisons must always be normalized via eDPI — raw in-game sensitivity values must never be compared directly, since DPI differs between users and makes raw sensitivity numbers meaningless across profiles.
- Delete Slot: supports full profile deletion via cascading delete (`ON DELETE CASCADE` at the database level, see `ARCHITECTURE.md`). A mandatory confirmation dialog must be shown before execution, since the action is irreversible. The system must prevent deleting a profile that is currently active/selected — the user must exit the profile first.

---

## 2. Test Mode Specifications

The testing system consists of four modes. Each mode measures a distinct dimension of sensitivity suitability, based on concepts from the source research.

| # | Mode | Description | Primary Metric | Research Basis |
|---|---|---|---|---|
| 1 | Flick — Close Range | Targets spawn around a central point at close distance, high spawn frequency (Aim Lab style) | Reaction Time, Overflick/Underflick | High sensitivity favors flicky entry players |
| 2 | Flick — Far Range | Targets spawn far apart in both time and position | Travel Time (isolated from Reaction Time) | Low sensitivity favors methodical/precision aimers |
| 3 | Tracking | Continuously moving target (3 patterns: linear / curved / variable-speed) | Time-on-Target %, Tracking Deviation (SD) | Grip Tension 60-80% concept (see `RESEARCH.md`, Section 14) |
| 4 | Micro-Correction | Stationary, small-sized target near current crosshair position (5-20 px offset; see `RESEARCH.md`, Section 13) | Micro-Adjustment Count, Final Precision Error, Submovement Count | PSA Method "natural feel" concept + Submovement Analysis |

### 2.1 Target Size Variation (Fitts's Law Compliance)

Every mode must randomize target size (Small / Medium / Large) together with distance — distance alone must never be the only varied parameter. This is required to comply with Fitts's Law, which defines the Index of Difficulty from both distance and target size jointly (see `RESEARCH.md`, Section 10).

### 2.2 Crosshair Consistency

The crosshair uses an application-fixed dot style and dot size. During profile creation, the user may select only a high-contrast color; that selected color is then locked for the entire lifetime of the profile. This eliminates a confounding variable unrelated to sensitivity itself. The selected color is stored in `profiles.crosshair_config` and must not change between test sessions or test modes.

### 2.3 Per-Mode Performance Score Storage

Each test mode produces its own Performance Score for a given sensitivity value. These per-mode scores must be stored individually (see `ARCHITECTURE.md`, `sensitivity_tests.performance_score_by_mode`) before being aggregated into `avg_performance_score`. Never discard the per-mode breakdown — it is required for the Visualization Layer (Section 4 below) and for diagnosing which mode drove a Winner decision.

For shot-based modes, Final Precision Error is inverted and normalized into the scoring component named **Precision Score**. Center-Hit Percentage is retained only as a diagnostic. For Tracking, Time-on-Target replaces Accuracy and inverted Tracking Deviation supplies Precision Score; Reaction Speed and Submovement Penalty are omitted, with the remaining positive weights redistributed proportionally as defined in `RESEARCH.md`, Section 4.1.

### 2.4 Reproducible Test Geometry

All production tests use fixed world geometry with locked FOV, camera configuration, and Target Frame Rate. Concrete target sizes, distances, spawn frequency, Tracking speed/duration, arena dimensions, center-hit zone, and frame-rate value must be measured and versioned during Phase 0: Signal Calibration before the Test Engine is implemented.

---

## 3. Testing Protocol (Execution Flow)

### 3.1 Step 0 — Initial Setup

- The user enters Hardware DPI (or uses the Physical Ruler Test if unknown) and their current in-game sensitivity.
- The user completes the Physical Profile fields described in Section 1.2 above.
- The system calculates the PSA Baseline (see `RESEARCH.md`, Section 2) and compares it against the user's current value, along with Mousepad Constraint Validation.

### 3.2 Phase 1 — PSA Method (7 Test Values)

The number of values and minimum sample size are defined in `RESEARCH.md`, Section 11.1.

- **Candidate values:** PSA baseline at 0%, plus symmetric +/-5%, +/-10%, and +/-20% offsets (seven values total).
- **Counterbalancing:** randomize the test order of all 7 values to prevent order effects.
- **Minimum sample:** at least 30 shots per sensitivity value separately for each shot-based mode. Tracking uses a distinct duration/trial-based contract that must be calibrated in Phase 0; samples are never pooled across modes.
- **Blind testing:** the numeric sensitivity value must never be displayed to the user during testing, to prevent placebo effects from influencing performance.
- **Winner selection:** select the Winner based on Performance Score, and verify statistical significance between the top 2 candidates before finalizing.
- **Unresolved test method:** the required statistical test and alpha threshold are blocked pending a decision in `PROGRESS.md`, OQ-005.
- **Adaptation Period:** discard the first 50% of shots recorded per tested value before computing any metric (see `RESEARCH.md`, Section 8). This is not a fixed shot count — it scales with the actual number of shots recorded for that value.

```
adaptation_cutoff = floor(total_shots_per_value x 0.5)
valid_shots = shots[adaptation_cutoff:]  # only these contribute to Performance Score
```

### 3.3 Phase 2 — Progressive Narrowing (+/- 10%)

The narrowing range, session limits, and stabilization threshold are defined in `RESEARCH.md`, Section 11.2. The exact statistical interpretation of the 10% threshold remains an open question recorded in `PROGRESS.md` and must be resolved before implementation.

- Test the Phase 1 Winner, Winner+10%, and Winner-10%.
- Minimum 5 sessions per value.
- Condition for concluding: the standard deviation of Performance Score across sessions must fall below 10% before a result is finalized. Up to 10 sessions may be run per value if the result has not stabilized within 5.

### 3.4 Phase 3 — Final Narrowing (+/- 5%)

The narrowing range is defined in `RESEARCH.md`, Section 11.3.

- Repeat the same test structure as Phase 2, but around the Phase 2 Winner, at a +/- 5% range.
- Output: a preliminary Best Sensitivity value.

### 3.4.1 Session and Protocol Battery Contract

A database Session is one Test Mode at one sensitivity value. A Protocol Battery is the complete set of four mode-specific Sessions at one sensitivity value and is grouped by `battery_id` (see `ARCHITECTURE.md`). Protocol repetition and Phase 2/3 completion counts must use complete batteries; an incomplete battery must not count toward the 5-10 requirement.

### 3.5 Step 4 — Performance Gate Check

Evaluate the preliminary Best Sensitivity using Reaction Time and Consistency to assign separate tiers, then use the worse tier as the final Grade (S/A/B/C/D; see `RESEARCH.md`, Sections 6 and 17.3). If the Grade is low, the system must recommend additional practice at that value rather than concluding that the sensitivity value itself is the problem. Do not immediately trigger a re-test of other values on a single low Grade result.

### 3.6 Continuous Improvement Cycle

The 5-10 session training block is defined in `RESEARCH.md`, Section 11.4.

Fatigue Detection compares the Performance Score of the chronological first and second halves of valid post-adaptation observations in the same session. A second-half decline greater than 15% sets an informational fatigue flag, but never excludes the session from Winner selection (see `RESEARCH.md`, Section 17.2).

```
Cycle N: Train at Best Sensitivity (5-10 sessions)
   -> Monitor Grade progression + Fatigue Detection
   -> If Grade is unchanged for 3 cycles AND Score changes <5%
      -> Immediately re-run Phase 1-3 (new baseline = current value)
   -> If Grade improves   -> Continue training at current value
   -> Proceed to Cycle N+1
```

---

## 4. Visualization & Reporting Layer

### 4.1 Layer 1 — Unity In-App (Immediate Feedback)

- Bar chart: Accuracy% per tested sensitivity value, shown within the current session immediately after testing.

### 4.2 Layer 2 — Python Deep Analysis (Exported HTML Report)

| # | Chart | Purpose |
|---|---|---|
| 1 | Sensitivity vs Performance Score Curve | Identify the peak eDPI value |
| 2 | Overflick vs Underflick Balance Chart | Indicate whether sensitivity should increase or decrease |
| 3 | Movement vs Stationary Error Graph | Compare error while stationary vs. moving |
| 4 | Progressive Narrowing Timeline | Visualize Phase 1 -> 2 -> 3 convergence |
| 5 | Consistency Trend Over Time | Track standard deviation of error over the training history |
| 6 | Reaction Time Distribution | Histogram of reaction times per session |
| 7 | Performance Grade Timeline | Track Grade (S-D) alongside eDPI over time |
| 8 | Reaction Time vs Sensitivity Scatter Plot | Correlate eDPI with reaction speed |
| 9 | Submovement Count vs eDPI Curve | Visualize the monotonic relationship confirmed in IEEE CoG 2022 |
| 10 | Profile Comparison Chart | Cross-profile comparison, normalized via eDPI |

All 10 charts must be generated in the exported HTML report. Chart 9 in particular depends on `shots.submovement_count` being populated correctly per the algorithm in `RESEARCH.md`, Section 5 — verify this data exists before attempting to render it.
