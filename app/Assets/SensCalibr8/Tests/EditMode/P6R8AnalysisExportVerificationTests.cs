using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;

namespace SensCalibr8.Tests.EditMode
{
    public sealed class P6R8AnalysisExportVerificationTests
    {
        private string root;

        [SetUp] public void SetUp(){root=Path.Combine(Path.GetTempPath(),"SensCalibr8-P6R8-"+Guid.NewGuid().ToString("N"));}
        [TearDown] public void TearDown(){if(Directory.Exists(root))Directory.Delete(root,true);}

        [Test] public void ExportedManifestAndCsvRoundTripAsPortableDataWithoutImportSurface()
        {
            char quote=(char)34;
            var profile=new AnalysisProfileIdentity(27,"Pilot, "+quote+"One"+quote,1600);
            var row=new Dictionary<string,object>{{"id",27L},{"name",profile.ProfileName},{"edpi",280d},{"performance_score",77d},{"nullable",null}};
            var table=new ProfileDataExportTable("sensitivity_tests",new[]{"id","name","edpi","performance_score","nullable"},new[]{row});
            var dataset=new ProfileDataExportDataset(profile,new[]{table});

            ProfileDataExportResult result=ProfileDataExportWriter.Write(dataset,root,new DateTime(2026,7,19,0,0,0,DateTimeKind.Utc));
            using(JsonDocument manifest=JsonDocument.Parse(File.ReadAllText(result.ManifestPath)))
            {
                Assert.That(manifest.RootElement.GetProperty("exportVersion").GetString(),Is.EqualTo(ProfileDataExportContract.Version));
                Assert.That(manifest.RootElement.GetProperty("profile").GetProperty("profileId").GetInt64(),Is.EqualTo(27L));
                Assert.That(manifest.RootElement.GetProperty("profile").GetProperty("profileName").GetString(),Is.EqualTo(profile.ProfileName));
                Assert.That(manifest.RootElement.GetProperty("disclaimer").GetString(),Does.Contain("Data Export only"));
            }

            IReadOnlyList<IReadOnlyList<string>> rows=ParseCsv(File.ReadAllText(result.CsvPaths[0]));
            Assert.That(rows.Count,Is.EqualTo(2));
            Assert.That(rows[0][0],Is.EqualTo("id"));
            Assert.That(rows[1][0],Is.EqualTo("27"));
            Assert.That(rows[1][1],Is.EqualTo(profile.ProfileName));
            Assert.That(rows[1][2],Is.EqualTo("280"));
            Assert.That(rows[1][3],Is.EqualTo("77"));
            Assert.That(rows[1][4],Is.EqualTo(string.Empty));
            Assert.That(typeof(ProfileDataExportService).GetMethod("Import"),Is.Null);
            Assert.That(typeof(ProfileDataExportService).GetMethod("Restore"),Is.Null);
            Assert.That(typeof(ProfileDataExportWriter).GetMethod("Import"),Is.Null);
            Assert.That(typeof(ProfileDataExportWriter).GetMethod("Restore"),Is.Null);
        }

        private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string text)
        {
            var rows=new List<IReadOnlyList<string>>();var row=new List<string>();var value=new System.Text.StringBuilder();bool quoted=false;
            for(int index=0;index<text.Length;index++)
            {
                char current=text[index];
                if(current=='"')
                {
                    if(quoted&&index+1<text.Length&&text[index+1]=='"'){value.Append('"');index++;}
                    else quoted=!quoted;
                }
                else if(current==','&&!quoted){row.Add(value.ToString());value.Clear();}
                else if((current=='\r'||current=='\n')&&!quoted)
                {
                    if(current=='\r'&&index+1<text.Length&&text[index+1]=='\n')index++;
                    row.Add(value.ToString());value.Clear();rows.Add(row.AsReadOnly());row=new List<string>();
                }
                else value.Append(current);
            }
            if(quoted)throw new InvalidDataException("CSV is not terminated.");
            if(value.Length!=0||row.Count!=0){row.Add(value.ToString());rows.Add(row.AsReadOnly());}
            return rows.AsReadOnly();
        }
    }
}
