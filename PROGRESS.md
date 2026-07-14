# PROGRESS.md

## Purpose

This file is the running log of development progress for SensCalibr8. It is maintained entirely by the coding agent (Codex), not by the human user. At the start of every session, read this file in full before doing anything else. At the end of every completed task, update this file before ending the session.

This file starts empty of phase content. The coding agent is responsible for analyzing `FEATURES.md` and `ARCHITECTURE.md` in full and designing its own phase breakdown, then recording that plan below.

---

## Phase Plan

0. **Signal Calibration** — Capture representative raw traces; validate and freeze the input sampling policy, signal-filter response, angular-velocity event thresholds, target geometry, camera/FOV, frame rate, Tracking duration/speed, center-hit zone, normalization/Submovement bounds, Consistency-tier cutpoints, scoring-zero tolerance, and confirmatory sample contract. This phase must complete before Test Engine implementation.
1. **Foundation and Data Layer** — Create the Unity project foundation, SQLite schema/migrations, validated repositories, formula/config versioning, and automated database tests.
2. **Profiles and Setup** — Implement profile lifecycle, physical setup fields, PSA baseline, eDPI/mousepad validation, and non-blocking ergonomic warnings.
3. **Test Engine Core** — Implement deterministic raw-input capture, precision timing, session/battery orchestration, adaptation finalization, and calibrated target spawning. Blocked until Phase 0 completes.
4. **Four Test Modes** — Build and verify Close Flick, Far Flick, Tracking, and Micro-Correction against the frozen Phase 0 contracts.
5. **Protocol and Scoring** — Implement Phase 1-3 execution, counterbalancing, normalization, significance gate, fatigue, outlier handling, Grade, plateau, and Winner selection after Phase 0 freezes every dependent configuration and Phase 1 tie expansion is resolved.
6. **Analysis and Data Export** — Add in-app feedback, Python analysis, HTML reporting, JSON/CSV Data Export, and cross-profile comparisons.
7. **Integration and Release QA** — Run end-to-end protocol validation, worked examples, validation edge cases, performance/reproducibility checks, and release packaging.

---

## Status Log

(For each phase, add an entry using the format below as work progresses. Do not delete old entries — this is a historical log.)

### Pre-Development: Specification Audit

- Status: Completed
- Date: 2026-07-14
- What was done: Audited the numeric protocol and scientific-rigor requirements against the human-confirmed SensCalibr8 Project Proposal V3.0 authority. Migrated the confirmed Phase 1-3, continuous training, outlier, and Micro-Correction constants into `RESEARCH.md`; added cross-references from `FEATURES.md` and `RULES.md`; and recorded eight unresolved specification gaps below without selecting implementation values or algorithms.
- Test results (per Definition of Done in AGENTS.md): Documentation-only change. Verified that all six confirmed protocol groups appear in the `RESEARCH.md` constants summary, all requested feature/rule cross-references resolve to the new sections, and all eight unresolved items include an implementation block.
- Issues encountered: The source Proposal V3.0 is not stored in the workspace; section attribution follows the project owner's confirmed mapping of Testing Protocol values to Section 7 and Scientific Rigor values to Section 8.
- Next step: Obtain human decisions for OQ-001 through OQ-008 before designing development phases that depend on those specifications.

### Pre-Development: Source Verification and Schema Reconciliation

- Status: Completed
- Date: 2026-07-14
- What was done: Read the complete local Proposal V3.0 and Consolidated Research Report source files; moved them into `reference/`; added directly verified Grip Tension, 9-Week Rule, Submovement Penalty range, and informational wrist-warning citations to `RESEARCH.md`; corrected the Micro-Correction citation and metric list; reconciled required SQLite constraints and relationships; clarified Reaction Time boundaries, adaptation finalization, current sensitivity storage, and crosshair configurability; resolved OQ-001; refined OQ-004 and OQ-007 from source evidence; and added OQ-009 through OQ-018 without selecting unsupported values or algorithms.
- Test results (per Definition of Done in AGENTS.md): Documentation-only change. Verified the cited source text directly in both local files; confirmed every schema foreign key includes `ON DELETE CASCADE`; confirmed `PRAGMA foreign_keys = ON`, required `NOT NULL` fields, Tracking sensitivity, and cycle links are present; confirmed Reaction Time tiers cover all boundaries without overlap; confirmed 18 total OQ records exist, with 1 resolved and 17 awaiting human decisions; and passed `git diff --check`.
- Issues encountered: The source files initially appeared in the workspace root rather than `reference/`; they were moved without content modification. The 9-Week Rule confirms +/-20%, +/-10%, and +/-5% progression but does not define the seven-value Phase 1 sequence. Proposal V3.0 confirms a 0.0-1.0 Submovement Penalty range but not the raw-count normalization formula.
- Next step: Obtain human decisions for the 17 unresolved questions before implementing any dependent protocol, scoring, filtering, geometry, outlier, Grade, plateau, or restore behavior.

### Pre-Development: Product Decisions and Technical Proposal

- Status: Completed
- Date: 2026-07-14
- What was done: Applied the project owner's decisions for OQ-002, OQ-006, OQ-007, OQ-009, OQ-010, OQ-011, OQ-013, OQ-014, OQ-015, OQ-017, and OQ-018; added Phase 0: Signal Calibration; researched and proposed externally sourced designs for OQ-003, OQ-004, OQ-005, OQ-008, OQ-012, and OQ-016; then incorporated all six after project-owner approval. Added immutable calibration/version contracts, raw mouse samples, metric-level outlier audit records, and significance-test audit storage to the schema.
- Test results (per Definition of Done in AGENTS.md): Documentation-only work. Final verification for the approved Group A incorporation is recorded in the next status entry.
- Issues encountered: Values requiring empirical measurement cannot be invented during specification work. Input sampling rate/tolerance, normalization and Submovement bounds, Consistency-tier cutpoints, geometry, Tracking contract, and confirmatory sample contract are therefore explicit Phase 0 deliverables.
- Next step: Execute Phase 0: Signal Calibration before Test Engine implementation.

### Pre-Development: Approved Technical Designs Incorporated

- Status: Completed
- Date: 2026-07-14
- What was done: Incorporated the approved designs for OQ-003, OQ-004, OQ-005, OQ-008, OQ-012, and OQ-016 into the authoritative research, feature, rule, and architecture contracts. Added fixed/versioned normalization, capped linear Submovement mapping, paired permutation confirmation, CV stabilization, the complete signal pipeline, flag-first outlier handling, immutable calibration configuration, raw input samples, outlier audits, and significance pair traceability.
- Test results (per Definition of Done in AGENTS.md): Documentation-only work. Verified 19 OQ records with 18 Resolved and 1 newly discovered integration question; all 27 schema foreign keys include `ON DELETE CASCADE`; both local primary-source links resolve; `RESEARCH.md` Sections 1-17 remain ordered; no previously pending Group A status remains; and `git diff --check` passed.
- Issues encountered: Combining the newly approved statistical-tie behavior with the existing Phase 2 Winner +/-10% rule exposes an undefined two-anchor candidate expansion. This is recorded as OQ-019 instead of being guessed.
- Next step: Obtain the project owner's OQ-019 choice. Phase 0 planning may proceed independently, but Phase 2 tie candidate generation remains blocked.

### Phase 0: Signal Calibration

- Status: Not Started — Ready
- Date: 2026-07-14
- What was done: Defined the required calibration outputs, versioning contract, approved signal-processing method, and implementation blockers.
- Test results (per Definition of Done in AGENTS.md): No calibration data has been collected yet; this entry does not claim Phase 0 completion.
- Issues encountered: Representative raw traces and the target execution environment are required to determine the remaining measured values.
- Next step: Design the Phase 0 data-collection procedure and acceptance tests, then collect representative traces before freezing configuration version 1.

### Phase [number]: [name]

- Status: Not Started / In Progress / Blocked / Completed
- Date: [date of last update]
- What was done:
- Test results (per Definition of Done in AGENTS.md):
- Issues encountered:
- Next step:

---

## Open Questions / Ambiguities

(Record here any point where a specification in FEATURES.md, RESEARCH.md, ARCHITECTURE.md, DESIGN.md, SKILL.md, or RULES.md was unclear or conflicting, per the Error Handling Expectation in AGENTS.md. Do not resolve these by guessing — flag them here for human review.)

Current register status: **18 Resolved, 1 Open**. Phase 0 measured deliverables are implementation prerequisites, not unresolved specification decisions.

### OQ-001: Wrist-Strain Warning Threshold

- Status: Resolved on 2026-07-14 by direct source verification
- Resolution: SensCalibr8 Project Proposal V3.0, Section 9 explicitly defines the example warning condition `eDPI < 200` with Wrist-dominant movement. The threshold is now documented in `RESEARCH.md`, Section 16 with a direct link to the local source file.
- Implementation constraint: The condition may be implemented only as an informational, non-diagnostic warning. It must never block testing, alter Performance Score, or assert a causal optimal eDPI.

### OQ-002: Consistency Metric Definition

- Status: Resolved — user decision on 2026-07-14
- Resolution: Consistency is the standard deviation of the primary metric after the adaptation cutoff: Final Precision Error for shot-based modes and Tracking Deviation for Tracking. Lower values are better and must be inverted before normalization.
- Specification gap: Performance Score assigns a 0.35 weight to Consistency, but no document defines the underlying observation or distribution whose variability represents Consistency.
- Possible options: standard deviation of final precision error; standard deviation of Performance Score across sessions; coefficient of variation of a selected shot-level metric; or a composite consistency measure.
- Implementation constraint: Production Consistency scoring waits for Phase 0 normalization bounds and tier cutpoints.

### OQ-003: Performance Score Metric Normalization

- Status: Resolved — user approved technical proposal on 2026-07-14
- Resolution: Use fixed, versioned min-max utility scaling to `[0,1]`, with direction inversion for lower-is-better metrics and clamping at the configured bounds. Phase 0 calibrates and freezes mode/metric bounds; current comparison data must never redefine them. Multiply the weighted aggregate by 100.
- Specification gap: Accuracy percentage, Reaction Time in milliseconds, and Submovement Count use incompatible units and directions, but no normalization/scaling formula is defined before applying the documented weights.
- Possible options: fixed benchmark-based 0-100 scaling; min-max scaling against documented bounds; z-score standardization against an approved reference population; or percentile-based scaling.
- Implementation constraint: Do not implement production scoring until Phase 0 freezes a valid `normalization_version`, including bounds, scoring-zero tolerance, and Consistency-tier cutpoints.

### OQ-004: Submovement Penalty Transformation

- Status: Resolved — user approved technical proposal on 2026-07-14
- Resolution: Use `clamp((count - L_mode) / (U_mode - L_mode), 0, 1)` on valid post-adaptation observations. Phase 0 calibrates immutable mode-specific bounds under the normalization version.
- Specification gap: Direct verification of SensCalibr8 Project Proposal V3.0, Section 6.4 confirms that Submovement Penalty must be `normalized_submovement_count` on a 0.0-1.0 scale, with higher values producing more penalty. However, the proposal still does not define how raw Submovement Count is transformed into that normalized range.
- Possible options: capped linear mapping into 0.0-1.0; benchmark-based mapping normalized into 0.0-1.0; percentile mapping expressed as 0.0-1.0; or another transformation supported by research that preserves the confirmed output range.
- Implementation constraint: Do not implement the penalty until Phase 0 freezes `L_mode` and `U_mode` with `U_mode > L_mode`.

### OQ-005: Statistical Significance Test

- Status: Resolved — user approved technical proposal on 2026-07-14
- Resolution: After exploratory top-2 selection, collect fresh matched confirmatory blocks and use a two-sided paired randomization/permutation test at `alpha = 0.05`. Report effect estimate, 95% confidence interval, p-value, and sample size. A non-significant result is a statistical tie and both candidates continue to Phase 2.
- Specification gap: Phase 1 requires significance testing between the top two candidates, but the test type, pairing structure, significance level, assumptions, and fallback behavior are unspecified.
- Possible options: paired t-test when justified by paired, approximately normal observations; Wilcoxon signed-rank test for paired non-normal observations; or a pre-defined decision tree selected with statistical justification.
- Implementation constraint: Phase 0 must freeze the matched-block and confirmatory sample contract before Winner implementation.

### OQ-006: Fatigue Detection Algorithm

- Status: Resolved — user decision on 2026-07-14
- Resolution: Compare Performance Score between chronological halves of valid post-adaptation observations in the same session. Flag a second-half decline greater than 15%; retain the session in Winner selection.
- Specification gap: Fatigue Detection is mandatory, but no measured variable, comparison window, decline threshold, minimum sample, or warning behavior is defined.
- Possible options: compare early versus late session Performance Score; detect a reaction-time increase together with an accuracy decline; use a rolling trend over valid shots; or use a session-to-session trend.
- Implementation constraint: Production calculation waits for Phase 0 scoring configuration.

### OQ-007: Generation of Seven Phase 1 Sensitivity Values

- Status: Resolved — user decision on 2026-07-14
- Resolution: Generate seven values at 0%, +/-5%, +/-10%, and +/-20% around the PSA baseline, supported by the 9-Week Rule progression in the Consolidated Research Report.
- Specification gap: Phase 1 requires seven sensitivity values around the PSA baseline, but their spacing, range, rounding, and eDPI-floor interaction are unspecified.
- Source verification note: The Consolidated Research Report's 9-Week Rule defines baseline versus +/-20%, followed by +/-10% and +/-5%, while Proposal V3.0 separately requires seven Phase 1 values. Neither source maps them into one sequence; the project-owner decision above supplies that mapping.
- Possible options: symmetric percentage offsets around the PSA baseline; symmetric fixed-eDPI offsets; logarithmic spacing; or another research-supported sequence.
- Implementation constraint: Candidate generation must use the exact approved offsets and comply with the existing eDPI floor and input-validation rules.

### OQ-008: Meaning of the Phase 2 "SD < 10%" Threshold

- Status: Resolved — user approved technical proposal on 2026-07-14
- Resolution: Interpret the threshold as `CV% = 100 x sample_SD / abs(mean)` across complete Protocol Batteries at one sensitivity. Stabilization requires `CV < 10%`; a zero or numerically near-zero mean is undefined and does not pass.
- Specification gap: The 10% stabilization threshold is authoritative in SensCalibr8 Project Proposal V3.0, but raw standard deviation is expressed in score units and cannot itself be interpreted as a percentage without a defined denominator.
- Possible options: coefficient of variation (`SD / mean * 100`); SD below 10% of the score scale; or SD below 10% of another explicitly defined reference value.
- Implementation constraint: Use the Phase 0 scoring-zero tolerance and never substitute a raw-SD or zero fallback.

### OQ-009: Definition of a Phase 2/3 Session

- Status: Resolved — user decision on 2026-07-14
- Resolution: A database Session is one mode at one sensitivity value. A Protocol Battery is all four modes at one value, linked using `battery_id`; only complete batteries count toward protocol repetition requirements.
- Specification gap: The protocol requires 5-10 sessions per sensitivity value but does not define whether one session is one mode/value run, a complete four-mode battery at one value, or one continuous application visit containing multiple values.
- Possible options: define a session as one Test Mode at one sensitivity; define it as all four Test Modes at one sensitivity; or separate database sessions from higher-level protocol batteries with an additional grouping concept.
- Implementation constraint: Enforce one session per mode within each battery and never count an incomplete battery toward the 5-10 protocol requirement.

### OQ-010: Tracking-Mode Performance Score

- Status: Resolved — user decision on 2026-07-14
- Resolution: Map Time-on-Target to Accuracy and inverted Tracking Deviation to Precision Score. Omit Reaction Speed and Submovement Penalty, then proportionally redistribute the remaining positive weights to Consistency 0.4375, Time-on-Target 0.3750, and Precision Score 0.1875.
- Specification gap: Tracking produces Time-on-Target and Tracking Deviation, but the global Performance Score expects Consistency, Accuracy, Reaction Speed, Precision/Headshot, and Submovement Penalty. The sources do not define how unavailable components are handled for Tracking.
- Possible options: define a Tracking-specific score with separately approved weights; transform Tracking metrics into the shared normalized component model; or report Tracking separately and exclude it from the aggregate Winner score.
- Implementation constraint: Production calculation waits for Phase 0 normalization bounds.

### OQ-011: Precision Metric for Spherical Targets

- Status: Resolved — user decision on 2026-07-14
- Resolution: Store both metrics. Final Precision Error is inverted and normalized as the scoring component named Precision Score; Center-Hit Percentage is diagnostic only.
- Specification gap: The arena uses a single-color spherical target with no head region, so Valorant Headshot Percentage cannot be measured literally.
- Possible options: use mean Final Precision Error; use Center-Hit Percentage with an approved center-radius definition; retain both as separate diagnostics and select one normalized component for Performance Score; or redesign the target with a defined precision zone.
- Implementation constraint: Precision Score bounds and Center-Hit geometry must be frozen in Phase 0.

### OQ-012: Submovement Signal-Processing Parameters

- Status: Resolved — user approved technical proposal on 2026-07-14
- Resolution: Capture timestamped raw deltas, convert to angular traces, uniformly resample at the validated input-event rate, apply a fifth-order 7 Hz Butterworth SOS filter with forward-backward offline filtering, derive angular velocity, and detect events at 8/4 degrees per second with an 80 ms refractory period.
- Specification gap: The sources recommend a Butterworth low-pass filter and approximate angular-velocity thresholds, but do not specify filter order, cutoff frequency, sampling-rate policy, delta-to-angle conversion, or handling of variable input sample intervals.
- Possible options: reproduce parameters from the underlying IEEE CoG 2022 implementation if obtainable; define and validate a fixed sampling pipeline against recorded traces; or make the filter sampling-rate-aware with versioned configuration after empirical calibration.
- Implementation constraint: Phase 0 must validate and freeze sampling rate, timing tolerance, edge handling, and trace acceptance criteria under `signal_pipeline_version` before production implementation.

### OQ-013: Performance Grade Assignment Formula

- Status: Resolved — user decision on 2026-07-14
- Resolution: Assign Reaction Time and Consistency tiers separately and use the worse tier as the final Grade.
- Specification gap: Step 4 requires a single S-D Grade from Reaction Time and Consistency, but only Reaction Time tiers are defined and no combination rule exists.
- Possible options: assign the worse of the two metric tiers; use an approved two-dimensional lookup matrix; or normalize both metrics and apply a separately approved weighted composite before tiering.
- Implementation constraint: Final Grade waits for Phase 0 to freeze Consistency-tier cutpoints under the calibration configuration.

### OQ-014: Plateau Detection Criteria

- Status: Resolved — user decision on 2026-07-14
- Resolution: Plateau requires an unchanged Grade across three consecutive cycles and an absolute relative Performance Score change below 5% across the same window. Trigger an immediate Phase 1-3 rerun using the current sensitivity as baseline.
- Specification gap: The continuous cycle triggers recalibration when Grade plateaus, but does not define the observation window, required number of sessions, allowed change, trend method, or behavior when Grade oscillates.
- Possible options: unchanged Grade across an approved consecutive-session window; Performance Score trend slope below an approved threshold; or a statistical no-improvement test over an approved rolling window.
- Implementation constraint: The trigger waits for Phase 0 scoring and Consistency-tier configuration plus the required three-cycle history.

### OQ-015: Target Geometry and Test Parameters

- Status: Resolved — user decision on 2026-07-14
- Resolution: Use fixed world geometry and lock FOV, camera, and frame rate. Determine and validate every concrete geometry/test value in Phase 0: Signal Calibration before Test Engine implementation.
- Specification gap: The sources do not define concrete Small/Medium/Large target dimensions and units, close/far distances, spawn frequency, Tracking speed and duration, arena dimensions, or fixed Target Frame Rate. Without these values, tests are not reproducible.
- Possible options: define a fixed visual-angle/Fitts-ID matrix independent of resolution; define engine-world dimensions with a locked camera configuration; or provide versioned hardware-aware presets while preserving identical parameters within a comparison cycle.
- Implementation constraint: Do not implement production target spawning or the Test Engine until Phase 0 freezes and versions all listed values.

### OQ-016: Outlier Detection Scope and Handling

- Status: Resolved — user approved technical proposal on 2026-07-14
- Resolution: Apply adaptation first, then a one-pass metric-specific 3-SD rule within `(profile, cycle, phase, mode, sensitivity, metric)`. Tracking uses trial/window aggregates. Preserve raw data and flag by default; authoritative Winner scores retain statistical flags, while reports also show an excluded sensitivity analysis. Authoritative exclusion requires a separately documented acquisition/data-quality error.
- Specification gap: The 3-SD threshold is confirmed, but the metric, grouping scope, minimum sample, order relative to adaptation filtering, and whether outliers are excluded or only flagged are unspecified.
- Possible options: compute per metric within each mode/sensitivity/session after adaptation and exclude flagged rows from aggregates; flag without exclusion for transparent reporting; or use a pre-defined metric-specific policy with raw and cleaned results retained side by side.
- Implementation constraint: Use metric-level audit records; never repeatedly trim, pool heterogeneous groups, or convert statistical extremeness alone into data exclusion.

### OQ-017: Scope of the Phase 1 Minimum 30 Shots

- Status: Resolved — user decision on 2026-07-14
- Resolution: Require at least 30 shots per sensitivity value separately for every shot-based mode. Tracking uses a separate duration/trial-based contract established in Phase 0; samples are never pooled across modes.
- Specification gap: Proposal V3.0 confirms at least 30 shots per sensitivity value but does not state whether this minimum applies separately to each shot-based Test Mode or collectively across all four modes; Tracking is duration-based rather than naturally shot-based.
- Possible options: 30 shots per shot-based mode per value with a separate Tracking duration requirement; 30 shots total distributed through an approved balanced allocation; or define a complete per-mode sample contract using shots for discrete modes and time/trials for Tracking.
- Implementation constraint: Tracking completion remains blocked until Phase 0 freezes its duration/trial contract.

### OQ-018: Import / Restore Scope

- Status: Resolved — user decision on 2026-07-14
- Resolution: Product scope is Data Export only. JSON/CSV Import and Restore are explicitly Out of Scope and exports must not be described as restorable backups.
- Specification gap: The project defines per-profile JSON/CSV export as backup but does not define an Import/Restore path, schema compatibility policy, duplicate-profile handling, or formula-version migration behavior.
- Possible options: keep export-only and explicitly add Import/Restore to `CONTEXT.md` Out of Scope; support validated JSON restore while treating CSV as analysis-only; or provide full SQLite snapshot backup/restore in addition to profile exports.
- Implementation constraint: Do not implement Import/Restore or represent Data Export as a recovery guarantee.

### OQ-019: Phase 2 Candidate Set After a Phase 1 Statistical Tie

- Status: Awaiting human decision
- Specification gap: OQ-005 requires both tied candidates to continue to Phase 2, while the existing Phase 2 rule defines one Phase 1 Winner plus/minus 10%. With two anchors, the candidate-set expansion and deduplication behavior are not defined.
- Possible options: use the deduplicated union of each anchor and its +/-10% variants (preserves the narrowing rule but may produce up to six values); run two independent three-value Phase 2 branches and compare branch winners later (clean separation but substantially more testing); or test only the two tied values in Phase 2 (least work but suspends the documented +/-10% narrowing rule for ties).
- Recommendation: Use the deduplicated union of both anchors and their +/-10% variants, with the existing eDPI floor applied and original anchor/offset provenance retained for auditability.
- Implementation constraint: Phase 0 may proceed, but do not implement Phase 2 candidate generation for a statistical tie until this product behavior is approved.
