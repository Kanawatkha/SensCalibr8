using System;
using System.Collections.Generic;

namespace SensCalibr8.Data.Repositories
{
    public enum OutlierAuditObservationKind{Shot,TrackingTrial}
    public sealed class OutlierAuditObservationRecord
    {
        public OutlierAuditObservationRecord(long sessionId,long observationId,OutlierAuditObservationKind kind,double observedValue,bool isStatisticalOutlier)
        {SessionId=Positive(sessionId,nameof(sessionId));ObservationId=Positive(observationId,nameof(observationId));Kind=kind;ObservedValue=Finite(observedValue,nameof(observedValue));IsStatisticalOutlier=isStatisticalOutlier;}
        public long SessionId{get;}public long ObservationId{get;}public OutlierAuditObservationKind Kind{get;}public double ObservedValue{get;}public bool IsStatisticalOutlier{get;}
        private static long Positive(long value,string field)=>value>0?value:throw new ArgumentOutOfRangeException(field);
        private static double Finite(double value,string field)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException(field);
    }
    public sealed class OutlierAnalysisRunRecord
    {
        public OutlierAnalysisRunRecord(long profileId,long cycleId,long calibrationConfigId,long phase,string mode,double sensitivityValue,string metricName,string scopeKey,double groupMean,double sampleSd,double thresholdValue,double inclusiveMean,double flaggedExcludedMean,string algorithmVersion,IReadOnlyList<OutlierAuditObservationRecord> observations)
        {ProfileId=Positive(profileId,nameof(profileId));CycleId=Positive(cycleId,nameof(cycleId));CalibrationConfigId=Positive(calibrationConfigId,nameof(calibrationConfigId));Phase=Positive(phase,nameof(phase));Mode=Required(mode,nameof(mode));SensitivityValue=FinitePositive(sensitivityValue,nameof(sensitivityValue));MetricName=Required(metricName,nameof(metricName));ScopeKey=Required(scopeKey,nameof(scopeKey));GroupMean=Finite(groupMean,nameof(groupMean));SampleSd=FiniteNonNegative(sampleSd,nameof(sampleSd));ThresholdValue=FiniteNonNegative(thresholdValue,nameof(thresholdValue));InclusiveMean=Finite(inclusiveMean,nameof(inclusiveMean));FlaggedExcludedMean=Finite(flaggedExcludedMean,nameof(flaggedExcludedMean));AlgorithmVersion=Required(algorithmVersion,nameof(algorithmVersion));Observations=observations??throw new ArgumentNullException(nameof(observations));if(observations.Count<2)throw new ArgumentException("Outlier run requires at least two observations.",nameof(observations));}
        public long ProfileId{get;}public long CycleId{get;}public long CalibrationConfigId{get;}public long Phase{get;}public string Mode{get;}public double SensitivityValue{get;}public string MetricName{get;}public string ScopeKey{get;}public double GroupMean{get;}public double SampleSd{get;}public double ThresholdValue{get;}public double InclusiveMean{get;}public double FlaggedExcludedMean{get;}public string AlgorithmVersion{get;}public IReadOnlyList<OutlierAuditObservationRecord> Observations{get;}
        private static long Positive(long value,string field)=>value>0?value:throw new ArgumentOutOfRangeException(field);private static string Required(string value,string field)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException(field+" is required.",field);private static double Finite(double value,string field)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException(field);private static double FiniteNonNegative(double value,string field)=>Finite(value,field)>=0d?value:throw new ArgumentOutOfRangeException(field);private static double FinitePositive(double value,string field)=>Finite(value,field)>0d?value:throw new ArgumentOutOfRangeException(field);
    }
    public sealed class GradeAuditRecord
    {
        public GradeAuditRecord(string reactionTier,string consistencyTier,string finalGrade,double closeReactionTimeMs,double batteryConsistencyUtility,string contractVersion)
        {ReactionTier=Grade(reactionTier,nameof(reactionTier));ConsistencyTier=Grade(consistencyTier,nameof(consistencyTier));FinalGrade=Grade(finalGrade,nameof(finalGrade));CloseReactionTimeMs=NonNegative(closeReactionTimeMs,nameof(closeReactionTimeMs));BatteryConsistencyUtility=Unit(batteryConsistencyUtility,nameof(batteryConsistencyUtility));ContractVersion=!string.IsNullOrWhiteSpace(contractVersion)?contractVersion:throw new ArgumentException(nameof(contractVersion));}
        public string ReactionTier{get;}public string ConsistencyTier{get;}public string FinalGrade{get;}public double CloseReactionTimeMs{get;}public double BatteryConsistencyUtility{get;}public string ContractVersion{get;}
        private static string Grade(string value,string field)=>(value=="S"||value=="A"||value=="B"||value=="C"||value=="D")?value:throw new ArgumentException(field+" is invalid.",field);private static double NonNegative(double value,string field)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0d?value:throw new ArgumentOutOfRangeException(field);private static double Unit(double value,string field)=>NonNegative(value,field)<=1d?value:throw new ArgumentOutOfRangeException(field);
    }
}
