using System;

namespace SensCalibr8.Core.Domain
{
    public enum TestMode
    {
        FlickClose,
        FlickFar,
        Tracking,
        MicroCorrection
    }

    public enum ProtocolPhase
    {
        PhaseOne,
        PhaseTwo,
        PhaseThree
    }

    public enum PerformanceGrade
    {
        S,
        A,
        B,
        C,
        D
    }

    public enum DominantHand { Left, Right }
    public enum MovementStrategy { Wrist, Arm, Hybrid }
    public enum GripStyle { Fingertip, Palm, Claw, Hybrid }

    public readonly struct FormulaVersion : IEquatable<FormulaVersion>
    {
        public FormulaVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Formula version is required.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool Equals(FormulaVersion other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FormulaVersion other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct CalibrationConfigVersion : IEquatable<CalibrationConfigVersion>
    {
        public CalibrationConfigVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Calibration configuration version is required.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool Equals(CalibrationConfigVersion other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CalibrationConfigVersion other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }
}
