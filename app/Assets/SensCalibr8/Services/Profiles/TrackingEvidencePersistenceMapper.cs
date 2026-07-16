using System;
using System.Collections.Generic;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Profiles
{
    public static class TrackingEvidencePersistenceMapper
    {
        public static IReadOnlyList<TrackingTrialCaptureRecord> ToTrialRecords(IReadOnlyList<TrackingCaptureEvidence> evidence)
        { if(evidence==null)throw new ArgumentNullException(nameof(evidence));var records=new List<TrackingTrialCaptureRecord>(evidence.Count);foreach(TrackingCaptureEvidence value in evidence){if(value==null)throw new ArgumentException("Tracking evidence cannot contain null.",nameof(evidence));records.Add(new TrackingTrialCaptureRecord(value.SensitivityValue,value.TrialIndex,value.BlockIndex,null,value.PatternType,value.TargetSize,value.PathContractId,value.PathParametersJson,Milliseconds(value.DurationSeconds),value.DeviationSamplesJson,Milliseconds(value.TimeOnTargetSeconds),value.TimeOnTargetPercentage));}return records.AsReadOnly(); }
        public static IReadOnlyList<TrackingWindowCaptureRecord> ToWindowRecords(IReadOnlyList<TrackingCaptureEvidence> evidence)
        { if(evidence==null)throw new ArgumentNullException(nameof(evidence));var records=new List<TrackingWindowCaptureRecord>();for(int trial=0;trial<evidence.Count;trial++){TrackingCaptureEvidence value=evidence[trial]??throw new ArgumentException("Tracking evidence cannot contain null.",nameof(evidence));foreach(TrackingWindowEvidence window in value.Windows)records.Add(new TrackingWindowCaptureRecord(trial,window.WindowIndex,Milliseconds(window.StartSeconds),Milliseconds(window.EndSeconds),Milliseconds(window.TimeOnTargetSeconds),window.TimeOnTargetPercentage,window.DeviationRmsDeg));}return records.AsReadOnly(); }
        private static long Milliseconds(double seconds)=>checked((long)Math.Round(seconds*1000d,MidpointRounding.AwayFromZero));
    }
}
