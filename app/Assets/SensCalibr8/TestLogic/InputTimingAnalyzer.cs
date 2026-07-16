using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.TestLogic
{
    public sealed class InputTimingException : InvalidOperationException
    {
        public InputTimingException(string errorCode) : base(errorCode) { ErrorCode = errorCode; }
        public string ErrorCode { get; }
    }

    public sealed class UniformAngularSegment
    {
        public UniformAngularSegment(int sourceStartIndex, int sourceStopIndexExclusive, IReadOnlyList<double> timeSeconds,
            IReadOnlyList<double> cumulativeAzimuthDeg, IReadOnlyList<double> cumulativeElevationDeg, bool filterEligible)
        {
            SourceStartIndex = sourceStartIndex;
            SourceStopIndexExclusive = sourceStopIndexExclusive;
            TimeSeconds = new ReadOnlyCollection<double>(new List<double>(timeSeconds));
            CumulativeAzimuthDeg = new ReadOnlyCollection<double>(new List<double>(cumulativeAzimuthDeg));
            CumulativeElevationDeg = new ReadOnlyCollection<double>(new List<double>(cumulativeElevationDeg));
            FilterEligible = filterEligible;
        }

        public int SourceStartIndex { get; }
        public int SourceStopIndexExclusive { get; }
        public IReadOnlyList<double> TimeSeconds { get; }
        public IReadOnlyList<double> CumulativeAzimuthDeg { get; }
        public IReadOnlyList<double> CumulativeElevationDeg { get; }
        public bool FilterEligible { get; }
    }

    public sealed class InputTimingAnalyzer
    {
        private readonly FrozenInputTimingContract contract;

        public InputTimingAnalyzer(FrozenInputTimingContract contract)
        {
            this.contract = contract ?? throw new ArgumentNullException(nameof(contract));
        }

        public InputTimingDiagnostics Analyze(IReadOnlyList<CapturedMouseSample> samples)
        {
            ValidateSamples(samples);
            double[] intervals = Differences(samples.Select(item => item.Source.InputEventTimestampSeconds).ToArray());
            long duplicates = intervals.LongCount(value => value == 0d);
            long reversals = intervals.LongCount(value => value < 0d);
            double[] positive = intervals.Where(value => value > 0d).OrderBy(value => value).ToArray();
            if (positive.Length == 0) throw new InputTimingException("timing_no_positive_interval");
            double median = Percentile(positive, 0.5d);
            double[] absoluteDeviation = positive.Select(value => Math.Abs(value - median)).OrderBy(value => value).ToArray();
            double nominal = contract.IntervalSeconds;
            var singleResiduals = new List<double>();
            long bursts = 0;
            long single = 0;
            long gaps = 0;
            foreach (double interval in positive)
            {
                long cadenceClass = (long)Math.Floor(interval / nominal + 0.5d);
                if (cadenceClass == 0) bursts++;
                else if (cadenceClass == 1) { single++; singleResiduals.Add(Math.Abs(interval - nominal)); }
                else gaps++;
            }
            if (singleResiduals.Count == 0) throw new InputTimingException("timing_no_nominal_single_cadence_interval");
            singleResiduals.Sort();
            double p99ResidualMs = Percentile(singleResiduals.ToArray(), 0.99d) * 1000d;
            long medianCadenceClass = (long)Math.Floor(median / nominal + 0.5d);
            bool passed = duplicates == 0 && reversals == 0 && medianCadenceClass == 1 &&
                single > bursts && single > gaps && p99ResidualMs <= contract.ResamplingToleranceMs;
            string disposition = passed ? "accepted-integrity-modal-cadence" : BuildDisposition(duplicates, reversals,
                medianCadenceClass, single, bursts, gaps, p99ResidualMs);
            string identity = samples.Select(item => item.Source.DeviceIdentity).Distinct(StringComparer.Ordinal).Single();
            return new InputTimingDiagnostics(contract.SignalPipelineVersion, identity, 1d / median,
                median * 1000d, Percentile(absoluteDeviation, 0.5d) * 1000d,
                Percentile(positive, 0.95d) * 1000d, Percentile(positive, 0.99d) * 1000d,
                duplicates, reversals, bursts, single, gaps, p99ResidualMs, passed, disposition);
        }

        public IReadOnlyList<UniformAngularSegment> ResampleGapSafe(IReadOnlyList<CapturedMouseSample> samples)
        {
            ValidateSamples(samples);
            double[] times = samples.Select(item => item.Source.InputEventTimestampSeconds).ToArray();
            double[] intervals = Differences(times);
            if (intervals.Any(value => value <= 0d)) throw new InputTimingException("timing_non_increasing_cannot_resample");
            var boundaries = new List<int> { 0 };
            double gapLimit = contract.IntervalSeconds + contract.ToleranceSeconds;
            for (int index = 0; index < intervals.Length; index++)
                if (intervals[index] > gapLimit) boundaries.Add(index + 1);
            boundaries.Add(samples.Count);

            var result = new List<UniformAngularSegment>();
            for (int boundary = 0; boundary < boundaries.Count - 1; boundary++)
            {
                int start = boundaries[boundary];
                int stop = boundaries[boundary + 1];
                double duration = times[stop - 1] - times[start];
                int fullSteps = (int)Math.Floor(duration / contract.IntervalSeconds);
                var grid = new List<double>(fullSteps + 1);
                var azimuth = new List<double>(fullSteps + 1);
                var elevation = new List<double>(fullSteps + 1);
                for (int step = 0; step <= fullSteps; step++)
                {
                    double time = times[start] + step * contract.IntervalSeconds;
                    grid.Add(time);
                    azimuth.Add(Interpolate(samples, start, stop, time, true));
                    elevation.Add(Interpolate(samples, start, stop, time, false));
                }
                result.Add(new UniformAngularSegment(start, stop, grid, azimuth, elevation,
                    grid.Count >= contract.MinimumFilterableSegmentSamples));
            }
            return new ReadOnlyCollection<UniformAngularSegment>(result);
        }

        private static void ValidateSamples(IReadOnlyList<CapturedMouseSample> samples)
        {
            if (samples == null || samples.Count < 2) throw new InputTimingException("timing_at_least_two_samples_required");
            for (int index = 0; index < samples.Count; index++)
            {
                if (samples[index] == null || samples[index].SampleIndex != index)
                    throw new InputTimingException("timing_sample_sequence_invalid");
            }
            if (samples.Select(item => item.Source.DeviceIdentity).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new InputTimingException("timing_device_identity_changed");
        }

        private static double Interpolate(IReadOnlyList<CapturedMouseSample> samples, int start, int stop, double time, bool azimuth)
        {
            int right = start;
            while (right < stop && samples[right].Source.InputEventTimestampSeconds < time) right++;
            if (right <= start) return Axis(samples[start], azimuth);
            if (right >= stop) return Axis(samples[stop - 1], azimuth);
            CapturedMouseSample leftSample = samples[right - 1];
            CapturedMouseSample rightSample = samples[right];
            double leftTime = leftSample.Source.InputEventTimestampSeconds;
            double ratio = (time - leftTime) / (rightSample.Source.InputEventTimestampSeconds - leftTime);
            return Axis(leftSample, azimuth) + ratio * (Axis(rightSample, azimuth) - Axis(leftSample, azimuth));
        }

        private static double Axis(CapturedMouseSample sample, bool azimuth) => azimuth
            ? sample.CumulativeAzimuthDeg : sample.CumulativeElevationDeg;

        private static double[] Differences(double[] values)
        {
            var result = new double[values.Length - 1];
            for (int index = 1; index < values.Length; index++) result[index - 1] = values[index] - values[index - 1];
            return result;
        }

        private static double Percentile(double[] sorted, double quantile)
        {
            if (sorted == null || sorted.Length == 0) throw new ArgumentException("Percentile requires values.", nameof(sorted));
            double position = (sorted.Length - 1) * quantile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }

        private string BuildDisposition(long duplicates, long reversals, long medianClass, long single, long bursts, long gaps, double p99ResidualMs)
        {
            if (duplicates > 0 || reversals > 0) return "rejected-non-increasing-timestamps";
            if (medianClass != 1) return "rejected-median-cadence-class";
            if (single <= bursts || single <= gaps) return "rejected-single-cadence-not-modal";
            if (p99ResidualMs > contract.ResamplingToleranceMs) return "rejected-single-cadence-residual";
            return "rejected-timing-contract";
        }
    }
}
