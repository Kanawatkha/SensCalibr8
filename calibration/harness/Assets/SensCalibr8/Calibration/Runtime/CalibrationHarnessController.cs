using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SensCalibr8.Calibration
{
    public sealed class CalibrationHarnessController : MonoBehaviour
    {
        [Header("Immutable run identity")]
        [SerializeField] private string protocolId = "sc8-p0-r1-protocol-v1";
        [SerializeField] private string environmentId;
        [SerializeField] private string capturePlanId;
        [SerializeField] private string conditionId;
        [SerializeField] private int plannedRepeatCount;
        [SerializeField] private int repetitionOrdinal;
        [SerializeField] private double traceDurationSeconds;
        [SerializeField] private int targetMouseDeviceId = InputDevice.InvalidDeviceId;

        [Header("Predeclared capture plan")]
        [SerializeField] private string evidenceState;
        [SerializeField] private string acceptanceOwner;
        [SerializeField] private string executionOrder;
        [SerializeField] private string controlledMotionInstruction;
        [SerializeField] private string controlledVariablesJson;

        [Header("Manual environment evidence")]
        [SerializeField] private CalibrationManualEnvironment manualEnvironment = new CalibrationManualEnvironment();

        private readonly CalibrationClock clock = new CalibrationClock();
        private CalibrationRunStore runStore;
        private string runId;
        private string traceId;
        private long mouseSequence;
        private long frameSequence;
        private long targetSequence;
        private bool subscribed;
        private long captureStartTicks;

        public event Action<CalibrationCaptureFinalizedEvent> CaptureFinalized;

        public bool IsCapturing
        {
            get { return runStore != null; }
        }

        public string CurrentRunId
        {
            get { return runId; }
        }

        public string ArtifactRoot
        {
            get
            {
                return CalibrationArtifactLocation.CaptureRoot;
            }
        }

        public void Configure(P0R3CaptureConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            if (IsCapturing)
            {
                throw new InvalidOperationException("Cannot reconfigure an active capture.");
            }

            protocolId = configuration.ProtocolId;
            environmentId = configuration.EnvironmentId;
            capturePlanId = configuration.CapturePlanId;
            conditionId = configuration.ConditionId;
            plannedRepeatCount = configuration.PlannedRepeatCount;
            repetitionOrdinal = configuration.RepetitionOrdinal;
            traceDurationSeconds = configuration.TraceDurationSeconds;
            evidenceState = configuration.EvidenceState;
            acceptanceOwner = configuration.AcceptanceOwner;
            executionOrder = configuration.ExecutionOrder;
            controlledMotionInstruction = configuration.ControlledMotionInstruction;
            controlledVariablesJson = configuration.ControlledVariablesJson;
            targetMouseDeviceId = configuration.TargetMouseDeviceId;
            manualEnvironment = configuration.ManualEnvironment;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (IsCapturing)
            {
                InterruptCapture("controller-disabled");
            }
        }

        private void OnApplicationQuit()
        {
            if (IsCapturing)
            {
                InterruptCapture("application-quit");
            }
        }

        private void Update()
        {
            if (!IsCapturing)
            {
                return;
            }

            CalibrationTimestamp timestamp = clock.Capture();
            runStore.RecordFrameTiming(new FrameTimingRecord
            {
                RunId = runId,
                EnvironmentId = CalibrationIdFactory.NormalizeToken(environmentId),
                Sequence = frameSequence++,
                MonotonicTimestampTicks = timestamp.Ticks,
                MonotonicTimestampSeconds = timestamp.Seconds,
                UnityFrameIndex = Time.frameCount,
                UnscaledDeltaTimeSeconds = Time.unscaledDeltaTime,
                ApplicationFocused = Application.isFocused,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                RefreshRateHz = Screen.currentResolution.refreshRateRatio.value,
                FullScreenMode = Screen.fullScreenMode.ToString()
            });

            if ((timestamp.Ticks - captureStartTicks) / (double)clock.Frequency >= traceDurationSeconds)
            {
                CompleteCapture();
            }
        }

        public void StartCapture()
        {
            if (IsCapturing)
            {
                throw new InvalidOperationException("A calibration run is already active.");
            }

            ValidateCapturePlan();
            CalibrationRecoveryService.RecoverAbandonedRuns(ArtifactRoot);

            DateTime startedUtc = DateTime.UtcNow;
            runId = CalibrationIdFactory.CreateRunId(startedUtc, repetitionOrdinal);
            traceId = runId + "-mouse";
            CalibrationEnvironmentManifest environment = CalibrationEnvironmentFactory.Capture(
                protocolId,
                environmentId,
                manualEnvironment,
                targetMouseDeviceId);
            CalibrationCapturePlanManifest capturePlan = new CalibrationCapturePlanManifest
            {
                ProtocolId = CalibrationIdFactory.NormalizeToken(protocolId),
                CapturePlanId = CalibrationIdFactory.NormalizeToken(capturePlanId),
                EnvironmentId = CalibrationIdFactory.NormalizeToken(environmentId),
                ConditionId = CalibrationIdFactory.NormalizeToken(conditionId),
                PlannedRepeatCount = plannedRepeatCount,
                RepetitionOrdinal = repetitionOrdinal,
                ExecutionOrder = ValueOrUnknown(executionOrder),
                ControlledVariablesJson = ValueOrUnknown(controlledVariablesJson),
                EvidenceState = evidenceState,
                AcceptanceOwner = acceptanceOwner.Trim(),
                ControlledMotionInstruction = controlledMotionInstruction.Trim(),
                TraceDurationSeconds = traceDurationSeconds,
                CreatedUtc = startedUtc.ToString("o")
            };
            CalibrationRunManifest runManifest = new CalibrationRunManifest
            {
                ProtocolId = capturePlan.ProtocolId,
                EnvironmentId = capturePlan.EnvironmentId,
                CapturePlanId = capturePlan.CapturePlanId,
                ConditionId = capturePlan.ConditionId,
                RunId = runId,
                TraceId = traceId,
                HarnessVersion = CalibrationHarnessMetadata.HarnessVersion,
                HarnessChecksum = environment.HarnessChecksum,
                Status = "started",
                Reason = CalibrationHarnessMetadata.UnknownValue,
                StartedUtc = startedUtc.ToString("o"),
                EndedUtc = CalibrationHarnessMetadata.UnknownValue,
                StopwatchFrequency = clock.Frequency
            };

            runStore = new CalibrationRunStore(ArtifactRoot, environment, capturePlan, runManifest);
            mouseSequence = 0;
            frameSequence = 0;
            targetSequence = 0;
            captureStartTicks = clock.Capture().Ticks;
        }

        public void CompleteCapture()
        {
            EnsureCapturing();
            string completedRunId = runId;
            CalibrationRunStore store = runStore;
            runStore = null;
            store.Complete("completed", "operator-completed");
            NotifyFinalized(completedRunId, "completed", "operator-completed");
        }

        public void InterruptCapture(string reason)
        {
            EnsureCapturing();
            string interruptedRunId = runId;
            string normalizedReason = ValueOrUnknown(reason);
            CalibrationRunStore store = runStore;
            runStore = null;
            store.Interrupt(normalizedReason);
            NotifyFinalized(interruptedRunId, "interrupted", normalizedReason);
        }

        public void RecordTargetCameraEvent(
            string eventType,
            string targetId,
            Vector3 targetPosition,
            float cameraAzimuthDegrees,
            float cameraElevationDegrees)
        {
            EnsureCapturing();
            CalibrationTimestamp timestamp = clock.Capture();
            runStore.RecordTargetCameraEvent(new TargetCameraEventRecord
            {
                RunId = runId,
                ConditionId = CalibrationIdFactory.NormalizeToken(conditionId),
                Sequence = targetSequence++,
                MonotonicTimestampTicks = timestamp.Ticks,
                MonotonicTimestampSeconds = timestamp.Seconds,
                EventType = ValueOrUnknown(eventType),
                TargetId = ValueOrUnknown(targetId),
                TargetPositionX = targetPosition.x,
                TargetPositionY = targetPosition.y,
                TargetPositionZ = targetPosition.z,
                CameraAzimuthDegrees = cameraAzimuthDegrees,
                CameraElevationDegrees = cameraElevationDegrees
            });
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!IsCapturing)
            {
                return;
            }

            Mouse mouse = device as Mouse;
            if (mouse == null || mouse.deviceId != targetMouseDeviceId ||
                (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()))
            {
                return;
            }

            Vector2 delta;
            if (!mouse.delta.ReadValueFromEvent(eventPtr, out delta))
            {
                return;
            }

            CalibrationTimestamp timestamp = clock.Capture();
            runStore.RecordMouseEvent(new RawMouseEventRecord
            {
                TraceId = traceId,
                RunId = runId,
                Sequence = mouseSequence++,
                MonotonicTimestampTicks = timestamp.Ticks,
                MonotonicTimestampSeconds = timestamp.Seconds,
                InputEventTimestampSeconds = eventPtr.time,
                DeviceId = mouse.deviceId,
                DeviceLayout = mouse.layout,
                DeviceInterface = ValueOrUnknown(mouse.description.interfaceName),
                DeviceProduct = ValueOrUnknown(mouse.description.product),
                DeviceManufacturer = ValueOrUnknown(mouse.description.manufacturer),
                DeviceVersion = ValueOrUnknown(mouse.description.version),
                EventType = eventPtr.type.ToString(),
                RawDeltaX = delta.x,
                RawDeltaY = delta.y
            });
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            InputSystem.onEvent += OnInputEvent;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            InputSystem.onEvent -= OnInputEvent;
            subscribed = false;
        }

        private void ValidateCapturePlan()
        {
            P0R3CaptureGate.ValidateInputCaptureRuntime(
                InputSystem.settings.disableRedundantEventsMerging);

            if (!(InputSystem.GetDeviceById(targetMouseDeviceId) is Mouse))
            {
                throw new InvalidOperationException("Select one active mouse device before capture.");
            }

            P0R3CaptureGate.ValidatePlan(
                protocolId,
                environmentId,
                capturePlanId,
                conditionId,
                plannedRepeatCount,
                repetitionOrdinal,
                traceDurationSeconds,
                evidenceState,
                acceptanceOwner,
                executionOrder,
                controlledMotionInstruction,
                controlledVariablesJson,
                manualEnvironment);
        }

        private void EnsureCapturing()
        {
            if (!IsCapturing)
            {
                throw new InvalidOperationException("No calibration run is active.");
            }
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? CalibrationHarnessMetadata.UnknownValue : value.Trim();
        }

        private void NotifyFinalized(string finalizedRunId, string status, string reason)
        {
            Action<CalibrationCaptureFinalizedEvent> handler = CaptureFinalized;
            if (handler != null)
            {
                handler(new CalibrationCaptureFinalizedEvent
                {
                    RunId = finalizedRunId,
                    Status = status,
                    Reason = reason
                });
            }
        }
    }
}
