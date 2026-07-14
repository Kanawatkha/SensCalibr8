# CONTEXT.md

## What This Project Is

SensCalibr8 is a standalone, single-user desktop application built to help one player (and optionally their friends, via isolated profiles) find their optimal mouse sensitivity for Valorant through a structured, data-driven testing process. It replaces guesswork and copying professional players' settings with a repeatable measurement protocol grounded in a consolidated research report and externally verified sources.

The application is built on findings synthesized from four professional Valorant players' setups (TenZ, CHARLATAN, Rem, Konpeki), combined with Fitts's Law (a movement-time model from human motor control research) and statistical rigor techniques borrowed from behavioral experiment design.

## Why This Exists

Most players choose a mouse sensitivity by copying a pro player's exact settings or by trial and error with no measurement. Neither approach accounts for individual differences in hardware, grip, physical setup, or skill level. SensCalibr8 exists to close that gap by running the user through a series of aim tests, measuring performance objectively, and converging on a personally optimal sensitivity value through progressive narrowing.

## Design Philosophy

- **Sensitivity-only scope.** The application measures and helps tune exactly one variable: mouse sensitivity. It does not touch, read, or modify any other in-game setting.
- **Fully offline and standalone.** The test engine runs in its own self-contained environment. It never interacts with the Valorant game client or its anti-cheat system (Vanguard). This is a deliberate safety decision, not a technical limitation.
- **Evidence-based.** Every formula, threshold, and benchmark used anywhere in this project must be traceable to either the consolidated research report or an externally verifiable source. No formula is chosen arbitrarily.
- **Multi-profile by design.** Multiple users can test on the same machine, each with fully isolated data (own DPI, own history, own results). No profile can read or affect another's data.
- **Scope discipline.** Feature requests that fall outside sensitivity measurement are rejected by default unless a request explicitly updates this document.

## Out of Scope

The following are permanently excluded from this project. Do not implement these even if they seem like natural extensions:

- Automatic modification of any in-game setting. This is blocked intentionally due to anti-cheat risk (Vanguard).
- Graphics, keybinds, audio, or minimap configuration. These are addressed in the original research report, not in this application.
- Automatic DPI detection from mouse firmware. This is technically infeasible without brand-specific SDKs (see DPI Acquisition Method below). DPI must always be entered manually or derived from the physical ruler test fallback.
- A dedicated Scoped/ADS (Aim-Down-Sight) test mode. ADS sensitivity multiplier is a setting the user can already configure directly in Valorant; adding a dedicated test mode for it is unnecessary scope creep. The value may be stored as a reference field only (see `ARCHITECTURE.md`), never tested separately.
- Role-based weighting of the Performance Score (e.g. adjusting weights based on whether the user plays Duelist, Controller, or Sentinel). No research basis exists for role-specific weight values; this would add complexity without evidentiary support.
- Any causal claim that a specific grip style or movement strategy (wrist-dominant vs. arm-dominant) determines a "correct" optimal eDPI. These fields are stored as descriptive metadata only (see `ARCHITECTURE.md`) and must never be used to bias or pre-adjust calculated results.

## DPI Acquisition Method

DPI values are stored in mouse firmware and cannot be read directly through OS-level APIs. The user must check their current DPI using their mouse manufacturer's software (e.g. SIGNO, Logitech G HUB, Razer Synapse) and manually enter it during setup.

A fallback method, the Physical Ruler Test, is available when the user does not know their DPI. It calculates DPI from a measured physical distance against the number of counts detected by the system:

```
DPI (Physical Measurement Fallback) =
    mouse_movement_counts / (measured_distance_cm / 2.54)
```

## Target User

A single primary user (and optionally friends testing on the same machine) who wants an evidence-based, repeatable process to find their optimal Valorant mouse sensitivity, without relying on pro player settings or subjective feel alone.
