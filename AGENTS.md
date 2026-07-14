# AGENTS.md

## Purpose

This file is the entry point for any coding agent (Codex) working on the SensCalibr8 project. Read this file first, every session, before touching any code.

## Reading Order

1. `AGENTS.md` — this file (how to work)
2. `CONTEXT.md` — why this project exists, what it is, what it is not
3. `PROGRESS.md` — what has been done so far, what phase is active
4. `RESEARCH.md` + `ARCHITECTURE.md` + `FEATURES.md` + `DESIGN.md` + `SKILL.md` + `RULES.md` — read only the sections relevant to the task at hand

Do not skip step 3. Never assume the project state from memory alone — always re-check `PROGRESS.md` at the start of a session, since it may have been updated in a previous session.

## Phase Planning

There is no fixed phase plan provided for this project. You are responsible for analyzing `FEATURES.md` and `ARCHITECTURE.md` in full, then designing your own development phase breakdown (e.g. Database setup, Test Engine, Multi-Profile System, Analysis Layer, Integration). Write this phase plan into `PROGRESS.md` at the start of the first working session, and update it as understanding evolves.

## Working Protocol

- Before writing any code, confirm which phase is currently active by reading `PROGRESS.md`.
- Never hardcode a numeric constant, threshold, or formula weight without first checking `RESEARCH.md`. If a number is not present in `RESEARCH.md`, do not invent it — flag it as a question in `PROGRESS.md` instead.
- Follow the coding standards in `SKILL.md` and the scientific/behavioral constraints in `RULES.md` without exception. These are not suggestions.
- Work in small, testable increments. Do not attempt to implement multiple phases in a single pass.
- Commit your work with git at logical checkpoints (e.g. after a phase or sub-feature is completed and verified).

## Definition of Done

A task is only considered complete when all of the following are true:

1. Any formula or calculation implemented has been tested against a worked example from `RESEARCH.md` and produces the exact expected output (e.g. DPI = 1600 must yield Starting Sensitivity = 0.175 exactly).
2. All applicable Input Validation Rules from `RULES.md` have been tested, including edge cases (invalid DPI, eDPI below floor, duplicate profile names, deletion of an active profile).
3. The outcome, test results, and any issues encountered have been recorded in `PROGRESS.md` before the task is marked complete.

If any of these three conditions is not met, the task is still in progress, regardless of whether the code appears to run.

## Error Handling Expectation

If a requirement in `FEATURES.md` or `RESEARCH.md` is ambiguous or contradicts another file, stop and record the conflict in `PROGRESS.md` rather than guessing a resolution. Do not silently deviate from documented specifications.
