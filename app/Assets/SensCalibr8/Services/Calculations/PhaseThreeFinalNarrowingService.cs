using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Calculations
{
    public sealed class PersistedPhaseThreeCandidateSet
    {
        public PersistedPhaseThreeCandidateSet(PhaseHistoryRecord sourceWinner,
            IReadOnlyList<ProtocolCandidateRecord> candidates, IReadOnlyList<EdpiFloorNotification> notifications)
        {
            SourceWinner = sourceWinner ?? throw new ArgumentNullException(nameof(sourceWinner));
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            FloorNotifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        public PhaseHistoryRecord SourceWinner { get; }
        public IReadOnlyList<ProtocolCandidateRecord> Candidates { get; }
        public IReadOnlyList<EdpiFloorNotification> FloorNotifications { get; }
    }

    public sealed class PhaseThreeFinalNarrowingService
    {
        private readonly ResearchConstants research;
        private readonly PhaseThreeProtocolContract contract;
        private readonly PhaseTwoProtocolContract repetitionContract;
        private readonly ProtocolRepository protocolRepository;
        private readonly NarrowingRepository narrowingRepository;
        private readonly PhaseHistoryRepository historyRepository;

        public PhaseThreeFinalNarrowingService(ResearchConstants research, PhaseThreeProtocolContract contract,
            PhaseTwoProtocolContract repetitionContract,
            ProtocolRepository protocolRepository, NarrowingRepository narrowingRepository,
            PhaseHistoryRepository historyRepository)
        {
            this.research = research ?? throw new ArgumentNullException(nameof(research));
            this.contract = contract ?? throw new ArgumentNullException(nameof(contract));
            this.repetitionContract = repetitionContract ?? throw new ArgumentNullException(nameof(repetitionContract));
            this.protocolRepository = protocolRepository ?? throw new ArgumentNullException(nameof(protocolRepository));
            this.narrowingRepository = narrowingRepository ?? throw new ArgumentNullException(nameof(narrowingRepository));
            this.historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        }

        public int MaximumCompleteBatteries => repetitionContract.MaximumCompleteBatteries;

        public PersistedPhaseThreeCandidateSet Create(long profileId, long cycleId, int hardwareDpi, string createdDate)
        {
            if (hardwareDpi <= 0) throw new ArgumentOutOfRangeException(nameof(hardwareDpi));
            if (narrowingRepository.ListPhaseCandidates(profileId, cycleId, (long)ProtocolPhase.PhaseThree).Count != 0)
                throw new InvalidOperationException("Phase 3 candidates already exist for this cycle.");
            PhaseHistoryRecord winner = historyRepository.Require(profileId, cycleId, (long)ProtocolPhase.PhaseTwo);
            if (narrowingRepository.ListPhaseCandidates(profileId, cycleId, (long)ProtocolPhase.PhaseTwo)
                .Count(value => value.Edpi.Equals(winner.WinnerEdpi)) != 1)
                throw new InvalidOperationException("Phase 2 winner does not map to one canonical candidate.");

            var byEdpi = new Dictionary<double, List<ProtocolCandidateSourceRecord>>();
            var notifications = new List<EdpiFloorNotification>();
            foreach (double offset in contract.OffsetsPercent)
            {
                double preFloor = winner.WinnerEdpi * (1d + offset / 100d);
                double effective = Math.Max(preFloor, research.EdpiFloor);
                bool floorApplied = preFloor < research.EdpiFloor;
                if (!byEdpi.TryGetValue(effective, out List<ProtocolCandidateSourceRecord> sources))
                {
                    sources = new List<ProtocolCandidateSourceRecord>();
                    byEdpi.Add(effective, sources);
                }
                sources.Add(new ProtocolCandidateSourceRecord(winner.WinnerEdpi, offset, preFloor, floorApplied));
                if (floorApplied) notifications.Add(new EdpiFloorNotification(offset, preFloor, effective));
            }
            ProtocolCandidateCreateRequest[] requests = byEdpi.OrderBy(value => value.Key).Select(value =>
                new ProtocolCandidateCreateRequest(new ProtocolCandidateRecord(null, profileId, cycleId,
                    (long)ProtocolPhase.PhaseThree, value.Key, value.Key / hardwareDpi,
                    contract.GenerationRule, createdDate), new ReadOnlyCollection<ProtocolCandidateSourceRecord>(value.Value))).ToArray();
            return new PersistedPhaseThreeCandidateSet(winner, protocolRepository.CreateCandidateSetWithSources(requests),
                new ReadOnlyCollection<EdpiFloorNotification>(notifications));
        }

        public ProtocolBatteryRecord CreateNarrowingBattery(ProtocolCandidateRecord candidate, string startedDate)
        {
            if (candidate == null || candidate.Id == null || candidate.Phase != (long)ProtocolPhase.PhaseThree)
                throw new InvalidOperationException("A persisted Phase 3 candidate is required.");
            return protocolRepository.CreateBattery(new ProtocolBatteryRecord(null, candidate.ProfileId, candidate.CycleId,
                candidate.Id.Value, candidate.SensitivityValue, candidate.Phase, contract.BatteryPurpose, startedDate, null));
        }
    }
}
