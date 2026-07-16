# P2-R1: Calculation and Validation Services

## Scope

P2-R1 implements engine-independent setup calculations and user-input validation. It does not create/update/delete profiles, persist active-profile state, render UI, display warnings, or implement scoring/Test Engine behavior.

## Calculation contract

`SensitivityCalculationService` consumes only the accepted immutable `ResearchConstants` contract and implements:

- eDPI = Hardware DPI x in-game sensitivity.
- PSA Starting Sensitivity = baseline eDPI / Hardware DPI.
- Physical Ruler estimated DPI = counts / (distance cm / cm-per-inch).
- eDPI floor adjustment with original/effective values, effective sensitivity, and an explicit `WasAdjusted` signal for later user notification.
- cm/360 using the accepted centimeters-per-inch, degrees-per-turn, and Valorant yaw multiplier.
- current-versus-PSA comparison in eDPI with below/equal/above relationship.
- non-blocking mousepad warning evaluation using the strict `cm/360 > mousepad width` rule.

All result contracts are immutable plain C# classes and have no Unity/`MonoBehaviour` dependency.

## Input validation

`SetupInputValidationService` accepts raw invariant-culture text and returns typed `ValidationResult<T>` values with stable error codes. Hardware DPI and Physical Ruler counts require positive integers. Current sensitivity, configured polling rate, mousepad width/height, and Physical Ruler distance require positive finite numbers. Missing, non-numeric, zero, negative, NaN, and infinity inputs are rejected before calculations run.

Duplicate profile names and active-profile deletion are intentionally not part of this stateless round; they belong to P2-R2/P2-R4 where repository state and active selection exist.

## Physical Ruler precision note

The documented Physical Ruler formula can produce a fractional estimated DPI, while `profiles.mouse_dpi` and `RULES.md` require a positive integer. No source defines a rounding/confirmation policy. P2-R1 therefore returns the exact unrounded estimate and does not silently convert it. OQ-020 records the required product decision before that estimate is persisted by the profile lifecycle.

## Verification

Unity EditMode/NUnit passed **47/47**, including **15** P2-R1 cases/fixtures for the exact 1600 -> 0.175 worked example, eDPI 280, Physical Ruler formula/no implicit rounding, floor/boundary behavior, cm/360, mousepad strict boundary, all comparison relationships, raw-input validation, typed-boundary rejection, and immutable plain-C# contracts.
