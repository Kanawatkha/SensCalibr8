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

All foreign keys use `ON DELETE CASCADE`. Deleting a profile therefore removes all associated data, and deleting a parent session or cycle removes its dependent rows. This cascading behavior must be enforced at the database level, not application level. SQLite foreign-key enforcement is disabled by default on each new connection, so every Data Layer connection must execute `PRAGMA foreign_keys = ON` immediately after opening and verify that it is enabled before any schema mutation or write.

```sql
PRAGMA foreign_keys = ON;

profiles (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    created_date TEXT NOT NULL,
    mouse_dpi INTEGER NOT NULL,
    current_sensitivity REAL NOT NULL,
    dominant_hand TEXT NOT NULL,
    crosshair_config TEXT NOT NULL, -- user-selected color only; dot style and size are application-fixed
    grip_style TEXT NOT NULL,       -- Fingertip / Palm / Claw / Hybrid (descriptive only, see CONTEXT.md)
    movement_strategy TEXT NOT NULL,-- Wrist / Arm / Hybrid (descriptive only, see CONTEXT.md)
    mousepad_width_cm REAL NOT NULL,
    mousepad_height_cm REAL NOT NULL,
    ads_multiplier REAL NOT NULL,   -- reference field only, no dedicated test mode (see CONTEXT.md)
    last_active_date TEXT NOT NULL
)
   |
   | (ON DELETE CASCADE — applies to all tables below via profile_id)
   v

sessions (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    date TEXT NOT NULL,
    mode TEXT NOT NULL,             -- one of the 4 Test Modes defined in FEATURES.md
    duration_sec INTEGER NOT NULL
)

shots (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    target_id INTEGER NOT NULL,
    distance_zone TEXT NOT NULL,
    target_size TEXT NOT NULL,
    spawn_position TEXT NOT NULL,
    spawn_timestamp REAL NOT NULL,
    first_mouse_movement_timestamp REAL,
    hit_timestamp REAL,
    hit_position TEXT,
    is_hit BOOLEAN NOT NULL,
    is_outlier BOOLEAN,
    is_adaptation_shot BOOLEAN,     -- NULL while active; finalized after the session ends (see Schema Notes)
    sensitivity_value REAL NOT NULL,
    initial_offset_distance REAL NOT NULL,
    micro_adjustment_count INTEGER,
    submovement_count INTEGER,       -- computed per algorithm in RESEARCH.md, Section 5
    final_precision_error REAL
)

tracking_data (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    sensitivity_value REAL NOT NULL,
    pattern_type TEXT NOT NULL,      -- linear / curved / variable-speed
    target_speed REAL NOT NULL,
    duration_ms INTEGER NOT NULL,
    deviation_samples TEXT NOT NULL,
    time_on_target_ms INTEGER NOT NULL,
    time_on_target_percentage REAL NOT NULL
)

cycles (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_number INTEGER NOT NULL,
    start_date TEXT NOT NULL,
    end_date TEXT,
    outcome TEXT
)

sensitivity_tests (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    edpi REAL NOT NULL,
    cm_360 REAL NOT NULL,
    avg_performance_score REAL NOT NULL,
    performance_score_by_mode TEXT NOT NULL, -- JSON: {mode: score}, one entry per Test Mode
    grade TEXT,                      -- S / A / B / C / D
    formula_version TEXT NOT NULL,   -- must be recorded on every row, see RESEARCH.md Section 4
    phase INTEGER NOT NULL,          -- 1, 2, or 3 (Testing Protocol phase, see FEATURES.md)
    sample_size INTEGER NOT NULL
)

phase_history (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    phase_number INTEGER NOT NULL,
    winner_edpi REAL NOT NULL,
    timestamp TEXT NOT NULL
)

injury_risk_flags (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    flag_type TEXT NOT NULL,         -- e.g. 'low_edpi_wrist_strain', 'mousepad_constraint_violation'
    triggered_date TEXT NOT NULL,
    edpi_at_trigger REAL NOT NULL,
    acknowledged BOOLEAN NOT NULL DEFAULT 0
)
```

### 4.1 Schema Notes

- `grip_style`, `movement_strategy`, `ads_multiplier` are descriptive/reference fields only. They must never be read by any Performance Score calculation or used to bias results (see `CONTEXT.md`, Out of Scope).
- `current_sensitivity` stores the user's current Valorant sensitivity entered during profile setup so that Step 0 can compare it with the PSA baseline.
- `crosshair_config` stores only the high-contrast color selected at profile creation. Dot style and dot size are fixed by the application and must not be stored as user-editable settings.
- `formula_version` on `sensitivity_tests` is mandatory on every insert. Never leave it null.
- `is_adaptation_shot` must remain null while shots are being captured. After the session ends and the actual shot total for each sensitivity value is known, the Data Layer must compute the cutoff from `RESEARCH.md`, Section 8, and update every shot in a single transaction. Analysis must reject session data containing an unfinalized null adaptation flag.
- `cycle_id` is mandatory on `sensitivity_tests` and `phase_history`, preserving an explicit relationship between repeated Phase 1-3 runs and their owning continuous-improvement cycle.
- `tracking_data.sensitivity_value` is mandatory so Tracking results can be grouped and compared by the sensitivity actually tested.
- `PRAGMA foreign_keys = ON` must be executed and verified separately for every SQLite connection; setting it only during initial schema creation is insufficient.
- `injury_risk_flags` records are informational only and must never block or alter test execution; they exist purely to surface warnings to the user (see `RULES.md`).

---

## 5. Naming Convention Cross-Reference

Database columns use `snake_case` as shown above. C# class and method names in the Unity layer use `PascalCase`. Full conventions are defined in `SKILL.md`; this file only defines the data shape, not the code style.
