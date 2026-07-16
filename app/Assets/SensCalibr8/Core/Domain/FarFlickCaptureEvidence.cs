using System;

namespace SensCalibr8.Core.Domain
{
    public sealed class FarFlickCaptureEvidence
    {
        public FarFlickCaptureEvidence(long targetId, string targetSize, double targetCenterAzimuthDeg,
            double targetCenterElevationDeg, double initialOffsetDistanceDeg, double previewTimestampSeconds,
            double activationTimestampSeconds, double? movementOnsetTimestampSeconds,
            double resolutionTimestampSeconds, bool isHit, string outcomeReason, double finalAimAzimuthDeg,
            double finalAimElevationDeg, double signedOverflickUnderflickDeg, double finalPrecisionErrorDeg,
            bool isCenterHit)
        {
            TargetId = targetId >= 0 ? targetId : throw new ArgumentOutOfRangeException(nameof(targetId));
            TargetSize = Required(targetSize, nameof(targetSize));
            TargetCenterAzimuthDeg = Finite(targetCenterAzimuthDeg, nameof(targetCenterAzimuthDeg));
            TargetCenterElevationDeg = Finite(targetCenterElevationDeg, nameof(targetCenterElevationDeg));
            InitialOffsetDistanceDeg = Finite(initialOffsetDistanceDeg, nameof(initialOffsetDistanceDeg));
            PreviewTimestampSeconds = NonNegative(previewTimestampSeconds, nameof(previewTimestampSeconds));
            ActivationTimestampSeconds = NonNegative(activationTimestampSeconds, nameof(activationTimestampSeconds));
            MovementOnsetTimestampSeconds = OptionalNonNegative(movementOnsetTimestampSeconds, nameof(movementOnsetTimestampSeconds));
            ResolutionTimestampSeconds = NonNegative(resolutionTimestampSeconds, nameof(resolutionTimestampSeconds));
            IsHit = isHit;
            OutcomeReason = Required(outcomeReason, nameof(outcomeReason));
            FinalAimAzimuthDeg = Finite(finalAimAzimuthDeg, nameof(finalAimAzimuthDeg));
            FinalAimElevationDeg = Finite(finalAimElevationDeg, nameof(finalAimElevationDeg));
            SignedOverflickUnderflickDeg = Finite(signedOverflickUnderflickDeg, nameof(signedOverflickUnderflickDeg));
            FinalPrecisionErrorDeg = NonNegative(finalPrecisionErrorDeg, nameof(finalPrecisionErrorDeg));
            IsCenterHit = isCenterHit;
            if (ActivationTimestampSeconds < PreviewTimestampSeconds || ResolutionTimestampSeconds < ActivationTimestampSeconds ||
                MovementOnsetTimestampSeconds.HasValue && (MovementOnsetTimestampSeconds.Value < ActivationTimestampSeconds || MovementOnsetTimestampSeconds.Value > ResolutionTimestampSeconds))
                throw new ArgumentException("Far Flick timestamps are not ordered.");
        }

        public long TargetId { get; }
        public string TargetSize { get; }
        public double TargetCenterAzimuthDeg { get; }
        public double TargetCenterElevationDeg { get; }
        public double InitialOffsetDistanceDeg { get; }
        public double PreviewTimestampSeconds { get; }
        public double ActivationTimestampSeconds { get; }
        public double? MovementOnsetTimestampSeconds { get; }
        public double ResolutionTimestampSeconds { get; }
        public bool IsHit { get; }
        public string OutcomeReason { get; }
        public double FinalAimAzimuthDeg { get; }
        public double FinalAimElevationDeg { get; }
        public double SignedOverflickUnderflickDeg { get; }
        public double FinalPrecisionErrorDeg { get; }
        public bool IsCenterHit { get; }
        public double? TravelTimeSeconds => MovementOnsetTimestampSeconds.HasValue
            ? ResolutionTimestampSeconds - MovementOnsetTimestampSeconds.Value : (double?)null;

        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
        private static double NonNegative(double value, string name) => Finite(value, name) >= 0d ? value : throw new ArgumentOutOfRangeException(name);
        private static double? OptionalNonNegative(double? value, string name) { if (!value.HasValue) return null; return NonNegative(value.Value, name); }
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
    }
}
