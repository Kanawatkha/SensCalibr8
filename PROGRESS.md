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
5. **Protocol and Scoring** — Implement Phase 1-3 execution, counterbalancing, normalization, significance gate, fatigue, outlier handling, Grade, plateau, and Winner selection after Phase 0 freezes every dependent configuration.
6. **Analysis and Data Export** — Add in-app feedback, Python analysis, HTML reporting, JSON/CSV Data Export, and cross-profile comparisons.
7. **Integration and Release QA** — Run end-to-end protocol validation, worked examples, validation edge cases, performance/reproducibility checks, and release packaging.

---

## Current Execution State

- Active phase: **Phase 1 — Foundation & Data Layer (Ready)**
- Last completed round: **P0-R7 — Freeze Calibration Configuration v1**
- Active round: **P1-R1 — Production project foundation (Not Started)**
- Next planned round: **P1-R1 — Establish the production Unity project structure, assemblies, build/test baseline, and environment preflight without implementing Test Engine behavior**
- Production Test Engine status: **Phase 0 configuration dependency unblocked; implementation remains not started and follows the planned Phase 1-3 sequence**

---

## Detailed Execution Plan

The project is divided into eight sequential phases and execution rounds. A round is the normal unit the project owner authorizes in one instruction. Complete, verify, document, and commit one round before moving to the next; never silently continue into another round or phase.

### Execution-Round Protocol

Every round follows this gate sequence:

1. Re-read `AGENTS.md`, `CONTEXT.md`, and `PROGRESS.md`, then the task-relevant sections of the remaining authoritative Markdown files.
2. Confirm that the round belongs to the active phase and that all entry dependencies are complete. Record a conflict or missing decision in Open Questions instead of guessing.
3. Define the smallest testable increment and identify every `RESEARCH.md` constant/configuration it consumes. No code may depend on an unfrozen Phase 0 value.
4. Implement only that round's scope. Preserve the Test Logic, Data, UI, Service, and Python Analysis boundaries from `SKILL.md`.
5. Run unit, integration, reproducibility, data-integrity, and/or visual checks appropriate to the change. Calculation rounds must include the applicable worked examples and validation edge cases from `AGENTS.md` and `RULES.md`.
6. Update the phase status in this file with the outcome, exact test results, issues, and next authorized round.
7. Create one logical local Git commit. Push, tag, publish, or start the next round only when the project owner explicitly requests it.

### Phase Dependency Chain

`Phase 0 -> Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 -> Phase 5 -> Phase 6 -> Phase 7`

- Phase 0 may use a minimal calibration-only harness, but no production target spawning, scoring, Winner selection, or production Test Engine behavior may be implemented before its configuration is frozen.
- Later phases may prepare interfaces for future consumers, but may not bypass the exit gate of an earlier phase.
- A phase is complete only when its exit gate and the project-wide Definition of Done in `AGENTS.md` both pass.

### Phase 0 — Signal Calibration

**Objective:** Replace every intentionally unfrozen measured value with validated, immutable, versioned configuration before production Test Engine or scoring work begins.

**Entry gate:** The specification audit is complete; all 19 Open Questions are resolved; the target Unity/runtime environment and representative input hardware are available for measurement.

**Execution rounds:**

- **P0-R1 — Calibration protocol and acceptance matrix:** Inventory the target runtime, display, input device, DPI, polling behavior, resolution, and operating conditions; define the trace naming/version format, controlled test procedures, repeated-run requirements, invalid-trace rules, and evidence required to accept or reject each measured configuration field. This round defines procedures, not unsupported production constants.
- **P0-R2 — Minimal calibration harness:** Create only the instrumentation needed to collect timestamped raw deltas, camera/angular traces, frame timing, target events, and environment metadata. Provide an append-only calibration export and integrity checks. Keep the harness isolated from the future production scoring and protocol engine.
- **P0-R3 — Input and timing calibration:** Collect representative traces and validate event-rate policy, interval variability, uniform-resampling behavior, timestamp precision, dropped/duplicate event handling, timing tolerance, trace-length requirements, and forward/backward filter edge handling. Confirm that raw samples are never overwritten by derived data.
- **P0-R4 — Arena and visual-geometry calibration:** Validate and select the fixed arena dimensions, camera configuration, FOV, Target Frame Rate, target Small/Medium/Large world dimensions, close/far placement, spawn constraints, visual-angle/Fitts-condition coverage, fixed crosshair size, and Center-Hit zone. Verify identical geometry across repeated runs and supported display conditions.
- **P0-R5 — Mode and signal-response calibration:** Validate the approved fifth-order 7 Hz Butterworth SOS, zero-phase offline filtering, 8/4 degrees-per-second event thresholds, and 80 ms refractory behavior against representative traces. Calibrate Tracking patterns, target speeds, duration/trial contract, spawn timing, Micro-Correction behavior, and mode-specific trace acceptance rules without changing source-authorized constants.
- **P0-R6 — Scoring and statistical calibration:** Derive fixed mode/metric min-max bounds, Submovement lower/upper bounds, normalized Consistency tier cutpoints, numerically-near-zero score tolerance, and the fresh matched confirmatory block/sample and confidence-interval procedure. Validate clamping, lower-is-better inversion, insufficient-data behavior, and reproducibility without deriving bounds dynamically from a production comparison set.
- **P0-R7 — Freeze Calibration Configuration v1:** Consolidate accepted measurements into one immutable complete configuration; assign signal-pipeline, normalization, geometry, protocol, and calibration versions; update `RESEARCH.md` with measured values, method, evidence, and source/artifact references; add regression fixtures; and reject any incomplete/draft configuration for production use.

**Primary outputs:** Calibration procedure, raw trace corpus, calibration-only harness, analysis evidence, complete immutable `calibration_config_v1`, regression fixtures, and updated research/configuration documentation.

**Exit gate:** Every required `calibration_configs` field is populated and validated; repeated runs reproduce accepted results within the frozen tolerances; no Phase 0-dependent constant remains undefined; configuration and evidence are versioned; all Phase 0 tests pass and results are recorded here. Only then may Phase 1 begin and the production Test Engine dependency be considered unblocked.

### Phase 1 — Foundation and Data Layer

**Objective:** Establish the production Unity/C#/SQLite/Python foundation and a tested persistence boundary before product features are built.

**Entry gate:** Phase 0 exit gate passed and Calibration Configuration v1 is frozen.

**Execution rounds:**

- **P1-R1 — Production project foundation:** Pin the supported Unity/runtime and Python environments; establish repository folders and assembly/package boundaries for Test Logic, Data, Service, UI, and Analysis; configure the Unity Input System; add repeatable build/test commands; and document local setup. Do not duplicate calibration-harness responsibilities in production classes.
- **P1-R2 — Central configuration and domain contracts:** Implement typed immutable loaders for every `RESEARCH.md` constant and versioned Phase 0 field, domain value objects/enums, validation of complete configurations, and C#/Python configuration parity. Ensure calculation classes remain plain C# and independent of `MonoBehaviour`.
- **P1-R3 — SQLite schema and migrations:** Implement the complete `ARCHITECTURE.md` schema, indexes/uniqueness checks, all `NOT NULL` constraints, all `ON DELETE CASCADE` relationships, migration/version metadata, and verified `PRAGMA foreign_keys = ON` on every connection.
- **P1-R4 — Repositories and transaction boundary:** Implement centralized connection handling, repositories, mapping, transactional session preservation, database error logging/user surfacing, and recovery behavior. No UI, Test Logic, Service, or Python class may issue raw SQL.
- **P1-R5 — Data-integrity verification:** Test schema creation and migration, foreign-key enforcement per connection, cascade behavior, required-field rejection, candidate/source provenance, one-mode-per-battery uniqueness, configuration immutability, formula/calibration version persistence, rollback behavior, and controlled database failures.

**Primary outputs:** Buildable project shell, environment manifest, centralized versioned configuration, schema/migrations, repository layer, transaction/error policy, and automated data-layer test suite.

**Exit gate:** A clean workspace can build and create/migrate the database; every schema contract and failure path is covered by passing automated tests; layers access data only through the Data Layer; Phase 0 configuration cannot be incomplete or mutated after use.

### Phase 2 — Profiles and Physical Setup

**Objective:** Deliver a complete profile-scoped setup workflow and all foundational sensitivity calculations/validations without starting the aim-test engine.

**Entry gate:** Phase 1 persistence/configuration APIs are stable and tested.

**Execution rounds:**

- **P2-R1 — Calculation and validation services:** Implement the manual-DPI and Physical Ruler Test paths, PSA baseline, eDPI, starting sensitivity, current-versus-baseline comparison, eDPI floor adjustment, cm/360, mousepad constraint, and input-validation services. Verify the required worked example where DPI 1600 yields Starting Sensitivity 0.175 exactly, the physical-counts/distance formula, and all relevant invalid/edge inputs.
- **P2-R2 — Profile lifecycle services:** Implement create/list/select/update/delete behavior, unique local names, profile isolation, active-profile state, current sensitivity, physical setup fields, last-active date, and deletion cascade orchestration.
- **P2-R3 — Slot Selection and Setup UI:** Build the simple table/grid screens from `DESIGN.md`; expose all required fields; show crosshair style/size as fixed; allow only supported high-contrast color selection; include the Physical Ruler Test fallback entry point; keep calculations out of UI classes.
- **P2-R4 — Lifecycle guards and persistence:** Enforce the active-profile deletion prohibition, duplicate-name rejection, confirmation flows for allowed deletion, setup resume/edit behavior, lifetime locking of the selected crosshair color, and profile-scoped data retrieval without cross-profile leakage.
- **P2-R5 — Ergonomic warnings and Dashboard shell:** Evaluate and persist non-diagnostic wrist/mousepad warning flags, display non-blocking banner rows, and build the Dashboard shell for Best Sensitivity, Grade, activity summary, and future mode launch points. Warnings must never alter results or block execution.
- **P2-R6 — Profile/setup acceptance:** Run end-to-end profile creation, selection, edit, restart persistence, multiple-profile isolation, validation edge cases, active/inactive deletion, cascade, formula examples, and warning acknowledgement tests.

**Primary outputs:** Profile and setup workflow, calculation/validation services, Slot Selection, Setup, Dashboard shell, ergonomic warning persistence, and profile acceptance tests.

**Exit gate:** Every setup field persists correctly; all `RULES.md` input-validation cases pass; profiles are isolated; the exact PSA worked example passes; warnings remain informational; active-profile deletion is impossible and inactive deletion cascades correctly.

### Phase 3 — Production Test Engine Core

**Objective:** Build the deterministic, mode-independent runtime that all four tests share, using only frozen Phase 0 configuration.

**Entry gate:** Phases 0-2 complete; production configuration, data access, and profile context are stable.

**Execution rounds:**

- **P3-R1 — Engine contracts and state machine:** Define `ITestMode` and shared lifecycle contracts for prepare/start/capture/end/report/cancel/recover; implement explicit cycle, protocol candidate, battery, session, and session-state models; reject invalid transitions and incomplete configuration.
- **P3-R2 — Deterministic input and timing:** Integrate Unity Input System raw mouse deltas, the approved high-resolution timer, frozen frame policy, timestamp ordering, angular conversion, and raw sample persistence. Do not use `Time.time`, legacy `Input.GetAxis`, or frame-count-derived reaction timing.
- **P3-R3 — Calibrated arena runtime:** Build the enclosed checkerboard arena, fixed camera/FOV/frame policy, cyan spherical target service, application-fixed dot crosshair with profile color, minimal hand/weapon/shoot animation, and unobtrusive HUD exactly within `DESIGN.md` scope.
- **P3-R4 — Target sequencing and confound control:** Implement deterministic seedable condition sequences, size/distance randomization within the frozen matrix, blind sensitivity labels, counterbalanced candidate/mode order, spawn safety constraints, and auditable sequence metadata.
- **P3-R5 — Session/battery persistence lifecycle:** Persist one mode and sensitivity per session, exactly four distinct modes per complete battery, candidate provenance, pause/cancel/failure disposition, transactional completion, and post-session adaptation finalization in one transaction while active shots remain null.
- **P3-R6 — Core-engine verification:** Test timer/input accuracy against calibration fixtures, deterministic replay/sequence generation, frame and geometry invariance, invalid state transitions, incomplete battery behavior, interrupted-write recovery, raw-data preservation, and rejection of unfinalized adaptation data.

**Primary outputs:** Shared Test Engine state machine, deterministic capture/timing, calibrated arena, confound-control sequencer, session/battery persistence, and engine-core regression suite.

**Exit gate:** The engine can run and persist a deterministic synthetic/stub mode without scoring; calibration fixtures reproduce; raw data survives failures; adaptation finalization and battery integrity pass; no mode-specific duplicated plumbing exists.

### Phase 4 — Four Test Modes

**Objective:** Implement and validate each required mode against the shared engine and frozen calibration contracts.

**Entry gate:** Phase 3 shared engine passes synthetic-mode and recovery tests.

**Execution rounds:**

- **P4-R1 — Close Flick:** Implement close-distance/high-frequency target behavior, calibrated size-and-distance randomization, spawn-to-shot Reaction Time, hit/miss, hit position, signed Overflick/Underflick, Final Precision Error, Center-Hit diagnostic, movement/stationary state, raw trace link, and mode completion contract.
- **P4-R2 — Far Flick:** Implement far-separated target timing/position conditions, calibrated size-and-distance randomization, and Travel Time isolated from Reaction Time. Preserve hit/miss, signed error, precision, movement/stationary state, Center-Hit, and raw trace data while limiting differences from Close Flick to documented/calibrated mode behavior.
- **P4-R3 — Tracking:** Implement linear, curved, and variable-speed patterns; calibrated speed/duration/trial contract; Time-on-Target; deviation trace/aggregate; pattern metadata; raw mouse data; and completion rules suitable for post-adaptation trial/window analysis.
- **P4-R4 — Micro-Correction:** Implement randomized stationary offsets of 5-20 pixels, initial offset, micro-adjustment/final precision metrics, Center-Hit diagnostic, and the approved angular Submovement pipeline while preserving raw traces.
- **P4-R5 — Cross-mode battery integration:** Connect all modes to one battery workflow, prevent duplicate modes, preserve blind labels and counterbalanced order, validate mode-specific sample completion, and keep score-independent raw/derived data clearly separated.
- **P4-R6 — Mode acceptance and reproducibility:** Verify target geometry/sequence, metrics, timing, persistence, cancellation/restart, repeatability, HUD non-interference, fixed crosshair, and conformity to every mode requirement in `FEATURES.md` and `DESIGN.md`.

**Primary outputs:** Four production test modes, their derived metric services, battery integration, and per-mode acceptance fixtures/tests.

**Exit gate:** Each mode independently and collectively meets its frozen completion contract; repeated controlled inputs reproduce metrics; all raw and derived records map to the correct profile/cycle/candidate/battery/session; no scoring or protocol rule is hidden inside a mode class.

### Phase 5 — Protocol, Scoring, and Scientific Rigor

**Objective:** Turn validated mode data into the complete Phase 1-3 calibration protocol, auditable scores, grades, and continuous recalibration behavior.

**Entry gate:** All four modes and their metric contracts are complete and stable.

**Execution rounds:**

- **P5-R1 — Metric normalization and score services:** Implement fixed versioned min-max utility scaling, direction inversion, clamping, Submovement penalty mapping, shot-mode Performance Score, Tracking-specific redistributed weights, per-mode aggregation, and formula-version persistence. Test every boundary and worked example available in `RESEARCH.md`.
- **P5-R2 — Phase 1 exploratory protocol:** Generate baseline candidates at 0%, +/-5%, +/-10%, and +/-20% without intermediate rounding; apply and notify the eDPI floor; retain source provenance; blind and counterbalance testing; require 30 valid shots per value separately for each shot-based mode and the frozen Tracking contract.
- **P5-R3 — Confirmatory significance gate:** Select exploratory top candidates, collect fresh non-reused matched blocks, run the two-sided paired randomization/permutation test at alpha 0.05, calculate effect estimate and 95% confidence interval, persist every pair/version, and produce either a Winner or statistical tie.
- **P5-R4 — Phase 2 progressive narrowing:** Generate Winner +/-10%, or the floored/deduplicated union of tied anchors at -10%/0%/+10% with all provenance; count only complete batteries; evaluate stabilization from 5 through 10 batteries using sample-SD CV below 10%; treat near-zero means as undefined/non-passing.
- **P5-R5 — Phase 3 final narrowing:** Generate the Phase 2 Winner and +/-5% candidates, execute the same blind/counterbalanced scientific controls, and persist the final Best Sensitivity with complete cycle/phase/config/formula lineage.
- **P5-R6 — Adaptation, outlier, fatigue, and Grade:** Enforce 50% post-session adaptation labeling; one-pass metric-specific post-adaptation 3-SD flagging in homogeneous scopes; inclusive authoritative score plus excluded sensitivity analysis; separately documented data-quality exclusions; first/second-half fatigue flag over 15% without exclusion; Reaction and Consistency tiers with the worse tier as final Grade.
- **P5-R7 — Continuous cycle and plateau:** Execute 5-10 current-Best sessions, preserve cycle history, detect plateau only when Grade is unchanged for three cycles and relative score change is below 5%, and automatically initialize a new Phase 1-3 cycle from current sensitivity without overwriting history.
- **P5-R8 — Full protocol regression:** Exercise normal Winner, statistical tie, floor collision/deduplication, incomplete batteries, undefined CV, stabilization minimum/maximum, outlier/data-quality split, fatigue, Grade boundaries, plateau/non-plateau, cancellation, restart, and historical version preservation end to end.

**Primary outputs:** Versioned scoring services, complete Phase 1-3 state machine, scientific-rigor controls, Winner/Best Sensitivity, Grade, continuous cycle/plateau behavior, and protocol audit records/tests.

**Exit gate:** Every mechanism in `RULES.md` Section 1 is enforced and auditable; all formula/version examples and boundaries pass; tie and provenance paths lose no data; no statistical flag is silently deleted; historical scores cannot be silently recalculated under new versions.

### Phase 6 — Analysis, Reporting, Comparison, and Data Export

**Objective:** Provide immediate user feedback and reproducible deeper analysis without duplicating authoritative business calculations or exceeding export-only scope.

**Entry gate:** Phase 5 produces stable, versioned, queryable results and audit records.

**Execution rounds:**

- **P6-R1 — Analysis data contracts:** Define read-only, version-aware datasets/views/exports for Python; validate profile isolation, units, null/incomplete-session handling, inclusive versus outlier-excluded aggregates, and parity with authoritative C# results.
- **P6-R2 — Immediate Unity feedback:** Implement the current-session Accuracy-per-sensitivity bar chart and Dashboard result/activity updates using Service/Data outputs only; do not recompute scores in UI code.
- **P6-R3 — HTML report charts 1-5:** Build and verify Sensitivity vs Performance Score Curve, Overflick vs Underflick Balance Chart, Movement vs Stationary Error Graph, Progressive Narrowing Timeline, and Consistency Trend Over Time with traceable source data and versions.
- **P6-R4 — HTML report charts 6-10:** Build and verify Reaction Time Distribution, Performance Grade Timeline, Reaction Time vs Sensitivity Scatter Plot, Submovement Count vs eDPI Curve, and eDPI-normalized Profile Comparison Chart. Verify `shots.submovement_count` exists and is valid before rendering Chart 9.
- **P6-R5 — HTML report composition:** Assemble the versioned standalone report, scientific notes, Winner/tie context, formula/calibration metadata, inclusive authoritative results, sensitivity analysis, warnings, and clear handling of missing/insufficient data.
- **P6-R6 — Profile-separated Data Export:** Implement validated JSON and CSV export with raw/derived/audit/version metadata, safe filenames, deterministic encoding/columns, and explicit messaging that export is not Import/Restore or a recovery guarantee.
- **P6-R7 — Cross-profile Comparison Page:** Implement the simple table from `DESIGN.md` for profile rows and Consistency, Reaction Time Tier, and Performance Score columns normalized through eDPI; preserve profile separation and avoid unsupported causal comparisons.
- **P6-R8 — Analysis/export verification:** Compare Python and C# fixtures, inspect all charts/report layouts, validate empty/partial/tie/outlier/fatigue cases, round-trip parse JSON/CSV as data files only, and confirm no Import/Restore behavior exists.

**Primary outputs:** Stable analysis contract, Unity feedback, ten report charts, standalone HTML report, JSON/CSV Data Export, profile comparison table, and parity/visual/export tests.

**Exit gate:** All ten required charts and the Comparison Page are correct and readable; exported values match the database and preserve versions/profile scope; Python never becomes a second authority for business formulas; Import/Restore remains out of scope.

### Phase 7 — Integration and Release QA

**Objective:** Prove the whole offline application is reproducible, robust, scientifically compliant, and packageable on the target environment.

**Entry gate:** Phases 0-6 are complete with no unresolved Open Questions or failed phase gates.

**Execution rounds:**

- **P7-R1 — Golden-path end-to-end validation:** On a clean database, exercise setup through Phase 1-3, Best Sensitivity, Grade, continuous cycle, report, comparison, and Data Export; verify every persisted relationship and version against the documented journey.
- **P7-R2 — Edge and failure matrix:** Re-run all `RULES.md` input cases plus duplicate/active profile operations, floor notification, tie/floor deduplication, invalid/incomplete configuration, incomplete batteries, near-zero CV, acquisition errors, statistical outliers, fatigue, database interruption, cancellation, and restart/recovery.
- **P7-R3 — Reproducibility, timing, and performance:** Repeat fixed-seed/calibration-fixture runs; inspect sampling/frame/timer tolerances, long-session stability, database growth/write latency, analysis runtime, and raw-data preservation under the frozen target environment.
- **P7-R4 — Migration and historical compatibility:** Test database migration from every supported schema state, immutable historical formula/calibration interpretation, report/export compatibility, cascade behavior, and controlled rejection of unsupported/corrupt states without silent data loss.
- **P7-R5 — UX, visual, and scope compliance:** Inspect every screen/arena state against `DESIGN.md`; verify fixed palette/crosshair, non-blocking warnings, readable HUD/tables/reports, no decorative scope creep, no unsupported grip/ADS causal scoring, no cloud/multiplayer/Import/Restore functionality, and fully offline operation.
- **P7-R6 — Release packaging and clean-machine smoke:** Produce the release candidate, dependency/license inventory, configuration/database locations, operator instructions, and checksums; install/run on a clean target environment and execute a smoke workflow without development-only dependencies.
- **P7-R7 — Final evidence and release handoff:** Run the complete automated suite, archive calibration/test/report evidence, resolve or explicitly block every failure, update all documentation and this status log, and prepare the release commit. Tagging, publishing, or pushing remains a separate project-owner-authorized action.

**Primary outputs:** End-to-end evidence pack, edge/failure matrix, reproducibility/performance results, migration tests, visual/scope audit, packaged release candidate, operating notes, and final release checklist.

**Exit gate:** Every `AGENTS.md` Definition of Done condition passes; all phase gates remain green on the packaged build; no open ambiguity, critical defect, silent data loss, or undocumented deviation remains; the project owner can authorize the release/tag/push from a fully evidenced release candidate.

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

### Pre-Development: Phase 1 Tie Expansion Resolved

- Status: Completed
- Date: 2026-07-14
- What was done: Applied the project-owner decision for OQ-019. A Phase 1 statistical tie now generates the deduplicated Phase 2 union of both anchors at -10%, 0%, and +10%, after applying the eDPI floor. Added canonical candidate and many-to-one provenance storage so deduplication never loses anchor/offset history.
- Test results (per Definition of Done in AGENTS.md): Documentation-only work. Verified all 19 OQ records are Resolved with 0 Open; all 31 schema foreign keys include `ON DELETE CASCADE`; the tie-union/deduplication rule appears consistently in research, feature, architecture, and progress contracts; and `git diff --check` passed.
- Issues encountered: None. Candidate generation explicitly forbids intermediate rounding and deduplicates only after floor application.
- Next step: Proceed with Phase 0 planning and calibration.

### Pre-Development: Final Documentation Readiness Audit

- Status: Completed
- Date: 2026-07-14
- What was done: Audited all 11 Markdown files, including both local primary-source documents, after resolving OQ-001 through OQ-019. Verified specification cross-references, source links, numeric contracts, schema relationships, scope boundaries, and Git synchronization.
- Test results (per Definition of Done in AGENTS.md): Confirmed 19 Resolved and 0 Open questions; 17 ordered `RESEARCH.md` sections; 23 valid local Markdown links with 0 broken links; all 31 schema foreign keys include `ON DELETE CASCADE`; no active TODO/TBD/Pending Proposal markers exist outside the reusable historical log template; `git diff --check` passed; and local `HEAD` matched `origin/main` at the audited commit before this audit entry.
- Issues encountered: The decision/specification baseline is complete, but production calibration data is intentionally not complete because Phase 0 has not started. Missing measured outputs are input sampling rate/tolerance and edge handling, normalization and Submovement bounds, Consistency-tier cutpoints, scoring-zero tolerance, target/arena/camera/FOV/frame-rate geometry, Tracking duration/speed/trial contract, center-hit zone, and confirmatory analysis/sample contract.
- Next step: Treat the Markdown set as ready for Phase 0, not as permission to implement the production Test Engine. Complete and version Phase 0 outputs first.

### Pre-Development: Whole-Project Execution Plan

- Status: Completed
- Date: 2026-07-14
- What was done: Re-read all nine authoritative root Markdown files in the required order and expanded the eight-phase roadmap into 53 individually authorizable execution rounds. Added the per-round working protocol, strict dependency chain, entry gates, primary outputs, and exit gates covering Signal Calibration, production foundation/data, profiles/setup, Test Engine core, four modes, Phase 1-3 protocol/scoring, analysis/export, and integration/release QA.
- Test results (per Definition of Done in AGENTS.md): Documentation-only planning task. Verified 8 Phase headings, 53 unique round IDs, and exactly 8 Entry Gates, 8 Primary Output groups, and 8 Exit Gates. Confirmed every feature group, schema area, all 10 exact report charts, scientific-rigor mechanism, input-validation rule, design surface, coding-layer constraint, analysis/export deliverable, Phase 0 blocker, worked-example requirement, and release concern has an owning round and exit gate. Confirmed the plan does not authorize production Test Engine or scoring implementation before Calibration Configuration v1 is frozen; `git diff --check` passed.
- Issues encountered: No new specification contradiction or Open Question was found. The plan intentionally requires real target-environment measurements and user-assisted trace collection in Phase 0 rather than inventing missing calibration values.
- Next step: Begin only P0-R1 (Calibration protocol and acceptance matrix) when explicitly authorized by the project owner.

### Phase 0 / P0-R1: Calibration Protocol and Acceptance Matrix

- Status: Completed
- Date: 2026-07-15
- What was done: Created `calibration/P0_R1_CALIBRATION_PROTOCOL.md` as the controlling Phase 0 procedure and acceptance matrix. Defined environment/runtime/input/display inventory, stable artifact IDs and naming, append-only evidence packages, controlled capture/repeat/review workflow, structural rejection versus environmental quarantine, version/change rules, all downstream acceptance gates, and the exact handoff contract for the calibration-only harness. Created `calibration/ENVIRONMENT_INVENTORY.md` with an automatically observed baseline plus explicit manual-confirmation fields; unknown values are never replaced by assumptions.
- Test results (per Definition of Done in AGENTS.md): Documentation/procedure round; no formula or application input validation was implemented. Automated structural review found all 19 non-ID `calibration_configs` fields represented in the protocol, 16 acceptance-matrix rows, 9 completed P0-R1 exit checks, all required protocol sections present, a positive P0-R2 readiness decision, and an explicit P0-R3 block while mandatory environment fields remain unknown. Confirmed that no unsupported measured threshold, sample size, or repeat count was invented and that no source citation was added or changed.
- Issues encountered: Restricted Windows CIM access prevented automatic CPU/GPU/display/mouse model discovery, and no Unity installation was detected in the checked standard Hub locations. Mouse DPI/polling/firmware, display refresh/native resolution, GPU/driver, Unity version, and physical operating conditions therefore require operator or harness confirmation. These are measured environment prerequisites, not a new specification ambiguity; they block production-quality trace capture in P0-R3, but P0-R2 may begin by selecting/locating the Unity environment. Per the project owner's instruction, no Git operation was performed in this round.
- Next step: Execute only P0-R2 (Minimal calibration harness): resolve the Unity environment preflight, then implement append-only raw input/frame/target capture, manifests, integrity hashes, interruption behavior, and fixtures without production scoring or Test Engine behavior.

### Phase 0 / P0-R2: Minimal Calibration Harness

- Status: Completed
- Date: 2026-07-15
- What was done: Installed and verified the official Unity 6000.3.18f1 Editor (`5ebeb53e4c07`) and created the isolated `calibration/harness` project with Input System 1.17.0 and the Editor-matched Test Framework 1.6.0. Implemented a calibration-only `MonoBehaviour` adapter over plain C# services; timestamped raw mouse deltas from `InputSystem.onEvent`; independent high-resolution frame timing; target/camera event capture; automatic environment identity and runtime-assembly checksum; explicit `unknown` normalization for operator-only fields; immutable environment/capture/start/final manifests; separate background append-only JSONL streams; strict run/trace/environment/condition linkage plus contiguous sequence and monotonic-time enforcement; per-artifact ID/size/SHA-256/creation/producer/source metadata; completion/interruption handling; and abandoned-run recovery. Recorded the complete acceptance decision in `calibration/P0_R2_HARNESS_ACCEPTANCE.md`. No production target spawning, scoring, Grade, Winner selection, or protocol progression was added.
- Test results (per Definition of Done in AGENTS.md): Unity EditMode/NUnit run on the pinned Editor passed **9/9 tests**, with 0 failed, 0 inconclusive, and 0 skipped; the authoritative output is `calibration/harness/editmode-test-results.xml` and the corresponding Editor log reports exit code 0. Fixtures verify ID normalization, UTC enforcement, append-only drain and overwrite refusal, high-resolution clock ordering, automatic runtime identity/checksum and explicit unknown fields, completed raw/frame/target evidence with every hash and integrity relationship checked, rejection of broken relationships/non-contiguous sequences/non-monotonic timestamps, dispose interruption, and abandoned-run recovery. A final static scope/API scan found no `Input.GetAxis`, `Time.time`, production scoring, Winner selection, protocol progression, or non-`CreateNew` raw-file creation. No formula or product input-validation behavior was introduced in this calibration-only round, so the worked PSA/input-validation portions of the project-wide Definition of Done are not applicable here.
- Issues encountered: The initial batch verification was blocked by missing Unity activation (return code 198); the project owner completed Unity Hub authentication and the Personal license became active. The first package import also exposed the missing Test Framework declaration and the incorrect use of `-quit` with `-runTests`; both were corrected and documented before the accepted clean run. Mandatory operator environment fields in `calibration/ENVIRONMENT_INVENTORY.md` remain unknown and therefore still block acceptance-bearing P0-R3 trace capture, as designed. Unity Editor/installers and generated Unity caches are local tooling/build outputs and are not project deliverables. Per the project owner's standing instruction, no Git operation was performed.
- Next step: Await explicit authorization for `P0-R3 — Input and timing calibration`. Before its first production-quality trace, complete and verify every mandatory manual environment field, then freeze a P0-R3 capture plan and collect representative repeated traces without changing the raw evidence.

### Phase 0 / P0-R3: Input and Timing Calibration

- Status: Completed — strict candidates v1/v2 remain rejected; owner-approved operational timing contract v1 accepted with limitations preserved
- Date: 2026-07-15
- What was done: Added the isolated timing-analysis workflow and advanced it to `p0-r3-timing-analysis-v3`. The project owner completed five fresh Confirmation v3 runs against candidate v1. Integrity, identity, plan state, device stability, ordinal completeness, and timestamp order all passed, but only 1/5 runs passed the unexpanded Pilot-extrema envelope; candidate v1 is retained as rejected and no Confirmation v3 value was used to modify it. Added an explicit immutable non-acceptance decision. Implemented and tested a new contract-derivation path using single-future-observation prediction intervals with Bonferroni correction across the predeclared 25 statements (5 metrics x 5 future runs), family-wise alpha 0.05, df 4, and NIST Student-t critical value 7.173 at cumulative probability 0.999. Candidate v2 was computed exclusively from the original Pilot v3 diagnostics: canonical grid `1000 Hz`, p99 tolerance `0.43251822850161387 ms`, median-rate interval `997.4538729569023-1006.8357184355203 Hz`, maximum MAD `0.06653463461157862 ms`, maximum burst fraction `0.0849851831868774`, and maximum gap fraction `0.22850524713073753`. Published `p0-r3-native-harness-v2` with fresh environment/confirmation-plan identity for Confirmation v4; capture behavior is unchanged. The executable SHA-256 remains `c9503babac13ac968e985842e332609c3d10b447751134b648bff554d3c6b60d`; runtime assembly SHA-256 is `c30a01a3282c8c357d76a373a9cd94c736464952065316e4ee9a5cf7385bb8f0`. Candidate v2 remains non-production until fresh Confirmation v4 passes.
- Test results (per Definition of Done in AGENTS.md): Confirmation v3 analyzed all **5/5** fresh integrity-verified packages and correctly returned `accepted=false`; run-level contract acceptance was **1/5**, with all **5/5** passing timestamp-order and evidence-integrity checks. Python `unittest` passed **20/20** with 0 failures, adding worked checks for the prediction formula and rejection of an inconsistent Bonferroni critical-value probability. Native v2 .NET Release publish completed with **0 warnings and 0 errors**; the published runtime self-test passed. Unity EditMode/NUnit previously passed **14/14**. Raw evidence and prior derived artifacts were never overwritten. Product formula/input-validation examples remain not applicable to calibration-only timing instrumentation.
- Issues encountered: Candidate v1's direct pilot minimum/maximum method understated future-run uncertainty: four valid runs slightly exceeded rate/burst bounds and two slightly exceeded p99 residual. The failure is treated as a contract-design rejection, not operator failure or permission to tune against confirmation. Candidate v2's normal-summary assumption with only five Pilot runs is explicitly documented; Bonferroni correction and a large NIST t critical value conservatively carry that uncertainty. SciPy is still absent from the pinned runtime, so the 18-sample pad length must be reverified against generated coefficients in P0-R5. The Bluetooth-versus-USB-HID observation remains preserved. Per the project owner's standing instruction, no Git operation was performed.
- Closure update: Confirmation v4 retained five fresh integrity-valid runs. Strict candidate v2 returned **0/5 accepted** because Windows user-space receipt burst/MAD behavior again exceeded the Pilot-derived distribution limits; all five retained strict timestamp order and the 1000 Hz cadence class remained modal. On 2026-07-15 the project owner explicitly ended further mouse calibration and authorized progression. The closure does not relabel candidate v2 as passed. It accepts `sc8-p0-r3-timing-contract-v1` under `integrity-modal-cadence`: 1000 Hz canonical grid, 0.5 ms half-cadence partition tolerance, 18-sample structural pad length, strict timestamp ordering, nominal median/modal cadence, diagnostic burst/gap retention, and gap-safe resampling. User-entered DPI, current sensitivity, and configured polling Hz are persisted while actual cadence is measured per session. Post-waiver evaluation passed these operational gates for **5/5** Confirmation v4 runs. Python tests passed **21/21** after adding the accepted policy fixture. `RESEARCH.md`, `FEATURES.md`, `DESIGN.md`, `RULES.md`, and `ARCHITECTURE.md` now carry the operational contract and input/storage policy. No Git operation was performed.
- Next step: Execute `P0-R4 — Arena and visual-geometry calibration` using deterministic mathematical and automated render verification; no further P0-R3 operator capture is required.

### Phase 0 / P0-R4: Arena and Visual-Geometry Calibration

- Status: Completed
- Date: 2026-07-15
- What was done: Froze and accepted immutable geometry contract `sc8-test-geometry-v1` without requiring further operator aim runs. Added the candidate/accepted JSON contracts, a deterministic Python geometry analyzer, Python invariant tests, plain-C# Unity projection math, Unity EditMode parity fixtures, and a Unity reference renderer. The accepted contract fixes a 1920 x 1080 16:9 reference viewport with letterboxing on other aspect ratios; 103-degree horizontal/70.53280043291679-degree vertical FOV; a 10-world-unit target plane; a 20 x 12 x 21 enclosed unlit checkerboard arena; 144 Hz frame policy with VSync/adaptive sync disabled; 0.75/1.50/2.25-degree cyan target diameters; Close 5/10/15-degree and Far 20/30/40-degree center offsets with full 3 x 3 size crossing; a four-pixel filled-dot crosshair; a Center-Hit radius ratio of 0.5; a 32-pixel edge margin; and a 64-pixel top HUD reserve. Updated `RESEARCH.md`, `FEATURES.md`, `DESIGN.md`, and `ARCHITECTURE.md` to make the accepted contract authoritative while leaving spawn timing, Tracking, and signal response to P0-R5.
- Test results (per Definition of Done in AGENTS.md): The deterministic derived artifact reports every configuration, FOV, reprojection, size-separation, Fitts, safe-viewport, containment, Center-Hit, Micro-Correction, frame-policy, and repeatability gate as passed. Python geometry tests passed **8/8** and the combined P0-R3/P0-R4 suite passed **29/29**. Unity 6000.5.3f1 EditMode/NUnit passed **19/19**, 0 failed/skipped/inconclusive. The 1920 x 1080 reference render passed visual inspection for enclosure, contrast, target-edge safety, HUD clearance, crosshair visibility, and absence of shadows/clipping/decorative geometry. No production formula or profile input validation was changed, so the PSA worked example and unrelated `RULES.md` validation cases are not applicable to this calibration-only round.
- Issues encountered: The first Unity parity execution exposed that EditMode uses a default 640 x 480 screen unless projection dimensions are supplied explicitly. The next execution passed assertions but logged an attached-RenderTexture cleanup error. The fixture and cleanup were corrected, and only the clean v4 result is authoritative; failed v2/v3 evidence remains preserved. Spawn frequency, Tracking pattern speed/duration/trials, Butterworth generated-coefficient parity, mode-specific trace acceptance, normalization bounds, Consistency tiers, scoring-zero tolerance, and confirmatory sampling remain intentionally unresolved Phase 0 deliverables. Per the project owner's standing instruction, no Git operation was performed.
- Next step: Await explicit authorization for `P0-R5 — Mode and signal-response calibration`. This round will verify the approved fifth-order 7 Hz zero-phase Butterworth pipeline and 8/4 deg/s plus 80 ms event logic, then freeze spawn timing, Tracking behavior, and mode-specific trace contracts without asking the owner to repeat P0-R3 polling tests.

### Phase 0 / P0-R5: Mode and Signal-Response Calibration

- Status: Completed
- Date: 2026-07-15
- What was done: Accepted combined contract `sc8-p0-r5-signal-mode-contract-v1` with signal version `sc8-signal-pipeline-v1` and mode version `sc8-mode-contract-v1`. Implemented a dependency-validating Python analyzer that derives a canonical fifth-order 7 Hz Butterworth SOS at the accepted 1000 Hz grid without adding a SciPy runtime dependency; odd-pad forward/backward filtering; first-difference Euclidean angular velocity; explicit `>=8` onset, `<4` end, and less-than-80-ms refractory merge semantics; deterministic condition balancing independent of sensitivity/blind label; 30-opportunity shot-mode completion with 1.5-second timeout; Close 500-1000 ms foreperiod; Far reaction/travel separation; Small-only Micro-Correction; two balanced 9-trial Tracking blocks; analytic Linear/Curved/Variable-Speed paths; six-second trials; one-second interval-weighted windows; and RMS Tracking Deviation. Added plain-C# parity implementations/tests and immutable accepted/candidate/evidence artifacts. Reconciled the old generic size rule with the newer specific P0-R4 Micro-Correction contract and revised the documented Tracking schema from one scalar speed to versioned path parameters plus trial/window rows.
- Test results (per Definition of Done in AGENTS.md): All **15/15** P0-R5 gates passed. Python calibration tests passed **44/44** total, including **15** new P0-R5 tests for filter response, edge length, zero-phase peak, threshold/refractory boundaries, invalid dependencies/inputs, shot balance, Tracking path safety, interval-weighted metric invariance, exact trial boundaries, reproducibility, and append-only output. Unity 6000.5.3f1 EditMode/NUnit passed **27/27** total, including **8** new C#/Python parity and behavior tests, with 0 failed/skipped/inconclusive. Verified three stable SOS sections, DC gain 1.0, 7 Hz single-pass gain 0.7071067811866065, forward-backward gain 0.5000000000000835, pad length 18, 20-sample minimum, and zero impulse-peak displacement. No production scoring/profile calculation was changed, so the PSA 1600 -> 0.175 example and unrelated `RULES.md` profile validation cases are not applicable to this calibration-only round.
- Issues encountered: `FEATURES.md` generically required all modes to vary three target sizes while the specific Micro-Correction definition and accepted `sc8-test-geometry-v1` fixed Small. P0-R5 preserved the later specific immutable contract and documented Micro as the explicit exception; Close, Far, and Tracking retain balanced size variation. The first zero-phase unit assertion compared the entire finite padded impulse including distant edge transients; the peak was correct but tail symmetry differed by up to approximately 4.5e-8. The test was corrected to assess central impulse symmetry plus exact peak position without changing filter coefficients or acceptance thresholds. The pinned Python runtime lacks SciPy, so canonical coefficients are generated in-project and cross-validated against analytic Butterworth response, SciPy-documented SOS/pad semantics, and C# parity. P0-R6 scoring/statistical values remain intentionally undefined. Per the project owner's standing instruction, no Git operation was performed.
- Next step: Await explicit authorization for `P0-R6 — Scoring and statistical calibration`: derive and freeze fixed normalization and Submovement bounds, Consistency-tier cutpoints, scoring-zero tolerance, and the fresh matched confirmatory sample/CI contract. No additional P0-R3 polling test is required.

### Phase 0 / P0-R6: Scoring and Statistical Calibration

- Status: Completed
- Date: 2026-07-15
- What was done: Accepted combined contract `sc8-p0-r6-scoring-statistics-contract-v1` with formula `sc8-performance-score-v1`, normalization `sc8-normalization-v1`, Consistency tiers `sc8-consistency-tier-v1`, and confirmatory contract `sc8-confirmatory-v1`. Froze mode-specific percentage, reaction/travel/correction-time, precision/deviation, and Consistency sample-SD bounds from accepted P0-R4/P0-R5 task spans and sample counts; Submovement bounds 1-6 from the IEEE CoG 2022 Figure 8 analysis scale; scoring-zero tolerance 1e-9 points; 15-shot/54-window aggregation; fail-closed zero-hit behavior; the source formula's unclamped -10 to 100 shot range; equal four-mode battery averaging; fixed Consistency utility tiers; Close Reaction Time as the Grade reaction input; and exactly 10 fresh complete-battery pairs with 5/5 order counterbalancing and exhaustive 1024-assignment two-sided sign-flip testing. Added a deterministic Python analyzer, 13 Python tests, plain-C# parity logic, 8 Unity tests, candidate/accepted contracts, append-only evidence, and the full protocol/acceptance record. Updated `RESEARCH.md`, `FEATURES.md`, `RULES.md`, and `ARCHITECTURE.md`, including pair-order/seed/condition audit fields and mandatory final-state/error fields for every resolved hit, miss-click, or timeout.
- Test results (per Definition of Done in AGENTS.md): All **15/15** P0-R6 deterministic gates passed. The complete Python calibration suite passed **57/57**, 0 failed, including exact worked results Shot = **77.0**, Tracking = **81.875**, CV = **5.0%**, formula floor = **-10**, and the confirmatory positive fixture `n=10`, **1024** assignments, p = **0.001953125**, effect = **5**, 95% CI **[5,5]**, candidate A; the symmetric fixture returned p = **1.0** and statistical tie. Unity 6000.5.3f1 EditMode/NUnit passed **35/35**, 0 failed/skipped/inconclusive, including all 8 new Python/C# parity fixtures. Invalid normalization bounds, out-of-range components, incomplete batteries/pairs, dependency drift, zero-CV division, exact tier boundaries, append-only overwrite refusal, and deterministic byte reproduction were tested. The PSA 1600 -> 0.175 example and profile input-validation cases were not changed by this isolated calibration round and remain owned by their planned production rounds.
- Issues encountered: One initial Python assertion compared the mathematically maximal shot score to literal 100 using exact binary equality; IEEE-754 evaluation produced 99.99999999999999. The assertion was corrected to the frozen 1e-12 calculation tolerance without changing the formula or evidence. The 95% Student-t interval is explicitly descriptive uncertainty with its small-sample/shape assumptions; it is not a second Winner gate, while the exhaustive randomization p-value controls Winner versus tie. Fixed `n=10` enables balanced order and useful exact p-value resolution but makes no guaranteed-power claim. No operator capture was required. Per the project owner's standing instruction, no Git operation was performed.
- Next step: Await explicit authorization for `P0-R7 — Freeze Calibration Configuration v1`: verify every `calibration_configs` field is populated from P0-R3 through P0-R6, consolidate the SHA-pinned contracts into one immutable configuration, add cross-language/config regression fixtures, run the Phase 0 exit audit, and only then unblock Phase 1.

### Phase 0 / P0-R7: Freeze Calibration Configuration v1

- Status: Completed
- Date: 2026-07-15
- What was done: Consolidated the SHA-pinned P0-R3 timing and owner-waiver record, P0-R4 geometry, P0-R5 signal/mode, and P0-R6 accepted scoring/statistical payload into immutable `sc8-calibration-config-v1` / `calibration_config_v1`. Populated the exact 20-field non-ID `calibration_configs` projection, canonicalized all six embedded JSON contracts, retained formula/mode/tier/confirmatory versions, and published a separate acceptance envelope pinning the final config hash. Added a deterministic Python freeze builder and validator, append-only derived evidence, mutation/draft/incomplete/overwrite rejection tests, and a plain-C# Unity loader that verifies config hash, all six source hashes, version identities, required values, embedded contracts, and scalar parity before returning a read-only snapshot. Updated `RESEARCH.md`, `ARCHITECTURE.md`, `FEATURES.md`, and `RULES.md` to make the single accepted artifact authoritative.
- Test results (per Definition of Done in AGENTS.md): P0-R7 deterministic gates passed **20/20** and all schema fields were populated **20/20**. The complete Python calibration suite passed **72/72**, including **15** P0-R7 tests, with 0 failures. Unity 6000.5.3f1 EditMode/NUnit passed **41/41**, including **6** P0-R7 tests, with 0 failed/skipped/inconclusive. Verified byte-deterministic builds, exact canonical JSON round-trips, source/config/evidence/Unity SHA-256 pins, PSA **1600 -> 0.175**, Shot **77.0**, Tracking **81.875**, CV **5.0%**, formula floor **-10**, Reaction boundary behavior, and confirmatory minimum p **0.001953125**. Draft, incomplete, missing-field, scalar-drift, source-hash-drift, config-hash-drift, and overwrite cases were rejected. The Phase 0 exit gate passed.
- Issues encountered: The accepted P0-R3 timing contract remains explicitly based on the project-owner calibration waiver; both strict distribution candidates remain rejected and `strict_timing_confirmation_passed` remains false. The frozen contract therefore claims an operational current-host 1000 Hz processing/cadence policy, not universal physical 1000 Hz delivery. This limitation and its production safeguards are embedded in the config and acceptance envelope. No operator retest was required. Per the project owner's standing instruction, no Git operation was performed.
- Next step: Await explicit authorization for `Phase 1 — P1-R1: Production project foundation`. Establish the production project/build/test structure and environment baseline first; do not jump directly to the Test Engine.

### Repository Hygiene Audit Before Phase 1

- Status: Completed
- Date: 2026-07-15
- What was done: Audited the complete Git working set before any staging or push. Added a root `.gitignore` that excludes downloaded Unity editor/installers, Unity `Library`/`Logs`/`UserSettings` and other generated directories, calibration build binaries, native .NET `bin`/`obj`, Python caches, local duplicate test-run outputs, IDE/OS noise, and verbose evidence logs. Added `.gitattributes` to normalize source/project text to LF across platforms while preserving evidence images as binary. Kept all reproducible source, Unity `Assets` plus `.meta` files, Unity `Packages` manifests/lock file, `ProjectSettings`, Python/C# tests, native capture source, calibration plans/contracts, structured JSON/XML/PNG/Markdown evidence, and authoritative project documentation visible to Git.
- Verification: Before the ignore rules Git reported **54,579 untracked files**: 49,716 under the locally downloaded `calibration/.unity` editor, 4,429 under Unity `Library`, 164 build artifacts, and additional caches/installers/logs. After the rules plus the new `.gitignore` and `.gitattributes`, Git reports **179 intentional untracked files**. Representative source/config/evidence paths are confirmed not ignored, generated roots are confirmed ignored, no ignored generated file is already tracked, and `git diff --check` passes.
- Disk note: Ignored local artifacts remain available and were not deleted: approximately 7.93 GB downloaded Unity editor data, 3.98 GB Unity installer, 178.6 MB Hub installer, 99 MB Unity `Library`, 90 MB calibration builds, and small native/Python caches. They may be deleted later only if the project owner wants to reclaim disk space.
- Git action: No file was staged, committed, or pushed during this audit.
- Next step: Review this audit with the project owner. Push only after explicit authorization; otherwise continue with `Phase 1 — P1-R1` when authorized.

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

Current register status: **19 Resolved, 0 Open**. Phase 0 measured deliverables are implementation prerequisites, not unresolved specification decisions.

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

- Status: Resolved — user selected option 1 on 2026-07-14
- Resolution: Build the deduplicated union of each tied anchor at -10%, 0%, and +10%. Generate without intermediate rounding, apply the eDPI floor before deduplication, and retain every anchor/offset/pre-floor provenance record for each surviving canonical candidate.
- Specification gap: OQ-005 requires both tied candidates to continue to Phase 2, while the existing Phase 2 rule defines one Phase 1 Winner plus/minus 10%. With two anchors, the candidate-set expansion and deduplication behavior are not defined.
- Possible options: use the deduplicated union of each anchor and its +/-10% variants (preserves the narrowing rule but may produce up to six values); run two independent three-value Phase 2 branches and compare branch winners later (clean separation but substantially more testing); or test only the two tied values in Phase 2 (least work but suspends the documented +/-10% narrowing rule for ties).
- Recommendation: Use the deduplicated union of both anchors and their +/-10% variants, with the existing eDPI floor applied and original anchor/offset provenance retained for auditability.
- Implementation constraint: Candidate generation must use canonical final eDPI uniqueness per cycle/phase and must never discard provenance when multiple sources collapse to one value.
