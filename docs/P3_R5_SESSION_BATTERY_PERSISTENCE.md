# P3-R5 Session/Battery Persistence Lifecycle

`session_attempts` records every capture attempt before a completed session exists. It supports capture, pause/resume, cancellation, and faults without allowing a terminal attempt to resume. Each attempt is bound to profile, cycle, candidate, battery, accepted calibration configuration, canonical sequence identity, and opaque blind label.

`SessionBatteryPersistenceService` orchestrates completion through the Data Layer. The one SQLite transaction inserts raw evidence and timing diagnostics, writes `session_sequence_audits`, finalizes adaptation flags, marks the attempt completed, and completes the battery only when four distinct modes exist. Any failure rolls all of this back. Pre-finalization shot/trial flags must be null; the frozen mode contract supplies the 50% shot fraction and one initial Tracking adaptation block.

Verification passed: Unity EditMode 110/110; Windows production build; Python production tests 11/11; Phase 0 calibration regressions 72/72.
