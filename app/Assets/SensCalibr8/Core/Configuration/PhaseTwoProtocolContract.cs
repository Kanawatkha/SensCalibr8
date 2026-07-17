using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SensCalibr8.Core.Configuration
{
    public sealed class PhaseTwoProtocolContract
    {
        public PhaseTwoProtocolContract(string version, IReadOnlyList<double> offsetsPercent,
            int minimumCompleteBatteries, int maximumCompleteBatteries, double stabilizationCvExclusiveUpper,
            string batteryPurpose, string singleAnchorGenerationRule, string tieGenerationRule)
        {
            Version = Required(version, nameof(version));
            if (offsetsPercent == null || offsetsPercent.Count == 0 || offsetsPercent.Any(value => !Finite(value)) || offsetsPercent.Distinct().Count() != offsetsPercent.Count)
                throw new ArgumentException("Phase 2 offsets must be finite and unique.", nameof(offsetsPercent));
            OffsetsPercent = new ReadOnlyCollection<double>(offsetsPercent.ToArray());
            MinimumCompleteBatteries = minimumCompleteBatteries > 1 ? minimumCompleteBatteries : throw new ArgumentOutOfRangeException(nameof(minimumCompleteBatteries));
            MaximumCompleteBatteries = maximumCompleteBatteries >= minimumCompleteBatteries ? maximumCompleteBatteries : throw new ArgumentOutOfRangeException(nameof(maximumCompleteBatteries));
            StabilizationCvExclusiveUpper = PositiveFinite(stabilizationCvExclusiveUpper, nameof(stabilizationCvExclusiveUpper));
            BatteryPurpose = Required(batteryPurpose, nameof(batteryPurpose));
            SingleAnchorGenerationRule = Required(singleAnchorGenerationRule, nameof(singleAnchorGenerationRule));
            TieGenerationRule = Required(tieGenerationRule, nameof(tieGenerationRule));
        }

        public string Version { get; }
        public IReadOnlyList<double> OffsetsPercent { get; }
        public int MinimumCompleteBatteries { get; }
        public int MaximumCompleteBatteries { get; }
        public double StabilizationCvExclusiveUpper { get; }
        public string BatteryPurpose { get; }
        public string SingleAnchorGenerationRule { get; }
        public string TieGenerationRule { get; }

        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double PositiveFinite(double value, string name) => Finite(value) && value > 0d ? value : throw new ArgumentOutOfRangeException(name);
    }
}
