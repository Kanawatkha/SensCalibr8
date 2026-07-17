using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Calculations
{
    public sealed class EdpiFloorNotification
    {
        public EdpiFloorNotification(double offsetPercent,double originalEdpi,double adjustedEdpi){OffsetPercent=offsetPercent;OriginalEdpi=originalEdpi;AdjustedEdpi=adjustedEdpi;}
        public double OffsetPercent{get;}public double OriginalEdpi{get;}public double AdjustedEdpi{get;}
    }
    public sealed class PhaseOneCandidateSource
    {
        public PhaseOneCandidateSource(double anchorEdpi,double offsetPercent,double preFloorEdpi,bool floorApplied){AnchorEdpi=anchorEdpi;OffsetPercent=offsetPercent;PreFloorEdpi=preFloorEdpi;FloorApplied=floorApplied;}
        public double AnchorEdpi{get;}public double OffsetPercent{get;}public double PreFloorEdpi{get;}public bool FloorApplied{get;}
    }
    public sealed class PhaseOneCandidateDefinition
    {
        public PhaseOneCandidateDefinition(double edpi,double sensitivity,IReadOnlyList<PhaseOneCandidateSource> sources){Edpi=edpi;Sensitivity=sensitivity;Sources=sources??throw new ArgumentNullException(nameof(sources));}
        public double Edpi{get;}public double Sensitivity{get;}public IReadOnlyList<PhaseOneCandidateSource> Sources{get;}
    }
    public sealed class PhaseOneCandidateGeneration
    {
        public PhaseOneCandidateGeneration(IReadOnlyList<PhaseOneCandidateDefinition> candidates,IReadOnlyList<EdpiFloorNotification> notifications){Candidates=candidates;FloorNotifications=notifications;}
        public IReadOnlyList<PhaseOneCandidateDefinition>Candidates{get;}public IReadOnlyList<EdpiFloorNotification>FloorNotifications{get;}
    }
    public sealed class PersistedPhaseOneCandidateSet
    {
        public PersistedPhaseOneCandidateSet(CycleRecord cycle,IReadOnlyList<ProtocolCandidateRecord> candidates,IReadOnlyList<EdpiFloorNotification> notifications){Cycle=cycle;Candidates=candidates;FloorNotifications=notifications;}
        public CycleRecord Cycle{get;}public IReadOnlyList<ProtocolCandidateRecord>Candidates{get;}public IReadOnlyList<EdpiFloorNotification>FloorNotifications{get;}
    }

    public sealed class PhaseOneExploratoryProtocolService
    {
        private readonly ResearchConstants research;private readonly ProtocolConstants protocol;private readonly ProtocolRepository repository;
        public PhaseOneExploratoryProtocolService(ResearchConstants research,ProtocolConstants protocol,ProtocolRepository repository){this.research=research??throw new ArgumentNullException(nameof(research));this.protocol=protocol??throw new ArgumentNullException(nameof(protocol));this.repository=repository??throw new ArgumentNullException(nameof(repository));}
        public PhaseOneCandidateGeneration Generate(int hardwareDpi)=>GenerateAround(research.PsaBaselineEdpi,hardwareDpi);
        public PhaseOneCandidateGeneration GenerateAround(double anchorEdpi,int hardwareDpi)
        {
            if(double.IsNaN(anchorEdpi)||double.IsInfinity(anchorEdpi)||anchorEdpi<=0d)throw new ArgumentOutOfRangeException(nameof(anchorEdpi));if(hardwareDpi<=0)throw new ArgumentOutOfRangeException(nameof(hardwareDpi));var candidates=new Dictionary<double,List<PhaseOneCandidateSource>>();var notifications=new List<EdpiFloorNotification>();foreach(double offset in protocol.PhaseOneOffsetsPercent){double preFloor=anchorEdpi*(1d+offset/100d),effective=Math.Max(preFloor,research.EdpiFloor);bool floored=preFloor<research.EdpiFloor;if(!candidates.TryGetValue(effective,out List<PhaseOneCandidateSource> sources)){sources=new List<PhaseOneCandidateSource>();candidates.Add(effective,sources);}sources.Add(new PhaseOneCandidateSource(anchorEdpi,offset,preFloor,floored));if(floored)notifications.Add(new EdpiFloorNotification(offset,preFloor,effective));}var definitions=candidates.Select(pair=>new PhaseOneCandidateDefinition(pair.Key,pair.Key/hardwareDpi,new ReadOnlyCollection<PhaseOneCandidateSource>(pair.Value))).OrderBy(value=>value.Edpi).ToArray();return new PhaseOneCandidateGeneration(definitions,new ReadOnlyCollection<EdpiFloorNotification>(notifications));
        }
        public PersistedPhaseOneCandidateSet Create(long profileId,long cycleNumber,int hardwareDpi,string createdDate)
        {
            PhaseOneCandidateGeneration generated=Generate(hardwareDpi);CycleRecord cycle=repository.CreateCycle(new CycleRecord(null,profileId,cycleNumber,createdDate,null,null));var requests=BuildRequests(profileId,cycle.Id.Value,hardwareDpi,research.PsaBaselineEdpi,createdDate);IReadOnlyList<ProtocolCandidateRecord> stored=repository.CreateCandidateSetWithSources(requests);return new PersistedPhaseOneCandidateSet(cycle,stored,generated.FloorNotifications);
        }
        public IReadOnlyList<ProtocolCandidateCreateRequest> BuildRequests(long profileId,long cycleId,int hardwareDpi,double anchorEdpi,string createdDate)
        {return GenerateAround(anchorEdpi,hardwareDpi).Candidates.Select(value=>new ProtocolCandidateCreateRequest(new ProtocolCandidateRecord(null,profileId,cycleId,(long)ProtocolPhase.PhaseOne,value.Edpi,value.Sensitivity,protocol.PhaseOneGenerationRule,createdDate),value.Sources.Select(source=>new ProtocolCandidateSourceRecord(source.AnchorEdpi,source.OffsetPercent,source.PreFloorEdpi,source.FloorApplied)).ToArray())).ToArray();}
        public ProtocolBatteryRecord CreateExploratoryBattery(ProtocolCandidateRecord candidate,long repetition,string startedDate)
        { if(candidate==null)throw new ArgumentNullException(nameof(candidate));if(candidate.Phase!=(long)ProtocolPhase.PhaseOne)throw new InvalidOperationException("Phase 1 candidate is required.");if(repetition<=0)throw new ArgumentOutOfRangeException(nameof(repetition));return repository.CreateBattery(new ProtocolBatteryRecord(null,candidate.ProfileId,candidate.CycleId,candidate.Id.Value,candidate.SensitivityValue,(long)ProtocolPhase.PhaseOne,"exploratory",startedDate,null)); }
    }
}
