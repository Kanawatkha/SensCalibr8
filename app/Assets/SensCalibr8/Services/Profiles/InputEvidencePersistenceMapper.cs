using System;
using System.Collections.Generic;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Profiles
{
    public static class InputEvidencePersistenceMapper
    {
        public static IReadOnlyList<MouseSampleCaptureRecord> ToMouseSampleRecords(
            IReadOnlyList<CapturedMouseSample> samples, int? shotCaptureIndex = null)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var records = new List<MouseSampleCaptureRecord>(samples.Count);
            foreach (CapturedMouseSample sample in samples)
            {
                if (sample == null) throw new ArgumentException("Input samples cannot contain null.", nameof(samples));
                records.Add(new MouseSampleCaptureRecord(sample.SampleIndex, sample.SessionTimestampSeconds,
                    sample.Source.RawDeltaX, sample.Source.RawDeltaY, sample.CumulativeAzimuthDeg,
                    sample.CumulativeElevationDeg, shotCaptureIndex));
            }
            return records.AsReadOnly();
        }

        public static SessionTimingDiagnosticsRecord ToTimingRecord(InputTimingDiagnostics diagnostics,
            double configuredPollingRateHz)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            if (double.IsNaN(configuredPollingRateHz) || double.IsInfinity(configuredPollingRateHz) || configuredPollingRateHz <= 0d)
                throw new ArgumentOutOfRangeException(nameof(configuredPollingRateHz));
            return new SessionTimingDiagnosticsRecord(diagnostics.SignalPipelineVersion, diagnostics.DeviceIdentity,
                configuredPollingRateHz, diagnostics.MeasuredEventRateHz, diagnostics.MedianIntervalMs,
                diagnostics.IntervalMadMs, diagnostics.P95IntervalMs, diagnostics.P99IntervalMs,
                diagnostics.DuplicateTimestampCount, diagnostics.ReverseTimestampCount,
                diagnostics.BurstIntervalCount, diagnostics.SingleCadenceIntervalCount,
                diagnostics.GapIntervalCount, diagnostics.P99SingleCadenceResidualMs,
                diagnostics.TimingContractPassed, diagnostics.DispositionReason);
        }
    }
}
