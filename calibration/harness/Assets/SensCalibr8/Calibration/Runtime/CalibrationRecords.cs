using System;

namespace SensCalibr8.Calibration
{
    public static class CalibrationHarnessMetadata
    {
        public const string HarnessVersion = "p0-r3-harness-v4";
        public const string UnknownValue = "unknown";
    }

    [Serializable]
    public sealed class CalibrationManualEnvironment
    {
        public string DisplayModel;
        public string NativeResolution;
        public string DisplayRefreshRate;
        public string DisplayScaling;
        public string VSyncState;
        public string AdaptiveSyncState;
        public string MouseManufacturer;
        public string MouseModel;
        public string MouseConnection;
        public string MouseFirmware;
        public string MouseDpi;
        public string MouseDpiEvidenceSource;
        public string ConfiguredPollingRate;
        public string PollingRateEvidenceSource;
        public string UsbPathOrHub;
        public string MousePowerState;
        public string PointerSpeed;
        public string PointerAccelerationState;
        public string MousepadDescription;
        public string OperatorId;
        public string DominantHand;
        public string GripDescriptor;
        public string MovementDescriptor;
        public string PostureNotes;
        public string WarmupProcedure;
        public string PowerPlan;
        public string BackgroundLoadPolicy;
        public string ThermalPowerNotes;
        public string NetworkOfflineState;
    }

    [Serializable]
    public sealed class CalibrationEnvironmentManifest
    {
        public string ProtocolId;
        public string EnvironmentId;
        public string CapturedUtc;
        public string HarnessVersion;
        public string HarnessChecksum;
        public string RuntimeBuildType;
        public string ExecutableName;
        public string ExecutableChecksum;
        public string UnityVersion;
        public string InputSystemVersion;
        public string InputUpdateMode;
        public string TimestampSource;
        public bool RedundantEventMergingDisabled;
        public bool DedicatedRawInputMessagePump;
        public string OperatingSystem;
        public string DeviceModel;
        public string ProcessorType;
        public int ProcessorCount;
        public int SystemMemoryMb;
        public string GraphicsDeviceName;
        public string GraphicsDeviceVersion;
        public int ActiveWidth;
        public int ActiveHeight;
        public double ActiveRefreshRateHz;
        public string FullScreenMode;
        public bool ApplicationFocused;
        public string MouseLayout;
        public int MouseDeviceId;
        public string MouseInterface;
        public string MouseProduct;
        public string MouseManufacturer;
        public string MouseVersion;
        public CalibrationManualEnvironment Manual;
    }

    [Serializable]
    public sealed class CalibrationCapturePlanManifest
    {
        public string ProtocolId;
        public string CapturePlanId;
        public string EnvironmentId;
        public string ConditionId;
        public int PlannedRepeatCount;
        public int RepetitionOrdinal;
        public string ExecutionOrder;
        public string ControlledVariablesJson;
        public string EvidenceState;
        public string AcceptanceOwner;
        public string ControlledMotionInstruction;
        public double TraceDurationSeconds;
        public string CreatedUtc;
    }

    [Serializable]
    public sealed class CalibrationRunManifest
    {
        public string ProtocolId;
        public string EnvironmentId;
        public string CapturePlanId;
        public string ConditionId;
        public string RunId;
        public string TraceId;
        public string HarnessVersion;
        public string HarnessChecksum;
        public string Status;
        public string Reason;
        public string StartedUtc;
        public string EndedUtc;
        public long StopwatchFrequency;
    }

    [Serializable]
    public sealed class RawMouseEventRecord
    {
        public string TraceId;
        public string RunId;
        public long Sequence;
        public long MonotonicTimestampTicks;
        public double MonotonicTimestampSeconds;
        public double InputEventTimestampSeconds;
        public int DeviceId;
        public string DeviceLayout;
        public string DeviceInterface;
        public string DeviceProduct;
        public string DeviceManufacturer;
        public string DeviceVersion;
        public string EventType;
        public float RawDeltaX;
        public float RawDeltaY;
    }

    [Serializable]
    public sealed class FrameTimingRecord
    {
        public string RunId;
        public string EnvironmentId;
        public long Sequence;
        public long MonotonicTimestampTicks;
        public double MonotonicTimestampSeconds;
        public int UnityFrameIndex;
        public float UnscaledDeltaTimeSeconds;
        public bool ApplicationFocused;
        public int ScreenWidth;
        public int ScreenHeight;
        public double RefreshRateHz;
        public string FullScreenMode;
    }

    [Serializable]
    public sealed class TargetCameraEventRecord
    {
        public string RunId;
        public string ConditionId;
        public long Sequence;
        public long MonotonicTimestampTicks;
        public double MonotonicTimestampSeconds;
        public string EventType;
        public string TargetId;
        public float TargetPositionX;
        public float TargetPositionY;
        public float TargetPositionZ;
        public float CameraAzimuthDegrees;
        public float CameraElevationDegrees;
    }

    [Serializable]
    public sealed class FileIntegrityRecord
    {
        public string ArtifactId;
        public string RelativePath;
        public long ByteSize;
        public string Sha256;
        public string CreatedUtc;
        public string ProducerVersion;
        public string ProducerChecksum;
        public string ProtocolId;
        public string EnvironmentId;
        public string CapturePlanId;
        public string ConditionId;
        public string RunId;
        public string TraceId;
    }

    [Serializable]
    public sealed class IntegrityManifest
    {
        public string ProtocolId;
        public string EnvironmentId;
        public string CapturePlanId;
        public string ConditionId;
        public string RunId;
        public string TraceId;
        public string ProducerVersion;
        public string ProducerChecksum;
        public string CreatedUtc;
        public FileIntegrityRecord[] Artifacts;
    }

    public struct CalibrationTimestamp
    {
        public long Ticks;
        public double Seconds;
    }
}
