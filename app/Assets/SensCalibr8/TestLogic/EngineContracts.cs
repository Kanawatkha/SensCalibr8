using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.TestLogic
{
    public enum TestEngineSessionState
    {
        Created,
        Prepared,
        Capturing,
        Ending,
        Completed,
        Cancelled,
        Faulted
    }

    public sealed class TestEngineLifecycleException : InvalidOperationException
    {
        public TestEngineLifecycleException(string errorCode) : base(errorCode)
        {
            ErrorCode = errorCode;
        }

        public TestEngineLifecycleException(string errorCode, Exception innerException) : base(errorCode, innerException)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; }
    }

    public sealed class EngineCycleContext
    {
        public EngineCycleContext(long profileId, long cycleId, int cycleNumber)
        {
            ProfileId = Positive(profileId, nameof(profileId));
            CycleId = Positive(cycleId, nameof(cycleId));
            CycleNumber = cycleNumber > 0 ? cycleNumber : throw new ArgumentOutOfRangeException(nameof(cycleNumber));
        }

        public long ProfileId { get; }
        public long CycleId { get; }
        public int CycleNumber { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class EngineCandidateContext
    {
        public EngineCandidateContext(long profileId, long cycleId, long candidateId, ProtocolPhase phase,
            double edpi, double sensitivityValue)
        {
            ProfileId = Positive(profileId, nameof(profileId));
            CycleId = Positive(cycleId, nameof(cycleId));
            CandidateId = Positive(candidateId, nameof(candidateId));
            Phase = Defined(phase, nameof(phase));
            Edpi = PositiveFinite(edpi, nameof(edpi));
            SensitivityValue = PositiveFinite(sensitivityValue, nameof(sensitivityValue));
        }

        public long ProfileId { get; }
        public long CycleId { get; }
        public long CandidateId { get; }
        public ProtocolPhase Phase { get; }
        public double Edpi { get; }
        public double SensitivityValue { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double PositiveFinite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d
            ? value : throw new ArgumentOutOfRangeException(name);
        private static T Defined<T>(T value, string name) where T : struct => Enum.IsDefined(typeof(T), value)
            ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class EngineBatteryContext
    {
        public EngineBatteryContext(long profileId, long cycleId, long candidateId, long batteryId,
            ProtocolPhase phase, double sensitivityValue)
        {
            ProfileId = Positive(profileId, nameof(profileId));
            CycleId = Positive(cycleId, nameof(cycleId));
            CandidateId = Positive(candidateId, nameof(candidateId));
            BatteryId = Positive(batteryId, nameof(batteryId));
            Phase = Defined(phase, nameof(phase));
            SensitivityValue = PositiveFinite(sensitivityValue, nameof(sensitivityValue));
        }

        public long ProfileId { get; }
        public long CycleId { get; }
        public long CandidateId { get; }
        public long BatteryId { get; }
        public ProtocolPhase Phase { get; }
        public double SensitivityValue { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double PositiveFinite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d
            ? value : throw new ArgumentOutOfRangeException(name);
        private static T Defined<T>(T value, string name) where T : struct => Enum.IsDefined(typeof(T), value)
            ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class EngineSessionContext
    {
        public EngineSessionContext(string runId, EngineCycleContext cycle, EngineCandidateContext candidate,
            EngineBatteryContext battery, TestMode mode, CalibrationConfigVersion calibrationConfigVersion)
        {
            RunId = Required(runId, nameof(runId));
            Cycle = cycle ?? throw new ArgumentNullException(nameof(cycle));
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Battery = battery ?? throw new ArgumentNullException(nameof(battery));
            Mode = Defined(mode, nameof(mode));
            CalibrationConfigVersion = calibrationConfigVersion;
            ValidateLineage();
        }

        public string RunId { get; }
        public EngineCycleContext Cycle { get; }
        public EngineCandidateContext Candidate { get; }
        public EngineBatteryContext Battery { get; }
        public TestMode Mode { get; }
        public CalibrationConfigVersion CalibrationConfigVersion { get; }
        public long ProfileId => Cycle.ProfileId;

        private void ValidateLineage()
        {
            if (Candidate.ProfileId != Cycle.ProfileId || Battery.ProfileId != Cycle.ProfileId)
                throw new TestEngineLifecycleException("engine_profile_lineage_mismatch");
            if (Candidate.CycleId != Cycle.CycleId || Battery.CycleId != Cycle.CycleId)
                throw new TestEngineLifecycleException("engine_cycle_lineage_mismatch");
            if (Battery.CandidateId != Candidate.CandidateId)
                throw new TestEngineLifecycleException("engine_candidate_lineage_mismatch");
            if (Battery.Phase != Candidate.Phase)
                throw new TestEngineLifecycleException("engine_phase_lineage_mismatch");
            if (!Battery.SensitivityValue.Equals(Candidate.SensitivityValue))
                throw new TestEngineLifecycleException("engine_sensitivity_lineage_mismatch");
            if (string.IsNullOrWhiteSpace(CalibrationConfigVersion.Value))
                throw new TestEngineLifecycleException("engine_configuration_incomplete");
        }

        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(name + " is required.", name);
        private static T Defined<T>(T value, string name) where T : struct => Enum.IsDefined(typeof(T), value)
            ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class TestModeCaptureEvent
    {
        public TestModeCaptureEvent(string eventType, double timestampSeconds, IReadOnlyDictionary<string, string> metadata = null)
        {
            EventType = !string.IsNullOrWhiteSpace(eventType) ? eventType : throw new ArgumentException("Event type is required.", nameof(eventType));
            TimestampSeconds = !double.IsNaN(timestampSeconds) && !double.IsInfinity(timestampSeconds) && timestampSeconds >= 0d
                ? timestampSeconds : throw new ArgumentOutOfRangeException(nameof(timestampSeconds));
            Metadata = metadata == null
                ? EmptyMetadata
                : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata));
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        public string EventType { get; }
        public double TimestampSeconds { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }

    public sealed class TestModeCompletion
    {
        public TestModeCompletion(bool isComplete, string disposition)
        {
            IsComplete = isComplete;
            Disposition = !string.IsNullOrWhiteSpace(disposition) ? disposition : throw new ArgumentException("Disposition is required.", nameof(disposition));
        }

        public bool IsComplete { get; }
        public string Disposition { get; }
    }

    public sealed class TestModeReport
    {
        public TestModeReport(string summary)
        {
            Summary = !string.IsNullOrWhiteSpace(summary) ? summary : throw new ArgumentException("Summary is required.", nameof(summary));
        }

        public string Summary { get; }
    }

    public sealed class TestEngineReport
    {
        public TestEngineReport(EngineSessionContext session, TestModeCompletion completion, TestModeReport modeReport)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Completion = completion ?? throw new ArgumentNullException(nameof(completion));
            ModeReport = modeReport ?? throw new ArgumentNullException(nameof(modeReport));
        }

        public EngineSessionContext Session { get; }
        public TestModeCompletion Completion { get; }
        public TestModeReport ModeReport { get; }
    }

    public interface ITestMode
    {
        TestMode Mode { get; }
        void Prepare(EngineSessionContext session, FrozenCalibrationConfiguration configuration);
        void Start();
        void Capture(TestModeCaptureEvent captureEvent);
        TestModeCompletion End();
        TestModeReport Report();
        void Cancel(string reason);
        void Recover(string reason);
    }
}
