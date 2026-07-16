namespace SensCalibr8.Services.Calculations
{
    public enum BaselineRelationship
    {
        Below,
        Equal,
        Above
    }

    public sealed class EdpiFloorResult
    {
        public EdpiFloorResult(double originalEdpi, double effectiveEdpi, double effectiveSensitivity, bool wasAdjusted)
        { OriginalEdpi = originalEdpi; EffectiveEdpi = effectiveEdpi; EffectiveSensitivity = effectiveSensitivity; WasAdjusted = wasAdjusted; }
        public double OriginalEdpi { get; }
        public double EffectiveEdpi { get; }
        public double EffectiveSensitivity { get; }
        public bool WasAdjusted { get; }
    }

    public sealed class BaselineComparisonResult
    {
        public BaselineComparisonResult(double currentSensitivity, double startingSensitivity, double currentEdpi, double baselineEdpi, double edpiDifference, BaselineRelationship relationship)
        { CurrentSensitivity = currentSensitivity; StartingSensitivity = startingSensitivity; CurrentEdpi = currentEdpi; BaselineEdpi = baselineEdpi; EdpiDifference = edpiDifference; Relationship = relationship; }
        public double CurrentSensitivity { get; }
        public double StartingSensitivity { get; }
        public double CurrentEdpi { get; }
        public double BaselineEdpi { get; }
        public double EdpiDifference { get; }
        public BaselineRelationship Relationship { get; }
    }

    public sealed class MousepadConstraintResult
    {
        public MousepadConstraintResult(double centimetersPer360, double mousepadWidthCm, bool warningRequired)
        { CentimetersPer360 = centimetersPer360; MousepadWidthCm = mousepadWidthCm; WarningRequired = warningRequired; }
        public double CentimetersPer360 { get; }
        public double MousepadWidthCm { get; }
        public bool WarningRequired { get; }
    }
}
