using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SensCalibr8.Calibration
{
    public sealed class P0R3StandaloneCaptureApp : MonoBehaviour
    {
        private const int MenuWidth = 960;
        private const int MenuHeight = 540;
        private const int ApprovedRepeatCount = 5;
        private const double ApprovedDurationSeconds = 30.0;
        private const string ApprovedMotion =
            "Continuous figure-eight motion at a comfortable steady pace without lifting the mouse.";

        private enum AppState
        {
            Preflight,
            FullscreenReady,
            Capturing,
            Complete
        }

        private readonly Stopwatch captureDisplayClock = new Stopwatch();
        private CalibrationHarnessController controller;
        private AppState state;
        private int repetitionOrdinal = 1;
        private int selectedMouseDeviceId = InputDevice.InvalidDeviceId;
        private bool adaptiveSyncConfirmedOff;
        private bool mousePowerConfirmedStable;
        private bool backgroundLoadConfirmedControlled;
        private bool thermalPowerConfirmedStable;
        private bool offlineConfirmed = true;
        private string statusMessage = "Complete the preflight, then start Pilot run 1.";
        private string evidenceState;
        private double activeRefreshRateHz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new GameObject("SensCalibr8 P0-R3 Standalone Capture");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<P0R3StandaloneCaptureApp>();
        }

        private void Awake()
        {
            evidenceState = ResolveEvidenceState(Environment.GetCommandLineArgs());
            InputSystem.settings.disableRedundantEventsMerging = true;
            controller = gameObject.AddComponent<CalibrationHarnessController>();
            controller.CaptureFinalized += OnCaptureFinalized;

            Application.runInBackground = false;
            QualitySettings.vSyncCount = 0;
            activeRefreshRateHz = Screen.currentResolution.refreshRateRatio.value;
            if (activeRefreshRateHz > 0)
            {
                Application.targetFrameRate = (int)Math.Round(activeRefreshRateHz);
            }

            ReturnToWindowed();
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.CaptureFinalized -= OnCaptureFinalized;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && controller != null && controller.IsCapturing)
            {
                controller.InterruptCapture("application-focus-lost");
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f11Key.wasPressedThisFrame)
            {
                if (controller.IsCapturing)
                {
                    controller.InterruptCapture("fullscreen-toggle-during-capture");
                }
                else
                {
                    ToggleFullscreen();
                }
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (controller.IsCapturing)
                {
                    controller.InterruptCapture("operator-escape");
                }
                else if (Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    ReturnToWindowed();
                    state = AppState.Preflight;
                }
                else
                {
                    Application.Quit();
                }
            }
        }

        private void OnGUI()
        {
            GUI.skin.label.wordWrap = true;
            GUILayout.BeginArea(new Rect(24, 18, Screen.width - 48, Screen.height - 36));
            GUILayout.Label("SensCalibr8 — P0-R3 Input and Timing Calibration");
            GUILayout.Label("Standalone offline evidence capture • " + evidenceState.ToUpperInvariant());
            GUILayout.Space(12);

            if (state == AppState.Capturing)
            {
                DrawCaptureScreen();
            }
            else if (state == AppState.FullscreenReady)
            {
                DrawFullscreenReadyScreen();
            }
            else if (state == AppState.Complete)
            {
                DrawCompleteScreen();
            }
            else
            {
                DrawPreflightScreen();
            }

            GUILayout.EndArea();
        }

        private void DrawPreflightScreen()
        {
            GUILayout.Label(statusMessage);
            GUILayout.Space(8);
            GUILayout.Label("Approved plan: 5 independent runs × 30 seconds");
            GUILayout.Label("Motion: " + ApprovedMotion);
            GUILayout.Label("DPI: 1600 • configured polling: 1000 Hz");
            GUILayout.Label("Connection evidence: user reports Bluetooth; Windows also exposes USB HID VID_1D57:PID_FA60.");
            GUILayout.Label("Detected display: " + Screen.currentResolution.width + " × " +
                Screen.currentResolution.height + " @ " +
                activeRefreshRateHz.ToString("0.###", CultureInfo.InvariantCulture) + " Hz");
            GUILayout.Space(10);

            Mouse current = Mouse.current;
            if (current == null)
            {
                GUILayout.Label("No active mouse detected. Connect and move the SIGNO mouse.");
            }
            else
            {
                GUILayout.Label("Current mouse: " + DescribeMouse(current));
                if (GUILayout.Button("Use this currently active mouse for the trace"))
                {
                    selectedMouseDeviceId = current.deviceId;
                    statusMessage = "Selected mouse device " + current.deviceId + ".";
                }
            }

            if (selectedMouseDeviceId != InputDevice.InvalidDeviceId)
            {
                Mouse selected = InputSystem.GetDeviceById(selectedMouseDeviceId) as Mouse;
                GUILayout.Label("Selected device: " + (selected == null ? "disconnected" : DescribeMouse(selected)));
            }

            GUILayout.Space(10);
            adaptiveSyncConfirmedOff = GUILayout.Toggle(
                adaptiveSyncConfirmedOff,
                "I confirmed G-Sync / FreeSync / adaptive sync is OFF for this capture.");
            mousePowerConfirmedStable = GUILayout.Toggle(
                mousePowerConfirmedStable,
                "The wireless mouse has stable power for all five runs.");
            backgroundLoadConfirmedControlled = GUILayout.Toggle(
                backgroundLoadConfirmedControlled,
                "No material download, recording, overlay, or heavy background workload is active.");
            thermalPowerConfirmedStable = GUILayout.Toggle(
                thermalPowerConfirmedStable,
                "Laptop is plugged in on High performance with no known thermal/power anomaly.");
            offlineConfirmed = GUILayout.Toggle(
                offlineConfirmed,
                "The capture is offline; no network service is required.");

            GUILayout.Space(12);
            GUI.enabled = IsPreflightComplete();
            if (GUILayout.Button("Enter native borderless fullscreen for run " + repetitionOrdinal))
            {
                Mouse clickedMouse = Mouse.current;
                if (clickedMouse != null)
                {
                    selectedMouseDeviceId = clickedMouse.deviceId;
                }
                EnterFullscreen();
                state = AppState.FullscreenReady;
            }
            GUI.enabled = true;
            GUILayout.Space(8);
            GUILayout.Label("F11 toggles fullscreen outside capture. Escape interrupts capture or exits fullscreen.");
            GUILayout.Label("Evidence folder: " + controller.ArtifactRoot);
        }

        private void DrawFullscreenReadyScreen()
        {
            bool nativeFullscreenReady = IsNativeFullscreenReady();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Run " + repetitionOrdinal + " of " + ApprovedRepeatCount + " — ready");
            GUILayout.Label(ApprovedMotion);
            GUILayout.Label("The selected mouse is locked when capture starts. Escape retains an interrupted run and returns to the menu.");
            GUILayout.Label(nativeFullscreenReady
                ? "Native borderless fullscreen is active."
                : "Waiting for native borderless fullscreen before capture can begin...");
            GUILayout.Space(16);
            GUI.enabled = nativeFullscreenReady;
            if (GUILayout.Button("Begin 30-second capture"))
            {
                StartApprovedCapture();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Return without capturing"))
            {
                ReturnToWindowed();
                state = AppState.Preflight;
            }
            GUILayout.FlexibleSpace();
        }

        private void DrawCaptureScreen()
        {
            GUILayout.FlexibleSpace();
            double remaining = Math.Max(0.0, ApprovedDurationSeconds - captureDisplayClock.Elapsed.TotalSeconds);
            GUILayout.Label("CAPTURING — run " + repetitionOrdinal + " of " + ApprovedRepeatCount);
            GUILayout.Label("Remaining: " + remaining.ToString("0.0", CultureInfo.InvariantCulture) + " seconds");
            GUILayout.Label(ApprovedMotion);
            GUILayout.Label("Do not lift the mouse, change display mode, or switch applications.");
            GUILayout.FlexibleSpace();
        }

        private void DrawCompleteScreen()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(evidenceState.ToUpperInvariant() + " capture set complete: 5 valid runs recorded.");
            GUILayout.Label("Exit the app. Pilot evidence must be analyzed and frozen before fresh confirmation.");
            GUILayout.Label("Evidence folder: " + controller.ArtifactRoot);
            GUILayout.Space(12);
            if (GUILayout.Button("Exit"))
            {
                Application.Quit();
            }
            GUILayout.FlexibleSpace();
        }

        private void StartApprovedCapture()
        {
            Mouse selected = InputSystem.GetDeviceById(selectedMouseDeviceId) as Mouse;
            if (selected == null)
            {
                statusMessage = "Selected mouse disconnected; select it again.";
                ReturnToWindowed();
                state = AppState.Preflight;
                return;
            }

            try
            {
                controller.Configure(CreateConfiguration(selected));
                controller.StartCapture();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                captureDisplayClock.Restart();
                state = AppState.Capturing;
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogException(error);
                captureDisplayClock.Stop();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                statusMessage = "Capture did not start: " + error.GetType().Name +
                    ". See Player.log; no valid run was recorded.";
                ReturnToWindowed();
                state = AppState.Preflight;
            }
        }

        private P0R3CaptureConfiguration CreateConfiguration(Mouse selected)
        {
            string planId = evidenceState == "confirmation"
                ? "sc8-p0-r3-input-timing-confirmation-v2"
                : "sc8-p0-r3-input-timing-pilot-v2";
            string controlledVariables = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"mouse_dpi\":1600,\"configured_polling_rate_hz\":1000," +
                "\"selected_device_id\":{0},\"selected_device_interface\":\"{1}\"," +
                "\"selected_device_product\":\"{2}\",\"display_width\":{3}," +
                "\"display_height\":{4},\"display_refresh_rate_hz\":{5}," +
                "\"display_mode\":\"FullScreenWindow\",\"vsync\":\"off\"," +
                "\"mouse_event_merging\":\"disabled\"," +
                "\"adaptive_sync\":\"operator-confirmed-off\",\"network\":\"offline\"}}",
                selected.deviceId,
                EscapeJson(ValueOrUnknown(selected.description.interfaceName)),
                EscapeJson(ValueOrUnknown(selected.description.product)),
                Screen.currentResolution.width,
                Screen.currentResolution.height,
                activeRefreshRateHz);

            return new P0R3CaptureConfiguration
            {
                ProtocolId = "sc8-p0-r1-protocol-v1",
                EnvironmentId = "sc8-env-zulartan-wg903-v1",
                CapturePlanId = planId,
                ConditionId = "wg903-1600dpi-1000hz-user-bluetooth",
                PlannedRepeatCount = ApprovedRepeatCount,
                RepetitionOrdinal = repetitionOrdinal,
                TraceDurationSeconds = ApprovedDurationSeconds,
                EvidenceState = evidenceState,
                AcceptanceOwner = "project-owner",
                ExecutionOrder = "sequential-repetitions-1-through-5-single-condition",
                ControlledMotionInstruction = ApprovedMotion,
                ControlledVariablesJson = controlledVariables,
                TargetMouseDeviceId = selected.deviceId,
                ManualEnvironment = CreateManualEnvironment()
            };
        }

        private CalibrationManualEnvironment CreateManualEnvironment()
        {
            return new CalibrationManualEnvironment
            {
                DisplayModel = "NCP004D",
                NativeResolution = "1920x1080",
                DisplayRefreshRate = activeRefreshRateHz.ToString("0.###", CultureInfo.InvariantCulture) + " Hz",
                DisplayScaling = "125% host observation",
                VSyncState = "off-by-application",
                AdaptiveSyncState = "operator-confirmed-off",
                MouseManufacturer = "SIGNO",
                MouseModel = "WG-903",
                MouseConnection = "user-reported Bluetooth; host-observed USB HID VID_1D57 PID_FA60",
                MouseFirmware = CalibrationHarnessMetadata.UnknownValue,
                MouseDpi = "1600",
                MouseDpiEvidenceSource = "SIGNO software screenshot and project-owner confirmation 2026-07-15",
                ConfiguredPollingRate = "1000 Hz",
                PollingRateEvidenceSource = "SIGNO software and project-owner confirmation 2026-07-15; measured cadence authoritative",
                UsbPathOrHub = "host-observed direct USB HID composite Port_#0004.Hub_#0001",
                MousePowerState = "operator-confirmed-stable",
                PointerSpeed = "Windows registry MouseSensitivity=10",
                PointerAccelerationState = "audit-only registry MouseSpeed=1 Threshold1=6 Threshold2=10",
                MousepadDescription = "not-required-for-p0-r3-input-timing",
                OperatorId = "local-primary-operator",
                DominantHand = "not-required-for-p0-r3-input-timing",
                GripDescriptor = "not-required-for-p0-r3-input-timing",
                MovementDescriptor = "controlled-figure-eight",
                PostureNotes = "audit-only-not-supplied",
                WarmupProcedure = "operator-ready-confirmation",
                PowerPlan = "High performance; operator-confirmed-AC-power",
                BackgroundLoadPolicy = "operator-confirmed-no-material-background-load",
                ThermalPowerNotes = "operator-confirmed-no-known-anomaly",
                NetworkOfflineState = "offline"
            };
        }

        private bool IsPreflightComplete()
        {
            return selectedMouseDeviceId != InputDevice.InvalidDeviceId &&
                InputSystem.GetDeviceById(selectedMouseDeviceId) is Mouse &&
                adaptiveSyncConfirmedOff &&
                mousePowerConfirmedStable &&
                backgroundLoadConfirmedControlled &&
                thermalPowerConfirmedStable &&
                offlineConfirmed;
        }

        private static bool IsNativeFullscreenReady()
        {
            Resolution native = Screen.currentResolution;
            return Screen.fullScreenMode == FullScreenMode.FullScreenWindow &&
                Screen.width == native.width &&
                Screen.height == native.height;
        }

        private void OnCaptureFinalized(CalibrationCaptureFinalizedEvent result)
        {
            captureDisplayClock.Stop();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ReturnToWindowed();

            if (result.Status == "completed")
            {
                statusMessage = "Completed run " + repetitionOrdinal + ": " + result.RunId;
                repetitionOrdinal++;
                state = repetitionOrdinal > ApprovedRepeatCount ? AppState.Complete : AppState.Preflight;
            }
            else
            {
                statusMessage = "Run " + repetitionOrdinal + " retained as interrupted (" + result.Reason + "). Repeat the same ordinal.";
                state = AppState.Preflight;
            }
        }

        private void EnterFullscreen()
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        private void ReturnToWindowed()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Screen.SetResolution(MenuWidth, MenuHeight, FullScreenMode.Windowed);
        }

        private void ToggleFullscreen()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                EnterFullscreen();
            }
            else
            {
                ReturnToWindowed();
                state = AppState.Preflight;
            }
        }

        private static string DescribeMouse(Mouse mouse)
        {
            return "ID " + mouse.deviceId + " • " +
                ValueOrUnknown(mouse.description.product) + " • " +
                ValueOrUnknown(mouse.description.interfaceName);
        }

        private static string ResolveEvidenceState(string[] arguments)
        {
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "--p0r3-stage=confirmation", StringComparison.OrdinalIgnoreCase))
                {
                    return "confirmation";
                }
            }
            return "pilot";
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? CalibrationHarnessMetadata.UnknownValue : value.Trim();
        }
    }
}
