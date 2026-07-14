# PROGRESS.md

## Purpose

This file is the running log of development progress for SensCalibr8. It is maintained entirely by the coding agent (Codex), not by the human user. At the start of every session, read this file in full before doing anything else. At the end of every completed task, update this file before ending the session.

This file starts empty of phase content. The coding agent is responsible for analyzing `FEATURES.md` and `ARCHITECTURE.md` in full and designing its own phase breakdown, then recording that plan below.

---

## Phase Plan

(To be filled in by the coding agent on the first working session. List each phase with a short name and a one-line description of its scope.)

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

### OQ-001: Wrist-Strain Warning Threshold

- Status: Awaiting human decision
- Specification gap: `RULES.md` triggers `low_edpi_wrist_strain` when eDPI is below 200 and movement strategy is Wrist, but SensCalibr8 Project Proposal V3.0 does not define or support the 200 eDPI threshold.
- Possible options: remove this warning condition; retain it only after obtaining a verifiable ergonomic source; or replace it with a non-numeric general ergonomic notice.
- Implementation constraint: Do not implement or depend on the eDPI 200 threshold until confirmed.

### OQ-002: Consistency Metric Definition

- Status: Awaiting human decision
- Specification gap: Performance Score assigns a 0.35 weight to Consistency, but no document defines the underlying observation or distribution whose variability represents Consistency.
- Possible options: standard deviation of final precision error; standard deviation of Performance Score across sessions; coefficient of variation of a selected shot-level metric; or a composite consistency measure.
- Implementation constraint: Do not implement Consistency scoring until the metric, aggregation level, and direction are confirmed.

### OQ-003: Performance Score Metric Normalization

- Status: Awaiting human decision
- Specification gap: Accuracy percentage, Reaction Time in milliseconds, and Submovement Count use incompatible units and directions, but no normalization/scaling formula is defined before applying the documented weights.
- Possible options: fixed benchmark-based 0-100 scaling; min-max scaling against documented bounds; z-score standardization against an approved reference population; or percentile-based scaling.
- Implementation constraint: Do not implement aggregate Performance Score calculation until one normalization method and its bounds/reference data are confirmed.

### OQ-004: Submovement Penalty Transformation

- Status: Awaiting human decision
- Specification gap: The formula subtracts Submovement Penalty with a 0.10 weight, but does not define how raw Submovement Count becomes a bounded penalty compatible with the normalized positive metrics.
- Possible options: capped linear mapping; benchmark-based 0-100 mapping; percentile mapping; or another transformation supported by research.
- Implementation constraint: Do not implement the penalty term until its transformation, bounds, and aggregation method are confirmed.

### OQ-005: Statistical Significance Test

- Status: Awaiting human decision
- Specification gap: Phase 1 requires significance testing between the top two candidates, but the test type, pairing structure, significance level, assumptions, and fallback behavior are unspecified.
- Possible options: paired t-test when justified by paired, approximately normal observations; Wilcoxon signed-rank test for paired non-normal observations; or a pre-defined decision tree selected with statistical justification.
- Implementation constraint: Do not finalize a Phase 1 Winner using statistical significance until the method and alpha threshold are confirmed.

### OQ-006: Fatigue Detection Algorithm

- Status: Awaiting human decision
- Specification gap: Fatigue Detection is mandatory, but no measured variable, comparison window, decline threshold, minimum sample, or warning behavior is defined.
- Possible options: compare early versus late session Performance Score; detect a reaction-time increase together with an accuracy decline; use a rolling trend over valid shots; or use a session-to-session trend.
- Implementation constraint: Do not implement fatigue classification until the signals, thresholds, window, and required action are confirmed.

### OQ-007: Generation of Seven Phase 1 Sensitivity Values

- Status: Awaiting human decision
- Specification gap: Phase 1 requires seven sensitivity values around the PSA baseline, but their spacing, range, rounding, and eDPI-floor interaction are unspecified.
- Possible options: symmetric percentage offsets around the PSA baseline; symmetric fixed-eDPI offsets; logarithmic spacing; or another research-supported sequence.
- Implementation constraint: Do not generate the seven candidates until the sequence and rounding/floor rules are confirmed.

### OQ-008: Meaning of the Phase 2 "SD < 10%" Threshold

- Status: Awaiting human decision
- Specification gap: The 10% stabilization threshold is authoritative in SensCalibr8 Project Proposal V3.0, but raw standard deviation is expressed in score units and cannot itself be interpreted as a percentage without a defined denominator.
- Possible options: coefficient of variation (`SD / mean * 100`); SD below 10% of the score scale; or SD below 10% of another explicitly defined reference value.
- Implementation constraint: Do not implement Phase 2 stabilization or stopping logic until the denominator and zero/near-zero handling are confirmed.
