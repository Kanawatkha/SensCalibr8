using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Calculations
{
    public sealed class ScientificRigorPersistenceService
    {
        private readonly ScientificRigorRepository repository;
        public ScientificRigorPersistenceService(ScientificRigorRepository repository){this.repository=repository??throw new ArgumentNullException(nameof(repository));}
        public long PersistOutlierRun(long profileId,long cycleId,long configId,long phase,string mode,double sensitivity,string metric,OutlierAnalysisResult result)
        {if(result==null)throw new ArgumentNullException(nameof(result));IReadOnlyList<OutlierAuditObservationRecord> observations=result.Observations.Select(value=>new OutlierAuditObservationRecord(value.Observation.SessionId,value.Observation.ObservationId,value.Observation.Kind==OutlierObservationKind.Shot?OutlierAuditObservationKind.Shot:OutlierAuditObservationKind.TrackingTrial,value.Observation.Value,value.IsStatisticalOutlier)).ToArray();string scope=profileId.ToString(CultureInfo.InvariantCulture)+"|"+cycleId.ToString(CultureInfo.InvariantCulture)+"|"+phase.ToString(CultureInfo.InvariantCulture)+"|"+mode+"|"+sensitivity.ToString("G17",CultureInfo.InvariantCulture)+"|"+metric;return repository.PersistOutlierRun(new OutlierAnalysisRunRecord(profileId,cycleId,configId,phase,mode,sensitivity,metric,scope,result.Mean,result.SampleSd,result.Threshold,result.InclusiveMean,result.ExcludedMean,result.AlgorithmVersion,observations));}
        public void PersistFatigue(long sessionId,FatigueResult result){if(result==null)throw new ArgumentNullException(nameof(result));repository.ApplyFatigue(sessionId,result.DeclinePercent,result.IsFlagged,result.AlgorithmVersion);}
        public void PersistGrade(long sensitivityTestId,GradeResult result){if(result==null)throw new ArgumentNullException(nameof(result));repository.ApplyGrade(sensitivityTestId,new GradeAuditRecord(result.ReactionTier,result.ConsistencyTier,result.FinalGrade,result.CloseReactionTimeMs,result.BatteryConsistencyUtility,result.ContractVersion));}
        public void ConfirmDataQualityExclusion(long flagId,string documentedReason)=>repository.ConfirmDataQualityExclusion(flagId,documentedReason);
    }
}
