# ARCHITECTURE.md

## Purpose

This file defines the technology stack, system design, timer precision requirements, and the full database schema for SensCalibr8. Treat the schema in this file as authoritative — do not add, remove, or rename fields without updating this file first.

---

## 1. Technology Stack

| Layer | Technology | Purpose |
|---|---|---|
| Aim-Test Engine | Unity (C#) | Raw mouse input capture, 3D test scenes, in-app quick charting |
| Data Storage | SQLite | Local relational database; single-file, no server required |
| Deep Analysis | Python (pandas, matplotlib) | Statistical computation, chart generation, HTML report export |
| Documentation | Markdown (.md) | Research reference and rule files consumed by the coding agent during development |

### 1.1 Rationale for Technology Choices

**Unity (C#) over Python for the Test Engine.** Unity's Input System provides direct access to raw mouse delta values, bypassing OS-level cursor acceleration that commonly interferes with Python-based input libraries (e.g. pynput). This is essential for replicating the Raw Input Buffer behavior referenced in the source research.

**SQLite for Storage.** SQLite requires no server setup, supports complex analytical queries, and scales sufficiently for a single-user, long-term dataset spanning months to years of testing history.

**Python for Analysis.** Separating the analysis layer from the Unity engine allows independent iteration on statistical formulas (e.g. Performance Score weighting) without requiring changes to the test engine itself.

---

## 2. Timer Precision Requirement

This is a hard technical constraint. Reaction Time and Travel Time are measured at millisecond resolution, and standard frame-bound timing is not accurate enough for this purpose.

Rules that must be followed without exception:

- Do NOT use `Time.time` for measuring Reaction Time, Travel Time, or any other latency-sensitive timestamp, because it is tied to frame rate and will drift when frame rate is unstable (e.g. dropping from 60fps to 45fps mid-test introduces measurement error).
- Use a high-resolution timer instead: `Time.realtimeSinceStartupAsDouble`, or a `System.Diagnostics.Stopwatch` instance, for all timestamp capture in shot-level and tracking-level data.
- Do NOT use `Input.GetAxis("Mouse X")` / `Input.GetAxis("Mouse Y")`, because these have built-in smoothing applied by Unity's legacy input system, which distorts the true raw mouse movement.
- Use Unity's Input System package to read raw mouse delta directly, with no smoothing or acceleration curve applied.
- Set a fixed Target Frame Rate and disable adaptive VSync during test sessions, so that frame time variance does not leak into recorded data as noise.

Any violation of these rules invalidates the scientific rigor mechanisms described in `RULES.md`, since timestamp noise cannot be distinguished from genuine performance variance.

---

## 3. System Architecture Diagram

```
+----------------------+           +--------------------------+
|   Unity (C#)          |  write    |    SQLite Database        |
|  - Aim Test Scenes x4  | -------->  |   - profiles                |
|  - Raw mouse capture   |           |   - sessions                  |
|  - In-app quick chart  | <--------  |   - shots (raw log)             |
+----------------------+   read     |   - tracking_data                 |
                                     |   - sensitivity_tests               |
                                     |   - phase_history                     |
                                     |   - cycles                              |
                                     |   - injury_risk_flags                     |
                                     +--------------------------+
                                               ^
                                               | read
                                     +--------------------------+
                                     |   Python Analysis Layer     |
                                     |  - pandas statistics          |
                                     |  - matplotlib charts             |
                                     |  - HTML / JSON / CSV export        |
                                     +--------------------------+
```

---

## 4. Database Schema

All tables reference `profile_id` with `ON DELETE CASCADE`, so deleting a profile removes all associated data across every table automatically. This cascading behavior must be enforced at the database level, not application level.

```sql
profiles (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    created_date TEXT,
    mouse_dpi INTEGER,
    dominant_hand TEXT,
    crosshair_config TEXT,
    grip_style TEXT,                -- Fingertip / Palm / Claw / Hybrid (descriptive only, see CONTEXT.md)
    movement_strategy TEXT,         -- Wrist / Arm / Hybrid (descriptive only, see CONTEXT.md)
    mousepad_width_cm REAL,
    mousepad_height_cm REAL,
    ads_multiplier REAL,            -- reference field only, no dedicated test mode (see CONTEXT.md)
    last_active_date TEXT
)
   |
   | (ON DELETE CASCADE — applies to all tables below via profile_id)
   v

sessions (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER REFERENCES profiles(id),
    date TEXT,
    mode TEXT,                      -- one of the 4 Test Modes defined in FEATURES.md
    duration_sec INTEGER
)

shots (
    id INTEGER PRIMARY KEY,
    session_id INTEGER REFERENCES sessions(id),
    profile_id INTEGER REFERENCES profiles(id),
    target_id INTEGER,
    distance_zone TEXT,
    target_size TEXT,
    spawn_position TEXT,
    spawn_timestamp REAL,
    first_mouse_movement_timestamp REAL,
    hit_timestamp REAL,
    hit_position TEXT,
    is_hit BOOLEAN,
    is_outlier BOOLEAN,
    is_adaptation_shot BOOLEAN,      -- TRUE if within the first 50% discard window (see RESEARCH.md)
    sensitivity_value REAL,
    initial_offset_distance REAL,
    micro_adjustment_count INTEGER,
    submovement_count INTEGER,       -- computed per algorithm in RESEARCH.md, Section 5
    final_precision_error REAL
)

tracking_data (
    id INTEGER PRIMARY KEY,
    session_id INTEGER REFERENCES sessions(id),
    profile_id INTEGER REFERENCES profiles(id),
    pattern_type TEXT,               -- linear / curved / variable-speed
    target_speed REAL,
    duration_ms INTEGER,
    deviation_samples TEXT,
    time_on_target_ms INTEGER,
    time_on_target_percentage REAL
)

sensitivity_tests (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER REFERENCES profiles(id),
    edpi REAL,
    cm_360 REAL,
    avg_performance_score REAL,
    performance_score_by_mode TEXT,  -- JSON: {mode: score}, one entry per Test Mode
    grade TEXT,                      -- S / A / B / C / D
    formula_version TEXT,            -- must be recorded on every row, see RESEARCH.md Section 4
    phase INTEGER,                   -- 1, 2, or 3 (Testing Protocol phase, see FEATURES.md)
    sample_size INTEGER
)

phase_history (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER REFERENCES profiles(id),
    phase_number INTEGER,
    winner_edpi REAL,
    timestamp TEXT
)

cycles (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER REFERENCES profiles(id),
    cycle_number INTEGER,
    start_date TEXT,
    end_date TEXT,
    outcome TEXT
)

injury_risk_flags (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER REFERENCES profiles(id),
    flag_type TEXT,                  -- e.g. 'low_edpi_wrist_strain', 'mousepad_constraint_violation'
    triggered_date TEXT,
    edpi_at_trigger REAL,
    acknowledged BOOLEAN DEFAULT 0
)
```

### 4.1 Schema Notes

- `grip_style`, `movement_strategy`, `ads_multiplier` are descriptive/reference fields only. They must never be read by any Performance Score calculation or used to bias results (see `CONTEXT.md`, Out of Scope).
- `formula_version` on `sensitivity_tests` is mandatory on every insert. Never leave it null.
- `is_adaptation_shot` must be set at write time based on the adaptation cutoff logic in `RESEARCH.md`, Section 8, so that analysis queries can filter it without recomputing the cutoff each time.
- `injury_risk_flags` records are informational only and must never block or alter test execution; they exist purely to surface warnings to the user (see `RULES.md`).

---

## 5. Naming Convention Cross-Reference

Database columns use `snake_case` as shown above. C# class and method names in the Unity layer use `PascalCase`. Full conventions are defined in `SKILL.md`; this file only defines the data shape, not the code style.
