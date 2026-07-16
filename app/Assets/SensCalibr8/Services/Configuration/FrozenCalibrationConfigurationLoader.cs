using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.Services.Configuration
{
    public static class FrozenCalibrationConfigurationLoader
    {
        public const string AcceptedConfigurationPath = "calibration/plans/calibration-config-v1.json";
        public const string AcceptanceEnvelopePath = "calibration/plans/p0-r7-calibration-config-accepted-v1.json";
        public const string ExpectedContractId = "sc8-calibration-config-v1";
        public const string ExpectedConfigVersion = "calibration_config_v1";

        private static readonly string[] RequiredRecordFields =
        {
            "config_version", "normalization_version", "signal_pipeline_version", "test_geometry_version", "created_date",
            "input_sampling_rate_hz", "resampling_tolerance_ms", "timing_acceptance_policy", "butterworth_order",
            "cutoff_frequency_hz", "submovement_start_deg_per_sec", "submovement_end_deg_per_sec", "refractory_period_ms",
            "normalization_bounds_json", "submovement_bounds_by_mode_json", "consistency_tier_cutpoints_json",
            "scoring_zero_tolerance", "target_geometry_json", "tracking_contract_json", "confirmatory_contract_json"
        };

        private static readonly string[] RequiredSourceRoles =
        {
            "timing", "timing_owner_waiver", "geometry", "signal_mode", "scoring_statistics_acceptance", "scoring_statistics_payload"
        };

        public static FrozenCalibrationConfiguration LoadFromRepository(string repositoryRoot)
        {
            return Load(repositoryRoot, AcceptedConfigurationPath, AcceptanceEnvelopePath);
        }

        public static FrozenCalibrationConfiguration Load(string repositoryRoot, string configRelativePath, string envelopeRelativePath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
                throw new InvalidDataException("Repository root is required.");

            string envelopePath = ResolveInsideRepository(repositoryRoot, envelopeRelativePath);
            using JsonDocument envelope = ParseFile(envelopePath, "Acceptance envelope");
            JsonElement root = envelope.RootElement;
            RequireString(root, "status", "accepted");
            RequireString(root, "config_contract_id", ExpectedContractId);
            RequireString(root, "config_version", ExpectedConfigVersion);
            string expectedHash = RequiredString(root, "config_sha256");
            string acceptedPath = RequiredString(root, "config_path");
            if (!string.Equals(NormalizePath(acceptedPath), NormalizePath(configRelativePath), StringComparison.Ordinal))
                throw new InvalidDataException("Acceptance envelope config path mismatch.");

            string configPath = ResolveInsideRepository(repositoryRoot, configRelativePath);
            byte[] bytes = File.ReadAllBytes(configPath);
            string actualHash = Sha256(bytes);
            if (!HashEquals(actualHash, expectedHash)) throw new InvalidDataException("Frozen configuration SHA-256 mismatch.");

            using JsonDocument document = Parse(bytes, "Frozen calibration configuration");
            return ValidateAndBuild(document.RootElement, repositoryRoot, actualHash);
        }

        private static FrozenCalibrationConfiguration ValidateAndBuild(JsonElement root, string repositoryRoot, string sha256)
        {
            RequireString(root, "contract_id", ExpectedContractId);
            RequireString(root, "config_version", ExpectedConfigVersion);
            RequireString(root, "status", "accepted");
            if (!GetRequiredProperty(root, "immutable").GetBoolean()) throw new InvalidDataException("Configuration must be immutable.");
            if (GetRequiredProperty(root, "limitations").GetProperty("strict_timing_confirmation_passed").GetBoolean())
                throw new InvalidDataException("The accepted timing limitation must remain explicit.");

            string formulaVersion = RequiredString(root, "formula_version");
            string modeContractVersion = RequiredString(root, "mode_contract_version");
            string consistencyTierVersion = RequiredString(root, "consistency_tier_version");
            string confirmatoryContractVersion = RequiredString(root, "confirmatory_contract_version");
            JsonElement recordElement = GetRequiredProperty(root, "calibration_configs_record");
            ValidateExactRecordShape(recordElement);
            CalibrationConfigurationRecord record = BuildRecord(recordElement);
            ValidateEmbeddedContracts(record, formulaVersion, modeContractVersion, consistencyTierVersion, confirmatoryContractVersion, root);
            IReadOnlyList<SourceContract> sources = ValidateSources(root, repositoryRoot);

            return new FrozenCalibrationConfiguration(
                new CalibrationConfigVersion(ExpectedConfigVersion), new FormulaVersion(formulaVersion),
                ExpectedContractId, sha256, record, sources);
        }

        private static CalibrationConfigurationRecord BuildRecord(JsonElement record)
        {
            return new CalibrationConfigurationRecord(
                RequiredString(record, "config_version"), RequiredString(record, "normalization_version"),
                RequiredString(record, "signal_pipeline_version"), RequiredString(record, "test_geometry_version"),
                RequiredString(record, "created_date"), RequiredFiniteNumber(record, "input_sampling_rate_hz"),
                RequiredFiniteNumber(record, "resampling_tolerance_ms"), RequiredString(record, "timing_acceptance_policy"),
                RequiredInt(record, "butterworth_order"), RequiredFiniteNumber(record, "cutoff_frequency_hz"),
                RequiredFiniteNumber(record, "submovement_start_deg_per_sec"), RequiredFiniteNumber(record, "submovement_end_deg_per_sec"),
                RequiredFiniteNumber(record, "refractory_period_ms"), RequiredCanonicalJsonString(record, "normalization_bounds_json"),
                RequiredCanonicalJsonString(record, "submovement_bounds_by_mode_json"), RequiredCanonicalJsonString(record, "consistency_tier_cutpoints_json"),
                RequiredFiniteNumber(record, "scoring_zero_tolerance"), RequiredCanonicalJsonString(record, "target_geometry_json"),
                RequiredCanonicalJsonString(record, "tracking_contract_json"), RequiredCanonicalJsonString(record, "confirmatory_contract_json"));
        }

        private static void ValidateEmbeddedContracts(CalibrationConfigurationRecord record, string formulaVersion,
            string modeContractVersion, string consistencyTierVersion, string confirmatoryContractVersion, JsonElement root)
        {
            using JsonDocument normalization = ParseString(record.NormalizationBoundsJson, "normalization_bounds_json");
            using JsonDocument submovement = ParseString(record.SubmovementBoundsByModeJson, "submovement_bounds_by_mode_json");
            using JsonDocument tiers = ParseString(record.ConsistencyTierCutpointsJson, "consistency_tier_cutpoints_json");
            using JsonDocument geometry = ParseString(record.TargetGeometryJson, "target_geometry_json");
            using JsonDocument tracking = ParseString(record.TrackingContractJson, "tracking_contract_json");
            using JsonDocument confirmatory = ParseString(record.ConfirmatoryContractJson, "confirmatory_contract_json");

            RequireString(normalization.RootElement, "normalization_version", record.NormalizationVersion);
            RequireEqual(normalization.RootElement.GetProperty("scoring_zero_tolerance_points").GetDouble(), record.ScoringZeroTolerance, "Scoring-zero tolerance drift.");
            RequireString(submovement.RootElement, "normalization_version", record.NormalizationVersion);
            RequireString(tiers.RootElement, "consistency_tier_version", consistencyTierVersion);
            RequireString(geometry.RootElement, "status", "accepted");
            RequireString(geometry.RootElement, "test_geometry_version", record.TestGeometryVersion);
            RequireString(tracking.RootElement, "status", "accepted");
            RequireString(tracking.RootElement, "signal_pipeline_version", record.SignalPipelineVersion);
            RequireString(tracking.RootElement, "mode_contract_version", modeContractVersion);
            JsonElement pipeline = tracking.RootElement.GetProperty("signal_pipeline");
            RequireEqual(pipeline.GetProperty("sampling_rate_hz").GetDouble(), record.InputSamplingRateHz, "Sampling-rate drift.");
            if (pipeline.GetProperty("filter_order").GetInt32() != record.ButterworthOrder) throw new InvalidDataException("Filter-order drift.");
            RequireEqual(pipeline.GetProperty("cutoff_frequency_hz").GetDouble(), record.CutoffFrequencyHz, "Cutoff-frequency drift.");
            RequireEqual(pipeline.GetProperty("start_threshold_deg_per_sec").GetDouble(), record.SubmovementStartDegPerSec, "Start-threshold drift.");
            RequireEqual(pipeline.GetProperty("end_threshold_deg_per_sec").GetDouble(), record.SubmovementEndDegPerSec, "End-threshold drift.");
            RequireEqual(pipeline.GetProperty("refractory_period_ms").GetDouble(), record.RefractoryPeriodMs, "Refractory-period drift.");
            RequireString(confirmatory.RootElement, "confirmatory_contract_version", confirmatoryContractVersion);
            RequireString(root.GetProperty("formula_contract"), "formula_version", formulaVersion);
        }

        private static IReadOnlyList<SourceContract> ValidateSources(JsonElement root, string repositoryRoot)
        {
            var expectedRoles = new HashSet<string>(RequiredSourceRoles, StringComparer.Ordinal);
            var sources = new List<SourceContract>();
            foreach (JsonElement source in GetRequiredProperty(root, "source_contracts").EnumerateArray())
            {
                string role = RequiredString(source, "role");
                if (!expectedRoles.Remove(role)) throw new InvalidDataException("Source role is duplicated or unexpected: " + role);
                string path = RequiredString(source, "path");
                string expectedHash = RequiredString(source, "sha256");
                string fullPath = ResolveInsideRepository(repositoryRoot, path);
                if (!File.Exists(fullPath)) throw new FileNotFoundException("Frozen source is missing.", fullPath);
                if (!HashEquals(Sha256(File.ReadAllBytes(fullPath)), expectedHash)) throw new InvalidDataException("Frozen source hash mismatch: " + role);
                sources.Add(new SourceContract(role, path, expectedHash));
            }
            if (expectedRoles.Count != 0) throw new InvalidDataException("Frozen source role set is incomplete.");
            return sources.AsReadOnly();
        }

        private static void ValidateExactRecordShape(JsonElement record)
        {
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in record.EnumerateObject()) actual.Add(property.Name);
            if (actual.Count != RequiredRecordFields.Length) throw new InvalidDataException("Calibration record must project exactly 20 fields.");
            foreach (string required in RequiredRecordFields)
                if (!actual.Contains(required)) throw new InvalidDataException("Calibration record field is missing: " + required);
        }

        private static string RequiredCanonicalJsonString(JsonElement parent, string name)
        {
            string json = RequiredString(parent, name);
            using JsonDocument parsed = ParseString(json, name);
            string canonical = JsonSerializer.Serialize(parsed.RootElement);
            if (!string.Equals(json, canonical, StringComparison.Ordinal)) throw new InvalidDataException(name + " must be canonical compact JSON.");
            return json;
        }

        private static JsonDocument ParseFile(string path, string label) => Parse(File.ReadAllBytes(path), label);
        private static JsonDocument ParseString(string value, string label) => Parse(Encoding.UTF8.GetBytes(value), label);
        private static JsonDocument Parse(byte[] value, string label)
        {
            try { return JsonDocument.Parse(value); }
            catch (JsonException exception) { throw new InvalidDataException(label + " is invalid JSON.", exception); }
        }

        private static JsonElement GetRequiredProperty(JsonElement objectElement, string name)
        {
            if (objectElement.ValueKind != JsonValueKind.Object || !objectElement.TryGetProperty(name, out JsonElement value))
                throw new InvalidDataException("Required configuration field is missing: " + name);
            return value;
        }

        private static string RequiredString(JsonElement objectElement, string name)
        {
            JsonElement value = GetRequiredProperty(objectElement, name);
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidDataException("Required string configuration field is missing: " + name);
            return value.GetString();
        }

        private static double RequiredFiniteNumber(JsonElement objectElement, string name)
        {
            JsonElement value = GetRequiredProperty(objectElement, name);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number) || double.IsNaN(number) || double.IsInfinity(number))
                throw new InvalidDataException("Required finite numeric configuration field is invalid: " + name);
            return number;
        }

        private static int RequiredInt(JsonElement objectElement, string name)
        {
            JsonElement value = GetRequiredProperty(objectElement, name);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
                throw new InvalidDataException("Required integer configuration field is invalid: " + name);
            return number;
        }

        private static void RequireString(JsonElement objectElement, string name, string expected)
        {
            if (!string.Equals(RequiredString(objectElement, name), expected, StringComparison.Ordinal))
                throw new InvalidDataException("Configuration identity mismatch: " + name);
        }

        private static void RequireEqual(double left, double right, string message)
        {
            if (Math.Abs(left - right) > 1e-12) throw new InvalidDataException(message);
        }

        private static string ResolveInsideRepository(string repositoryRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) throw new InvalidDataException("Configuration path must be relative.");
            string root = Path.GetFullPath(repositoryRoot);
            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Configuration path escapes the repository.");
            return fullPath;
        }

        private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');
        private static bool HashEquals(string left, string right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static string Sha256(byte[] bytes)
        {
            using SHA256 algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
