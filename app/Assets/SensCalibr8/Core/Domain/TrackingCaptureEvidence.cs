using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SensCalibr8.Core.Domain
{
    public sealed class TrackingWindowEvidence
    {
        public TrackingWindowEvidence(long windowIndex, double startSeconds, double endSeconds, double timeOnTargetSeconds, double timeOnTargetPercentage, double deviationRmsDeg)
        { WindowIndex=windowIndex; StartSeconds=NonNegative(startSeconds,nameof(startSeconds)); EndSeconds=NonNegative(endSeconds,nameof(endSeconds)); TimeOnTargetSeconds=NonNegative(timeOnTargetSeconds,nameof(timeOnTargetSeconds)); TimeOnTargetPercentage=NonNegative(timeOnTargetPercentage,nameof(timeOnTargetPercentage)); DeviationRmsDeg=NonNegative(deviationRmsDeg,nameof(deviationRmsDeg)); if(EndSeconds<=StartSeconds||TimeOnTargetSeconds>EndSeconds-StartSeconds||TimeOnTargetPercentage>100d)throw new ArgumentOutOfRangeException(nameof(endSeconds)); }
        public long WindowIndex { get; } public double StartSeconds { get; } public double EndSeconds { get; } public double TimeOnTargetSeconds { get; } public double TimeOnTargetPercentage { get; } public double DeviationRmsDeg { get; }
        private static double NonNegative(double value,string name)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0d?value:throw new ArgumentOutOfRangeException(name);
    }

    public sealed class TrackingCaptureEvidence
    {
        public TrackingCaptureEvidence(double sensitivityValue,long trialIndex,long blockIndex,string patternType,string targetSize,string pathContractId,string pathParametersJson,double durationSeconds,string deviationSamplesJson,double timeOnTargetSeconds,double timeOnTargetPercentage,IReadOnlyList<TrackingWindowEvidence> windows)
        { SensitivityValue=Positive(sensitivityValue,nameof(sensitivityValue)); TrialIndex=NonNegative(trialIndex,nameof(trialIndex)); BlockIndex=NonNegative(blockIndex,nameof(blockIndex)); PatternType=Required(patternType,nameof(patternType)); TargetSize=Required(targetSize,nameof(targetSize)); PathContractId=Required(pathContractId,nameof(pathContractId)); PathParametersJson=Required(pathParametersJson,nameof(pathParametersJson)); DurationSeconds=Positive(durationSeconds,nameof(durationSeconds)); DeviationSamplesJson=Required(deviationSamplesJson,nameof(deviationSamplesJson)); TimeOnTargetSeconds=NonNegative(timeOnTargetSeconds,nameof(timeOnTargetSeconds)); TimeOnTargetPercentage=NonNegative(timeOnTargetPercentage,nameof(timeOnTargetPercentage)); if(TimeOnTargetSeconds>DurationSeconds||TimeOnTargetPercentage>100d)throw new ArgumentOutOfRangeException(nameof(timeOnTargetPercentage)); Windows=new ReadOnlyCollection<TrackingWindowEvidence>(new List<TrackingWindowEvidence>(windows??throw new ArgumentNullException(nameof(windows)))); }
        public double SensitivityValue { get; } public long TrialIndex { get; } public long BlockIndex { get; } public string PatternType { get; } public string TargetSize { get; } public string PathContractId { get; } public string PathParametersJson { get; } public double DurationSeconds { get; } public string DeviationSamplesJson { get; } public double TimeOnTargetSeconds { get; } public double TimeOnTargetPercentage { get; } public IReadOnlyList<TrackingWindowEvidence> Windows { get; }
        private static long NonNegative(long value,string name)=>value>=0?value:throw new ArgumentOutOfRangeException(name); private static double Positive(double value,string name)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>0d?value:throw new ArgumentOutOfRangeException(name); private static double NonNegative(double value,string name)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0d?value:throw new ArgumentOutOfRangeException(name); private static string Required(string value,string name)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException(name+" is required.",name);
    }
}
