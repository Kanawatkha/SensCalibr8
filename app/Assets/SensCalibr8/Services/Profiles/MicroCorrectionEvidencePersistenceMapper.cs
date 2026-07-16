using System;
using System.Collections.Generic;
using System.Globalization;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Profiles
{
    public static class MicroCorrectionEvidencePersistenceMapper
    {
        public static IReadOnlyList<ShotCaptureRecord> ToShotCaptureRecords(IReadOnlyList<MicroCorrectionCaptureEvidence> opportunities,double sensitivityValue)
        {
            if(opportunities==null)throw new ArgumentNullException(nameof(opportunities));if(double.IsNaN(sensitivityValue)||double.IsInfinity(sensitivityValue)||sensitivityValue<=0d)throw new ArgumentOutOfRangeException(nameof(sensitivityValue));var records=new List<ShotCaptureRecord>(opportunities.Count);
            foreach(MicroCorrectionCaptureEvidence value in opportunities){if(value==null)throw new ArgumentException("Micro-Correction opportunities cannot contain null.",nameof(opportunities));records.Add(new ShotCaptureRecord(value.TargetId,"micro",value.TargetSize,Coordinates(value.TargetCenterAzimuthDeg,value.TargetCenterElevationDeg),value.ActivationTimestampSeconds,value.MovementOnsetTimestampSeconds,value.ResolutionTimestampSeconds,value.IsHit?value.ResolutionTimestampSeconds:(double?)null,value.IsHit?Coordinates(value.FinalAimAzimuthDeg,value.FinalAimElevationDeg):null,value.IsHit,value.OutcomeReason,Coordinates(value.FinalAimAzimuthDeg,value.FinalAimElevationDeg),null,null,sensitivityValue,value.InitialOffsetDistancePx,value.MicroAdjustmentCount,value.SubmovementCount,value.FinalPrecisionErrorDeg,value.IsCenterHit,null,value.PreviewTimestampSeconds));}
            return records.AsReadOnly();
        }
        private static string Coordinates(double azimuth,double elevation)=>string.Format(CultureInfo.InvariantCulture,"{0:G17},{1:G17}",azimuth,elevation);
    }
}
