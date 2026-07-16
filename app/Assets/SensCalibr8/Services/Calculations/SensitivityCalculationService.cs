using System;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Calculations
{
    public sealed class SensitivityCalculationService
    {
        private readonly ResearchConstants constants;

        public SensitivityCalculationService(ResearchConstants constants)
        { this.constants = constants ?? throw new ArgumentNullException(nameof(constants)); }

        public double CalculateEdpi(int hardwareDpi, double sensitivity)
        {
            RequirePositiveDpi(hardwareDpi);
            RequirePositiveFinite(sensitivity, nameof(sensitivity));
            return hardwareDpi * sensitivity;
        }

        public double CalculateStartingSensitivity(int hardwareDpi)
        {
            RequirePositiveDpi(hardwareDpi);
            return constants.PsaBaselineEdpi / hardwareDpi;
        }

        public double CalculatePhysicalRulerDpi(long mouseMovementCounts, double measuredDistanceCm)
        {
            if (mouseMovementCounts <= 0) throw new ArgumentOutOfRangeException(nameof(mouseMovementCounts));
            RequirePositiveFinite(measuredDistanceCm, nameof(measuredDistanceCm));
            return mouseMovementCounts / (measuredDistanceCm / constants.CmPerInch);
        }

        public EdpiFloorResult ApplyEdpiFloor(int hardwareDpi, double sensitivity)
        {
            double originalEdpi = CalculateEdpi(hardwareDpi, sensitivity);
            bool adjusted = originalEdpi < constants.EdpiFloor;
            double effectiveEdpi = adjusted ? constants.EdpiFloor : originalEdpi;
            return new EdpiFloorResult(originalEdpi, effectiveEdpi, effectiveEdpi / hardwareDpi, adjusted);
        }

        public double CalculateCentimetersPer360(int hardwareDpi, double sensitivity)
        {
            double edpi = CalculateEdpi(hardwareDpi, sensitivity);
            return (constants.CmPerInch * constants.DegreesPerTurn) / (edpi * constants.ValorantYawMultiplier);
        }

        public BaselineComparisonResult CompareCurrentToPsaBaseline(int hardwareDpi, double currentSensitivity)
        {
            double currentEdpi = CalculateEdpi(hardwareDpi, currentSensitivity);
            double difference = currentEdpi - constants.PsaBaselineEdpi;
            BaselineRelationship relationship = difference < 0d ? BaselineRelationship.Below : difference > 0d ? BaselineRelationship.Above : BaselineRelationship.Equal;
            return new BaselineComparisonResult(currentSensitivity, CalculateStartingSensitivity(hardwareDpi), currentEdpi, constants.PsaBaselineEdpi, difference, relationship);
        }

        public MousepadConstraintResult EvaluateMousepadConstraint(int hardwareDpi, double sensitivity, double mousepadWidthCm)
        {
            RequirePositiveFinite(mousepadWidthCm, nameof(mousepadWidthCm));
            double centimetersPer360 = CalculateCentimetersPer360(hardwareDpi, sensitivity);
            return new MousepadConstraintResult(centimetersPer360, mousepadWidthCm, centimetersPer360 > mousepadWidthCm);
        }

        private static void RequirePositiveDpi(int value)
        { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Hardware DPI must be a positive integer."); }
        private static void RequirePositiveFinite(double value, string field)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d) throw new ArgumentOutOfRangeException(field); }
    }
}
