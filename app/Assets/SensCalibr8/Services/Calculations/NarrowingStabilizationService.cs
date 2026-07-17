using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Calculations
{
    public enum NarrowingStabilizationState
    {
        Collecting,
        RequiresMoreEvidence,
        Stabilized,
        MaximumReachedWithoutStabilization
    }

    public sealed class NarrowingCandidateEvaluation
    {
        public NarrowingCandidateEvaluation(long candidateId, int completedBatteryCount, double? meanScore,
            double? sampleStandardDeviation, double? coefficientOfVariationPercent, NarrowingStabilizationState state)
        {
            CandidateId = candidateId;
            CompletedBatteryCount = completedBatteryCount;
            MeanScore = meanScore;
            SampleStandardDeviation = sampleStandardDeviation;
            CoefficientOfVariationPercent = coefficientOfVariationPercent;
            State = state;
        }

        public long CandidateId { get; }
        public int CompletedBatteryCount { get; }
        public double? MeanScore { get; }
        public double? SampleStandardDeviation { get; }
        public double? CoefficientOfVariationPercent { get; }
        public NarrowingStabilizationState State { get; }
        public bool CanCollectAnother => State == NarrowingStabilizationState.Collecting || State == NarrowingStabilizationState.RequiresMoreEvidence;
    }

    public sealed class PhaseTwoStabilizationEvaluation
    {
        public PhaseTwoStabilizationEvaluation(IReadOnlyList<NarrowingCandidateEvaluation> candidates)
        {
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        }

        public IReadOnlyList<NarrowingCandidateEvaluation> Candidates { get; }
        public bool AllCandidatesStabilized => Candidates.Count > 0 && Candidates.All(value => value.State == NarrowingStabilizationState.Stabilized);
        public bool HasMaximumFailure => Candidates.Any(value => value.State == NarrowingStabilizationState.MaximumReachedWithoutStabilization);
    }

    public sealed class NarrowingStabilizationService
    {
        private readonly NarrowingRepository repository;
        private readonly CalibrationConfigurationRepository configurations;
        private readonly FrozenCalibrationConfiguration configuration;
        private readonly PhaseTwoProtocolContract contract;

        public NarrowingStabilizationService(NarrowingRepository repository,
            CalibrationConfigurationRepository configurations, FrozenCalibrationConfiguration configuration,
            PhaseTwoProtocolContract contract)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.configurations = configurations ?? throw new ArgumentNullException(nameof(configurations));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.contract = contract ?? throw new ArgumentNullException(nameof(contract));
        }

        public PhaseTwoStabilizationEvaluation Evaluate(long profileId, long cycleId)
        {
            return EvaluatePhase(profileId, cycleId, ProtocolPhase.PhaseTwo);
        }

        public PhaseTwoStabilizationEvaluation EvaluatePhase(long profileId, long cycleId, ProtocolPhase phase)
        {
            IReadOnlyList<ProtocolCandidateRecord> candidates = repository.ListPhaseCandidates(profileId, cycleId, (long)phase);
            if (candidates.Count == 0) throw new InvalidOperationException("Narrowing candidates do not exist.");
            long configId = configurations.RequireId(configuration.ConfigVersion.Value);
            var results = candidates.Select(candidate => Evaluate(candidate,
                repository.ListCompletedNarrowingScores(candidate, configId, configuration.FormulaVersion.Value))).ToArray();
            return new PhaseTwoStabilizationEvaluation(new ReadOnlyCollection<NarrowingCandidateEvaluation>(results));
        }

        private NarrowingCandidateEvaluation Evaluate(ProtocolCandidateRecord candidate, IReadOnlyList<double> scores)
        {
            if (scores.Count > contract.MaximumCompleteBatteries)
                throw new InvalidOperationException("Phase 2 evidence exceeds the accepted maximum.");
            if (scores.Count < contract.MinimumCompleteBatteries)
                return new NarrowingCandidateEvaluation(candidate.Id.Value, scores.Count, null, null, null,
                    NarrowingStabilizationState.Collecting);
            double mean = scores.Average();
            double squared = scores.Sum(value => (value - mean) * (value - mean));
            double sampleSd = Math.Sqrt(squared / (scores.Count - 1));
            double? cv = Math.Abs(mean) <= configuration.Record.ScoringZeroTolerance
                ? (double?)null
                : 100d * sampleSd / Math.Abs(mean);
            bool stabilized = cv.HasValue && cv.Value < contract.StabilizationCvExclusiveUpper;
            NarrowingStabilizationState state = stabilized
                ? NarrowingStabilizationState.Stabilized
                : scores.Count < contract.MaximumCompleteBatteries
                    ? NarrowingStabilizationState.RequiresMoreEvidence
                    : NarrowingStabilizationState.MaximumReachedWithoutStabilization;
            return new NarrowingCandidateEvaluation(candidate.Id.Value, scores.Count, mean, sampleSd, cv, state);
        }
    }
}
