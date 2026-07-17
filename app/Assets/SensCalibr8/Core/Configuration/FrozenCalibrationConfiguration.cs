using System;
using System.Collections.Generic;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.Core.Configuration
{
    public sealed class FrozenCalibrationConfiguration
    {
        public FrozenCalibrationConfiguration(
            CalibrationConfigVersion configVersion,
            FormulaVersion formulaVersion,
            string contractId,
            string sha256,
            CalibrationConfigurationRecord record,
            IReadOnlyList<SourceContract> sourceContracts,
            ScoringFormulaContract scoringFormula)
        {
            ConfigVersion = configVersion;
            FormulaVersion = formulaVersion;
            ContractId = Require(contractId, nameof(contractId));
            Sha256 = Require(sha256, nameof(sha256));
            Record = record ?? throw new ArgumentNullException(nameof(record));
            SourceContracts = sourceContracts ?? throw new ArgumentNullException(nameof(sourceContracts));
            ScoringFormula = scoringFormula ?? throw new ArgumentNullException(nameof(scoringFormula));
        }

        public CalibrationConfigVersion ConfigVersion { get; }
        public FormulaVersion FormulaVersion { get; }
        public string ContractId { get; }
        public string Sha256 { get; }
        public CalibrationConfigurationRecord Record { get; }
        public IReadOnlyList<SourceContract> SourceContracts { get; }
        public ScoringFormulaContract ScoringFormula { get; }

        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class CalibrationConfigurationRecord
    {
        public CalibrationConfigurationRecord(
            string configVersion, string normalizationVersion, string signalPipelineVersion, string testGeometryVersion,
            string createdDate, double inputSamplingRateHz, double resamplingToleranceMs, string timingAcceptancePolicy,
            int butterworthOrder, double cutoffFrequencyHz, double submovementStartDegPerSec,
            double submovementEndDegPerSec, double refractoryPeriodMs, string normalizationBoundsJson,
            string submovementBoundsByModeJson, string consistencyTierCutpointsJson, double scoringZeroTolerance,
            string targetGeometryJson, string trackingContractJson, string confirmatoryContractJson)
        {
            ConfigVersion = Required(configVersion, nameof(configVersion));
            NormalizationVersion = Required(normalizationVersion, nameof(normalizationVersion));
            SignalPipelineVersion = Required(signalPipelineVersion, nameof(signalPipelineVersion));
            TestGeometryVersion = Required(testGeometryVersion, nameof(testGeometryVersion));
            CreatedDate = Required(createdDate, nameof(createdDate));
            InputSamplingRateHz = Positive(inputSamplingRateHz, nameof(inputSamplingRateHz));
            ResamplingToleranceMs = Positive(resamplingToleranceMs, nameof(resamplingToleranceMs));
            TimingAcceptancePolicy = Required(timingAcceptancePolicy, nameof(timingAcceptancePolicy));
            ButterworthOrder = butterworthOrder > 0 ? butterworthOrder : throw new ArgumentOutOfRangeException(nameof(butterworthOrder));
            CutoffFrequencyHz = Positive(cutoffFrequencyHz, nameof(cutoffFrequencyHz));
            SubmovementStartDegPerSec = Positive(submovementStartDegPerSec, nameof(submovementStartDegPerSec));
            SubmovementEndDegPerSec = Positive(submovementEndDegPerSec, nameof(submovementEndDegPerSec));
            RefractoryPeriodMs = Positive(refractoryPeriodMs, nameof(refractoryPeriodMs));
            NormalizationBoundsJson = Required(normalizationBoundsJson, nameof(normalizationBoundsJson));
            SubmovementBoundsByModeJson = Required(submovementBoundsByModeJson, nameof(submovementBoundsByModeJson));
            ConsistencyTierCutpointsJson = Required(consistencyTierCutpointsJson, nameof(consistencyTierCutpointsJson));
            ScoringZeroTolerance = NonNegative(scalingZeroTolerance: scoringZeroTolerance, nameof(scoringZeroTolerance));
            TargetGeometryJson = Required(targetGeometryJson, nameof(targetGeometryJson));
            TrackingContractJson = Required(trackingContractJson, nameof(trackingContractJson));
            ConfirmatoryContractJson = Required(confirmatoryContractJson, nameof(confirmatoryContractJson));
        }

        public string ConfigVersion { get; }
        public string NormalizationVersion { get; }
        public string SignalPipelineVersion { get; }
        public string TestGeometryVersion { get; }
        public string CreatedDate { get; }
        public double InputSamplingRateHz { get; }
        public double ResamplingToleranceMs { get; }
        public string TimingAcceptancePolicy { get; }
        public int ButterworthOrder { get; }
        public double CutoffFrequencyHz { get; }
        public double SubmovementStartDegPerSec { get; }
        public double SubmovementEndDegPerSec { get; }
        public double RefractoryPeriodMs { get; }
        public string NormalizationBoundsJson { get; }
        public string SubmovementBoundsByModeJson { get; }
        public string ConsistencyTierCutpointsJson { get; }
        public double ScoringZeroTolerance { get; }
        public string TargetGeometryJson { get; }
        public string TrackingContractJson { get; }
        public string ConfirmatoryContractJson { get; }

        private static string Required(string value, string field) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(field + " is required.", field);
        private static double Positive(double value, string field) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d
            ? value : throw new ArgumentOutOfRangeException(field);
        private static double NonNegative(double scalingZeroTolerance, string field) => !double.IsNaN(scalingZeroTolerance) && !double.IsInfinity(scalingZeroTolerance) && scalingZeroTolerance >= 0d
            ? scalingZeroTolerance : throw new ArgumentOutOfRangeException(field);
    }

    public sealed class SourceContract
    {
        public SourceContract(string role, string path, string sha256)
        {
            Role = Required(role, nameof(role));
            Path = Required(path, nameof(path));
            Sha256 = Required(sha256, nameof(sha256));
        }

        public string Role { get; }
        public string Path { get; }
        public string Sha256 { get; }

        private static string Required(string value, string field) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(field + " is required.", field);
    }
}
