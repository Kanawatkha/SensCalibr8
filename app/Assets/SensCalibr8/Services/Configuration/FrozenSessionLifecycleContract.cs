using System;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Configuration
{
    public sealed class FrozenSessionLifecycleContract
    {
        private FrozenSessionLifecycleContract(string modeContractVersion, double shotAdaptationFraction, int trackingAdaptationBlockCount)
        { ModeContractVersion=modeContractVersion; ShotAdaptationFraction=shotAdaptationFraction; TrackingAdaptationBlockCount=trackingAdaptationBlockCount; }
        public string ModeContractVersion { get; } public double ShotAdaptationFraction { get; } public int TrackingAdaptationBlockCount { get; }
        public SessionAdaptationFinalizationPolicy ToAdaptationPolicy() => new SessionAdaptationFinalizationPolicy(ShotAdaptationFraction,TrackingAdaptationBlockCount);
        public static FrozenSessionLifecycleContract From(FrozenCalibrationConfiguration configuration)
        {
            if(configuration==null)throw new ArgumentNullException(nameof(configuration));
            try
            {
                using JsonDocument document=JsonDocument.Parse(configuration.Record.TrackingContractJson);JsonElement root=document.RootElement;
                string version=root.GetProperty("mode_contract_version").GetString();double fraction=root.GetProperty("shared_shot_contract").GetProperty("adaptation_fraction").GetDouble();int blocks=root.GetProperty("modes").GetProperty("tracking").GetProperty("adaptation_blocks").GetInt32();
                if(string.IsNullOrWhiteSpace(version)||double.IsNaN(fraction)||double.IsInfinity(fraction)||fraction<0d||fraction>1d||blocks<0)throw new InvalidDataException("Frozen session lifecycle contract is invalid.");
                return new FrozenSessionLifecycleContract(version,fraction,blocks);
            }
            catch(JsonException exception){throw new InvalidDataException("Frozen session lifecycle contract is invalid.",exception);}
        }
    }
}
