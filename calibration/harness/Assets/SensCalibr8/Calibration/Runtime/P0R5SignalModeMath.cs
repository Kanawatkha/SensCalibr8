using System;
using System.Collections.Generic;

namespace SensCalibr8.Calibration
{
    public readonly struct P0R5SosSection
    {
        public P0R5SosSection(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            B0 = b0;
            B1 = b1;
            B2 = b2;
            A0 = a0;
            A1 = a1;
            A2 = a2;
        }

        public double B0 { get; }
        public double B1 { get; }
        public double B2 { get; }
        public double A0 { get; }
        public double A1 { get; }
        public double A2 { get; }
    }

    public readonly struct P0R5Submovement
    {
        public P0R5Submovement(int onsetSample, int endSample)
        {
            OnsetSample = onsetSample;
            EndSample = endSample;
        }

        public int OnsetSample { get; }
        public int EndSample { get; }
    }

    public readonly struct P0R5TrackingMetric
    {
        public P0R5TrackingMetric(int windowIndex, double timeOnTargetPercent, double deviationRmsDegrees)
        {
            WindowIndex = windowIndex;
            TimeOnTargetPercent = timeOnTargetPercent;
            DeviationRmsDegrees = deviationRmsDegrees;
        }

        public int WindowIndex { get; }
        public double TimeOnTargetPercent { get; }
        public double DeviationRmsDegrees { get; }
    }

    public static class P0R5SignalModeContract
    {
        public const double SamplingRateHz = 1000.0;
        public const int FilterOrder = 5;
        public const double CutoffFrequencyHz = 7.0;
        public const int PadLengthSamples = 18;
        public const int MinimumFilterableSamples = 20;
        public const double StartThresholdDegreesPerSecond = 8.0;
        public const double EndThresholdDegreesPerSecond = 4.0;
        public const double RefractoryPeriodMilliseconds = 80.0;
        public const int ShotTrialCount = 30;
        public const int ShotAdaptationCount = 15;
        public const int TrackingTrialCount = 18;
        public const int TrackingPostAdaptationWindowCount = 54;
        public const double TrackingTrialDurationSeconds = 6.0;
        public const double TrackingMetricWindowSeconds = 1.0;

        public static P0R5SosSection[] CreateSections()
        {
            return new[]
            {
                new P0R5SosSection(
                    4.793801754376094e-09,
                    4.793801754376094e-09,
                    0.0,
                    1.0,
                    -0.9569573219226812,
                    0.0),
                new P0R5SosSection(
                    1.0,
                    2.0,
                    1.0,
                    1.0,
                    -1.9294340574583,
                    0.9313017524091318),
                new P0R5SosSection(
                    1.0,
                    2.0,
                    1.0,
                    1.0,
                    -1.9712822627176634,
                    0.9731904667915525)
            };
        }
    }

    public static class P0R5SignalModeMath
    {
        public static int DefaultPadLength(IReadOnlyList<P0R5SosSection> sections)
        {
            RequireSections(sections);
            var numeratorZeros = 0;
            var denominatorZeros = 0;
            for (var index = 0; index < sections.Count; index++)
            {
                if (sections[index].B2 == 0.0)
                {
                    numeratorZeros++;
                }

                if (sections[index].A2 == 0.0)
                {
                    denominatorZeros++;
                }
            }

            return 3 * (2 * sections.Count + 1 - Math.Min(numeratorZeros, denominatorZeros));
        }

        public static double FrequencyMagnitude(
            IReadOnlyList<P0R5SosSection> sections,
            double frequencyHz,
            double samplingRateHz)
        {
            RequireSections(sections);
            RequireFiniteNonNegative(frequencyHz, nameof(frequencyHz));
            RequirePositive(samplingRateHz, nameof(samplingRateHz));
            var angle = -2.0 * Math.PI * frequencyHz / samplingRateHz;
            var z1Real = Math.Cos(angle);
            var z1Imaginary = Math.Sin(angle);
            var z2Real = Math.Cos(2.0 * angle);
            var z2Imaginary = Math.Sin(2.0 * angle);
            var responseReal = 1.0;
            var responseImaginary = 0.0;

            for (var index = 0; index < sections.Count; index++)
            {
                var section = sections[index];
                var numeratorReal = section.B0 + section.B1 * z1Real + section.B2 * z2Real;
                var numeratorImaginary = section.B1 * z1Imaginary + section.B2 * z2Imaginary;
                var denominatorReal = section.A0 + section.A1 * z1Real + section.A2 * z2Real;
                var denominatorImaginary = section.A1 * z1Imaginary + section.A2 * z2Imaginary;
                var denominatorMagnitudeSquared =
                    denominatorReal * denominatorReal + denominatorImaginary * denominatorImaginary;
                var quotientReal =
                    (numeratorReal * denominatorReal + numeratorImaginary * denominatorImaginary)
                    / denominatorMagnitudeSquared;
                var quotientImaginary =
                    (numeratorImaginary * denominatorReal - numeratorReal * denominatorImaginary)
                    / denominatorMagnitudeSquared;
                var nextReal = responseReal * quotientReal - responseImaginary * quotientImaginary;
                var nextImaginary = responseReal * quotientImaginary + responseImaginary * quotientReal;
                responseReal = nextReal;
                responseImaginary = nextImaginary;
            }

            return Math.Sqrt(responseReal * responseReal + responseImaginary * responseImaginary);
        }

        public static double[] FilterForwardBackward(
            IReadOnlyList<P0R5SosSection> sections,
            IReadOnlyList<double> values,
            int padLength)
        {
            RequireSections(sections);
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (padLength < 0 || padLength >= values.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(padLength));
            }

            var extended = new double[values.Count + 2 * padLength];
            for (var index = 0; index < padLength; index++)
            {
                extended[index] = 2.0 * values[0] - values[padLength - index];
            }

            for (var index = 0; index < values.Count; index++)
            {
                if (double.IsNaN(values[index]) || double.IsInfinity(values[index]))
                {
                    throw new ArgumentException("Signal values must be finite.", nameof(values));
                }

                extended[padLength + index] = values[index];
            }

            for (var index = 0; index < padLength; index++)
            {
                extended[padLength + values.Count + index] =
                    2.0 * values[values.Count - 1] - values[values.Count - 2 - index];
            }

            var steadyState = CreateSteadyState(sections);
            var forward = Filter(sections, extended, steadyState, extended[0]);
            Array.Reverse(forward);
            var backward = Filter(sections, forward, steadyState, forward[0]);
            Array.Reverse(backward);
            var result = new double[values.Count];
            Array.Copy(backward, padLength, result, 0, result.Length);
            return result;
        }

        public static IReadOnlyList<P0R5Submovement> DetectSubmovements(
            IReadOnlyList<double> velocityDegreesPerSecond,
            double samplingRateHz,
            double startThreshold,
            double endThreshold,
            double refractoryPeriodMilliseconds)
        {
            if (velocityDegreesPerSecond == null)
            {
                throw new ArgumentNullException(nameof(velocityDegreesPerSecond));
            }

            RequirePositive(samplingRateHz, nameof(samplingRateHz));
            RequirePositive(startThreshold, nameof(startThreshold));
            RequirePositive(endThreshold, nameof(endThreshold));
            RequirePositive(refractoryPeriodMilliseconds, nameof(refractoryPeriodMilliseconds));
            if (endThreshold >= startThreshold)
            {
                throw new ArgumentException("End threshold must be below start threshold.");
            }

            var refractorySamples =
                (int)Math.Round(refractoryPeriodMilliseconds * samplingRateHz / 1000.0);
            var raw = new List<P0R5Submovement>();
            int? onset = null;
            for (var index = 0; index < velocityDegreesPerSecond.Count; index++)
            {
                var speed = velocityDegreesPerSecond[index];
                if (double.IsNaN(speed) || double.IsInfinity(speed))
                {
                    throw new ArgumentException("Velocity values must be finite.");
                }

                if (!onset.HasValue && speed >= startThreshold)
                {
                    onset = index;
                }
                else if (onset.HasValue && speed < endThreshold)
                {
                    raw.Add(new P0R5Submovement(onset.Value, index));
                    onset = null;
                }
            }

            if (onset.HasValue)
            {
                raw.Add(new P0R5Submovement(onset.Value, velocityDegreesPerSecond.Count - 1));
            }

            var merged = new List<P0R5Submovement>();
            foreach (var movement in raw)
            {
                if (merged.Count > 0
                    && movement.OnsetSample - merged[merged.Count - 1].EndSample < refractorySamples)
                {
                    var previous = merged[merged.Count - 1];
                    merged[merged.Count - 1] =
                        new P0R5Submovement(previous.OnsetSample, movement.EndSample);
                }
                else
                {
                    merged.Add(movement);
                }
            }

            return merged;
        }

        public static IReadOnlyDictionary<string, int> ShotConditionCounts(int repetitionOrdinal)
        {
            if (repetitionOrdinal <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitionOrdinal));
            }

            var conditions = new List<string>();
            for (var distance = 0; distance < 3; distance++)
            {
                for (var size = 0; size < 3; size++)
                {
                    conditions.Add($"d{distance}-s{size}");
                }
            }

            var counts = new Dictionary<string, int>();
            foreach (var condition in conditions)
            {
                counts[condition] = 3;
            }

            var extraStart = ((repetitionOrdinal - 1) * 3) % conditions.Count;
            for (var offset = 0; offset < 3; offset++)
            {
                counts[conditions[(extraStart + offset) % conditions.Count]]++;
            }

            return counts;
        }

        public static (double HorizontalDegrees, double VerticalDegrees) TrackingPosition(
            string pattern,
            double timeSeconds)
        {
            RequireFiniteNonNegative(timeSeconds, nameof(timeSeconds));
            if (pattern == "linear")
            {
                var phase = timeSeconds % 4.0 / 4.0;
                var horizontal = phase < 0.5 ? -15.0 + 60.0 * phase : 45.0 - 60.0 * phase;
                return (horizontal, 0.0);
            }

            if (pattern == "curved")
            {
                var phase = 2.0 * Math.PI * timeSeconds / 6.0;
                return (15.0 * Math.Cos(phase), 10.0 * Math.Sin(phase));
            }

            if (pattern == "variable_speed")
            {
                var phase = 2.0 * Math.PI * timeSeconds / 4.0;
                return (15.0 * Math.Sin(phase), 0.0);
            }

            throw new ArgumentException("Unknown Tracking pattern.", nameof(pattern));
        }

        public static IReadOnlyList<P0R5TrackingMetric> TrackingWindowMetrics(
            IReadOnlyList<double> sampleTimesSeconds,
            IReadOnlyList<double> radialErrorDegrees,
            double targetRadiusDegrees,
            double trialDurationSeconds,
            double windowSeconds)
        {
            if (sampleTimesSeconds == null || radialErrorDegrees == null)
            {
                throw new ArgumentNullException();
            }

            if (sampleTimesSeconds.Count != radialErrorDegrees.Count || sampleTimesSeconds.Count < 2)
            {
                throw new ArgumentException("Tracking arrays must be equal and contain boundaries.");
            }

            RequirePositive(targetRadiusDegrees, nameof(targetRadiusDegrees));
            RequirePositive(trialDurationSeconds, nameof(trialDurationSeconds));
            RequirePositive(windowSeconds, nameof(windowSeconds));
            if (Math.Abs(sampleTimesSeconds[0]) > 1e-12
                || Math.Abs(sampleTimesSeconds[sampleTimesSeconds.Count - 1] - trialDurationSeconds) > 1e-12)
            {
                throw new ArgumentException("Tracking samples must cover exact trial boundaries.");
            }

            for (var index = 1; index < sampleTimesSeconds.Count; index++)
            {
                if (sampleTimesSeconds[index] <= sampleTimesSeconds[index - 1])
                {
                    throw new ArgumentException("Tracking timestamps must be strictly increasing.");
                }
            }

            var windowCount = (int)Math.Round(trialDurationSeconds / windowSeconds);
            if (Math.Abs(windowCount * windowSeconds - trialDurationSeconds) > 1e-12)
            {
                throw new ArgumentException("Trial duration must contain whole metric windows.");
            }

            var results = new List<P0R5TrackingMetric>();
            for (var windowIndex = 0; windowIndex < windowCount; windowIndex++)
            {
                var windowStart = windowIndex * windowSeconds;
                var windowEnd = windowStart + windowSeconds;
                var covered = 0.0;
                var onTarget = 0.0;
                var squaredError = 0.0;
                for (var sampleIndex = 0; sampleIndex < sampleTimesSeconds.Count - 1; sampleIndex++)
                {
                    var overlap = Math.Max(
                        0.0,
                        Math.Min(sampleTimesSeconds[sampleIndex + 1], windowEnd)
                        - Math.Max(sampleTimesSeconds[sampleIndex], windowStart));
                    if (overlap == 0.0)
                    {
                        continue;
                    }

                    var error = radialErrorDegrees[sampleIndex];
                    covered += overlap;
                    squaredError += error * error * overlap;
                    if (error <= targetRadiusDegrees)
                    {
                        onTarget += overlap;
                    }
                }

                if (Math.Abs(covered - windowSeconds) > 1e-10)
                {
                    throw new ArgumentException("Tracking window coverage is incomplete.");
                }

                results.Add(
                    new P0R5TrackingMetric(
                        windowIndex,
                        100.0 * onTarget / covered,
                        Math.Sqrt(squaredError / covered)));
            }

            return results;
        }

        private static double[,] CreateSteadyState(IReadOnlyList<P0R5SosSection> sections)
        {
            var states = new double[sections.Count, 2];
            var scale = 1.0;
            for (var index = 0; index < sections.Count; index++)
            {
                var section = sections[index];
                if (Math.Abs(section.A0 - 1.0) > 1e-15)
                {
                    throw new ArgumentException("Canonical SOS requires A0 = 1.");
                }

                var dcGain =
                    (section.B0 + section.B1 + section.B2)
                    / (section.A0 + section.A1 + section.A2);
                states[index, 0] = scale * (dcGain - section.B0);
                states[index, 1] = scale * (section.B2 - section.A2 * dcGain);
                scale *= dcGain;
            }

            return states;
        }

        private static double[] Filter(
            IReadOnlyList<P0R5SosSection> sections,
            IReadOnlyList<double> values,
            double[,] steadyState,
            double initialScale)
        {
            var output = new double[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                output[index] = values[index];
            }

            for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                var section = sections[sectionIndex];
                var z0 = steadyState[sectionIndex, 0] * initialScale;
                var z1 = steadyState[sectionIndex, 1] * initialScale;
                var sectionOutput = new double[output.Length];
                for (var sampleIndex = 0; sampleIndex < output.Length; sampleIndex++)
                {
                    var result = section.B0 * output[sampleIndex] + z0;
                    z0 = section.B1 * output[sampleIndex] - section.A1 * result + z1;
                    z1 = section.B2 * output[sampleIndex] - section.A2 * result;
                    sectionOutput[sampleIndex] = result;
                }

                output = sectionOutput;
            }

            return output;
        }

        private static void RequireSections(IReadOnlyList<P0R5SosSection> sections)
        {
            if (sections == null || sections.Count == 0)
            {
                throw new ArgumentException("At least one SOS section is required.", nameof(sections));
            }
        }

        private static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void RequireFiniteNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
