using System;

namespace SensCalibr8.Core.Configuration
{
    public sealed class ConfirmatoryStatisticsContract
    {
        public ConfirmatoryStatisticsContract(string version, int freshPairsRequired, int enumerationCount,
            string testMethod, string alternative, double alpha, double confidenceLevel, double tCritical,
            double comparisonTolerance, bool reuseExploratoryData, bool earlyStopping)
        {
            Version = Required(version, nameof(version));
            FreshPairsRequired = Positive(freshPairsRequired, nameof(freshPairsRequired));
            EnumerationCount = Positive(enumerationCount, nameof(enumerationCount));
            TestMethod = Required(testMethod, nameof(testMethod));
            Alternative = Required(alternative, nameof(alternative));
            Alpha = Probability(alpha, nameof(alpha));
            ConfidenceLevel = Probability(confidenceLevel, nameof(confidenceLevel));
            TCritical = PositiveFinite(tCritical, nameof(tCritical));
            ComparisonTolerance = NonNegativeFinite(comparisonTolerance, nameof(comparisonTolerance));
            ReuseExploratoryData = reuseExploratoryData;
            EarlyStopping = earlyStopping;
            if (freshPairsRequired >= sizeof(int) * 8 - 1 || enumerationCount != 1 << freshPairsRequired)
                throw new ArgumentException("Confirmatory enumeration count must equal 2^fresh-pair count.");
        }

        public string Version { get; }
        public int FreshPairsRequired { get; }
        public int EnumerationCount { get; }
        public string TestMethod { get; }
        public string Alternative { get; }
        public double Alpha { get; }
        public double ConfidenceLevel { get; }
        public double TCritical { get; }
        public double ComparisonTolerance { get; }
        public bool ReuseExploratoryData { get; }
        public bool EarlyStopping { get; }

        private static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
        private static double Probability(double value, string name) => PositiveFinite(value, name) < 1d ? value : throw new ArgumentOutOfRangeException(name);
        private static double PositiveFinite(double value, string name) => NonNegativeFinite(value, name) > 0d ? value : throw new ArgumentOutOfRangeException(name);
        private static double NonNegativeFinite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d ? value : throw new ArgumentOutOfRangeException(name);
    }
}
