using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Calculations
{
    public sealed class NarrowingWinnerSelection
    {
        public NarrowingWinnerSelection(ProtocolPhase phase, double highestMeanScore,
            IReadOnlyList<long> tiedCandidateIds, PhaseHistoryRecord persistedWinner)
        {
            Phase = phase;
            HighestMeanScore = highestMeanScore;
            TiedCandidateIds = tiedCandidateIds ?? throw new ArgumentNullException(nameof(tiedCandidateIds));
            PersistedWinner = persistedWinner;
        }

        public ProtocolPhase Phase { get; }
        public double HighestMeanScore { get; }
        public IReadOnlyList<long> TiedCandidateIds { get; }
        public PhaseHistoryRecord PersistedWinner { get; }
        public bool IsExactMeanTie => TiedCandidateIds.Count > 1;
        public bool HasWinner => PersistedWinner != null;
    }

    public sealed class NarrowingWinnerSelectionService
    {
        private readonly NarrowingStabilizationService stabilization;
        private readonly NarrowingRepository candidates;
        private readonly PhaseHistoryRepository history;

        public NarrowingWinnerSelectionService(NarrowingStabilizationService stabilization,
            NarrowingRepository candidates, PhaseHistoryRepository history)
        {
            this.stabilization = stabilization ?? throw new ArgumentNullException(nameof(stabilization));
            this.candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            this.history = history ?? throw new ArgumentNullException(nameof(history));
        }

        public NarrowingWinnerSelection SelectAndPersist(long profileId, long cycleId, ProtocolPhase phase, string timestamp)
        {
            PhaseTwoStabilizationEvaluation evaluation = stabilization.EvaluatePhase(profileId, cycleId, phase);
            if (!evaluation.AllCandidatesStabilized || evaluation.HasMaximumFailure)
                throw new InvalidOperationException("Every narrowing candidate must stabilize before winner selection.");
            double highest = evaluation.Candidates.Max(value => value.MeanScore.Value);
            long[] tiedIds = evaluation.Candidates.Where(value => value.MeanScore.Value.Equals(highest))
                .Select(value => value.CandidateId).OrderBy(value => value).ToArray();
            if (tiedIds.Length > 1) return new NarrowingWinnerSelection(phase, highest, tiedIds, null);
            ProtocolCandidateRecord candidate = candidates.ListPhaseCandidates(profileId, cycleId, (long)phase)
                .Single(value => value.Id.Value == tiedIds[0]);
            PhaseHistoryRecord persisted = history.Create(new PhaseHistoryRecord(null, profileId, cycleId,
                (long)phase, candidate.Edpi, timestamp));
            return new NarrowingWinnerSelection(phase, highest, tiedIds, persisted);
        }
    }
}
