using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SensCalibr8.Core.Domain
{
    public sealed class RawMouseInputEvent
    {
        public RawMouseInputEvent(long monotonicTimestampTicks, double monotonicTimestampSeconds,
            double inputEventTimestampSeconds, double rawDeltaX, double rawDeltaY, string deviceIdentity)
        {
            MonotonicTimestampTicks = monotonicTimestampTicks >= 0 ? monotonicTimestampTicks : throw new ArgumentOutOfRangeException(nameof(monotonicTimestampTicks));
            MonotonicTimestampSeconds = FiniteNonNegative(monotonicTimestampSeconds, nameof(monotonicTimestampSeconds));
            InputEventTimestampSeconds = FiniteNonNegative(inputEventTimestampSeconds, nameof(inputEventTimestampSeconds));
            RawDeltaX = Finite(rawDeltaX, nameof(rawDeltaX));
            RawDeltaY = Finite(rawDeltaY, nameof(rawDeltaY));
            DeviceIdentity = Required(deviceIdentity, nameof(deviceIdentity));
        }

        public long MonotonicTimestampTicks { get; }
        public double MonotonicTimestampSeconds { get; }
        public double InputEventTimestampSeconds { get; }
        public double RawDeltaX { get; }
        public double RawDeltaY { get; }
        public string DeviceIdentity { get; }

        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value)
            ? value : throw new ArgumentOutOfRangeException(name);
        private static double FiniteNonNegative(double value, string name) => Finite(value, name) >= 0d
            ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(name + " is required.", name);
    }

    public sealed class CapturedMouseSample
    {
        public CapturedMouseSample(long sampleIndex, double sessionTimestampSeconds, RawMouseInputEvent source,
            double cumulativeAzimuthDeg, double cumulativeElevationDeg)
        {
            SampleIndex = sampleIndex >= 0 ? sampleIndex : throw new ArgumentOutOfRangeException(nameof(sampleIndex));
            SessionTimestampSeconds = FiniteNonNegative(sessionTimestampSeconds, nameof(sessionTimestampSeconds));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            CumulativeAzimuthDeg = Finite(cumulativeAzimuthDeg, nameof(cumulativeAzimuthDeg));
            CumulativeElevationDeg = Finite(cumulativeElevationDeg, nameof(cumulativeElevationDeg));
        }

        public long SampleIndex { get; }
        public double SessionTimestampSeconds { get; }
        public RawMouseInputEvent Source { get; }
        public double CumulativeAzimuthDeg { get; }
        public double CumulativeElevationDeg { get; }

        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value)
            ? value : throw new ArgumentOutOfRangeException(name);
        private static double FiniteNonNegative(double value, string name) => Finite(value, name) >= 0d
            ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class InputTimingDiagnostics
    {
        public InputTimingDiagnostics(string signalPipelineVersion, string deviceIdentity, double measuredEventRateHz,
            double medianIntervalMs, double intervalMadMs, double p95IntervalMs, double p99IntervalMs,
            long duplicateTimestampCount, long reverseTimestampCount, long burstIntervalCount,
            long singleCadenceIntervalCount, long gapIntervalCount, double p99SingleCadenceResidualMs,
            bool timingContractPassed, string dispositionReason)
        {
            SignalPipelineVersion = Required(signalPipelineVersion, nameof(signalPipelineVersion));
            DeviceIdentity = Required(deviceIdentity, nameof(deviceIdentity));
            MeasuredEventRateHz = PositiveFinite(measuredEventRateHz, nameof(measuredEventRateHz));
            MedianIntervalMs = NonNegativeFinite(medianIntervalMs, nameof(medianIntervalMs));
            IntervalMadMs = NonNegativeFinite(intervalMadMs, nameof(intervalMadMs));
            P95IntervalMs = NonNegativeFinite(p95IntervalMs, nameof(p95IntervalMs));
            P99IntervalMs = NonNegativeFinite(p99IntervalMs, nameof(p99IntervalMs));
            DuplicateTimestampCount = NonNegative(duplicateTimestampCount, nameof(duplicateTimestampCount));
            ReverseTimestampCount = NonNegative(reverseTimestampCount, nameof(reverseTimestampCount));
            BurstIntervalCount = NonNegative(burstIntervalCount, nameof(burstIntervalCount));
            SingleCadenceIntervalCount = NonNegative(singleCadenceIntervalCount, nameof(singleCadenceIntervalCount));
            GapIntervalCount = NonNegative(gapIntervalCount, nameof(gapIntervalCount));
            P99SingleCadenceResidualMs = NonNegativeFinite(p99SingleCadenceResidualMs, nameof(p99SingleCadenceResidualMs));
            TimingContractPassed = timingContractPassed;
            DispositionReason = Required(dispositionReason, nameof(dispositionReason));
        }

        public string SignalPipelineVersion { get; }
        public string DeviceIdentity { get; }
        public double MeasuredEventRateHz { get; }
        public double MedianIntervalMs { get; }
        public double IntervalMadMs { get; }
        public double P95IntervalMs { get; }
        public double P99IntervalMs { get; }
        public long DuplicateTimestampCount { get; }
        public long ReverseTimestampCount { get; }
        public long BurstIntervalCount { get; }
        public long SingleCadenceIntervalCount { get; }
        public long GapIntervalCount { get; }
        public double P99SingleCadenceResidualMs { get; }
        public bool TimingContractPassed { get; }
        public string DispositionReason { get; }

        private static long NonNegative(long value, string name) => value >= 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double PositiveFinite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d
            ? value : throw new ArgumentOutOfRangeException(name);
        private static double NonNegativeFinite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d
            ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(name + " is required.", name);
    }

    public sealed class InputCaptureSnapshot
    {
        public InputCaptureSnapshot(IReadOnlyList<CapturedMouseSample> samples, InputTimingDiagnostics diagnostics)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            Samples = new ReadOnlyCollection<CapturedMouseSample>(new List<CapturedMouseSample>(samples));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public IReadOnlyList<CapturedMouseSample> Samples { get; }
        public InputTimingDiagnostics Diagnostics { get; }
    }
}
