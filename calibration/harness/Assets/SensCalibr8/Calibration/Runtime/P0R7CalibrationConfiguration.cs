using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace SensCalibr8.Calibration
{
    public sealed class P0R7CalibrationSnapshot
    {
        internal P0R7CalibrationSnapshot(P0R7CalibrationConfiguration document, string sha256)
        {
            Document = document;
            Sha256 = sha256;
        }

        public P0R7CalibrationConfiguration Document { get; }
        public string Sha256 { get; }
        public string ConfigVersion => Document.config_version;
        public string FormulaVersion => Document.formula_version;
        public string SignalPipelineVersion => Document.calibration_configs_record.signal_pipeline_version;
        public string TestGeometryVersion => Document.calibration_configs_record.test_geometry_version;
    }

    public static class P0R7CalibrationConfigurationLoader
    {
        public const string ContractId = "sc8-calibration-config-v1";
        public const string ConfigVersion = "calibration_config_v1";

        public static P0R7CalibrationSnapshot Load(
            string configPath,
            string repositoryRoot,
            string expectedConfigSha256)
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                throw new FileNotFoundException("Frozen calibration configuration was not found.", configPath);
            }

            byte[] bytes = File.ReadAllBytes(configPath);
            string actualHash = Sha256(bytes);
            if (!HashEquals(actualHash, expectedConfigSha256))
            {
                throw new InvalidDataException("Frozen calibration configuration SHA-256 mismatch.");
            }

            P0R7CalibrationConfiguration document = JsonUtility.FromJson<P0R7CalibrationConfiguration>(
                System.Text.Encoding.UTF8.GetString(bytes));
            Validate(document, repositoryRoot);
            return new P0R7CalibrationSnapshot(document, actualHash);
        }

        public static void Validate(P0R7CalibrationConfiguration document, string repositoryRoot)
        {
            Require(document != null, "Configuration JSON is invalid.");
            Require(document.contract_id == ContractId, "Unexpected calibration contract ID.");
            Require(document.config_version == ConfigVersion, "Unexpected calibration config version.");
            Require(document.status == "accepted", "Draft or incomplete calibration configuration rejected.");
            Require(document.immutable, "Calibration configuration must be immutable.");
            Require(!string.IsNullOrWhiteSpace(document.created_date), "created_date is required.");
            Require(document.limitations != null && !document.limitations.strict_timing_confirmation_passed,
                "The P0-R3 owner-waiver limitation must remain explicit.");
            Require(document.calibration_configs_record != null, "calibration_configs_record is required.");
            Require(document.source_contracts != null && document.source_contracts.Length == 6,
                "Exactly six frozen source entries are required.");

            P0R7CalibrationDatabaseRecord record = document.calibration_configs_record;
            Require(record.config_version == ConfigVersion, "Database config version mismatch.");
            Require(record.created_date == document.created_date, "Database created date mismatch.");
            Require(!string.IsNullOrWhiteSpace(record.normalization_version), "normalization_version is required.");
            Require(!string.IsNullOrWhiteSpace(record.signal_pipeline_version), "signal_pipeline_version is required.");
            Require(!string.IsNullOrWhiteSpace(record.test_geometry_version), "test_geometry_version is required.");
            Require(!string.IsNullOrWhiteSpace(record.timing_acceptance_policy), "timing_acceptance_policy is required.");
            RequirePositiveFinite(record.input_sampling_rate_hz, "input_sampling_rate_hz");
            RequirePositiveFinite(record.resampling_tolerance_ms, "resampling_tolerance_ms");
            Require(record.butterworth_order > 0, "butterworth_order must be positive.");
            RequirePositiveFinite(record.cutoff_frequency_hz, "cutoff_frequency_hz");
            RequirePositiveFinite(record.submovement_start_deg_per_sec, "submovement_start_deg_per_sec");
            RequirePositiveFinite(record.submovement_end_deg_per_sec, "submovement_end_deg_per_sec");
            RequirePositiveFinite(record.refractory_period_ms, "refractory_period_ms");
            RequireFiniteNonNegative(record.scoring_zero_tolerance, "scoring_zero_tolerance");

            P0R7Geometry geometry = Parse<P0R7Geometry>(record.target_geometry_json, "target_geometry_json");
            P0R7SignalMode signalMode = Parse<P0R7SignalMode>(record.tracking_contract_json, "tracking_contract_json");
            P0R7Normalization normalization = Parse<P0R7Normalization>(record.normalization_bounds_json, "normalization_bounds_json");
            P0R7Submovement submovement = Parse<P0R7Submovement>(record.submovement_bounds_by_mode_json, "submovement_bounds_by_mode_json");
            P0R7Consistency consistency = Parse<P0R7Consistency>(record.consistency_tier_cutpoints_json, "consistency_tier_cutpoints_json");
            P0R7Confirmatory confirmatory = Parse<P0R7Confirmatory>(record.confirmatory_contract_json, "confirmatory_contract_json");

            Require(geometry.status == "accepted" && geometry.test_geometry_version == record.test_geometry_version,
                "Embedded geometry identity mismatch.");
            Require(signalMode.status == "accepted" && signalMode.signal_pipeline_version == record.signal_pipeline_version,
                "Embedded signal/mode identity mismatch.");
            Require(signalMode.mode_contract_version == document.mode_contract_version,
                "Embedded mode-contract identity mismatch.");
            Require(signalMode.signal_pipeline != null, "Embedded signal pipeline is required.");
            Require(Equal(signalMode.signal_pipeline.sampling_rate_hz, record.input_sampling_rate_hz), "Sampling rate drift detected.");
            Require(signalMode.signal_pipeline.filter_order == record.butterworth_order, "Filter-order drift detected.");
            Require(Equal(signalMode.signal_pipeline.cutoff_frequency_hz, record.cutoff_frequency_hz), "Cutoff drift detected.");
            Require(Equal(signalMode.signal_pipeline.start_threshold_deg_per_sec, record.submovement_start_deg_per_sec), "Start-threshold drift detected.");
            Require(Equal(signalMode.signal_pipeline.end_threshold_deg_per_sec, record.submovement_end_deg_per_sec), "End-threshold drift detected.");
            Require(Equal(signalMode.signal_pipeline.refractory_period_ms, record.refractory_period_ms), "Refractory drift detected.");
            Require(normalization.normalization_version == record.normalization_version, "Normalization version drift detected.");
            Require(Equal(normalization.scoring_zero_tolerance_points, record.scoring_zero_tolerance), "Zero-tolerance drift detected.");
            Require(submovement.normalization_version == record.normalization_version, "Submovement normalization drift detected.");
            Require(consistency.consistency_tier_version == document.consistency_tier_version, "Consistency tier drift detected.");
            Require(confirmatory.confirmatory_contract_version == document.confirmatory_contract_version,
                "Confirmatory version drift detected.");
            Require(document.formula_contract != null && document.formula_contract.formula_version == document.formula_version,
                "Formula version drift detected.");

            ValidateSourceManifest(document.source_contracts, repositoryRoot);
        }

        private static void ValidateSourceManifest(P0R7SourceContract[] entries, string repositoryRoot)
        {
            Require(!string.IsNullOrWhiteSpace(repositoryRoot) && Directory.Exists(repositoryRoot),
                "Repository root is required for source verification.");
            var expectedRoles = new HashSet<string>
            {
                "timing", "timing_owner_waiver", "geometry", "signal_mode",
                "scoring_statistics_acceptance", "scoring_statistics_payload"
            };
            foreach (P0R7SourceContract entry in entries)
            {
                Require(entry != null && expectedRoles.Remove(entry.role), "Source role is missing, duplicated, or unexpected.");
                Require(!string.IsNullOrWhiteSpace(entry.path) && !string.IsNullOrWhiteSpace(entry.sha256),
                    "Source path and SHA-256 are required.");
                string path = Path.GetFullPath(Path.Combine(repositoryRoot, entry.path.Replace('/', Path.DirectorySeparatorChar)));
                Require(File.Exists(path), "Frozen source file is missing: " + entry.path);
                Require(HashEquals(Sha256(File.ReadAllBytes(path)), entry.sha256), "Frozen source hash mismatch: " + entry.role);
            }
            Require(expectedRoles.Count == 0, "Frozen source-role set is incomplete.");
        }

        private static T Parse<T>(string json, string field) where T : class
        {
            Require(!string.IsNullOrWhiteSpace(json), field + " is required.");
            T value = JsonUtility.FromJson<T>(json);
            Require(value != null, field + " is invalid.");
            return value;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }

        private static bool HashEquals(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Equal(double left, double right) => Math.Abs(left - right) <= 1e-12;

        private static void RequirePositiveFinite(double value, string field)
        {
            Require(!double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0,
                field + " must be finite and positive.");
        }

        private static void RequireFiniteNonNegative(double value, string field)
        {
            Require(!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0,
                field + " must be finite and non-negative.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }

    [Serializable]
    public sealed class P0R7CalibrationConfiguration
    {
        public string contract_id;
        public string config_version;
        public string status;
        public bool immutable;
        public string created_date;
        public string formula_version;
        public string mode_contract_version;
        public string consistency_tier_version;
        public string confirmatory_contract_version;
        public P0R7SourceContract[] source_contracts;
        public P0R7Limitations limitations;
        public P0R7FormulaContract formula_contract;
        public P0R7CalibrationDatabaseRecord calibration_configs_record;
    }

    [Serializable]
    public sealed class P0R7SourceContract { public string role; public string path; public string sha256; }

    [Serializable]
    public sealed class P0R7Limitations
    {
        public string timing_acceptance;
        public bool strict_timing_confirmation_passed;
        public string strict_candidate_v1_disposition;
        public string strict_candidate_v2_disposition;
        public string scientific_limitation;
    }

    [Serializable]
    public sealed class P0R7FormulaContract { public string formula_version; }

    [Serializable]
    public sealed class P0R7CalibrationDatabaseRecord
    {
        public string config_version;
        public string normalization_version;
        public string signal_pipeline_version;
        public string test_geometry_version;
        public string created_date;
        public double input_sampling_rate_hz;
        public double resampling_tolerance_ms;
        public string timing_acceptance_policy;
        public int butterworth_order;
        public double cutoff_frequency_hz;
        public double submovement_start_deg_per_sec;
        public double submovement_end_deg_per_sec;
        public double refractory_period_ms;
        public string normalization_bounds_json;
        public string submovement_bounds_by_mode_json;
        public string consistency_tier_cutpoints_json;
        public double scoring_zero_tolerance;
        public string target_geometry_json;
        public string tracking_contract_json;
        public string confirmatory_contract_json;
    }

    [Serializable]
    public sealed class P0R7Geometry { public string test_geometry_version; public string status; }

    [Serializable]
    public sealed class P0R7SignalMode
    {
        public string signal_pipeline_version;
        public string mode_contract_version;
        public string status;
        public P0R7SignalPipeline signal_pipeline;
    }

    [Serializable]
    public sealed class P0R7SignalPipeline
    {
        public double sampling_rate_hz;
        public int filter_order;
        public double cutoff_frequency_hz;
        public double start_threshold_deg_per_sec;
        public double end_threshold_deg_per_sec;
        public double refractory_period_ms;
    }

    [Serializable]
    public sealed class P0R7Normalization { public string normalization_version; public double scoring_zero_tolerance_points; }
    [Serializable]
    public sealed class P0R7Submovement { public string normalization_version; }
    [Serializable]
    public sealed class P0R7Consistency { public string consistency_tier_version; }
    [Serializable]
    public sealed class P0R7Confirmatory { public string confirmatory_contract_version; }
}
