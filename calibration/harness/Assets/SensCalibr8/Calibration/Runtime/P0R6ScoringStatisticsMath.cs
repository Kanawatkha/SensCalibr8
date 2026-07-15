using System;
using System.Collections.Generic;

namespace SensCalibr8.Calibration
{
    public sealed class P0R6ConfirmatoryResult
    {
        public P0R6ConfirmatoryResult(
            double[] differences,
            double effectEstimate,
            double confidenceLower,
            double confidenceUpper,
            int assignmentCount,
            int extremeCount,
            double pValue,
            bool isSignificant,
            string result)
        {
            Differences = differences;
            EffectEstimate = effectEstimate;
            ConfidenceLower = confidenceLower;
            ConfidenceUpper = confidenceUpper;
            AssignmentCount = assignmentCount;
            ExtremeCount = extremeCount;
            PValue = pValue;
            IsSignificant = isSignificant;
            Result = result;
        }

        public double[] Differences { get; }
        public double EffectEstimate { get; }
        public double ConfidenceLower { get; }
        public double ConfidenceUpper { get; }
        public int AssignmentCount { get; }
        public int ExtremeCount { get; }
        public double PValue { get; }
        public bool IsSignificant { get; }
        public string Result { get; }
    }

    public static class P0R6ScoringStatisticsContract
    {
        public const string FormulaVersion = "sc8-performance-score-v1";
        public const string NormalizationVersion = "sc8-normalization-v1";
        public const string ConsistencyTierVersion = "sc8-consistency-tier-v1";
        public const string ConfirmatoryContractVersion = "sc8-confirmatory-v1";
        public const double ScoringZeroTolerancePoints = 1e-9;
        public const double SubmovementLower = 1.0;
        public const double SubmovementUpper = 6.0;
        public const int AuthoritativeShotCount = 15;
        public const int AuthoritativeTrackingWindowCount = 54;
        public const int ConfirmatoryPairCount = 10;
        public const double ConfirmatoryAlpha = 0.05;
        public const double TCriticalDf9 = 2.2621571628540993;
        public const double PermutationComparisonTolerance = 1e-12;
    }

    public static class P0R6ScoringStatisticsMath
    {
        public static double NormalizeHigh(double value, double lower, double upper)
        {
            RequireFinite(value, nameof(value));
            ValidateBounds(lower, upper);
            return Clamp((value - lower) / (upper - lower), 0.0, 1.0);
        }

        public static double NormalizeLow(double value, double lower, double upper)
        {
            return 1.0 - NormalizeHigh(value, lower, upper);
        }

        public static double MaximumBoundedSampleStandardDeviation(double upper, int observationCount)
        {
            RequireFinite(upper, nameof(upper));
            if (upper <= 0.0 || observationCount < 2)
            {
                throw new ArgumentOutOfRangeException();
            }

            var lowerCount = observationCount / 2;
            var upperCount = observationCount - lowerCount;
            return upper * Math.Sqrt(
                (double)lowerCount * upperCount / (observationCount * (observationCount - 1)));
        }

        public static double SubmovementPenalty(double count, double lower, double upper)
        {
            return NormalizeHigh(count, lower, upper);
        }

        public static double ShotPerformanceScore(
            double consistency,
            double accuracy,
            double reactionSpeed,
            double precision,
            double submovementPenalty)
        {
            RequireComponent(consistency, nameof(consistency));
            RequireComponent(accuracy, nameof(accuracy));
            RequireComponent(reactionSpeed, nameof(reactionSpeed));
            RequireComponent(precision, nameof(precision));
            RequireComponent(submovementPenalty, nameof(submovementPenalty));
            return 100.0 * (
                consistency * 0.35
                + accuracy * 0.30
                + reactionSpeed * 0.20
                + precision * 0.15
                - submovementPenalty * 0.10);
        }

        public static double TrackingPerformanceScore(
            double consistency,
            double timeOnTarget,
            double precision)
        {
            RequireComponent(consistency, nameof(consistency));
            RequireComponent(timeOnTarget, nameof(timeOnTarget));
            RequireComponent(precision, nameof(precision));
            return 100.0 * (
                consistency * 0.4375 + timeOnTarget * 0.375 + precision * 0.1875);
        }

        public static double BatteryPerformanceScore(IReadOnlyList<double> modeScores)
        {
            if (modeScores == null || modeScores.Count != 4)
            {
                throw new ArgumentException("A complete battery requires four mode scores.");
            }

            var sum = 0.0;
            for (var index = 0; index < modeScores.Count; index++)
            {
                RequireFinite(modeScores[index], "mode score");
                sum += modeScores[index];
            }

            return sum / modeScores.Count;
        }

        public static double SampleStandardDeviation(IReadOnlyList<double> values)
        {
            if (values == null || values.Count < 2)
            {
                throw new ArgumentException("Sample SD requires at least two observations.");
            }

            var mean = Mean(values);
            var squared = 0.0;
            for (var index = 0; index < values.Count; index++)
            {
                var difference = values[index] - mean;
                squared += difference * difference;
            }

            return Math.Sqrt(squared / (values.Count - 1));
        }

        public static string ConsistencyTier(double normalizedConsistency)
        {
            RequireComponent(normalizedConsistency, nameof(normalizedConsistency));
            if (normalizedConsistency >= 0.8) return "S";
            if (normalizedConsistency >= 0.6) return "A";
            if (normalizedConsistency >= 0.4) return "B";
            if (normalizedConsistency >= 0.2) return "C";
            return "D";
        }

        public static string ReactionTimeTier(double milliseconds)
        {
            RequireFinite(milliseconds, nameof(milliseconds));
            if (milliseconds < 0.0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (milliseconds < 200.0) return "S";
            if (milliseconds < 250.0) return "A";
            if (milliseconds < 350.0) return "B";
            if (milliseconds <= 500.0) return "C";
            return "D";
        }

        public static string WorseGrade(string first, string second)
        {
            var order = new Dictionary<string, int>
            {
                { "S", 0 }, { "A", 1 }, { "B", 2 }, { "C", 3 }, { "D", 4 }
            };
            if (!order.ContainsKey(first) || !order.ContainsKey(second))
            {
                throw new ArgumentException("Grade must be S, A, B, C, or D.");
            }

            return order[first] >= order[second] ? first : second;
        }

        public static double? CoefficientOfVariationPercent(
            IReadOnlyList<double> scores,
            double zeroTolerance)
        {
            RequireFinite(zeroTolerance, nameof(zeroTolerance));
            if (zeroTolerance < 0.0) throw new ArgumentOutOfRangeException(nameof(zeroTolerance));
            var mean = Mean(scores);
            if (Math.Abs(mean) <= zeroTolerance) return null;
            return 100.0 * SampleStandardDeviation(scores) / Math.Abs(mean);
        }

        public static P0R6ConfirmatoryResult ExactPairedSignFlipTest(
            IReadOnlyList<double> candidateA,
            IReadOnlyList<double> candidateB,
            double alpha,
            double tCritical,
            double comparisonTolerance)
        {
            if (candidateA == null || candidateB == null
                || candidateA.Count != candidateB.Count || candidateA.Count < 2)
            {
                throw new ArgumentException("Paired arrays must have equal n >= 2.");
            }

            RequireFinite(alpha, nameof(alpha));
            RequireFinite(tCritical, nameof(tCritical));
            RequireFinite(comparisonTolerance, nameof(comparisonTolerance));
            if (alpha <= 0.0 || alpha >= 1.0 || tCritical <= 0.0 || comparisonTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException();
            }

            var differences = new double[candidateA.Count];
            for (var index = 0; index < differences.Length; index++)
            {
                RequireFinite(candidateA[index], "candidate A score");
                RequireFinite(candidateB[index], "candidate B score");
                differences[index] = candidateA[index] - candidateB[index];
            }

            var effect = Mean(differences);
            var assignmentCount = 1 << differences.Length;
            var extremeCount = 0;
            for (var mask = 0; mask < assignmentCount; mask++)
            {
                var permutedSum = 0.0;
                for (var index = 0; index < differences.Length; index++)
                {
                    permutedSum += (mask & (1 << index)) != 0
                        ? differences[index]
                        : -differences[index];
                }

                if (Math.Abs(permutedSum / differences.Length) + comparisonTolerance
                    >= Math.Abs(effect))
                {
                    extremeCount++;
                }
            }

            var pValue = (double)extremeCount / assignmentCount;
            var margin = tCritical * SampleStandardDeviation(differences) / Math.Sqrt(differences.Length);
            var significant = pValue < alpha;
            var result = !significant
                ? "statistical_tie"
                : effect > 0.0
                    ? "candidate_a"
                    : effect < 0.0
                        ? "candidate_b"
                        : throw new InvalidOperationException("Significant zero effect is impossible.");
            return new P0R6ConfirmatoryResult(
                differences,
                effect,
                effect - margin,
                effect + margin,
                assignmentCount,
                extremeCount,
                pValue,
                significant,
                result);
        }

        private static double Mean(IReadOnlyList<double> values)
        {
            if (values == null || values.Count < 2)
            {
                throw new ArgumentException("At least two finite values are required.");
            }

            var sum = 0.0;
            for (var index = 0; index < values.Count; index++)
            {
                RequireFinite(values[index], "value");
                sum += values[index];
            }

            return sum / values.Count;
        }

        private static double Clamp(double value, double lower, double upper)
        {
            return Math.Min(upper, Math.Max(lower, value));
        }

        private static void ValidateBounds(double lower, double upper)
        {
            RequireFinite(lower, nameof(lower));
            RequireFinite(upper, nameof(upper));
            if (upper <= lower) throw new ArgumentException("Normalization requires U > L.");
        }

        private static void RequireComponent(double value, string name)
        {
            RequireFinite(value, name);
            if (value < 0.0 || value > 1.0) throw new ArgumentOutOfRangeException(name);
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException("Value must be finite.", name);
            }
        }
    }
}
