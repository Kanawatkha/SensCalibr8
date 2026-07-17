using System;

namespace SensCalibr8.Core.Configuration
{
    public sealed class ScoringFormulaContract
    {
        public ScoringFormulaContract(string formulaVersion, double multiplier, int authoritativeShotObservations,
            int authoritativeTrackingWindows, double shotConsistencyWeight, double shotAccuracyWeight,
            double shotReactionWeight, double shotPrecisionWeight, double shotSubmovementPenaltyWeight,
            double trackingConsistencyWeight, double trackingTimeOnTargetWeight, double trackingPrecisionWeight)
        {
            FormulaVersion = Required(formulaVersion, nameof(formulaVersion));
            Multiplier = Positive(multiplier, nameof(multiplier));
            AuthoritativeShotObservations = Positive(authoritativeShotObservations, nameof(authoritativeShotObservations));
            AuthoritativeTrackingWindows = Positive(authoritativeTrackingWindows, nameof(authoritativeTrackingWindows));
            ShotConsistencyWeight = NonNegative(shotConsistencyWeight, nameof(shotConsistencyWeight));
            ShotAccuracyWeight = NonNegative(shotAccuracyWeight, nameof(shotAccuracyWeight));
            ShotReactionWeight = NonNegative(shotReactionWeight, nameof(shotReactionWeight));
            ShotPrecisionWeight = NonNegative(shotPrecisionWeight, nameof(shotPrecisionWeight));
            ShotSubmovementPenaltyWeight = NonNegative(shotSubmovementPenaltyWeight, nameof(shotSubmovementPenaltyWeight));
            TrackingConsistencyWeight = NonNegative(trackingConsistencyWeight, nameof(trackingConsistencyWeight));
            TrackingTimeOnTargetWeight = NonNegative(trackingTimeOnTargetWeight, nameof(trackingTimeOnTargetWeight));
            TrackingPrecisionWeight = NonNegative(trackingPrecisionWeight, nameof(trackingPrecisionWeight));
        }

        public string FormulaVersion { get; }
        public double Multiplier { get; }
        public int AuthoritativeShotObservations { get; }
        public int AuthoritativeTrackingWindows { get; }
        public double ShotConsistencyWeight { get; }
        public double ShotAccuracyWeight { get; }
        public double ShotReactionWeight { get; }
        public double ShotPrecisionWeight { get; }
        public double ShotSubmovementPenaltyWeight { get; }
        public double TrackingConsistencyWeight { get; }
        public double TrackingTimeOnTargetWeight { get; }
        public double TrackingPrecisionWeight { get; }

        private static string Required(string value, string field) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(field + " is required.", field);
        private static int Positive(int value, string field) => value > 0 ? value : throw new ArgumentOutOfRangeException(field);
        private static double Positive(double value, string field) => Finite(value) && value > 0d ? value : throw new ArgumentOutOfRangeException(field);
        private static double NonNegative(double value, string field) => Finite(value) && value >= 0d ? value : throw new ArgumentOutOfRangeException(field);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
