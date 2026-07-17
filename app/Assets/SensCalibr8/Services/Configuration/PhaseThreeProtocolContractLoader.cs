using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Configuration
{
    public static class PhaseThreeProtocolContractLoader
    {
        public const string ContractPath = "config/phase-three-protocol-v1.json";

        public static PhaseThreeProtocolContract LoadFromRepository(string repositoryRoot)
        {
            string path = Path.Combine(repositoryRoot ?? string.Empty, ContractPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Phase 3 protocol contract was not found.", path);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            Require(root, "schema_version", "sc8-phase-three-protocol-v1");
            Require(root, "status", "accepted");
            if (!root.GetProperty("immutable").GetBoolean()) throw new InvalidDataException("Phase 3 protocol contract must be immutable.");
            return new PhaseThreeProtocolContract(Required(root, "contract_version"),
                root.GetProperty("candidate_offsets_percent").EnumerateArray().Select(value => value.GetDouble()).ToArray(),
                Required(root, "battery_purpose"), Required(root, "generation_rule"));
        }

        private static void Require(JsonElement element, string name, string expected)
        {
            if (!string.Equals(Required(element, name), expected, StringComparison.Ordinal))
                throw new InvalidDataException("Phase 3 contract identity mismatch: " + name);
        }

        private static string Required(JsonElement element, string name)
        {
            string value = element.GetProperty(name).GetString();
            return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException("Phase 3 contract field is required: " + name);
        }
    }
}
