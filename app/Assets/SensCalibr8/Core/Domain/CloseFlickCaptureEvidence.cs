using System;

namespace SensCalibr8.Core.Domain
{
    public sealed class CloseFlickCaptureEvidence
    {
        public CloseFlickCaptureEvidence(long targetId, string targetSize, double targetCenterAzimuthDeg,
            double targetCenterElevationDeg, double initialOffsetDistanceDeg, double visibleTimestampSeconds,
            double? firstMouseMovementTimestampSeconds, double resolutionTimestampSeconds, bool isHit,
            string outcomeReason, double finalAimAzimuthDeg, double finalAimElevationDeg,
            double signedOverflickUnderflickDeg, double finalPrecisionErrorDeg, bool isCenterHit)
        {
            TargetId = targetId >= 0 ? targetId : throw new ArgumentOutOfRangeException(nameof(targetId));
            TargetSize = Required(targetSize, nameof(targetSize));
            TargetCenterAzimuthDeg = Finite(targetCenterAzimuthDeg, nameof(targetCenterAzimuthDeg));
            TargetCenterElevationDeg = Finite(targetCenterElevationDeg, nameof(targetCenterElevationDeg));
            InitialOffsetDistanceDeg = Finite(initialOffsetDistanceDeg, nameof(initialOffsetDistanceDeg));
            VisibleTimestampSeconds = NonNegative(visibleTimestampSeconds, nameof(visibleTimestampSeconds));
            FirstMouseMovementTimestampSeconds = OptionalNonNegative(firstMouseMovementTimestampSeconds, nameof(firstMouseMovementTimestampSeconds));
            ResolutionTimestampSeconds = NonNegative(resolutionTimestampSeconds, nameof(resolutionTimestampSeconds));
            IsHit = isHit;
            OutcomeReason = Required(outcomeReason, nameof(outcomeReason));
            FinalAimAzimuthDeg = Finite(finalAimAzimuthDeg, nameof(finalAimAzimuthDeg));
            FinalAimElevationDeg = Finite(finalAimElevationDeg, nameof(finalAimElevationDeg));
            SignedOverflickUnderflickDeg = Finite(signedOverflickUnderflickDeg, nameof(signedOverflickUnderflickDeg));
            FinalPrecisionErrorDeg = NonNegative(finalPrecisionErrorDeg, nameof(finalPrecisionErrorDeg));
            IsCenterHit = isCenterHit;
        }

        public long TargetId { get; }
        public string TargetSize { get; }
        public double TargetCenterAzimuthDeg { get; }
        public double TargetCenterElevationDeg { get; }
        public double InitialOffsetDistanceDeg { get; }
        public double VisibleTimestampSeconds { get; }
        public double? FirstMouseMovementTimestampSeconds { get; }
        public double ResolutionTimestampSeconds { get; }
        public bool IsHit { get; }
        public string OutcomeReason { get; }
        public double FinalAimAzimuthDeg { get; }
        public double FinalAimElevationDeg { get; }
        public double SignedOverflickUnderflickDeg { get; }
        public double FinalPrecisionErrorDeg { get; }
        public bool IsCenterHit { get; }

        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value)
            ? value : throw new ArgumentOutOfRangeException(name);
        private static double NonNegative(double value, string name) => Finite(value, name) >= 0d
            ? value : throw new ArgumentOutOfRangeException(name);
        private static double? OptionalNonNegative(double? value, string name)
        {
            if (!value.HasValue) return null;
            return NonNegative(value.Value, name);
        }
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(name + " is required.", name);
    }
}
