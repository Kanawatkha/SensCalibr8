using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Calculations
{
    public sealed class PersistedPhaseTwoCandidateSet
    {
        public PersistedPhaseTwoCandidateSet(long sourceSignificanceTestId,
            IReadOnlyList<ProtocolCandidateRecord> candidates, IReadOnlyList<EdpiFloorNotification> notifications)
        {
            SourceSignificanceTestId = sourceSignificanceTestId;
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            FloorNotifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        public long SourceSignificanceTestId { get; }
        public IReadOnlyList<ProtocolCandidateRecord> Candidates { get; }
        public IReadOnlyList<EdpiFloorNotification> FloorNotifications { get; }
    }

    public sealed class PhaseTwoNarrowingService
    {
        private readonly ResearchConstants research;
        private readonly PhaseTwoProtocolContract contract;
        private readonly ProtocolRepository protocolRepository;
        private readonly NarrowingRepository narrowingRepository;

        public PhaseTwoNarrowingService(ResearchConstants research, PhaseTwoProtocolContract contract,
            ProtocolRepository protocolRepository, NarrowingRepository narrowingRepository)
        {
            this.research = research ?? throw new ArgumentNullException(nameof(research));
            this.contract = contract ?? throw new ArgumentNullException(nameof(contract));
            this.protocolRepository = protocolRepository ?? throw new ArgumentNullException(nameof(protocolRepository));
            this.narrowingRepository = narrowingRepository ?? throw new ArgumentNullException(nameof(narrowingRepository));
        }

        public int MaximumCompleteBatteries => contract.MaximumCompleteBatteries;

        public PersistedPhaseTwoCandidateSet Create(long profileId, long cycleId, int hardwareDpi, string createdDate)
        {
            if (hardwareDpi <= 0) throw new ArgumentOutOfRangeException(nameof(hardwareDpi));
            if (narrowingRepository.ListPhaseTwoCandidates(profileId, cycleId).Count != 0)
                throw new InvalidOperationException("Phase 2 candidates already exist for this cycle.");
            PhaseOneNarrowingDecision decision = narrowingRepository.RequirePhaseOneDecision(profileId, cycleId);
            var sourcesByEdpi = new Dictionary<double, List<ProtocolCandidateSourceRecord>>();
            var notifications = new List<EdpiFloorNotification>();
            foreach (double anchor in decision.Anchors)
            {
                foreach (double offset in contract.OffsetsPercent)
                {
                    double preFloor = anchor * (1d + offset / 100d);
                    double effective = Math.Max(preFloor, research.EdpiFloor);
                    bool floorApplied = preFloor < research.EdpiFloor;
                    if (!sourcesByEdpi.TryGetValue(effective, out List<ProtocolCandidateSourceRecord> sources))
                    {
                        sources = new List<ProtocolCandidateSourceRecord>();
                        sourcesByEdpi.Add(effective, sources);
                    }
                    sources.Add(new ProtocolCandidateSourceRecord(anchor, offset, preFloor, floorApplied));
                    if (floorApplied) notifications.Add(new EdpiFloorNotification(offset, preFloor, effective));
                }
            }

            string rule = decision.Anchors.Count == 1 ? contract.SingleAnchorGenerationRule : contract.TieGenerationRule;
            ProtocolCandidateCreateRequest[] requests = sourcesByEdpi.OrderBy(value => value.Key).Select(value =>
                new ProtocolCandidateCreateRequest(new ProtocolCandidateRecord(null, profileId, cycleId,
                    (long)ProtocolPhase.PhaseTwo, value.Key, value.Key / hardwareDpi, rule, createdDate),
                    new ReadOnlyCollection<ProtocolCandidateSourceRecord>(value.Value))).ToArray();
            IReadOnlyList<ProtocolCandidateRecord> candidates = protocolRepository.CreateCandidateSetWithSources(requests);
            return new PersistedPhaseTwoCandidateSet(decision.SignificanceTestId, candidates,
                new ReadOnlyCollection<EdpiFloorNotification>(notifications));
        }

        public ProtocolBatteryRecord CreateNarrowingBattery(ProtocolCandidateRecord candidate, string startedDate)
        {
            if (candidate == null || candidate.Id == null || candidate.Phase != (long)ProtocolPhase.PhaseTwo)
                throw new InvalidOperationException("A persisted Phase 2 candidate is required.");
            return protocolRepository.CreateBattery(new ProtocolBatteryRecord(null, candidate.ProfileId,
                candidate.CycleId, candidate.Id.Value, candidate.SensitivityValue, candidate.Phase,
                contract.BatteryPurpose, startedDate, null));
        }
    }
}
