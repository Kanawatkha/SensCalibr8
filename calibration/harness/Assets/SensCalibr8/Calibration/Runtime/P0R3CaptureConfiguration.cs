using System;

namespace SensCalibr8.Calibration
{
    [Serializable]
    public sealed class P0R3CaptureConfiguration
    {
        public string ProtocolId;
        public string EnvironmentId;
        public string CapturePlanId;
        public string ConditionId;
        public int PlannedRepeatCount;
        public int RepetitionOrdinal;
        public double TraceDurationSeconds;
        public string EvidenceState;
        public string AcceptanceOwner;
        public string ExecutionOrder;
        public string ControlledMotionInstruction;
        public string ControlledVariablesJson;
        public int TargetMouseDeviceId;
        public CalibrationManualEnvironment ManualEnvironment;
    }

    public sealed class CalibrationCaptureFinalizedEvent
    {
        public string RunId;
        public string Status;
        public string Reason;
    }
}
