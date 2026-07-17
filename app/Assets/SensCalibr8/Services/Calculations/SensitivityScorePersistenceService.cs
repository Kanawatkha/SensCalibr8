using System;
using System.IO;
using System.Text;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Calculations
{
    public sealed class SensitivityScorePersistenceService
    {
        private readonly SensitivityTestRepository scores;private readonly CalibrationConfigurationRepository configurations;private readonly FrozenCalibrationConfiguration configuration;
        public SensitivityScorePersistenceService(SensitivityTestRepository scores,CalibrationConfigurationRepository configurations,FrozenCalibrationConfiguration configuration)
        { this.scores=scores??throw new ArgumentNullException(nameof(scores));this.configurations=configurations??throw new ArgumentNullException(nameof(configurations));this.configuration=configuration??throw new ArgumentNullException(nameof(configuration)); }
        public SensitivityTestRecord Persist(long profileId,long cycleId,long batteryId,double edpi,double cm360,long phase,long sampleSize,BatteryScoreResult result)
        {
            if(result==null)throw new ArgumentNullException(nameof(result));if(!string.Equals(result.FormulaVersion,configuration.FormulaVersion.Value,StringComparison.Ordinal)||!string.Equals(result.NormalizationVersion,configuration.Record.NormalizationVersion,StringComparison.Ordinal))throw new InvalidDataException("Score result does not match the accepted configuration versions.");
            long configId=configurations.RequireId(configuration.ConfigVersion.Value);string json=ModeJson(result);
            return scores.Create(new SensitivityTestRecord(null,profileId,cycleId,configId,batteryId,edpi,cm360,result.AveragePerformanceScore,json,null,result.FormulaVersion,phase,sampleSize));
        }
        private static string ModeJson(BatteryScoreResult result)
        {
            using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream)){writer.WriteStartObject();Write(writer,"flick_close",result,TestMode.FlickClose);Write(writer,"flick_far",result,TestMode.FlickFar);Write(writer,"micro_correction",result,TestMode.MicroCorrection);Write(writer,"tracking",result,TestMode.Tracking);writer.WriteEndObject();}return Encoding.UTF8.GetString(stream.ToArray());
        }
        private static void Write(Utf8JsonWriter writer,string name,BatteryScoreResult result,TestMode mode){if(!result.PerformanceScoreByMode.TryGetValue(mode,out double score))throw new InvalidDataException("Battery mode score is missing.");writer.WriteNumber(name,score);}
    }
}
