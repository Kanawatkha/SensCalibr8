using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Calculations;
using SensCalibr8.TestLogic;

namespace SensCalibr8.Integration
{
    public sealed class BlindNarrowingCandidate
    {
        public BlindNarrowingCandidate(long candidateId, string blindLabel, int orderIndex)
        {
            CandidateId = candidateId;
            BlindLabel = blindLabel;
            OrderIndex = orderIndex;
        }

        public long CandidateId { get; }
        public string BlindLabel { get; }
        public int OrderIndex { get; }
    }

    public sealed class PhaseTwoNarrowingPlan
    {
        internal PhaseTwoNarrowingPlan(long sourceSignificanceTestId,
            IReadOnlyList<ProtocolCandidateRecord> persistedCandidates,
            IReadOnlyList<BlindNarrowingCandidate> blindCandidates,
            IReadOnlyList<EdpiFloorNotification> notifications)
        {
            SourceSignificanceTestId = sourceSignificanceTestId;
            PersistedCandidates = persistedCandidates;
            Candidates = blindCandidates;
            FloorNotifications = notifications;
        }

        internal IReadOnlyList<ProtocolCandidateRecord> PersistedCandidates { get; }
        public long SourceSignificanceTestId { get; }
        public IReadOnlyList<BlindNarrowingCandidate> Candidates { get; }
        public IReadOnlyList<EdpiFloorNotification> FloorNotifications { get; }
    }

    public sealed class NarrowingBatteryLaunch
    {
        internal NarrowingBatteryLaunch(ProtocolBatteryRecord battery, string blindLabel, int repetition,
            IReadOnlyList<TestMode> modes)
        {
            Battery = battery;
            BlindLabel = blindLabel;
            Repetition = repetition;
            OrderedModes = modes;
        }

        internal ProtocolBatteryRecord Battery { get; }
        public long BatteryId => Battery.Id.Value;
        public string BlindLabel { get; }
        public int Repetition { get; }
        public IReadOnlyList<TestMode> OrderedModes { get; }
    }

    public sealed class PhaseTwoNarrowingWorkflow
    {
        private readonly PhaseTwoNarrowingService protocol;
        private readonly NarrowingStabilizationService stabilization;
        private readonly DeterministicTargetSequencer sequencer;
        private readonly HashSet<string> launched = new HashSet<string>(StringComparer.Ordinal);

        public PhaseTwoNarrowingWorkflow(PhaseTwoNarrowingService protocol,
            NarrowingStabilizationService stabilization, DeterministicTargetSequencer sequencer)
        {
            this.protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            this.stabilization = stabilization ?? throw new ArgumentNullException(nameof(stabilization));
            this.sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
        }

        public PhaseTwoNarrowingPlan CreatePlan(long profileId, long cycleId, int hardwareDpi, string createdDate)
        {
            PersistedPhaseTwoCandidateSet persisted = protocol.Create(profileId, cycleId, hardwareDpi, createdDate);
            long[] candidateIds = persisted.Candidates.Select(value => value.Id.Value).ToArray();
            CounterbalancedOrder order = sequencer.CreateCounterbalancedOrder(profileId, cycleId,
                ProtocolPhase.PhaseTwo, 1, candidateIds);
            BlindNarrowingCandidate[] blind = order.Candidates.Select(value =>
                new BlindNarrowingCandidate(value.CandidateId, value.BlindLabel, value.OrderIndex)).ToArray();
            return new PhaseTwoNarrowingPlan(persisted.SourceSignificanceTestId, persisted.Candidates,
                new ReadOnlyCollection<BlindNarrowingCandidate>(blind), persisted.FloorNotifications);
        }

        public NarrowingBatteryLaunch Launch(PhaseTwoNarrowingPlan plan, string blindLabel,
            int repetition, string startedDate)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (string.IsNullOrWhiteSpace(blindLabel)) throw new ArgumentException("Blind label is required.", nameof(blindLabel));
            if (repetition <= 0 || repetition > protocol.MaximumCompleteBatteries) throw new ArgumentOutOfRangeException(nameof(repetition));
            BlindNarrowingCandidate blind = plan.Candidates.SingleOrDefault(value => value.BlindLabel == blindLabel)
                ?? throw new InvalidOperationException("Unknown Phase 2 blind candidate.");
            ProtocolCandidateRecord candidate = plan.PersistedCandidates.Single(value => value.Id == blind.CandidateId);
            PhaseTwoStabilizationEvaluation current = stabilization.Evaluate(candidate.ProfileId, candidate.CycleId);
            NarrowingCandidateEvaluation candidateState = current.Candidates.Single(value => value.CandidateId == candidate.Id.Value);
            if (!candidateState.CanCollectAnother) throw new InvalidOperationException("This Phase 2 candidate cannot collect another battery.");
            string launchKey = candidate.Id.Value + "|" + repetition;
            if (!launched.Add(launchKey)) throw new InvalidOperationException("This Phase 2 candidate repetition is already launched.");
            ProtocolBatteryRecord battery = protocol.CreateNarrowingBattery(candidate, startedDate);
            CounterbalancedOrder order = sequencer.CreateCounterbalancedOrder(candidate.ProfileId,
                candidate.CycleId, ProtocolPhase.PhaseTwo, repetition,
                plan.PersistedCandidates.Select(value => value.Id.Value).ToArray());
            return new NarrowingBatteryLaunch(battery, blindLabel, repetition,
                new ReadOnlyCollection<TestMode>(order.Modes.ToArray()));
        }
    }
}
