using System;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.TestLogic
{
    public sealed class FrozenInputTimingContract
    {
        private FrozenInputTimingContract(string signalPipelineVersion, double samplingRateHz,
            double resamplingToleranceMs, string acceptancePolicy, int minimumFilterableSegmentSamples)
        {
            SignalPipelineVersion = signalPipelineVersion;
            SamplingRateHz = samplingRateHz;
            ResamplingToleranceMs = resamplingToleranceMs;
            AcceptancePolicy = acceptancePolicy;
            MinimumFilterableSegmentSamples = minimumFilterableSegmentSamples;
        }

        public string SignalPipelineVersion { get; }
        public double SamplingRateHz { get; }
        public double ResamplingToleranceMs { get; }
        public string AcceptancePolicy { get; }
        public int MinimumFilterableSegmentSamples { get; }
        public double IntervalSeconds => 1d / SamplingRateHz;
        public double ToleranceSeconds => ResamplingToleranceMs / 1000d;

        public static FrozenInputTimingContract From(FrozenCalibrationConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            CalibrationConfigurationRecord record = configuration.Record ?? throw new InvalidDataException("Calibration record is required.");
            using JsonDocument tracking = Parse(record.TrackingContractJson, "tracking_contract_json");
            JsonElement pipeline = tracking.RootElement.GetProperty("signal_pipeline");
            int minimum = pipeline.GetProperty("minimum_filterable_segment_samples").GetInt32();
            if (minimum <= 0 || record.InputSamplingRateHz <= 0d || record.ResamplingToleranceMs < 0d ||
                !string.Equals(record.TimingAcceptancePolicy, "integrity-modal-cadence", StringComparison.Ordinal))
                throw new InvalidDataException("Frozen input timing contract is incomplete or unsupported.");
            return new FrozenInputTimingContract(record.SignalPipelineVersion, record.InputSamplingRateHz,
                record.ResamplingToleranceMs, record.TimingAcceptancePolicy, minimum);
        }

        private static JsonDocument Parse(string value, string label)
        {
            try { return JsonDocument.Parse(value); }
            catch (JsonException exception) { throw new InvalidDataException(label + " is invalid.", exception); }
        }
    }

    public sealed class FrozenFramePolicy
    {
        private FrozenFramePolicy(int targetFrameRateHz, int vSyncCount, bool adaptiveSyncRequiredOff)
        {
            TargetFrameRateHz = targetFrameRateHz;
            VSyncCount = vSyncCount;
            AdaptiveSyncRequiredOff = adaptiveSyncRequiredOff;
        }

        public int TargetFrameRateHz { get; }
        public int VSyncCount { get; }
        public bool AdaptiveSyncRequiredOff { get; }

        public static FrozenFramePolicy From(FrozenCalibrationConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            using JsonDocument geometry = JsonDocument.Parse(configuration.Record.TargetGeometryJson);
            JsonElement policy = geometry.RootElement.GetProperty("frame_policy");
            int target = policy.GetProperty("target_frame_rate_hz").GetInt32();
            int vSync = policy.GetProperty("vsync_count").GetInt32();
            bool adaptiveOff = policy.GetProperty("adaptive_sync_required_off").GetBoolean();
            if (target <= 0 || vSync < 0) throw new InvalidDataException("Frozen frame policy is invalid.");
            return new FrozenFramePolicy(target, vSync, adaptiveOff);
        }
    }
}
