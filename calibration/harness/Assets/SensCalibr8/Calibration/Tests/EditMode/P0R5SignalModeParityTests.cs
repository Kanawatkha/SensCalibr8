using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SensCalibr8.Calibration.Tests
{
    public sealed class P0R5SignalModeParityTests
    {
        [Test]
        public void FrozenSosMatchesPythonEvidenceAndButterworthResponse()
        {
            SignalModeEvidence evidence = LoadEvidence();
            P0R5SosSection[] sections = P0R5SignalModeContract.CreateSections();
            Assert.That(sections.Length, Is.EqualTo(evidence.signal.sos_sections.Length));
            for (var index = 0; index < sections.Length; index++)
            {
                SosSection expected = evidence.signal.sos_sections[index];
                Assert.That(sections[index].B0, Is.EqualTo(expected.b0).Within(1e-18));
                Assert.That(sections[index].B1, Is.EqualTo(expected.b1).Within(1e-18));
                Assert.That(sections[index].B2, Is.EqualTo(expected.b2).Within(1e-18));
                Assert.That(sections[index].A0, Is.EqualTo(expected.a0).Within(1e-18));
                Assert.That(sections[index].A1, Is.EqualTo(expected.a1).Within(1e-15));
                Assert.That(sections[index].A2, Is.EqualTo(expected.a2).Within(1e-15));
            }

            Assert.That(P0R5SignalModeMath.DefaultPadLength(sections), Is.EqualTo(18));
            Assert.That(
                P0R5SignalModeMath.FrequencyMagnitude(sections, 0.0, 1000.0),
                Is.EqualTo(1.0).Within(1e-12));
            Assert.That(
                P0R5SignalModeMath.FrequencyMagnitude(sections, 7.0, 1000.0),
                Is.EqualTo(Math.Sqrt(0.5)).Within(1e-10));
        }

        [Test]
        public void ForwardBackwardFilterPreservesImpulsePeakSample()
        {
            var impulse = new double[2001];
            impulse[1000] = 1.0;
            double[] filtered = P0R5SignalModeMath.FilterForwardBackward(
                P0R5SignalModeContract.CreateSections(),
                impulse,
                P0R5SignalModeContract.PadLengthSamples);
            var peakIndex = 0;
            var peakMagnitude = 0.0;
            for (var index = 0; index < filtered.Length; index++)
            {
                if (Math.Abs(filtered[index]) > peakMagnitude)
                {
                    peakIndex = index;
                    peakMagnitude = Math.Abs(filtered[index]);
                }
            }

            Assert.That(peakIndex, Is.EqualTo(1000));
        }

        [Test]
        public void SignalBoundaryAndRefractorySemanticsMatchPython()
        {
            var threshold = new double[220];
            for (var index = 20; index < 80; index++)
            {
                threshold[index] = 8.0;
            }

            IReadOnlyList<P0R5Submovement> thresholdEvents = Detect(threshold);
            Assert.That(thresholdEvents.Count, Is.EqualTo(1));
            Assert.That(thresholdEvents[0].OnsetSample, Is.EqualTo(20));
            Assert.That(thresholdEvents[0].EndSample, Is.EqualTo(80));

            var merged = new double[300];
            Fill(merged, 10, 20, 9.0);
            Fill(merged, 50, 60, 9.0);
            Assert.That(Detect(merged).Count, Is.EqualTo(1));

            var exact = new double[300];
            Fill(exact, 10, 20, 9.0);
            Fill(exact, 100, 110, 9.0);
            Assert.That(Detect(exact).Count, Is.EqualTo(2));
        }

        [Test]
        public void ShotConditionsRemainBalancedAcrossRotatingOrdinals()
        {
            for (var ordinal = 1; ordinal <= 9; ordinal++)
            {
                IReadOnlyDictionary<string, int> counts =
                    P0R5SignalModeMath.ShotConditionCounts(ordinal);
                Assert.That(counts.Count, Is.EqualTo(9));
                Assert.That(counts.Values.Sum(), Is.EqualTo(30));
                Assert.That(counts.Values.Min(), Is.EqualTo(3));
                Assert.That(counts.Values.Max(), Is.EqualTo(4));
            }
        }

        [Test]
        public void TrackingPathsMatchFrozenPythonKeyPoints()
        {
            var linearStart = P0R5SignalModeMath.TrackingPosition("linear", 0.0);
            var linearMid = P0R5SignalModeMath.TrackingPosition("linear", 2.0);
            var curvedQuarter = P0R5SignalModeMath.TrackingPosition("curved", 1.5);
            var variableQuarter = P0R5SignalModeMath.TrackingPosition("variable_speed", 1.0);

            Assert.That(linearStart.HorizontalDegrees, Is.EqualTo(-15.0).Within(1e-12));
            Assert.That(linearMid.HorizontalDegrees, Is.EqualTo(15.0).Within(1e-12));
            Assert.That(curvedQuarter.HorizontalDegrees, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(curvedQuarter.VerticalDegrees, Is.EqualTo(10.0).Within(1e-12));
            Assert.That(variableQuarter.HorizontalDegrees, Is.EqualTo(15.0).Within(1e-12));
        }

        [Test]
        public void TrackingMetricsAreInvariantToEquivalentFramePartitions()
        {
            double[] regularTimes = Enumerable.Range(0, 25).Select(index => index * 0.25).ToArray();
            double[] regularErrors = regularTimes.Select(value => value < 3.0 ? 0.25 : 1.0).ToArray();
            double[] irregularTimes =
                { 0.0, 0.1, 0.7, 1.0, 1.8, 2.0, 2.6, 3.0, 3.4, 4.0, 4.9, 5.0, 5.8, 6.0 };
            double[] irregularErrors = irregularTimes.Select(value => value < 3.0 ? 0.25 : 1.0).ToArray();
            IReadOnlyList<P0R5TrackingMetric> regular = P0R5SignalModeMath.TrackingWindowMetrics(
                regularTimes, regularErrors, 0.5, 6.0, 1.0);
            IReadOnlyList<P0R5TrackingMetric> irregular = P0R5SignalModeMath.TrackingWindowMetrics(
                irregularTimes, irregularErrors, 0.5, 6.0, 1.0);

            Assert.That(regular.Count, Is.EqualTo(6));
            for (var index = 0; index < regular.Count; index++)
            {
                Assert.That(
                    regular[index].TimeOnTargetPercent,
                    Is.EqualTo(irregular[index].TimeOnTargetPercent).Within(1e-10));
                Assert.That(
                    regular[index].DeviationRmsDegrees,
                    Is.EqualTo(irregular[index].DeviationRmsDegrees).Within(1e-10));
            }
        }

        [Test]
        public void FrozenCompletionCountsMatchEvidence()
        {
            SignalModeEvidence evidence = LoadEvidence();
            Assert.That(P0R5SignalModeContract.ShotTrialCount, Is.EqualTo(30));
            Assert.That(P0R5SignalModeContract.ShotAdaptationCount, Is.EqualTo(15));
            Assert.That(P0R5SignalModeContract.TrackingTrialCount, Is.EqualTo(18));
            Assert.That(P0R5SignalModeContract.TrackingPostAdaptationWindowCount, Is.EqualTo(54));
            Assert.That(evidence.accepted, Is.True);
        }

        [Test]
        public void PlainSignalModeMathRejectsInvalidInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => P0R5SignalModeMath.ShotConditionCounts(0));
            Assert.Throws<ArgumentException>(
                () => P0R5SignalModeMath.TrackingPosition("unknown", 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => P0R5SignalModeMath.FilterForwardBackward(
                    P0R5SignalModeContract.CreateSections(), new double[19], 18));
        }

        private static IReadOnlyList<P0R5Submovement> Detect(double[] velocity)
        {
            return P0R5SignalModeMath.DetectSubmovements(
                velocity,
                P0R5SignalModeContract.SamplingRateHz,
                P0R5SignalModeContract.StartThresholdDegreesPerSecond,
                P0R5SignalModeContract.EndThresholdDegreesPerSecond,
                P0R5SignalModeContract.RefractoryPeriodMilliseconds);
        }

        private static void Fill(double[] values, int start, int end, double value)
        {
            for (var index = start; index < end; index++)
            {
                values[index] = value;
            }
        }

        private static SignalModeEvidence LoadEvidence()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "evidence",
                "p0-r5",
                "p0-r5-signal-mode-derived-v1.json"));
            Assert.That(File.Exists(path), Is.True, path);
            SignalModeEvidence evidence = JsonUtility.FromJson<SignalModeEvidence>(File.ReadAllText(path));
            Assert.That(evidence, Is.Not.Null);
            return evidence;
        }

        [Serializable]
        private sealed class SignalModeEvidence
        {
            public bool accepted;
            public SignalEvidence signal;
        }

        [Serializable]
        private sealed class SignalEvidence
        {
            public SosSection[] sos_sections;
        }

        [Serializable]
        private sealed class SosSection
        {
            public double b0;
            public double b1;
            public double b2;
            public double a0;
            public double a1;
            public double a2;
        }
    }
}
