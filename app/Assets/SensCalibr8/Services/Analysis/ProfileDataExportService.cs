using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Analysis
{
    public sealed class ProfileDataExportResult
    {
        public ProfileDataExportResult(string directory,string manifestPath,IReadOnlyList<string> csvPaths){Directory=directory;ManifestPath=manifestPath;CsvPaths=csvPaths;}
        public string Directory{get;}public string ManifestPath{get;}public IReadOnlyList<string> CsvPaths{get;}
    }
    public sealed class ProfileDataExportService
    {
        private readonly ProfileDataExportRepository repository;
        public ProfileDataExportService(ProfileDataExportRepository repository){this.repository=repository??throw new ArgumentNullException(nameof(repository));}
        public ProfileDataExportResult Export(long profileId,string outputRoot,DateTime exportedUtc)=>ProfileDataExportWriter.Write(repository.Read(profileId),outputRoot,exportedUtc);
    }
    public static class ProfileDataExportWriter
    {
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false);
        public static ProfileDataExportResult Write(ProfileDataExportDataset dataset,string outputRoot,DateTime exportedUtc)
        {
            if(dataset==null)throw new ArgumentNullException(nameof(dataset));if(string.IsNullOrWhiteSpace(outputRoot))throw new ArgumentException("Output root is required.",nameof(outputRoot));if(exportedUtc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Export timestamp must be UTC.",nameof(exportedUtc));
            string root=Path.GetFullPath(outputRoot),name="senscalibr8-data-export-"+Safe(dataset.Profile.ProfileName)+"-"+dataset.Profile.ProfileId.ToString(CultureInfo.InvariantCulture)+"-"+exportedUtc.ToString("yyyyMMddTHHmmssZ",CultureInfo.InvariantCulture),directory=Path.Combine(root,name);
            Directory.CreateDirectory(root);if(Directory.Exists(directory))throw new IOException("Refusing to overwrite an existing Data Export.");
            Directory.CreateDirectory(directory);var csvPaths=new List<string>();try{string manifest=Path.Combine(directory,"manifest.json");WriteNew(manifest,JsonSerializer.Serialize(new {exportVersion=dataset.ExportVersion,exportedUtc,disclaimer=ProfileDataExportContract.Disclaimer,profile=dataset.Profile,tables=dataset.Tables},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true}));foreach(ProfileDataExportTable table in dataset.Tables){string path=Path.Combine(directory,Safe(table.Name)+".csv");WriteNew(path,Csv(table));csvPaths.Add(path);}return new ProfileDataExportResult(directory,manifest,csvPaths.AsReadOnly());}catch{if(Directory.Exists(directory))Directory.Delete(directory,true);throw;}
        }
        private static void WriteNew(string path,string content){using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);using var writer=new StreamWriter(stream,Utf8);writer.Write(content);}
        private static string Csv(ProfileDataExportTable table){var builder=new StringBuilder();builder.AppendLine(string.Join(",",table.Columns.Select(Escape)));foreach(var row in table.Rows)builder.AppendLine(string.Join(",",table.Columns.Select(column=>Escape(Value(row[column])))));return builder.ToString();}
        private static string Value(object value)=>value==null?string.Empty:Convert.ToString(value,CultureInfo.InvariantCulture);
        private static string Escape(string value){char quote=(char)34;string text=value??string.Empty;return quote+text.Replace(quote.ToString(),new string(quote,2))+quote;}
        private static string Safe(string value){var builder=new StringBuilder();foreach(char c in value??string.Empty)builder.Append(char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):'-');string result=builder.ToString().Trim('-');return string.IsNullOrEmpty(result)?"profile":result;}
    }
}
