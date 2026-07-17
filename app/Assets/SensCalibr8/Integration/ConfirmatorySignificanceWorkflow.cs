using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;

namespace SensCalibr8.Integration
{
    public sealed class ConfirmatoryCandidateSelection
    {
        internal ConfirmatoryCandidateSelection(RankedProtocolCandidate candidateA, RankedProtocolCandidate candidateB)
        {
            CandidateA = candidateA ?? throw new ArgumentNullException(nameof(candidateA));
            CandidateB = candidateB ?? throw new ArgumentNullException(nameof(candidateB));
        }

        internal RankedProtocolCandidate CandidateA { get; }
        internal RankedProtocolCandidate CandidateB { get; }
        public long CandidateAId => CandidateA.Candidate.Id.Value;
        public long CandidateBId => CandidateB.Candidate.Id.Value;
        public string CandidateALabel => "Candidate-A";
        public string CandidateBLabel => "Candidate-B";
    }

    public sealed class ConfirmatoryPairLaunch
    {
        internal ConfirmatoryPairLaunch(ConfirmatoryPairPlan plan, ConfirmatoryBatteryPairRecord batteries)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Batteries = batteries ?? throw new ArgumentNullException(nameof(batteries));
        }

        internal ConfirmatoryPairPlan Plan { get; }
        internal ConfirmatoryBatteryPairRecord Batteries { get; }
        public int PairIndex => Plan.PairIndex;
        public string FirstCandidateLabel => Plan.FirstCandidate == "A_then_B" ? "Candidate-A" : "Candidate-B";
        public string PairingSeed => Plan.PairingSeed;
        public string MatchedConditionKey => Plan.MatchedConditionKey;
        public long CandidateABatteryId => Batteries.CandidateA.Id.Value;
        public long CandidateBBatteryId => Batteries.CandidateB.Id.Value;
    }

    public sealed class CompletedConfirmatoryPair
    {
        public CompletedConfirmatoryPair(ConfirmatoryPairLaunch launch, double candidateAScore, double candidateBScore)
        {
            Launch = launch ?? throw new ArgumentNullException(nameof(launch));
            CandidateAScore = Finite(candidateAScore, nameof(candidateAScore));
            CandidateBScore = Finite(candidateBScore, nameof(candidateBScore));
        }

        public ConfirmatoryPairLaunch Launch { get; }
        public double CandidateAScore { get; }
        public double CandidateBScore { get; }

        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class ConfirmatorySignificanceWorkflow
    {
        private readonly ConfirmatoryRepository repository;
        private readonly CalibrationConfigurationRepository configurations;
        private readonly FrozenCalibrationConfiguration configuration;
        private readonly ConfirmatoryStatisticsContract statisticsContract;
        private readonly ConfirmatoryOrderContract orderContract;
        private readonly ConfirmatorySignificanceCalculator calculator;

        public ConfirmatorySignificanceWorkflow(ConfirmatoryRepository repository,
            CalibrationConfigurationRepository configurations, FrozenCalibrationConfiguration configuration)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.configurations = configurations ?? throw new ArgumentNullException(nameof(configurations));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            statisticsContract = ConfirmatoryStatisticsContractLoader.From(configuration);
            orderContract = ConfirmatoryOrderContract.From(configuration);
            calculator = new ConfirmatorySignificanceCalculator(statisticsContract);
            if (!string.Equals(statisticsContract.Version, orderContract.Version, StringComparison.Ordinal))
                throw new InvalidOperationException("Confirmatory order and statistics versions do not match.");
        }

        public ConfirmatoryCandidateSelection SelectTopTwo(long profileId, long cycleId, ProtocolPhase phase)
        {
            IReadOnlyList<RankedProtocolCandidate> ranked = repository.SelectTopExploratoryCandidates(
                profileId, cycleId, (long)phase, 2);
            if (ranked.Count != 2) throw new InvalidOperationException("Exactly two scored exploratory candidates are required.");
            return new ConfirmatoryCandidateSelection(ranked[0], ranked[1]);
        }

        public ConfirmatoryPairLaunch LaunchPair(ConfirmatoryCandidateSelection selection, int pairIndex, string startedDate)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            ProtocolCandidateRecord candidateA = selection.CandidateA.Candidate;
            ProtocolCandidateRecord candidateB = selection.CandidateB.Candidate;
            ConfirmatoryPairPlan plan = orderContract.CreatePairPlan(candidateA.ProfileId, candidateA.CycleId,
                (ProtocolPhase)candidateA.Phase, candidateA.Edpi, candidateB.Edpi, pairIndex);
            ConfirmatoryBatteryPairRecord batteries = repository.CreateBatteryPair(candidateA, candidateB, startedDate);
            return new ConfirmatoryPairLaunch(plan, batteries);
        }

        public PersistedSignificanceTest CompleteAndPersist(ConfirmatoryCandidateSelection selection,
            IReadOnlyList<CompletedConfirmatoryPair> completedPairs)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (completedPairs == null) throw new ArgumentNullException(nameof(completedPairs));
            ProtocolCandidateRecord candidateA = selection.CandidateA.Candidate;
            ProtocolCandidateRecord candidateB = selection.CandidateB.Candidate;
            var scorePairs = new List<ConfirmatoryScorePair>(completedPairs.Count);
            var storedPairs = new List<SignificanceTestPairRecord>(completedPairs.Count);
            foreach (CompletedConfirmatoryPair completed in completedPairs)
            {
                if (completed == null) throw new ArgumentException("Completed pairs cannot contain null.", nameof(completedPairs));
                ConfirmatoryPairLaunch launch = completed.Launch;
                ConfirmatoryPairPlan expected = orderContract.CreatePairPlan(candidateA.ProfileId, candidateA.CycleId,
                    (ProtocolPhase)candidateA.Phase, candidateA.Edpi, candidateB.Edpi, launch.PairIndex);
                if (launch.Batteries.CandidateA.CandidateId != candidateA.Id.Value ||
                    launch.Batteries.CandidateB.CandidateId != candidateB.Id.Value ||
                    !string.Equals(launch.Plan.FirstCandidate, expected.FirstCandidate, StringComparison.Ordinal) ||
                    !string.Equals(launch.PairingSeed, expected.PairingSeed, StringComparison.Ordinal) ||
                    !string.Equals(launch.MatchedConditionKey, expected.MatchedConditionKey, StringComparison.Ordinal))
                    throw new InvalidOperationException("Confirmatory pair plan lineage is invalid.");
                scorePairs.Add(new ConfirmatoryScorePair(launch.PairIndex, completed.CandidateAScore, completed.CandidateBScore));
                storedPairs.Add(new SignificanceTestPairRecord(launch.PairIndex,
                    expected.FirstCandidate == "A_then_B" ? "A" : "B", expected.PairingSeed,
                    expected.MatchedConditionKey, launch.CandidateABatteryId, launch.CandidateBBatteryId,
                    completed.CandidateAScore, completed.CandidateBScore));
            }

            ConfirmatorySignificanceResult result = calculator.Calculate(scorePairs);
            long configId = configurations.RequireId(configuration.ConfigVersion.Value);
            var record = new SignificanceTestRecord(null, candidateA.ProfileId, candidateA.CycleId, configId,
                candidateA.Phase, candidateA.Edpi, candidateB.Edpi, statisticsContract.TestMethod,
                statisticsContract.Alternative, statisticsContract.Alpha, result.PValue, result.EffectEstimate,
                statisticsContract.ConfidenceLevel, result.ConfidenceIntervalLower, result.ConfidenceIntervalUpper,
                completedPairs.Count, result.IsSignificant, configuration.FormulaVersion.Value, result.Result);
            return repository.CreateWithPairs(record, storedPairs);
        }
    }
}
