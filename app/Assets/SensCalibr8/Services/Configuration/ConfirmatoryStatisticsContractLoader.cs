using System;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Configuration
{
    public static class ConfirmatoryStatisticsContractLoader
    {
        public static ConfirmatoryStatisticsContract From(FrozenCalibrationConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            try
            {
                using JsonDocument document = JsonDocument.Parse(configuration.Record.ConfirmatoryContractJson);
                JsonElement root = document.RootElement;
                JsonElement numerical = root.GetProperty("numerical_policy");
                var contract = new ConfirmatoryStatisticsContract(
                    Required(root, "confirmatory_contract_version"),
                    root.GetProperty("fresh_pairs_required").GetInt32(),
                    root.GetProperty("enumeration_count").GetInt32(),
                    Required(root, "test"),
                    Required(root, "alternative"),
                    root.GetProperty("alpha").GetDouble(),
                    root.GetProperty("confidence_level").GetDouble(),
                    root.GetProperty("t_critical_df9").GetDouble(),
                    numerical.GetProperty("permutation_extreme_comparison_tolerance_points").GetDouble(),
                    root.GetProperty("reuse_exploratory_data").GetBoolean(),
                    root.GetProperty("early_stopping").GetBoolean());
                if (contract.ReuseExploratoryData || contract.EarlyStopping)
                    throw new InvalidDataException("Accepted confirmatory evidence must be fresh and cannot stop early.");
                return contract;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Confirmatory statistics contract is invalid.", exception);
            }
        }

        private static string Required(JsonElement element, string name)
        {
            string value = element.GetProperty(name).GetString();
            return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException("Confirmatory field is required: " + name);
        }
    }
}
