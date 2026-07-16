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
    public sealed class FrozenFarFlickContract
    {
        private FrozenFarFlickContract(string modeContractVersion, double shotTimeoutSeconds,
            double movementOnsetThresholdDegPerSec, double centerHitRadiusRatio,
            IReadOnlyDictionary<string, double> targetAngularDiameters)
        { ModeContractVersion = modeContractVersion; ShotTimeoutSeconds = shotTimeoutSeconds; MovementOnsetThresholdDegPerSec = movementOnsetThresholdDegPerSec; CenterHitRadiusRatio = centerHitRadiusRatio; TargetAngularDiameters = targetAngularDiameters; }
        public string ModeContractVersion { get; } public double ShotTimeoutSeconds { get; } public double MovementOnsetThresholdDegPerSec { get; } public double CenterHitRadiusRatio { get; } public IReadOnlyDictionary<string, double> TargetAngularDiameters { get; }
        public static FrozenFarFlickContract From(FrozenCalibrationConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            try
            {
                using JsonDocument modes = JsonDocument.Parse(configuration.Record.TrackingContractJson);
                using JsonDocument geometry = JsonDocument.Parse(configuration.Record.TargetGeometryJson);
                JsonElement modeRoot = modes.RootElement, geometryRoot = geometry.RootElement, far = modeRoot.GetProperty("modes").GetProperty("flick_far");
                string version = modeRoot.GetProperty("mode_contract_version").GetString();
                double timeout = modeRoot.GetProperty("shared_shot_contract").GetProperty("trial_timeout_seconds").GetDouble();
                if (string.IsNullOrWhiteSpace(version) || timeout <= 0d || configuration.Record.SubmovementStartDegPerSec <= 0d ||
                    !string.Equals(far.GetProperty("activation").GetString(), "center-reference-click-activates-visible-preview-target", StringComparison.Ordinal) ||
                    !string.Equals(far.GetProperty("reaction_time_definition").GetString(), "activation-to-first-8-deg-per-sec-movement-onset", StringComparison.Ordinal) ||
                    !string.Equals(far.GetProperty("travel_time_definition").GetString(), "first-movement-onset-to-click", StringComparison.Ordinal))
                    throw new InvalidDataException("Frozen Far Flick contract is unsupported.");
                var diameters = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (JsonProperty size in geometryRoot.GetProperty("target").GetProperty("sizes").EnumerateObject()) diameters.Add(size.Name, size.Value.GetProperty("angular_diameter_deg").GetDouble());
                double ratio = geometryRoot.GetProperty("target").GetProperty("center_hit_radius_ratio").GetDouble();
                if (diameters.Count == 0 || ratio <= 0d || ratio > 1d) throw new InvalidDataException("Frozen Far Flick geometry is incomplete.");
                return new FrozenFarFlickContract(version, timeout, configuration.Record.SubmovementStartDegPerSec, ratio, new ReadOnlyDictionary<string, double>(diameters));
            }
            catch (JsonException exception) { throw new InvalidDataException("Frozen Far Flick contract is invalid.", exception); }
        }
    }

    public sealed class FarFlickMode : ITestMode
    {
        private readonly DeterministicTargetSequencer sequencer; private readonly int batteryRepetitionOrdinal; private readonly List<FarFlickCaptureEvidence> resolved = new List<FarFlickCaptureEvidence>();
        private FrozenFarFlickContract contract; private DeterministicTargetSequence sequence; private PendingOpportunity pending; private int nextConditionIndex;
        public FarFlickMode(DeterministicTargetSequencer sequencer, int batteryRepetitionOrdinal) { this.sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer)); this.batteryRepetitionOrdinal = batteryRepetitionOrdinal > 0 ? batteryRepetitionOrdinal : throw new ArgumentOutOfRangeException(nameof(batteryRepetitionOrdinal)); }
        public TestMode Mode => TestMode.FlickFar;
        public DeterministicTargetSequence Sequence => sequence;
        public IReadOnlyList<FarFlickCaptureEvidence> ResolvedOpportunities => new ReadOnlyCollection<FarFlickCaptureEvidence>(resolved);
        public TargetCondition CurrentCondition => pending?.Condition;
        public void Prepare(EngineSessionContext session, FrozenCalibrationConfiguration configuration)
        {
            if (session == null) throw new ArgumentNullException(nameof(session)); if (session.Mode != Mode) throw new TestEngineLifecycleException("far_flick_session_mode_mismatch");
            contract = FrozenFarFlickContract.From(configuration); sequence = sequencer.Create(new SequenceSeedContext(session.ProfileId, session.Cycle.CycleId, session.Candidate.Phase, Mode, batteryRepetitionOrdinal));
            if (sequence.Conditions.Count == 0) throw new InvalidDataException("Frozen Far Flick sequence is incomplete.");
            foreach (TargetCondition condition in sequence.Conditions) if (condition.Mode != Mode || condition.ForeperiodMs.HasValue || !contract.TargetAngularDiameters.ContainsKey(condition.TargetSize)) throw new InvalidDataException("Frozen Far Flick sequence is incomplete.");
            Reset();
        }
        public void Start() { if (sequence == null || contract == null) throw new InvalidOperationException("Far Flick must be prepared before start."); Reset(); }
        public void Capture(TestModeCaptureEvent value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            switch (value.EventType)
            { case "preview_target_visible": Preview(value); break; case "center_reference_activated": Activate(value); break; case "movement_onset": MovementOnset(value); break; case "click": ResolveClick(value); break; case "timeout": ResolveTimeout(value); break; default: throw new TestEngineLifecycleException("far_flick_unknown_capture_event"); }
        }
        public TestModeCompletion End() { if (pending != null) return new TestModeCompletion(false, "far_flick_pending_opportunity"); return new TestModeCompletion(resolved.Count == sequence.Conditions.Count, resolved.Count == sequence.Conditions.Count ? "far_flick_complete" : "far_flick_incomplete"); }
        public TestModeReport Report() { if (pending != null || resolved.Count != sequence.Conditions.Count) throw new TestEngineLifecycleException("far_flick_report_before_completion"); return new TestModeReport("far-flick-resolved=" + resolved.Count.ToString(CultureInfo.InvariantCulture)); }
        public void Cancel(string reason) { if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason is required.", nameof(reason)); }
        public void Recover(string reason) { if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Recovery reason is required.", nameof(reason)); Reset(); }
        private void Preview(TestModeCaptureEvent value) { if (pending != null || nextConditionIndex >= sequence.Conditions.Count) throw new TestEngineLifecycleException("far_flick_preview_invalid"); pending = new PendingOpportunity(sequence.Conditions[nextConditionIndex], value.TimestampSeconds); }
        private void Activate(TestModeCaptureEvent value) { RequirePending(); if (pending.ActivationTimestampSeconds.HasValue || value.TimestampSeconds < pending.PreviewTimestampSeconds) throw new TestEngineLifecycleException("far_flick_activation_invalid"); pending.ActivationTimestampSeconds = value.TimestampSeconds; }
        private void MovementOnset(TestModeCaptureEvent value)
        { RequireActivated(); if (pending.MovementOnsetTimestampSeconds.HasValue || value.TimestampSeconds < pending.ActivationTimestampSeconds.Value || RequiredDouble(value, "angular_velocity_deg_per_sec") < contract.MovementOnsetThresholdDegPerSec) throw new TestEngineLifecycleException("far_flick_movement_onset_invalid"); pending.MovementOnsetTimestampSeconds = value.TimestampSeconds; }
        private void ResolveClick(TestModeCaptureEvent value) { RequireActivated(); if (value.TimestampSeconds < pending.ActivationTimestampSeconds.Value || value.TimestampSeconds - pending.ActivationTimestampSeconds.Value > contract.ShotTimeoutSeconds) throw new TestEngineLifecycleException("far_flick_click_outside_timeout"); Resolve(value, RequiredBoolean(value, "is_hit"), RequiredDouble(value, "final_aim_azimuth_deg"), RequiredDouble(value, "final_aim_elevation_deg")); }
        private void ResolveTimeout(TestModeCaptureEvent value) { RequireActivated(); if (value.TimestampSeconds - pending.ActivationTimestampSeconds.Value < contract.ShotTimeoutSeconds) throw new TestEngineLifecycleException("far_flick_timeout_before_contract_limit"); Resolve(value, false, RequiredDouble(value, "final_aim_azimuth_deg"), RequiredDouble(value, "final_aim_elevation_deg")); }
        private void Resolve(TestModeCaptureEvent value, bool isHit, double azimuth, double elevation)
        {
            double azimuthDelta = azimuth - pending.Condition.CenterAzimuthDeg.Value, elevationDelta = elevation - pending.Condition.CenterElevationDeg.Value, precision = Math.Sqrt(azimuthDelta * azimuthDelta + elevationDelta * elevationDelta), centerRadius = contract.TargetAngularDiameters[pending.Condition.TargetSize] * contract.CenterHitRadiusRatio / 2d;
            string outcome = isHit ? "hit" : value.EventType == "timeout" ? "timeout" : "miss_click";
            resolved.Add(new FarFlickCaptureEvidence(pending.Condition.TrialIndex, pending.Condition.TargetSize, pending.Condition.CenterAzimuthDeg.Value, pending.Condition.CenterElevationDeg.Value, pending.Condition.CenterOffsetDeg.Value, pending.PreviewTimestampSeconds, pending.ActivationTimestampSeconds.Value, pending.MovementOnsetTimestampSeconds, value.TimestampSeconds, isHit, outcome, azimuth, elevation, azimuthDelta, precision, precision <= centerRadius)); pending = null; nextConditionIndex++;
        }
        private void RequirePending() { if (pending == null) throw new TestEngineLifecycleException("far_flick_no_preview_target"); }
        private void RequireActivated() { RequirePending(); if (!pending.ActivationTimestampSeconds.HasValue) throw new TestEngineLifecycleException("far_flick_not_activated"); }
        private void Reset() { resolved.Clear(); pending = null; nextConditionIndex = 0; }
        private static double RequiredDouble(TestModeCaptureEvent value, string key) { if (!value.Metadata.TryGetValue(key, out string text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed)) throw new TestEngineLifecycleException("far_flick_metadata_" + key + "_invalid"); return parsed; }
        private static bool RequiredBoolean(TestModeCaptureEvent value, string key) { if (!value.Metadata.TryGetValue(key, out string text) || !bool.TryParse(text, out bool parsed)) throw new TestEngineLifecycleException("far_flick_metadata_" + key + "_invalid"); return parsed; }
        private sealed class PendingOpportunity { public PendingOpportunity(TargetCondition condition, double previewTimestampSeconds) { Condition = condition; PreviewTimestampSeconds = previewTimestampSeconds; } public TargetCondition Condition { get; } public double PreviewTimestampSeconds { get; } public double? ActivationTimestampSeconds { get; set; } public double? MovementOnsetTimestampSeconds { get; set; } }
    }
}
