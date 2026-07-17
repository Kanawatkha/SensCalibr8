using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SensCalibr8.Core.Configuration
{
    public sealed class PhaseThreeProtocolContract
    {
        public PhaseThreeProtocolContract(string version, IReadOnlyList<double> offsetsPercent,
            string batteryPurpose, string generationRule)
        {
            Version = Required(version, nameof(version));
            if (offsetsPercent == null || offsetsPercent.Count == 0 || offsetsPercent.Any(value => !Finite(value)) || offsetsPercent.Distinct().Count() != offsetsPercent.Count)
                throw new ArgumentException("Phase 3 offsets must be finite and unique.", nameof(offsetsPercent));
            OffsetsPercent = new ReadOnlyCollection<double>(offsetsPercent.ToArray());
            BatteryPurpose = Required(batteryPurpose, nameof(batteryPurpose));
            GenerationRule = Required(generationRule, nameof(generationRule));
        }

        public string Version { get; }
        public IReadOnlyList<double> OffsetsPercent { get; }
        public string BatteryPurpose { get; }
        public string GenerationRule { get; }

        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
