# Phase 0 Environment Inventory

## Inventory Control

| Field | Value |
|---|---|
| Inventory state | A-H approved; standalone build verified; operator pilot preflight pending |
| Environment ID | `sc8-env-zulartan-wg903-v1` |
| Protocol ID | `sc8-p0-r1-protocol-v1` |
| Automatic capture time | 2026-07-15T08:28:51+07:00 |
| Timezone | SE Asia Standard Time |
| Purpose | Frozen-host preflight for P0-R3 input/timing capture |

`unknown` means the value has not been verified. It is explicit missing evidence, not an assumed default. Automatically observed values below were refreshed on 2026-07-15; operator-only settings must still be confirmed immediately before capture.

## Automatically Observed Baseline

| Area | Field | Observed value | Evidence / limitation |
|---|---|---|---|
| Computer | Local identifier | `ZULARTAN` | Environment variable; local audit identifier only |
| Operating system | Platform | `Win32NT` | .NET environment observation |
| Operating system | Version | `10.0.26200.0` | Marketing edition/build name not inferred |
| Operating system | Architecture | 64-bit | .NET environment observation |
| Computer | Manufacturer/model | Acer `Nitro AN515-57` | Windows system inventory |
| Runtime capacity | Processor | Intel Core i5-11400H, 6 cores / 12 logical processors | Windows processor inventory |
| Runtime capacity | Installed memory | Approximately 16 GB | Windows system inventory |
| Display | Active panel identity | PnP ID `DISPLAY\\NCP004D`; manufacturer/model name not exposed | Windows display inventory; operator may supply a friendlier model name |
| Display | Active/native mode | 1920 x 1080 at 144 Hz | Windows display-controller inventory |
| Display | Application-visible bounds | 1536 x 864 | Reflects 125% Windows scaling relative to 1920 x 1080 |
| Graphics | Discrete adapter | NVIDIA GeForce RTX 3050 Laptop GPU, driver `32.0.15.9174` | Windows video-controller inventory |
| Graphics | Integrated adapter / active display path | Intel UHD Graphics, driver `30.0.100.9864` | Windows video-controller inventory |
| Mouse | PnP identity | HID `VID_1D57&PID_FA60`; generic HID-compliant mouse name | Windows PnP inventory; manufacturer/model/firmware are not exposed |
| Mouse | Connection path | Direct USB composite device, `Port_#0004.Hub_#0001`, parent USB root hub | Windows PnP relationship inventory |
| Pointer | Registry state | Sensitivity `10`, MouseSpeed `1`, Threshold1 `6`, Threshold2 `10` | Registry evidence; operator must translate/confirm acceleration UI state |
| Power | Active plan | High performance | Windows power-plan inventory |
| .NET | SDK version | `10.0.100` | `dotnet --version` |
| Unity | Editor | `6000.5.3f1` at `C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe` | Installed Editor used to verify and build P0-R3 |
| Unity | Input System / update mode | `1.19.0`; project active input handler is Input System | Package manifest, package lock, and ProjectSettings |
| Unity | Acceptance runtime/build type | Windows 64-bit standalone player | Build manifest dated 2026-07-15; Unity Editor remains test/build tooling only |
| Unity | Editor executable SHA-256 | `2b231b364bc8a506db821377fe16030552c4f504393947b89710c9188dc7362b` | SHA-256 measured 2026-07-15 |
| Harness | P0-R3 standalone build | `p0-r3-harness-v4`; build `sc8-p0-r3-windows-standalone-v3`; executable SHA-256 `5ae0c84993c6bff7d0f03166cafd148cdec61f67ec85b5d97600e04b2abb26e4`; runtime assembly SHA-256 `900d1e46578a0e5f60055b19821912a670999cddd3ecafe6278803b7678d6add` | Build manifest; v4 retains the v3 path fix and disables Unity redundant mouse-event merging before capture |
| Native timing helper | P0-R3 timestamp-correction build | `p0-r3-native-harness-v1`; build `sc8-p0-r3-native-wm-input-v1`; executable SHA-256 `c9503babac13ac968e985842e332609c3d10b447751134b648bff554d3c6b60d`; runtime assembly SHA-256 `c86f84d8890dc040afeaa53d1b57a23557ff8ea16d5fc9ffcec9ecbf5f508133` | Dedicated Win32 `WM_INPUT` message pump with QPC timestamps; published self-test passed |
| Mouse | User-confirmed identity/settings | SIGNO WG-903, DPI 1600, configured polling rate 1000 Hz, user reports Bluetooth | SIGNO software screenshot plus project-owner confirmation dated 2026-07-15 |
| Mouse | Connection discrepancy | User reports Bluetooth; Windows exposes `VID_1D57&PID_FA60` as USB HID composite | Preserve both observations; measured event cadence and captured Input System identity are authoritative |
| Python analysis | Runtime | CPython `3.12.13`, NumPy `2.3.5` | Bundled workspace runtime used by P0-R3 analysis |

## Mandatory Manual Confirmation Before P0-R3

| Area | Required field | Current state | Evidence to attach |
|---|---|---|---|
| Unity | Editor/Player version and installation path | Verified Editor/Player `6000.5.3f1`; Windows standalone build complete | P0-R3 build manifest |
| Unity | Input System package and update mode | Verified `1.19.0`, Input System active | Project package manifest, package lock, and ProjectSettings |
| Build | Harness version, build type, executable/runtime checksum | `p0-r3-harness-v4`; Windows 64-bit standalone; executable SHA-256 `5ae0c84993c6bff7d0f03166cafd148cdec61f67ec85b5d97600e04b2abb26e4`; runtime SHA-256 `900d1e46578a0e5f60055b19821912a670999cddd3ecafe6278803b7678d6add` | Build manifest; environment manifest must record `RedundantEventMergingDisabled=true` |
| Display | Manufacturer/model and native resolution | PnP `NCP004D`, 1920 x 1080; friendly model name `unknown` | Operator/display manufacturer record |
| Display | Active refresh rate and scaling | Observed 144 Hz and 125%; immediate pre-run confirmation required | Operating-system display settings capture |
| Display | VSync/adaptive-sync and display mode | Policy frozen: application VSync off, operator confirms adaptive sync off, native borderless fullscreen during capture | Standalone preflight and frame trace |
| GPU | Model and driver version | Verified NVIDIA/Intel identities above; immediate pre-run confirmation required | Driver/system record |
| Mouse | Manufacturer/model and connection type | SIGNO WG-903; connection observations recorded above | Model/firmware are optional audit data and do not block |
| Mouse | Firmware version | Not supplied | Optional audit/troubleshooting metadata; does not block |
| Mouse | DPI and source | 1600, SIGNO software screenshot/user confirmation | Required operational evidence: complete |
| Mouse | Configured polling rate and source | 1000 Hz, SIGNO software/user confirmation | Supporting evidence only; measured cadence remains authoritative |
| Mouse | USB path/hub and power state | Direct USB HID path observed; stable-power confirmation required in standalone preflight | Operator checkbox plus captured device identity |
| Operating system | Pointer speed and acceleration state | Registry values observed; audit only because raw input path is used | Does not block timing capture |
| Physical setup | Mousepad, grip, hand, posture, warm-up | Not required by P0-R3 input timing | Optional audit metadata; never scoring input |
| Operating conditions | Power plan and relevant background-load policy | High performance observed; operator confirms no material background load/thermal anomaly in standalone preflight | Captured with each run |

## Readiness Decision

- P0-R1 protocol design: **Accepted**.
- P0-R2 harness implementation: **Ready**, with Unity location/version selection as its first environment preflight item.
- P0-R3 production-quality trace capture: **Standalone build gate passed; awaiting the operator-confirmed preflight and five 30-second pilot traces.** Audit-only mouse firmware/physical descriptors do not block.
- Production Test Engine and scoring: **Blocked** until P0-R7 freezes the complete calibration configuration.

No automatic assumption may replace an `unknown` value in this inventory.
