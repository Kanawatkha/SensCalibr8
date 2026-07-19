using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;

namespace SensCalibr8.Tests.EditMode
{
    public sealed class P6R6DataExportTests
    {
        private string root;
        [SetUp] public void SetUp(){root=Path.Combine(Path.GetTempPath(),"SensCalibr8-P6R6-"+Guid.NewGuid().ToString("N"));}
        [TearDown] public void TearDown(){if(Directory.Exists(root))Directory.Delete(root,true);}
        [Test] public void WriterCreatesProfileSeparatedUtf8JsonAndDeterministicCsv()
        {
            char quote=(char)34;var row=new Dictionary<string,object>{{"id",1L},{"note","comma, quote "+quote+" retained"},{"nullable",null}};
            var dataset=new ProfileDataExportDataset(new AnalysisProfileIdentity(9,"Pilot / One",1600),new[]{new ProfileDataExportTable("shots",new[]{"id","note","nullable"},new[]{row})});
            var result=ProfileDataExportWriter.Write(dataset,root,new DateTime(2026,7,19,0,0,0,DateTimeKind.Utc));
            Assert.That(Path.GetFileName(result.Directory),Is.EqualTo("senscalibr8-data-export-pilot---one-9-20260719T000000Z"));
            Assert.That(File.ReadAllText(result.ManifestPath),Does.Contain("Data Export only. Not an Import/Restore backup or recovery guarantee."));
            Assert.That(result.CsvPaths,Has.Count.EqualTo(1));
            string expected=quote+"id"+quote+","+quote+"note"+quote+","+quote+"nullable"+quote+Environment.NewLine+quote+"1"+quote+","+quote+"comma, quote "+quote+quote+" retained"+quote+","+quote+quote+Environment.NewLine;Assert.That(File.ReadAllText(result.CsvPaths[0]),Is.EqualTo(expected));
        }
        [Test] public void WriterRejectsOverwriteAndNonUtcTimestamp()
        {
            var dataset=new ProfileDataExportDataset(new AnalysisProfileIdentity(9,"Pilot",1600),new[]{new ProfileDataExportTable("profiles",new[]{"id"},new[]{new Dictionary<string,object>{{"id",9L}}})});
            DateTime utc=new DateTime(2026,7,19,0,0,0,DateTimeKind.Utc);
            ProfileDataExportWriter.Write(dataset,root,utc);
            Assert.That(()=>ProfileDataExportWriter.Write(dataset,root,utc),Throws.TypeOf<IOException>());
            Assert.That(()=>ProfileDataExportWriter.Write(dataset,root,DateTime.SpecifyKind(utc,DateTimeKind.Local)),Throws.TypeOf<ArgumentException>());
        }
    }
}
