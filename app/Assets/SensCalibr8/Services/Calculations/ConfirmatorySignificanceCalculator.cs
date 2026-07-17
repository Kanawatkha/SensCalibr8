using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Calculations
{
    public sealed class ConfirmatoryScorePair
    {
        public ConfirmatoryScorePair(int pairIndex, double candidateAScore, double candidateBScore)
        {
            PairIndex = pairIndex > 0 ? pairIndex : throw new ArgumentOutOfRangeException(nameof(pairIndex));
            CandidateAScore = Finite(candidateAScore, nameof(candidateAScore));
            CandidateBScore = Finite(candidateBScore, nameof(candidateBScore));
        }

        public int PairIndex { get; }
        public double CandidateAScore { get; }
        public double CandidateBScore { get; }

        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class ConfirmatorySignificanceResult
    {
        public ConfirmatorySignificanceResult(IReadOnlyList<double> differences, double effectEstimate,
            double confidenceIntervalLower, double confidenceIntervalUpper, int assignmentCount, int extremeCount,
            double pValue, bool isSignificant, string result)
        {
            Differences = differences ?? throw new ArgumentNullException(nameof(differences));
            EffectEstimate = effectEstimate;
            ConfidenceIntervalLower = confidenceIntervalLower;
            ConfidenceIntervalUpper = confidenceIntervalUpper;
            AssignmentCount = assignmentCount;
            ExtremeCount = extremeCount;
            PValue = pValue;
            IsSignificant = isSignificant;
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public IReadOnlyList<double> Differences { get; }
        public double EffectEstimate { get; }
        public double ConfidenceIntervalLower { get; }
        public double ConfidenceIntervalUpper { get; }
        public int AssignmentCount { get; }
        public int ExtremeCount { get; }
        public double PValue { get; }
        public bool IsSignificant { get; }
        public string Result { get; }
    }

    public sealed class ConfirmatorySignificanceCalculator
    {
        private readonly ConfirmatoryStatisticsContract contract;

        public ConfirmatorySignificanceCalculator(ConfirmatoryStatisticsContract contract)
        {
            this.contract = contract ?? throw new ArgumentNullException(nameof(contract));
        }

        public ConfirmatorySignificanceResult Calculate(IReadOnlyList<ConfirmatoryScorePair> pairs)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            if (pairs.Count != contract.FreshPairsRequired)
                throw new InvalidOperationException("The exact fresh confirmatory pair count is required; early stopping is forbidden.");
            int[] indices = pairs.Select(value => value.PairIndex).OrderBy(value => value).ToArray();
            if (!indices.SequenceEqual(Enumerable.Range(1, contract.FreshPairsRequired)))
                throw new InvalidOperationException("Confirmatory pair indices must be complete and unique.");

            ConfirmatoryScorePair[] ordered = pairs.OrderBy(value => value.PairIndex).ToArray();
            double[] differences = ordered.Select(value => value.CandidateAScore - value.CandidateBScore).ToArray();
            double effect = Mean(differences);
            int assignmentCount = 1 << differences.Length;
            int extremeCount = 0;
            for (int mask = 0; mask < assignmentCount; mask++)
            {
                double permutedSum = 0d;
                for (int index = 0; index < differences.Length; index++)
                    permutedSum += (mask & (1 << index)) != 0 ? differences[index] : -differences[index];
                if (Math.Abs(permutedSum / differences.Length) + contract.ComparisonTolerance >= Math.Abs(effect))
                    extremeCount++;
            }

            if (assignmentCount != contract.EnumerationCount)
                throw new InvalidOperationException("Confirmatory enumeration does not match the accepted contract.");
            double pValue = (double)extremeCount / assignmentCount;
            double margin = contract.TCritical * SampleStandardDeviation(differences) / Math.Sqrt(differences.Length);
            bool significant = pValue < contract.Alpha;
            string result = !significant ? "statistical_tie" : effect > 0d ? "candidate_a" : effect < 0d ? "candidate_b" : throw new InvalidOperationException("A significant zero effect is impossible.");
            return new ConfirmatorySignificanceResult(new ReadOnlyCollection<double>(differences), effect,
                effect - margin, effect + margin, assignmentCount, extremeCount, pValue, significant, result);
        }

        private static double Mean(IReadOnlyList<double> values) => values.Sum() / values.Count;

        private static double SampleStandardDeviation(IReadOnlyList<double> values)
        {
            double mean = Mean(values);
            double squared = values.Sum(value => (value - mean) * (value - mean));
            return Math.Sqrt(squared / (values.Count - 1));
        }
    }
}
