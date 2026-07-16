using System;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Configuration
{
    public static class ResearchConstantsLoader
    {
        public const string ConstantsPath = "config/research-constants-v1.json";

        public static ResearchConstants LoadFromRepository(string repositoryRoot)
        {
            string path = Path.Combine(repositoryRoot ?? string.Empty, ConstantsPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Research constants were not found.", path);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            Required(root, "schema_version", "sc8-research-constants-v1");
            Required(root, "status", "accepted");
            if (!root.GetProperty("immutable").GetBoolean()) throw new InvalidDataException("Research constants must be immutable.");
            JsonElement values = root.GetProperty("general");
            return new ResearchConstants(RequiredString(root, "constants_version"), Number(values, "psa_baseline_edpi"), Number(values, "edpi_floor"), Number(values, "cm_per_inch"), Number(values, "degrees_per_turn"), Number(values, "valorant_yaw_multiplier"), Number(values, "fitts_distance_multiplier"), Number(values, "headshot_reference_ceiling_percent"), Number(values, "outlier_sample_sd_multiplier"), Number(values, "grip_tension_min_percent"), Number(values, "grip_tension_max_percent"), Number(values, "excessive_grip_tension_percent"), Number(values, "wrist_warning_edpi_exclusive_upper"));
        }

        private static void Required(JsonElement element, string name, string expected)
        {
            if (!string.Equals(RequiredString(element, name), expected, StringComparison.Ordinal)) throw new InvalidDataException("Research constants identity mismatch: " + name);
        }
        private static string RequiredString(JsonElement element, string name)
        {
            string value = element.GetProperty(name).GetString();
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Research constants field is required: " + name);
            return value;
        }
        private static double Number(JsonElement element, string name)
        {
            if (!element.GetProperty(name).TryGetDouble(out double value)) throw new InvalidDataException("Research constants number is invalid: " + name);
            return value;
        }
    }
}
