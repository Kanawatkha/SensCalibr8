using System;

namespace SensCalibr8.Core.Configuration
{
    public sealed class ResearchConstants
    {
        public ResearchConstants(string version, double psaBaselineEdpi, double edpiFloor, double cmPerInch,
            double degreesPerTurn, double valorantYawMultiplier, double fittsDistanceMultiplier,
            double headshotReferenceCeilingPercent, double outlierSampleSdMultiplier, double gripTensionMinPercent,
            double gripTensionMaxPercent, double excessiveGripTensionPercent, double wristWarningEdpiExclusiveUpper)
        {
            Version = Required(version, nameof(version));
            PsaBaselineEdpi = Positive(psaBaselineEdpi, nameof(psaBaselineEdpi));
            EdpiFloor = Positive(edpiFloor, nameof(edpiFloor));
            CmPerInch = Positive(cmPerInch, nameof(cmPerInch));
            DegreesPerTurn = Positive(degreesPerTurn, nameof(degreesPerTurn));
            ValorantYawMultiplier = Positive(valorantYawMultiplier, nameof(valorantYawMultiplier));
            FittsDistanceMultiplier = Positive(fittsDistanceMultiplier, nameof(fittsDistanceMultiplier));
            HeadshotReferenceCeilingPercent = Positive(headshotReferenceCeilingPercent, nameof(headshotReferenceCeilingPercent));
            OutlierSampleSdMultiplier = Positive(outlierSampleSdMultiplier, nameof(outlierSampleSdMultiplier));
            GripTensionMinPercent = Positive(gripTensionMinPercent, nameof(gripTensionMinPercent));
            GripTensionMaxPercent = Positive(gripTensionMaxPercent, nameof(gripTensionMaxPercent));
            ExcessiveGripTensionPercent = Positive(excessiveGripTensionPercent, nameof(excessiveGripTensionPercent));
            WristWarningEdpiExclusiveUpper = Positive(wristWarningEdpiExclusiveUpper, nameof(wristWarningEdpiExclusiveUpper));
        }

        public string Version { get; }
        public double PsaBaselineEdpi { get; }
        public double EdpiFloor { get; }
        public double CmPerInch { get; }
        public double DegreesPerTurn { get; }
        public double ValorantYawMultiplier { get; }
        public double FittsDistanceMultiplier { get; }
        public double HeadshotReferenceCeilingPercent { get; }
        public double OutlierSampleSdMultiplier { get; }
        public double GripTensionMinPercent { get; }
        public double GripTensionMaxPercent { get; }
        public double ExcessiveGripTensionPercent { get; }
        public double WristWarningEdpiExclusiveUpper { get; }

        private static string Required(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
        private static double Positive(double value, string field) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d ? value : throw new ArgumentOutOfRangeException(field);
    }
}
