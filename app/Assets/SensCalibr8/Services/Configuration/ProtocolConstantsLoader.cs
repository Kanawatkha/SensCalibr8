using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.Services.Configuration
{
    public static class ProtocolConstantsLoader
    {
        public const string ConstantsPath="config/protocol-constants-v1.json";
        public static ProtocolConstants LoadFromRepository(string repositoryRoot)
        {
            string path=Path.Combine(repositoryRoot??string.Empty,ConstantsPath.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(path))throw new FileNotFoundException("Protocol constants were not found.",path);using JsonDocument document=JsonDocument.Parse(File.ReadAllBytes(path));JsonElement root=document.RootElement;Require(root,"schema_version","sc8-protocol-constants-v1");Require(root,"status","accepted");if(!root.GetProperty("immutable").GetBoolean())throw new InvalidDataException("Protocol constants must be immutable.");JsonElement phase=root.GetProperty("phase_one");return new ProtocolConstants(Required(root,"constants_version"),phase.GetProperty("candidate_count").GetInt32(),phase.GetProperty("candidate_offsets_percent").EnumerateArray().Select(value=>value.GetDouble()).ToArray(),Required(phase,"generation_rule"));
        }
        private static void Require(JsonElement value,string name,string expected){if(!string.Equals(Required(value,name),expected,StringComparison.Ordinal))throw new InvalidDataException("Protocol constants identity mismatch: "+name);}private static string Required(JsonElement value,string name){string result=value.GetProperty(name).GetString();return !string.IsNullOrWhiteSpace(result)?result:throw new InvalidDataException("Protocol constants field is required: "+name);}
    }
}
