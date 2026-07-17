using System;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Configuration
{
    public static class ScientificRigorContractLoader
    {
        public const string ContractPath="config/scientific-rigor-v1.json";
        public static ScientificRigorContract LoadFromRepository(string repositoryRoot,FrozenCalibrationConfiguration configuration,ResearchConstants research)
        {
            if(configuration==null)throw new ArgumentNullException(nameof(configuration));if(research==null)throw new ArgumentNullException(nameof(research));
            string path=Path.Combine(repositoryRoot??string.Empty,ContractPath.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(path))throw new FileNotFoundException("Scientific-rigor contract was not found.",path);
            using JsonDocument document=JsonDocument.Parse(File.ReadAllBytes(path));JsonElement root=document.RootElement;
            Require(root,"schema_version","sc8-scientific-rigor-v1");Require(root,"status","accepted");if(!root.GetProperty("immutable").GetBoolean())throw new InvalidDataException("Scientific-rigor contract must be immutable.");
            JsonElement adaptation=root.GetProperty("adaptation"),outlier=root.GetProperty("outlier"),fatigue=root.GetProperty("fatigue"),grade=root.GetProperty("grade");
            Require(adaptation,"application_order","finalize-after-session-before-analysis");Require(outlier,"comparison","absolute-deviation-strictly-greater-than-threshold");Require(outlier,"statistical_disposition","flag-and-include");Require(outlier,"exclusion_policy","separate-documented-data-quality-error-only");
            if(outlier.GetProperty("passes").GetInt32()!=1)throw new InvalidDataException("Outlier detection must be one pass.");Require(fatigue,"comparison","strictly-greater-than");Require(fatigue,"split","chronological-first-floor-half-second-remainder");Require(fatigue,"zero_denominator_policy","undefined-at-accepted-scoring-zero-tolerance");Require(fatigue,"winner_disposition","informational-include");
            Require(grade,"reaction_source","flick-close-authoritative-mean-reaction-time");Require(grade,"consistency_source","arithmetic-mean-four-mode-normalized-consistency-utilities");Require(grade,"combination","worse-of-reaction-and-consistency-tier");
            var contract=new ScientificRigorContract(String(root,"contract_version"),Number(adaptation,"shot_fraction"),adaptation.GetProperty("tracking_adaptation_blocks").GetInt32(),String(outlier,"algorithm_version"),Number(outlier,"sample_sd_multiplier"),String(fatigue,"algorithm_version"),Number(fatigue,"decline_threshold_percent"),Number(fatigue,"percentage_scale"),String(grade,"contract_version"));
            FrozenSessionLifecycleContract lifecycle=FrozenSessionLifecycleContract.From(configuration);
            if(contract.AdaptationFraction!=lifecycle.ShotAdaptationFraction||contract.TrackingAdaptationBlocks!=lifecycle.TrackingAdaptationBlockCount)throw new InvalidDataException("Scientific-rigor adaptation contract drifted from frozen mode configuration.");
            if(contract.OutlierSampleSdMultiplier!=research.OutlierSampleSdMultiplier)throw new InvalidDataException("Scientific-rigor outlier multiplier drifted from research constants.");
            return contract;
        }
        private static void Require(JsonElement element,string name,string expected){if(!string.Equals(String(element,name),expected,StringComparison.Ordinal))throw new InvalidDataException("Scientific-rigor identity mismatch: "+name);}
        private static string String(JsonElement element,string name){string value=element.GetProperty(name).GetString();return !string.IsNullOrWhiteSpace(value)?value:throw new InvalidDataException("Scientific-rigor field is required: "+name);}
        private static double Number(JsonElement element,string name){if(!element.GetProperty(name).TryGetDouble(out double value)||double.IsNaN(value)||double.IsInfinity(value))throw new InvalidDataException("Scientific-rigor number is invalid: "+name);return value;}
    }
}
