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
    public sealed class BlindPhaseOneCandidate
    {
        public BlindPhaseOneCandidate(long candidateId,string blindLabel,int orderIndex){CandidateId=candidateId;BlindLabel=blindLabel;OrderIndex=orderIndex;}
        public long CandidateId{get;}public string BlindLabel{get;}public int OrderIndex{get;}
    }
    public sealed class PhaseOneExploratoryPlan
    {
        internal PhaseOneExploratoryPlan(PersistedPhaseOneCandidateSet persisted,IReadOnlyList<BlindPhaseOneCandidate> candidates){Persisted=persisted;Candidates=candidates;}
        internal PersistedPhaseOneCandidateSet Persisted{get;}public long CycleId=>Persisted.Cycle.Id.Value;public IReadOnlyList<BlindPhaseOneCandidate>Candidates{get;}public IReadOnlyList<EdpiFloorNotification>FloorNotifications=>Persisted.FloorNotifications;
    }
    public sealed class PhaseOneBatteryLaunch
    {
        internal PhaseOneBatteryLaunch(ProtocolBatteryRecord battery,string blindLabel,IReadOnlyList<TestMode> modes){Battery=battery;BatteryId=battery.Id.Value;BlindLabel=blindLabel;OrderedModes=modes;}
        internal ProtocolBatteryRecord Battery{get;}public long BatteryId{get;}public string BlindLabel{get;}public IReadOnlyList<TestMode>OrderedModes{get;}
    }
    public sealed class PhaseOneExploratoryWorkflow
    {
        private readonly PhaseOneExploratoryProtocolService protocol;private readonly DeterministicTargetSequencer sequencer;private readonly HashSet<string> launched=new HashSet<string>(StringComparer.Ordinal);
        public PhaseOneExploratoryWorkflow(PhaseOneExploratoryProtocolService protocol,DeterministicTargetSequencer sequencer){this.protocol=protocol??throw new ArgumentNullException(nameof(protocol));this.sequencer=sequencer??throw new ArgumentNullException(nameof(sequencer));}
        public PhaseOneExploratoryPlan CreatePlan(long profileId,long cycleNumber,int hardwareDpi,string createdDate)
        {
            PersistedPhaseOneCandidateSet persisted=protocol.Create(profileId,cycleNumber,hardwareDpi,createdDate);long[] ids=persisted.Candidates.Select(value=>value.Id.Value).ToArray();CounterbalancedOrder order=sequencer.CreateCounterbalancedOrder(profileId,persisted.Cycle.Id.Value,ProtocolPhase.PhaseOne,1,ids);var blind=order.Candidates.Select(value=>new BlindPhaseOneCandidate(value.CandidateId,value.BlindLabel,value.OrderIndex)).ToArray();return new PhaseOneExploratoryPlan(persisted,new ReadOnlyCollection<BlindPhaseOneCandidate>(blind));
        }
        public PhaseOneBatteryLaunch Launch(PhaseOneExploratoryPlan plan,string blindLabel,int batteryRepetitionOrdinal,string startedDate)
        {
            if(plan==null)throw new ArgumentNullException(nameof(plan));if(string.IsNullOrWhiteSpace(blindLabel))throw new ArgumentException("Blind label is required.",nameof(blindLabel));if(batteryRepetitionOrdinal<=0)throw new ArgumentOutOfRangeException(nameof(batteryRepetitionOrdinal));BlindPhaseOneCandidate blind=plan.Candidates.SingleOrDefault(value=>value.BlindLabel==blindLabel)??throw new InvalidOperationException("Unknown Phase 1 blind candidate.");string key=plan.CycleId+"|"+blind.CandidateId+"|"+batteryRepetitionOrdinal;if(!launched.Add(key))throw new InvalidOperationException("This Phase 1 candidate repetition is already launched.");ProtocolCandidateRecord candidate=plan.Persisted.Candidates.Single(value=>value.Id==blind.CandidateId);ProtocolBatteryRecord battery=protocol.CreateExploratoryBattery(candidate,batteryRepetitionOrdinal,startedDate);CounterbalancedOrder order=sequencer.CreateCounterbalancedOrder(candidate.ProfileId,candidate.CycleId,ProtocolPhase.PhaseOne,batteryRepetitionOrdinal,plan.Persisted.Candidates.Select(value=>value.Id.Value).ToArray());return new PhaseOneBatteryLaunch(battery,blindLabel,new ReadOnlyCollection<TestMode>(order.Modes.ToArray()));
        }
    }
}
