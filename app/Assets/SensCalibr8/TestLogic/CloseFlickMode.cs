using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.TestLogic
{
    public sealed class FrozenCloseFlickContract
    {
        private FrozenCloseFlickContract(string modeContractVersion, double shotTimeoutSeconds,
            double centerHitRadiusRatio, IReadOnlyDictionary<string, double> targetAngularDiameters)
        {
            ModeContractVersion = modeContractVersion;
            ShotTimeoutSeconds = shotTimeoutSeconds;
            CenterHitRadiusRatio = centerHitRadiusRatio;
            TargetAngularDiameters = targetAngularDiameters;
        }

        public string ModeContractVersion { get; }
        public double ShotTimeoutSeconds { get; }
        public double CenterHitRadiusRatio { get; }
        public IReadOnlyDictionary<string, double> TargetAngularDiameters { get; }

        public static FrozenCloseFlickContract From(FrozenCalibrationConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            try
            {
                using JsonDocument modes = JsonDocument.Parse(configuration.Record.TrackingContractJson);
                using JsonDocument geometry = JsonDocument.Parse(configuration.Record.TargetGeometryJson);
                JsonElement modeRoot = modes.RootElement, geometryRoot = geometry.RootElement;
                string version = modeRoot.GetProperty("mode_contract_version").GetString();
                double timeout = modeRoot.GetProperty("shared_shot_contract").GetProperty("trial_timeout_seconds").GetDouble();
                JsonElement close = modeRoot.GetProperty("modes").GetProperty("flick_close");
                if (!string.Equals(close.GetProperty("activation").GetString(), "hidden-target-after-deterministic-random-foreperiod", StringComparison.Ordinal) ||
                    !string.Equals(close.GetProperty("metric_clock").GetString(), "target-visible-to-click-or-timeout", StringComparison.Ordinal) ||
                    timeout <= 0d)
                    throw new InvalidDataException("Frozen Close Flick contract is unsupported.");

                double ratio = geometryRoot.GetProperty("target").GetProperty("center_hit_radius_ratio").GetDouble();
                var diameters = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (JsonProperty size in geometryRoot.GetProperty("target").GetProperty("sizes").EnumerateObject())
                    diameters.Add(size.Name, size.Value.GetProperty("angular_diameter_deg").GetDouble());
                if (string.IsNullOrWhiteSpace(version) || ratio <= 0d || ratio > 1d || diameters.Count == 0)
                    throw new InvalidDataException("Frozen Close Flick contract is incomplete.");
                return new FrozenCloseFlickContract(version, timeout, ratio,
                    new ReadOnlyDictionary<string, double>(diameters));
            }
            catch (JsonException exception) { throw new InvalidDataException("Frozen Close Flick contract is invalid.", exception); }
        }
    }

    public sealed class CloseFlickMode : ITestMode
    {
        private readonly DeterministicTargetSequencer sequencer;
        private readonly int batteryRepetitionOrdinal;
        private readonly List<CloseFlickCaptureEvidence> resolved = new List<CloseFlickCaptureEvidence>();
        private FrozenCloseFlickContract contract;
        private DeterministicTargetSequence sequence;
        private PendingOpportunity pending;
        private int nextConditionIndex;

        public CloseFlickMode(DeterministicTargetSequencer sequencer, int batteryRepetitionOrdinal)
        {
            this.sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
            this.batteryRepetitionOrdinal = batteryRepetitionOrdinal > 0
                ? batteryRepetitionOrdinal : throw new ArgumentOutOfRangeException(nameof(batteryRepetitionOrdinal));
        }

        public TestMode Mode => TestMode.FlickClose;
        public DeterministicTargetSequence Sequence => sequence;
        public IReadOnlyList<CloseFlickCaptureEvidence> ResolvedOpportunities =>
            new ReadOnlyCollection<CloseFlickCaptureEvidence>(resolved);
        public TargetCondition CurrentCondition => pending?.Condition;
        public double? CurrentForeperiodMs => pending == null ? null : pending.Condition.ForeperiodMs;

        public void Prepare(EngineSessionContext session, FrozenCalibrationConfiguration configuration)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.Mode != Mode) throw new TestEngineLifecycleException("close_flick_session_mode_mismatch");
            contract = FrozenCloseFlickContract.From(configuration);
            sequence = sequencer.Create(new SequenceSeedContext(session.ProfileId, session.Cycle.CycleId,
                session.Candidate.Phase, Mode, batteryRepetitionOrdinal));
            if (sequence.Conditions.Count == 0)
                throw new InvalidDataException("Frozen Close Flick sequence is incomplete.");
            foreach (TargetCondition condition in sequence.Conditions)
                if (condition.Mode != Mode || !condition.ForeperiodMs.HasValue ||
                    !contract.TargetAngularDiameters.ContainsKey(condition.TargetSize))
                    throw new InvalidDataException("Frozen Close Flick sequence is incomplete.");
            Reset();
        }

        public void Start()
        {
            if (sequence == null || contract == null) throw new InvalidOperationException("Close Flick must be prepared before start.");
            Reset();
        }

        public void Capture(TestModeCaptureEvent captureEvent)
        {
            if (captureEvent == null) throw new ArgumentNullException(nameof(captureEvent));
            switch (captureEvent.EventType)
            {
                case "target_visible": BeginOpportunity(captureEvent); break;
                case "first_mouse_movement": RecordFirstMovement(captureEvent); break;
                case "click": ResolveClick(captureEvent); break;
                case "timeout": ResolveTimeout(captureEvent); break;
                default: throw new TestEngineLifecycleException("close_flick_unknown_capture_event");
            }
        }

        public TestModeCompletion End()
        {
            if (pending != null) return new TestModeCompletion(false, "close_flick_pending_opportunity");
            return new TestModeCompletion(resolved.Count == sequence.Conditions.Count,
                resolved.Count == sequence.Conditions.Count ? "close_flick_complete" : "close_flick_incomplete");
        }

        public TestModeReport Report()
        {
            if (pending != null || resolved.Count != sequence.Conditions.Count)
                throw new TestEngineLifecycleException("close_flick_report_before_completion");
            return new TestModeReport("close-flick-resolved=" + resolved.Count.ToString(CultureInfo.InvariantCulture));
        }

        public void Cancel(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason is required.", nameof(reason));
        }

        public void Recover(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Recovery reason is required.", nameof(reason));
            Reset();
        }

        private void BeginOpportunity(TestModeCaptureEvent captureEvent)
        {
            if (pending != null || nextConditionIndex >= sequence.Conditions.Count)
                throw new TestEngineLifecycleException("close_flick_target_visibility_invalid");
            pending = new PendingOpportunity(sequence.Conditions[nextConditionIndex], captureEvent.TimestampSeconds);
        }

        private void RecordFirstMovement(TestModeCaptureEvent captureEvent)
        {
            RequirePending();
            if (pending.FirstMouseMovementTimestampSeconds.HasValue || captureEvent.TimestampSeconds < pending.VisibleTimestampSeconds)
                throw new TestEngineLifecycleException("close_flick_first_movement_invalid");
            pending.FirstMouseMovementTimestampSeconds = captureEvent.TimestampSeconds;
        }

        private void ResolveClick(TestModeCaptureEvent captureEvent)
        {
            RequirePending();
            if (captureEvent.TimestampSeconds < pending.VisibleTimestampSeconds ||
                captureEvent.TimestampSeconds - pending.VisibleTimestampSeconds > contract.ShotTimeoutSeconds)
                throw new TestEngineLifecycleException("close_flick_click_outside_timeout");
            Resolve(captureEvent, RequiredBoolean(captureEvent, "is_hit"), RequiredDouble(captureEvent, "final_aim_azimuth_deg"),
                RequiredDouble(captureEvent, "final_aim_elevation_deg"));
        }

        private void ResolveTimeout(TestModeCaptureEvent captureEvent)
        {
            RequirePending();
            if (captureEvent.TimestampSeconds - pending.VisibleTimestampSeconds < contract.ShotTimeoutSeconds)
                throw new TestEngineLifecycleException("close_flick_timeout_before_contract_limit");
            Resolve(captureEvent, false, RequiredDouble(captureEvent, "final_aim_azimuth_deg"),
                RequiredDouble(captureEvent, "final_aim_elevation_deg"));
        }

        private void Resolve(TestModeCaptureEvent captureEvent, bool isHit, double finalAzimuthDeg, double finalElevationDeg)
        {
            double azimuthDelta = finalAzimuthDeg - pending.Condition.CenterAzimuthDeg.Value;
            double elevationDelta = finalElevationDeg - pending.Condition.CenterElevationDeg.Value;
            double precision = Math.Sqrt(azimuthDelta * azimuthDelta + elevationDelta * elevationDelta);
            double centerRadius = contract.TargetAngularDiameters[pending.Condition.TargetSize] * contract.CenterHitRadiusRatio / 2d;
            string outcome = isHit ? "hit" : captureEvent.EventType == "timeout" ? "timeout" : "miss_click";
            resolved.Add(new CloseFlickCaptureEvidence(pending.Condition.TrialIndex, pending.Condition.TargetSize,
                pending.Condition.CenterAzimuthDeg.Value, pending.Condition.CenterElevationDeg.Value,
                pending.Condition.CenterOffsetDeg.Value, pending.VisibleTimestampSeconds,
                pending.FirstMouseMovementTimestampSeconds, captureEvent.TimestampSeconds, isHit, outcome,
                finalAzimuthDeg, finalElevationDeg, azimuthDelta, precision, precision <= centerRadius));
            pending = null;
            nextConditionIndex++;
        }

        private void RequirePending()
        {
            if (pending == null) throw new TestEngineLifecycleException("close_flick_no_active_opportunity");
        }

        private void Reset()
        {
            resolved.Clear();
            pending = null;
            nextConditionIndex = 0;
        }

        private static double RequiredDouble(TestModeCaptureEvent value, string key)
        {
            if (!value.Metadata.TryGetValue(key, out string text) || !double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed))
                throw new TestEngineLifecycleException("close_flick_metadata_" + key + "_invalid");
            return parsed;
        }

        private static bool RequiredBoolean(TestModeCaptureEvent value, string key)
        {
            if (!value.Metadata.TryGetValue(key, out string text) || !bool.TryParse(text, out bool parsed))
                throw new TestEngineLifecycleException("close_flick_metadata_" + key + "_invalid");
            return parsed;
        }

        private sealed class PendingOpportunity
        {
            public PendingOpportunity(TargetCondition condition, double visibleTimestampSeconds)
            {
                Condition = condition;
                VisibleTimestampSeconds = visibleTimestampSeconds;
            }

            public TargetCondition Condition { get; }
            public double VisibleTimestampSeconds { get; }
            public double? FirstMouseMovementTimestampSeconds { get; set; }
        }
    }
}
