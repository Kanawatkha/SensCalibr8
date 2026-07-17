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
    public sealed class PhaseThreeFinalPlan
    {
        internal PhaseThreeFinalPlan(long sourcePhaseTwoHistoryId, IReadOnlyList<ProtocolCandidateRecord> candidates,
            IReadOnlyList<BlindNarrowingCandidate> blindCandidates, IReadOnlyList<EdpiFloorNotification> notifications)
        {
            SourcePhaseTwoHistoryId = sourcePhaseTwoHistoryId;
            PersistedCandidates = candidates;
            Candidates = blindCandidates;
            FloorNotifications = notifications;
        }

        internal IReadOnlyList<ProtocolCandidateRecord> PersistedCandidates { get; }
        public long SourcePhaseTwoHistoryId { get; }
        public IReadOnlyList<BlindNarrowingCandidate> Candidates { get; }
        public IReadOnlyList<EdpiFloorNotification> FloorNotifications { get; }
    }

    public sealed class PhaseThreeFinalNarrowingWorkflow
    {
        private readonly PhaseThreeFinalNarrowingService protocol;
        private readonly NarrowingStabilizationService stabilization;
        private readonly DeterministicTargetSequencer sequencer;
        private readonly HashSet<string> launched = new HashSet<string>(StringComparer.Ordinal);

        public PhaseThreeFinalNarrowingWorkflow(PhaseThreeFinalNarrowingService protocol,
            NarrowingStabilizationService stabilization, DeterministicTargetSequencer sequencer)
        {
            this.protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            this.stabilization = stabilization ?? throw new ArgumentNullException(nameof(stabilization));
            this.sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
        }

        public PhaseThreeFinalPlan CreatePlan(long profileId, long cycleId, int hardwareDpi, string createdDate)
        {
            PersistedPhaseThreeCandidateSet persisted = protocol.Create(profileId, cycleId, hardwareDpi, createdDate);
            long[] ids = persisted.Candidates.Select(value => value.Id.Value).ToArray();
            CounterbalancedOrder order = sequencer.CreateCounterbalancedOrder(profileId, cycleId, ProtocolPhase.PhaseThree, 1, ids);
            BlindNarrowingCandidate[] blind = order.Candidates.Select(value =>
                new BlindNarrowingCandidate(value.CandidateId, value.BlindLabel, value.OrderIndex)).ToArray();
            return new PhaseThreeFinalPlan(persisted.SourceWinner.Id.Value, persisted.Candidates,
                new ReadOnlyCollection<BlindNarrowingCandidate>(blind), persisted.FloorNotifications);
        }

        public NarrowingBatteryLaunch Launch(PhaseThreeFinalPlan plan, string blindLabel, int repetition, string startedDate)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (string.IsNullOrWhiteSpace(blindLabel) || repetition <= 0 || repetition > protocol.MaximumCompleteBatteries)
                throw new ArgumentOutOfRangeException();
            BlindNarrowingCandidate blind = plan.Candidates.SingleOrDefault(value => value.BlindLabel == blindLabel)
                ?? throw new InvalidOperationException("Unknown Phase 3 blind candidate.");
            ProtocolCandidateRecord candidate = plan.PersistedCandidates.Single(value => value.Id.Value == blind.CandidateId);
            NarrowingCandidateEvaluation state = stabilization.EvaluatePhase(candidate.ProfileId, candidate.CycleId, ProtocolPhase.PhaseThree)
                .Candidates.Single(value => value.CandidateId == candidate.Id.Value);
            if (!state.CanCollectAnother) throw new InvalidOperationException("This Phase 3 candidate cannot collect another battery.");
            if (!launched.Add(candidate.Id.Value + "|" + repetition)) throw new InvalidOperationException("This Phase 3 candidate repetition is already launched.");
            ProtocolBatteryRecord battery = protocol.CreateNarrowingBattery(candidate, startedDate);
            CounterbalancedOrder order = sequencer.CreateCounterbalancedOrder(candidate.ProfileId, candidate.CycleId,
                ProtocolPhase.PhaseThree, repetition, plan.PersistedCandidates.Select(value => value.Id.Value).ToArray());
            return new NarrowingBatteryLaunch(battery, blindLabel, repetition,
                new ReadOnlyCollection<TestMode>(order.Modes.ToArray()));
        }
    }
}
