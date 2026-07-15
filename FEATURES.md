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
- Configured mouse polling rate in Hz, entered by the user as supporting session metadata; it does not replace automatic cadence measurement
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
| 1 | Flick — Close Range | Hidden targets activate after a calibrated randomized foreperiod at Close offsets | Reaction Time, Overflick/Underflick | High sensitivity favors flicky entry players |
| 2 | Flick — Far Range | Center-reference activation starts a previewed Far target; movement onset separates Reaction from Travel | Travel Time (isolated from Reaction Time) | Low sensitivity favors methodical/precision aimers |
| 3 | Tracking | Continuously moving target (3 calibrated analytic patterns: linear / curved / variable-speed) | Time-on-Target %, Tracking Deviation (RMS window metric; SD across windows supplies Consistency) | Grip Tension 60-80% concept (see `RESEARCH.md`, Section 14) |
| 4 | Micro-Correction | Stationary, small-sized target near current crosshair position (5-20 px offset; see `RESEARCH.md`, Section 13) | Micro-Adjustment Count, Final Precision Error, Submovement Count | PSA Method "natural feel" concept + Submovement Analysis |

### 2.1 Target Size Variation (Fitts's Law Compliance)

Close Flick and Far Flick must vary Small / Medium / Large together with distance — distance alone must never be the only varied parameter. Tracking crosses all three target sizes with all three path patterns in each balanced block. Micro-Correction is the explicit exception: its dedicated mode requirement and accepted `sc8-test-geometry-v1` contract fix the target to Small while randomizing radial/directional offset within 5-20 px. This exception prevents the precision task from silently changing scale and resolves the earlier generic-versus-specific wording. See `RESEARCH.md`, Sections 10 and 17.7.

### 2.2 Crosshair Consistency

The crosshair uses an application-fixed dot style and dot size. During profile creation, the user may select only a high-contrast color; that selected color is then locked for the entire lifetime of the profile. This eliminates a confounding variable unrelated to sensitivity itself. The selected color is stored in `profiles.crosshair_config` and must not change between test sessions or test modes.

### 2.3 Per-Mode Performance Score Storage

Each test mode produces its own Performance Score for a given sensitivity value. These per-mode scores must be stored individually (see `ARCHITECTURE.md`, `sensitivity_tests.performance_score_by_mode`) before being aggregated into `avg_performance_score`. Never discard the per-mode breakdown — it is required for the Visualization Layer (Section 4 below) and for diagnosing which mode drove a Winner decision.

For shot-based modes, Final Precision Error is inverted and normalized into the scoring component named **Precision Score**. Center-Hit Percentage is retained only as a diagnostic. For Tracking, Time-on-Target replaces Accuracy and inverted Tracking Deviation supplies Precision Score; Reaction Speed and Submovement Penalty are omitted, with the remaining positive weights redistributed proportionally as defined in `RESEARCH.md`, Section 4.1.

Every component uses the fixed versioned normalization in `RESEARCH.md`, Section 4.2. Bounds must come from the active Phase 0 calibration configuration and must never be recomputed from the currently displayed profiles, sessions, or candidate values.

Accepted scoring contract `sc8-p0-r6-scoring-statistics-contract-v1` fixes the complete aggregation and missing-value behavior. Each shot mode uses 15 authoritative resolved opportunities; Tracking uses 54 authoritative one-second windows. A battery score is the unweighted arithmetic mean of four complete mode scores. The shot formula is not final-clamped and retains its documented theoretical -10 to 100 range; Tracking ranges 0 to 100. A zero-hit shot mode retains null raw Submovement Count and fails closed at component penalty 1.0, never a favorable zero. See `RESEARCH.md`, Section 4.4.

### 2.4 Reproducible Test Geometry

All production tests use accepted geometry version `sc8-test-geometry-v1`: a fixed 16:9 test viewport (letterboxed on other aspect ratios), 103-degree horizontal perspective FOV, 10-world-unit target plane, 20 x 12 x 21 enclosed arena, 144 Hz reference frame policy, and cyan spheres sized 0.75/1.50/2.25 degrees. Close and Far modes use the complete size cross-product with center offsets 5/10/15 degrees and 20/30/40 degrees respectively. The fixed four-pixel dot crosshair and 50%-of-target-radius Center-Hit diagnostic are also part of this immutable contract. See `RESEARCH.md`, Section 17.5, and `calibration/plans/p0-r4-geometry-accepted-v1.json` for authoritative values.

Spawn timing, Tracking speed/duration/trial behavior, and signal response are frozen under `sc8-mode-contract-v1` and `sc8-signal-pipeline-v1`; see `RESEARCH.md`, Sections 5 and 17.7, and `calibration/plans/p0-r5-signal-mode-accepted-v1.json`. Production must load these versioned contracts and must not duplicate their numbers as mode-local magic constants.

All production features consume these values through the single accepted `calibration_config_v1` artifact described in `RESEARCH.md`, Section 18. Loading is fail-closed: draft, incomplete, edited, hash-mismatched, or internally inconsistent configuration data must prevent a test session from starting. A future change creates a new version and never mutates historical results.

### 2.5 Input-Timing Compatibility

- Hardware DPI remains a required manual profile input (or Physical Ruler Test result). Mouse manufacturer, model, and firmware are optional audit/troubleshooting metadata and must never block normal setup or alter sensitivity calculations.
- Before each production session, copy the user's configured polling-rate value into the session record, measure the actual raw input-event cadence automatically, and store versioned timing diagnostics. Configured/advertised polling rate is supporting metadata only.
- Validate strict timestamp ordering and nominal modal cadence against the active timing contract before Submovement analysis. Receipt bursts and gaps are diagnostic rather than automatic rejection, but detected gaps must split the resampling trace. Never assume that all mice report at the configured rate.
- Resample accepted traces onto the canonical processing grid stored by the active `signal_pipeline_version`.
- Menu screens may run windowed, but every acceptance-bearing test session enters the frozen native borderless-fullscreen test state automatically and returns to the prior windowed state afterward.

---

## 3. Testing Protocol (Execution Flow)

### 3.1 Step 0 — Initial Setup

- The user enters Hardware DPI (or uses the Physical Ruler Test if unknown), current in-game sensitivity, and configured mouse polling rate in Hz.
- The user completes the Physical Profile fields described in Section 1.2 above.
- The system calculates the PSA Baseline (see `RESEARCH.md`, Section 2) and compares it against the user's current value, along with Mousepad Constraint Validation.

### 3.2 Phase 1 — PSA Method (7 Test Values)

The number of values and minimum sample size are defined in `RESEARCH.md`, Section 11.1.

- **Candidate values:** PSA baseline at 0%, plus symmetric +/-5%, +/-10%, and +/-20% offsets (seven values total).
- **Counterbalancing:** randomize the test order of all 7 values to prevent order effects.
- **Minimum sample:** at least 30 resolved opportunities per sensitivity value separately for each shot-based mode. Under the minimum contract, exactly 15 are adaptation and 15 are authoritative. Tracking uses two balanced 9-trial blocks at 6 seconds per trial; the first block is adaptation and the second produces 54 authoritative 1-second metric windows. Samples are never pooled across modes.
- **Blind testing:** the numeric sensitivity value must never be displayed to the user during testing, to prevent placebo effects from influencing performance.
- **Winner selection:** use exploratory Performance Score to identify the top 2, then collect exactly 10 fresh matched complete-battery pairs, counterbalanced five A-first/five B-first, and run the exhaustive 1024-assignment two-sided paired randomization/permutation test at `alpha = 0.05` defined in `RESEARCH.md`, Section 11.1.
- **Tie behavior:** if the top-2 difference is not statistically significant, declare a statistical tie and carry both anchors into the Phase 2 union/deduplication rule; never force a Phase 1 Winner from the p-value alone.
- **Adaptation Period:** discard the first 50% of shots recorded per tested value before computing any metric (see `RESEARCH.md`, Section 8). This is not a fixed shot count — it scales with the actual number of shots recorded for that value.

```
adaptation_cutoff = floor(total_shots_per_value x 0.5)
valid_shots = shots[adaptation_cutoff:]  # only these contribute to Performance Score
```

### 3.3 Phase 2 — Progressive Narrowing (+/- 10%)

The narrowing range, session limits, and stabilization threshold are defined in `RESEARCH.md`, Section 11.2.

- With one Phase 1 Winner, test the Winner, Winner+10%, and Winner-10%.
- With two statistically tied Phase 1 anchors, generate the union of each anchor at -10%, 0%, and +10%, apply the eDPI floor, then deduplicate final eDPI values. Preserve every anchor/offset provenance record when multiple sources collapse to one candidate (see `RESEARCH.md`, Section 11.2).
- Minimum 5 complete Protocol Batteries per value (equivalent to 5 Database Sessions per mode).
- Condition for concluding: the coefficient of variation of Performance Score across complete Protocol Batteries must be below 10% before a result is finalized. Under `sc8-normalization-v1`, `abs(mean score) <= 1e-9` is undefined and does not pass stabilization. Up to 10 complete batteries may be run per value if the result has not stabilized within 5.

### 3.4 Phase 3 — Final Narrowing (+/- 5%)

The narrowing range is defined in `RESEARCH.md`, Section 11.3.

- Repeat the same test structure as Phase 2, but around the Phase 2 Winner, at a +/- 5% range.
- Output: a preliminary Best Sensitivity value.

### 3.4.1 Session and Protocol Battery Contract

A database Session is one Test Mode at one sensitivity value. A Protocol Battery is the complete set of four mode-specific Sessions at one sensitivity value and is grouped by `battery_id` (see `ARCHITECTURE.md`). Protocol repetition and Phase 2/3 completion counts must use complete batteries; an incomplete battery must not count toward the 5-10 requirement.

### 3.4.2 Outlier Handling

Apply adaptation first, then evaluate the 3-SD rule within the metric-specific homogeneous scope defined in `RESEARCH.md`, Section 12. Statistical outliers are flagged and audited but remain in the authoritative Winner score by default. Reports must show both the inclusive aggregate and the flagged-row-excluded sensitivity analysis. Exclusion from the authoritative score requires a separately documented acquisition/data-quality error.

### 3.5 Step 4 — Performance Gate Check

Evaluate the preliminary Best Sensitivity using authoritative Close Flick mean Reaction Time and the arithmetic mean of all four normalized mode Consistency utilities. Assign Consistency S/A/B/C/D using fixed utility boundaries 0.8/0.6/0.4/0.2, then use the worse of Reaction and Consistency tiers as the final Grade (see `RESEARCH.md`, Sections 6 and 17.3). If the Grade is low, the system must recommend additional practice at that value rather than concluding that the sensitivity value itself is the problem. Do not immediately trigger a re-test of other values on a single low Grade result.

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
