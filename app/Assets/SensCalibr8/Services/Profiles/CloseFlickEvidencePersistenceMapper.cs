using System;
using System.Collections.Generic;
using System.Globalization;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Profiles
{
    public static class CloseFlickEvidencePersistenceMapper
    {
        public static IReadOnlyList<ShotCaptureRecord> ToShotCaptureRecords(
            IReadOnlyList<CloseFlickCaptureEvidence> opportunities, double sensitivityValue)
        {
            if (opportunities == null) throw new ArgumentNullException(nameof(opportunities));
            if (double.IsNaN(sensitivityValue) || double.IsInfinity(sensitivityValue) || sensitivityValue <= 0d)
                throw new ArgumentOutOfRangeException(nameof(sensitivityValue));
            var records = new List<ShotCaptureRecord>(opportunities.Count);
            foreach (CloseFlickCaptureEvidence opportunity in opportunities)
            {
                if (opportunity == null) throw new ArgumentException("Close Flick opportunities cannot contain null.", nameof(opportunities));
                records.Add(new ShotCaptureRecord(opportunity.TargetId, "close", opportunity.TargetSize,
                    Coordinates(opportunity.TargetCenterAzimuthDeg, opportunity.TargetCenterElevationDeg),
                    opportunity.VisibleTimestampSeconds, opportunity.FirstMouseMovementTimestampSeconds,
                    opportunity.ResolutionTimestampSeconds, opportunity.IsHit ? opportunity.ResolutionTimestampSeconds : (double?)null,
                    opportunity.IsHit ? Coordinates(opportunity.FinalAimAzimuthDeg, opportunity.FinalAimElevationDeg) : null,
                    opportunity.IsHit, opportunity.OutcomeReason,
                    Coordinates(opportunity.FinalAimAzimuthDeg, opportunity.FinalAimElevationDeg), null, null,
                    sensitivityValue, opportunity.InitialOffsetDistanceDeg, null, null,
                    opportunity.FinalPrecisionErrorDeg, opportunity.IsCenterHit,
                    opportunity.SignedOverflickUnderflickDeg));
            }
            return records.AsReadOnly();
        }

        private static string Coordinates(double azimuth, double elevation) => string.Format(CultureInfo.InvariantCulture,
            "{0:G17},{1:G17}", azimuth, elevation);
    }
}
