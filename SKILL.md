# SKILL.md

## Purpose

This file defines the coding standards that apply to every part of SensCalibr8: the Unity C# test engine, the SQLite schema interactions, and the Python analysis layer. These standards exist to keep the codebase maintainable at an enterprise-grade level, even though this is a personal single-user project. Follow them without exception.

---

## 1. No Hardcoding

Every numeric constant, threshold, or formula weight defined in `RESEARCH.md` (280, 160, 0.35, 0.30, 0.20, 0.15, 0.10, the reaction time tier boundaries, the 35% headshot ceiling, the 50% adaptation discard proportion, etc.) must live in a single, centralized configuration location — a Constants class in C#, and an equivalent config/constants module in Python. Never embed these values directly inside logic code as magic numbers. If a value needs to change, it should be changeable in exactly one place.

## 2. Separation of Concerns (SoC)

Keep the following layers strictly separate and never let one layer perform another layer's job:

- **Test Logic Layer** — runs the actual test mode behavior (target spawning, input capture, timing).
- **Data Layer** — handles all SQLite reads/writes. No other layer should execute raw SQL directly.
- **UI Layer** — renders screens and forwards user input. The UI layer must never compute a Performance Score, eDPI, or any other derived value itself — it must call into a dedicated Service Layer for all calculations.
- **Analysis Layer** (Python) — performs statistical computation and chart generation, reading only from the Data Layer's exported/queried data, never duplicating calculation logic that already exists in the Test Logic or Service Layer.

## 3. Dynamic Over Static Design

The four Test Modes must be implemented through a shared interface or abstract base (e.g. `ITestMode` in C#), not as four independent, unrelated classes. Each mode implements the same contract (start, capture shot, end, report result), so that adding a future fifth mode requires implementing one new class against an existing interface, not rewriting shared plumbing.

## 4. Naming Convention

- C# classes, methods, and public members: `PascalCase`.
- C# local variables and private fields: `camelCase`.
- SQLite database columns and table names: `snake_case`, exactly as shown in `ARCHITECTURE.md`. Do not rename columns to match C# convention when writing queries — keep the schema names as the single source of truth.
- Python variables and functions: `snake_case`, consistent with the database layer.

## 5. Testability

All business logic that performs a calculation (Performance Score, eDPI, cm/360, Submovement Count, Adaptation Period filtering, Grade assignment) must be implemented in plain C# classes that do not inherit from `MonoBehaviour`. This allows these calculations to be unit-tested directly, without needing to run the Unity game loop. `MonoBehaviour` classes should only orchestrate calls to these testable classes — they must not contain calculation logic themselves.

## 6. Error Handling

Every SQLite read or write operation must be wrapped in appropriate error handling (try-catch in C#, try-except in Python). A database error must never crash the application outright — it must be caught, logged, and surfaced to the user in a way that does not lose in-progress session data where possible. Never assume a query will succeed.
