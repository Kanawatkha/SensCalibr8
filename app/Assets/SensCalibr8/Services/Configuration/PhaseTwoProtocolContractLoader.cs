using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Configuration
{
    public static class PhaseTwoProtocolContractLoader
    {
        public const string ContractPath = "config/phase-two-protocol-v1.json";

        public static PhaseTwoProtocolContract LoadFromRepository(string repositoryRoot)
        {
            string path = Path.Combine(repositoryRoot ?? string.Empty, ContractPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Phase 2 protocol contract was not found.", path);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            Require(root, "schema_version", "sc8-phase-two-protocol-v1");
            Require(root, "status", "accepted");
            if (!root.GetProperty("immutable").GetBoolean()) throw new InvalidDataException("Phase 2 protocol contract must be immutable.");
            return new PhaseTwoProtocolContract(Required(root, "contract_version"),
                root.GetProperty("candidate_offsets_percent").EnumerateArray().Select(value => value.GetDouble()).ToArray(),
                root.GetProperty("minimum_complete_batteries_per_value").GetInt32(),
                root.GetProperty("maximum_complete_batteries_per_value").GetInt32(),
                root.GetProperty("stabilization_cv_percent_exclusive_upper").GetDouble(),
                Required(root, "battery_purpose"), Required(root, "single_anchor_generation_rule"),
                Required(root, "tie_generation_rule"));
        }

        private static void Require(JsonElement element, string name, string expected)
        {
            if (!string.Equals(Required(element, name), expected, StringComparison.Ordinal))
                throw new InvalidDataException("Phase 2 contract identity mismatch: " + name);
        }

        private static string Required(JsonElement element, string name)
        {
            string value = element.GetProperty(name).GetString();
            return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException("Phase 2 contract field is required: " + name);
        }
    }
}
