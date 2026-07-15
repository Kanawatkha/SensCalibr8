using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SensCalibr8.Calibration.Tests
{
    public sealed class P0R6ScoringStatisticsParityTests
    {
        [Test]
        public void NormalizationAndSubmovementBoundariesMatchPython()
        {
            Assert.That(P0R6ScoringStatisticsMath.NormalizeHigh(-1, 0, 100), Is.EqualTo(0));
            Assert.That(P0R6ScoringStatisticsMath.NormalizeHigh(50, 0, 100), Is.EqualTo(0.5));
            Assert.That(P0R6ScoringStatisticsMath.NormalizeHigh(101, 0, 100), Is.EqualTo(1));
            Assert.That(P0R6ScoringStatisticsMath.NormalizeLow(-1, 0, 100), Is.EqualTo(1));
            Assert.That(P0R6ScoringStatisticsMath.NormalizeLow(50, 0, 100), Is.EqualTo(0.5));
            Assert.That(P0R6ScoringStatisticsMath.NormalizeLow(101, 0, 100), Is.EqualTo(0));
            Assert.That(P0R6ScoringStatisticsMath.SubmovementPenalty(0, 1, 6), Is.EqualTo(0));
            Assert.That(P0R6ScoringStatisticsMath.SubmovementPenalty(3.5, 1, 6), Is.EqualTo(0.5));
            Assert.That(P0R6ScoringStatisticsMath.SubmovementPenalty(7, 1, 6), Is.EqualTo(1));
        }

        [Test]
        public void DerivedConsistencyBoundsMatchPythonEvidence()
        {
            ScoringEvidence evidence = LoadEvidence();
            Assert.That(
                P0R6ScoringStatisticsMath.MaximumBoundedSampleStandardDeviation(15, 15),
                Is.EqualTo(evidence.derived_bounds.flick_close.consistency_upper).Within(1e-14));
            Assert.That(
                P0R6ScoringStatisticsMath.MaximumBoundedSampleStandardDeviation(40, 15),
                Is.EqualTo(evidence.derived_bounds.flick_far.consistency_upper).Within(1e-14));
            Assert.That(
                P0R6ScoringStatisticsMath.MaximumBoundedSampleStandardDeviation(
                    evidence.derived_bounds.micro_correction.precision_upper, 15),
                Is.EqualTo(evidence.derived_bounds.micro_correction.consistency_upper).Within(1e-14));
            Assert.That(
                P0R6ScoringStatisticsMath.MaximumBoundedSampleStandardDeviation(15, 54),
                Is.EqualTo(evidence.derived_bounds.tracking.consistency_upper).Within(1e-14));
        }

        [Test]
        public void WorkedScoresAndUnclampedRangeMatchPythonEvidence()
        {
            ScoringEvidence evidence = LoadEvidence();
            Assert.That(
                P0R6ScoringStatisticsMath.ShotPerformanceScore(0.8, 0.9, 0.75, 0.6, 0.2),
                Is.EqualTo(evidence.fixtures.shot_worked_score).Within(1e-12));
            Assert.That(
                P0R6ScoringStatisticsMath.TrackingPerformanceScore(0.8, 0.9, 0.7),
                Is.EqualTo(evidence.fixtures.tracking_worked_score).Within(1e-12));
            Assert.That(
                P0R6ScoringStatisticsMath.ShotPerformanceScore(0, 0, 0, 0, 1),
                Is.EqualTo(-10).Within(1e-12));
            Assert.That(
                P0R6ScoringStatisticsMath.ShotPerformanceScore(1, 1, 1, 1, 0),
                Is.EqualTo(100).Within(1e-12));
            Assert.That(
                P0R6ScoringStatisticsMath.BatteryPerformanceScore(new[] { 60.0, 70.0, 80.0, 90.0 }),
                Is.EqualTo(75));
        }

        [Test]
        public void GradeBoundariesAreExhaustiveAndWorseTierWins()
        {
            Assert.That(P0R6ScoringStatisticsMath.ReactionTimeTier(199.999), Is.EqualTo("S"));
            Assert.That(P0R6ScoringStatisticsMath.ReactionTimeTier(200), Is.EqualTo("A"));
            Assert.That(P0R6ScoringStatisticsMath.ReactionTimeTier(250), Is.EqualTo("B"));
            Assert.That(P0R6ScoringStatisticsMath.ReactionTimeTier(350), Is.EqualTo("C"));
            Assert.That(P0R6ScoringStatisticsMath.ReactionTimeTier(500), Is.EqualTo("C"));
            Assert.That(P0R6ScoringStatisticsMath.ReactionTimeTier(500.001), Is.EqualTo("D"));
            Assert.That(P0R6ScoringStatisticsMath.ConsistencyTier(0.8), Is.EqualTo("S"));
            Assert.That(P0R6ScoringStatisticsMath.ConsistencyTier(0.6), Is.EqualTo("A"));
            Assert.That(P0R6ScoringStatisticsMath.ConsistencyTier(0.4), Is.EqualTo("B"));
            Assert.That(P0R6ScoringStatisticsMath.ConsistencyTier(0.2), Is.EqualTo("C"));
            Assert.That(P0R6ScoringStatisticsMath.ConsistencyTier(0.199), Is.EqualTo("D"));
            Assert.That(P0R6ScoringStatisticsMath.WorseGrade("A", "C"), Is.EqualTo("C"));
        }

        [Test]
        public void CvUsesSampleSdAndFrozenZeroTolerance()
        {
            double? cv = P0R6ScoringStatisticsMath.CoefficientOfVariationPercent(
                new[] { 95.0, 100.0, 105.0 },
                P0R6ScoringStatisticsContract.ScoringZeroTolerancePoints);
            Assert.That(cv.HasValue, Is.True);
            Assert.That(cv.Value, Is.EqualTo(5).Within(1e-12));
            Assert.That(
                P0R6ScoringStatisticsMath.CoefficientOfVariationPercent(
                    new[] { -1e-10, 1e-10 },
                    P0R6ScoringStatisticsContract.ScoringZeroTolerancePoints),
                Is.Null);
        }

        [Test]
        public void ExactPositiveFixtureMatchesAllPythonStatistics()
        {
            ScoringEvidence evidence = LoadEvidence();
            var result = P0R6ScoringStatisticsMath.ExactPairedSignFlipTest(
                Repeat(75),
                Repeat(70),
                P0R6ScoringStatisticsContract.ConfirmatoryAlpha,
                P0R6ScoringStatisticsContract.TCriticalDf9,
                P0R6ScoringStatisticsContract.PermutationComparisonTolerance);
            Assert.That(result.AssignmentCount, Is.EqualTo(1024));
            Assert.That(result.ExtremeCount, Is.EqualTo(2));
            Assert.That(result.PValue, Is.EqualTo(evidence.fixtures.confirmatory_positive.p_value));
            Assert.That(result.EffectEstimate, Is.EqualTo(5));
            Assert.That(result.ConfidenceLower, Is.EqualTo(5));
            Assert.That(result.ConfidenceUpper, Is.EqualTo(5));
            Assert.That(result.Result, Is.EqualTo("candidate_a"));
        }

        [Test]
        public void ExactSymmetricFixtureReturnsStatisticalTie()
        {
            var result = P0R6ScoringStatisticsMath.ExactPairedSignFlipTest(
                new[] { 71.0, 69, 72, 68, 73, 67, 74, 66, 75, 65 },
                Repeat(70),
                P0R6ScoringStatisticsContract.ConfirmatoryAlpha,
                P0R6ScoringStatisticsContract.TCriticalDf9,
                P0R6ScoringStatisticsContract.PermutationComparisonTolerance);
            Assert.That(result.EffectEstimate, Is.EqualTo(0));
            Assert.That(result.PValue, Is.EqualTo(1));
            Assert.That(result.Result, Is.EqualTo("statistical_tie"));
        }

        [Test]
        public void PlainMathRejectsInvalidOrIncompleteInputs()
        {
            Assert.Throws<ArgumentException>(() => P0R6ScoringStatisticsMath.NormalizeHigh(1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => P0R6ScoringStatisticsMath.ShotPerformanceScore(1.01, 1, 1, 1, 0));
            Assert.Throws<ArgumentException>(
                () => P0R6ScoringStatisticsMath.BatteryPerformanceScore(new[] { 1.0, 2.0, 3.0 }));
            Assert.Throws<ArgumentException>(
                () => P0R6ScoringStatisticsMath.ExactPairedSignFlipTest(
                    new[] { 1.0, 2.0 },
                    new[] { 1.0 },
                    0.05,
                    2.0,
                    1e-12));
        }

        private static double[] Repeat(double value)
        {
            return new[] { value, value, value, value, value, value, value, value, value, value };
        }

        private static ScoringEvidence LoadEvidence()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "evidence",
                "p0-r6",
                "p0-r6-scoring-statistics-derived-v1.json"));
            Assert.That(File.Exists(path), Is.True, path);
            ScoringEvidence evidence = JsonUtility.FromJson<ScoringEvidence>(File.ReadAllText(path));
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence.accepted, Is.True);
            return evidence;
        }

        [Serializable]
        private sealed class ScoringEvidence
        {
            public bool accepted;
            public DerivedBounds derived_bounds;
            public Fixtures fixtures;
        }

        [Serializable]
        private sealed class DerivedBounds
        {
            public BoundPair flick_close;
            public BoundPair flick_far;
            public BoundPair micro_correction;
            public BoundPair tracking;
        }

        [Serializable]
        private sealed class BoundPair
        {
            public double precision_upper;
            public double consistency_upper;
        }

        [Serializable]
        private sealed class Fixtures
        {
            public double shot_worked_score;
            public double tracking_worked_score;
            public ConfirmatoryFixture confirmatory_positive;
        }

        [Serializable]
        private sealed class ConfirmatoryFixture
        {
            public double p_value;
        }
    }
}
