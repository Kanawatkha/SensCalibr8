using System;
using System.Collections.Generic;

namespace SensCalibr8.Data.Repositories
{
    public static class AnalysisDatasetContract
    {
        public const string Version = "sc8-analysis-dataset-v1";
        public const string EdpiUnit = "dpi_x_in_game_sensitivity";
        public const string Cm360Unit = "cm_per_360_degrees";
        public const string PerformanceScoreUnit = "performance_score_points";
        public const string ReactionTimeUnit = "milliseconds";
    }

    public sealed class AnalysisProfileIdentity
    {
        public AnalysisProfileIdentity(long profileId, string profileName, long mouseDpi)
        {
            ProfileId = Positive(profileId, nameof(profileId));
            ProfileName = Required(profileName, nameof(profileName));
            MouseDpi = Positive(mouseDpi, nameof(mouseDpi));
        }

        public long ProfileId { get; }
        public string ProfileName { get; }
        public long MouseDpi { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
    }

    public sealed class AnalysisScoreRecord
    {
        public AnalysisScoreRecord(long sensitivityTestId, long cycleId, long batteryId, long phase, double edpi, double cm360,
            double performanceScore, string performanceScoreByModeJson, string grade, string formulaVersion,
            string calibrationConfigurationVersion, string completedDate)
        {
            SensitivityTestId = Positive(sensitivityTestId, nameof(sensitivityTestId));
            CycleId = Positive(cycleId, nameof(cycleId));
            BatteryId = Positive(batteryId, nameof(batteryId));
            Phase = Positive(phase, nameof(phase));
            Edpi = Finite(edpi, nameof(edpi));
            Cm360 = Finite(cm360, nameof(cm360));
            PerformanceScore = Finite(performanceScore, nameof(performanceScore));
            PerformanceScoreByModeJson = Required(performanceScoreByModeJson, nameof(performanceScoreByModeJson));
            Grade = grade;
            FormulaVersion = Required(formulaVersion, nameof(formulaVersion));
            CalibrationConfigurationVersion = Required(calibrationConfigurationVersion, nameof(calibrationConfigurationVersion));
            CompletedDate = Required(completedDate, nameof(completedDate));
        }

        public long SensitivityTestId { get; }
        public long CycleId { get; }
        public long BatteryId { get; }
        public long Phase { get; }
        public double Edpi { get; }
        public double Cm360 { get; }
        public double PerformanceScore { get; }
        public string PerformanceScoreByModeJson { get; }
        public string Grade { get; }
        public string FormulaVersion { get; }
        public string CalibrationConfigurationVersion { get; }
        public string CompletedDate { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
    }

    public sealed class AnalysisSessionRecord
    {
        public AnalysisSessionRecord(long sessionId, long cycleId, long batteryId, long phase, string mode, string date,
            double sensitivityValue, bool isCompleteBattery, bool fatigueFlag, double? fatigueScoreChangePercentage,
            string calibrationConfigurationVersion)
        {
            SessionId = Positive(sessionId, nameof(sessionId));
            CycleId = Positive(cycleId, nameof(cycleId));
            BatteryId = Positive(batteryId, nameof(batteryId));
            Phase = Positive(phase, nameof(phase));
            Mode = Required(mode, nameof(mode));
            Date = Required(date, nameof(date));
            SensitivityValue = Finite(sensitivityValue, nameof(sensitivityValue));
            IsCompleteBattery = isCompleteBattery;
            FatigueFlag = fatigueFlag;
            FatigueScoreChangePercentage = fatigueScoreChangePercentage;
            CalibrationConfigurationVersion = Required(calibrationConfigurationVersion, nameof(calibrationConfigurationVersion));
        }

        public long SessionId { get; }
        public long CycleId { get; }
        public long BatteryId { get; }
        public long Phase { get; }
        public string Mode { get; }
        public string Date { get; }
        public double SensitivityValue { get; }
        public bool IsCompleteBattery { get; }
        public bool FatigueFlag { get; }
        public double? FatigueScoreChangePercentage { get; }
        public string CalibrationConfigurationVersion { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
    }

    public sealed class AnalysisOutlierAggregateRecord
    {
        public AnalysisOutlierAggregateRecord(long analysisRunId, long cycleId, long phase, string mode, double sensitivityValue,
            string metricName, double inclusiveMean, double flaggedExcludedMean, long observationCount, long flaggedCount,
            string algorithmVersion, string calibrationConfigurationVersion)
        {
            AnalysisRunId = Positive(analysisRunId, nameof(analysisRunId));
            CycleId = Positive(cycleId, nameof(cycleId));
            Phase = Positive(phase, nameof(phase));
            Mode = Required(mode, nameof(mode));
            SensitivityValue = Finite(sensitivityValue, nameof(sensitivityValue));
            MetricName = Required(metricName, nameof(metricName));
            InclusiveMean = Finite(inclusiveMean, nameof(inclusiveMean));
            FlaggedExcludedMean = Finite(flaggedExcludedMean, nameof(flaggedExcludedMean));
            ObservationCount = Positive(observationCount, nameof(observationCount));
            FlaggedCount = NonNegative(flaggedCount, nameof(flaggedCount));
            AlgorithmVersion = Required(algorithmVersion, nameof(algorithmVersion));
            CalibrationConfigurationVersion = Required(calibrationConfigurationVersion, nameof(calibrationConfigurationVersion));
        }

        public long AnalysisRunId { get; }
        public long CycleId { get; }
        public long Phase { get; }
        public string Mode { get; }
        public double SensitivityValue { get; }
        public string MetricName { get; }
        public double InclusiveMean { get; }
        public double FlaggedExcludedMean { get; }
        public long ObservationCount { get; }
        public long FlaggedCount { get; }
        public string AlgorithmVersion { get; }
        public string CalibrationConfigurationVersion { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static long NonNegative(long value, string name) => value >= 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
    }

    public sealed class AnalysisProfileDataset
    {
        public AnalysisProfileDataset(AnalysisProfileIdentity profile, IReadOnlyList<AnalysisScoreRecord> authoritativeScores,
            IReadOnlyList<AnalysisSessionRecord> sessions, IReadOnlyList<AnalysisOutlierAggregateRecord> outlierAggregates)
        {
            AnalysisDatasetVersion = AnalysisDatasetContract.Version;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            AuthoritativeScores = authoritativeScores ?? throw new ArgumentNullException(nameof(authoritativeScores));
            Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            OutlierAggregates = outlierAggregates ?? throw new ArgumentNullException(nameof(outlierAggregates));
            EdpiUnit = AnalysisDatasetContract.EdpiUnit;
            Cm360Unit = AnalysisDatasetContract.Cm360Unit;
            PerformanceScoreUnit = AnalysisDatasetContract.PerformanceScoreUnit;
            ReactionTimeUnit = AnalysisDatasetContract.ReactionTimeUnit;
        }

        public string AnalysisDatasetVersion { get; }
        public AnalysisProfileIdentity Profile { get; }
        public IReadOnlyList<AnalysisScoreRecord> AuthoritativeScores { get; }
        public IReadOnlyList<AnalysisSessionRecord> Sessions { get; }
        public IReadOnlyList<AnalysisOutlierAggregateRecord> OutlierAggregates { get; }
        public string EdpiUnit { get; }
        public string Cm360Unit { get; }
        public string PerformanceScoreUnit { get; }
        public string ReactionTimeUnit { get; }
    }
}
