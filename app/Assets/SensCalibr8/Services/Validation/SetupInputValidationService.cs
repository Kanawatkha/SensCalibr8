using System.Globalization;

namespace SensCalibr8.Services.Validation
{
    public static class SetupInputValidationService
    {
        public static ValidationResult<int> HardwareDpi(string input)
        {
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value <= 0)
                return ValidationResult<int>.Invalid("hardware_dpi_positive_integer_required");
            return ValidationResult<int>.Valid(value);
        }

        public static ValidationResult<double> CurrentSensitivity(string input) => PositiveNumber(input, "current_sensitivity_positive_number_required");
        public static ValidationResult<double> ConfiguredPollingRateHz(string input) => PositiveNumber(input, "configured_polling_rate_positive_number_required");
        public static ValidationResult<double> MousepadWidthCm(string input) => PositiveNumber(input, "mousepad_width_positive_number_required");
        public static ValidationResult<double> MousepadHeightCm(string input) => PositiveNumber(input, "mousepad_height_positive_number_required");
        public static ValidationResult<double> PhysicalRulerDistanceCm(string input) => PositiveNumber(input, "physical_ruler_distance_positive_number_required");

        public static ValidationResult<long> PhysicalRulerCounts(string input)
        {
            if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) || value <= 0)
                return ValidationResult<long>.Invalid("physical_ruler_counts_positive_integer_required");
            return ValidationResult<long>.Valid(value);
        }

        private static ValidationResult<double> PositiveNumber(string input, string errorCode)
        {
            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                return ValidationResult<double>.Invalid(errorCode);
            return ValidationResult<double>.Valid(value);
        }
    }
}
