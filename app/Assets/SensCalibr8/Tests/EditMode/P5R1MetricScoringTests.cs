using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P5R1MetricScoringTests
    {
        private FrozenCalibrationConfiguration configuration;private PerformanceScoringService scoring;
        [SetUp]public void SetUp(){configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());scoring=new PerformanceScoringService(configuration);}

        [Test]public void FormulaAndSampleContractsComeFromTheAcceptedConfiguration()
        { Assert.That(configuration.ScoringFormula.FormulaVersion,Is.EqualTo("sc8-performance-score-v1"));Assert.That(configuration.ScoringFormula.AuthoritativeShotObservations,Is.EqualTo(15));Assert.That(configuration.ScoringFormula.AuthoritativeTrackingWindows,Is.EqualTo(54));Assert.That(FrozenScoringRules.From(configuration).NormalizationVersion,Is.EqualTo("sc8-normalization-v1")); }

        [Test]public void FixedNormalizationClampsAndInvertsAtEveryBoundary()
        { MetricBound bound=FrozenScoringRules.From(configuration).Require("flick_close","reaction_time_ms");Assert.That(scoring.NormalizeHigherIsBetter(100d,bound),Is.Zero);Assert.That(scoring.NormalizeHigherIsBetter(300d,bound),Is.EqualTo(0.5d));Assert.That(scoring.NormalizeHigherIsBetter(900d,bound),Is.EqualTo(1d));Assert.That(scoring.NormalizeLowerIsBetter(100d,bound),Is.EqualTo(1d));Assert.That(scoring.NormalizeLowerIsBetter(300d,bound),Is.EqualTo(0.5d));Assert.That(scoring.NormalizeLowerIsBetter(900d,bound),Is.Zero); }

        [Test]public void ResearchWorkedExamplesProduceExactAcceptedScores()
        { Assert.That(scoring.ComposeShotScore(0.8d,0.9d,0.75d,0.6d,0.2d),Is.EqualTo(77d).Within(1e-12));Assert.That(scoring.ComposeTrackingScore(0.8d,0.9d,0.7d),Is.EqualTo(81.875d).Within(1e-12)); }

        [Test]public void SubmovementBoundsClampAndShotFormulaRetainsNegativeFloor()
        { ModeScoreResult lower=scoring.ScoreShotMode(TestMode.FlickClose,Shots(15,true,100d,0d,0d)),upper=scoring.ScoreShotMode(TestMode.FlickClose,Shots(15,true,100d,0d,6d)),beyond=scoring.ScoreShotMode(TestMode.FlickClose,Shots(15,true,100d,0d,20d));Assert.That(lower.SubmovementPenalty,Is.Zero);Assert.That(upper.SubmovementPenalty,Is.EqualTo(1d));Assert.That(beyond.SubmovementPenalty,Is.EqualTo(1d));Assert.That(scoring.ComposeShotScore(0d,0d,0d,0d,1d),Is.EqualTo(-10d).Within(1e-12)); }

        [Test]public void CompletePerfectShotModesAggregateToOneHundredWithoutFinalClamping()
        {
            ModeScoreResult close=scoring.ScoreShotMode(TestMode.FlickClose,Shots(15,true,100d,0d,1d));ModeScoreResult far=scoring.ScoreShotMode(TestMode.FlickFar,Shots(15,true,0d,0d,1d));ModeScoreResult micro=scoring.ScoreShotMode(TestMode.MicroCorrection,Shots(15,true,0d,0d,1d));
            Assert.That(close.PerformanceScore,Is.EqualTo(100d).Within(1e-12));Assert.That(far.PerformanceScore,Is.EqualTo(100d).Within(1e-12));Assert.That(micro.PerformanceScore,Is.EqualTo(100d).Within(1e-12));Assert.That(close.SampleSize,Is.EqualTo(15));
        }

        [Test]public void ZeroHitModeKeepsNullRawCountsAndFailsClosedAtFullPenalty()
        { ModeScoreResult result=scoring.ScoreShotMode(TestMode.FlickClose,Shots(15,false,500d,15d,null));Assert.That(result.SubmovementPenalty,Is.EqualTo(1d));Assert.That(result.PerformanceScore,Is.EqualTo(25d).Within(1e-12)); }

        [Test]public void FarMissingOnsetUsesScoringCeilingWithoutRequiringFabricatedRawTime()
        { ModeScoreResult result=scoring.ScoreShotMode(TestMode.FlickFar,Shots(15,false,null,40d,null));Assert.That(result.ReactionUtility,Is.Zero); }

        [Test]public void TrackingAggregatesExactlyFiftyFourEqualWindows()
        { var windows=Enumerable.Range(0,54).Select(_=>new TrackingScoringWindow(100d,0d)).ToArray();ModeScoreResult result=scoring.ScoreTracking(windows);Assert.That(result.PerformanceScore,Is.EqualTo(100d).Within(1e-12));Assert.That(result.ReactionUtility,Is.Null);Assert.That(result.SubmovementPenalty,Is.Null);Assert.That(()=>scoring.ScoreTracking(windows.Take(53).ToArray()),Throws.TypeOf<InvalidDataException>()); }

        [Test]public void MissingHitSignalOrWrongShotCountFailsClosed()
        { var missing=Shots(15,true,100d,0d,null);Assert.That(()=>scoring.ScoreShotMode(TestMode.FlickClose,missing),Throws.TypeOf<InvalidDataException>());Assert.That(()=>scoring.ScoreShotMode(TestMode.FlickClose,Shots(14,true,100d,0d,1d)),Throws.TypeOf<InvalidDataException>()); }

        [Test]public void BatteryRequiresFourUniqueVersionMatchedModesAndPersistsFormulaLineage()
        {
            string directory=Path.Combine(Path.GetTempPath(),"senscalibr8-p5r1-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                ModeScoreResult close=scoring.ScoreShotMode(TestMode.FlickClose,Shots(15,true,100d,0d,1d)),far=scoring.ScoreShotMode(TestMode.FlickFar,Shots(15,true,0d,0d,1d)),micro=scoring.ScoreShotMode(TestMode.MicroCorrection,Shots(15,true,0d,0d,1d)),tracking=scoring.ScoreTracking(Enumerable.Range(0,54).Select(_=>new TrackingScoringWindow(100d,0d)).ToArray());BatteryScoreResult battery=scoring.ScoreBattery(new[]{close,far,tracking,micro});Assert.That(battery.AveragePerformanceScore,Is.EqualTo(100d).Within(1e-12));Assert.That(()=>scoring.ScoreBattery(new[]{close,far,micro,micro}),Throws.TypeOf<InvalidDataException>());
                string database=Path.Combine(directory,"scores.sqlite3"),native=Path.Combine(RepositoryRoot(),"app","Assets","Plugins","sqlite3.dll");new SqliteDatabaseBootstrapper().Initialize(database,configuration,native);var connections=new SqliteConnectionFactory(database,native);ProfileRecord profile=new ProfileRepository(connections).Create(new ProfileRecord(null,"p5-r1-profile","2026-07-16",1600,0.175d,1000d,"right","#FFE600","claw","arm",50d,50d,1d,"2026-07-16"));var protocol=new ProtocolRepository(connections);CycleRecord cycle=protocol.CreateCycle(new CycleRecord(null,profile.Id.Value,1,"2026-07-16",null,null));ProtocolCandidateRecord candidate=protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null,profile.Id.Value,cycle.Id.Value,1,280d,0.175d,"phase1_offsets","2026-07-16"),new[]{new ProtocolCandidateSourceRecord(280d,0d,280d,false)});ProtocolBatteryRecord scoredBattery=protocol.CreateBattery(new ProtocolBatteryRecord(null,profile.Id.Value,cycle.Id.Value,candidate.Id.Value,0.175d,1,"exploratory","2026-07-16","2026-07-16"));long configId=new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);using(SqliteDatabaseConnection setup=connections.Open()){foreach(string mode in new[]{"flick_close","flick_far","tracking","micro_correction"})setup.Execute("INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag) VALUES (@profile,@battery,@config,'2026-07-16',@mode,1,0);",new Dictionary<string,object>{{"@profile",profile.Id.Value},{"@battery",scoredBattery.Id.Value},{"@config",configId},{"@mode",mode}});}var service=new SensitivityScorePersistenceService(new SensitivityTestRepository(connections),new CalibrationConfigurationRepository(connections),configuration);SensitivityTestRecord persisted=service.Persist(profile.Id.Value,cycle.Id.Value,scoredBattery.Id.Value,280d,46.384d,1,1,battery);
                Assert.That(persisted.FormulaVersion,Is.EqualTo(configuration.FormulaVersion.Value));using SqliteDatabaseConnection connection=connections.Open();Assert.That(Convert.ToString(connection.Scalar("SELECT formula_version FROM sensitivity_tests WHERE id=@id;",new Dictionary<string,object>{{"@id",persisted.Id.Value}})),Is.EqualTo("sc8-performance-score-v1"));string json=Convert.ToString(connection.Scalar("SELECT performance_score_by_mode FROM sensitivity_tests WHERE id=@id;",new Dictionary<string,object>{{"@id",persisted.Id.Value}}));using JsonDocument document=JsonDocument.Parse(json);Assert.That(document.RootElement.EnumerateObject().Count(),Is.EqualTo(4));Assert.That(document.RootElement.GetProperty("flick_close").GetDouble(),Is.EqualTo(100d).Within(1e-12));Assert.That(document.RootElement.GetProperty("flick_far").GetDouble(),Is.EqualTo(100d).Within(1e-12));Assert.That(document.RootElement.GetProperty("micro_correction").GetDouble(),Is.EqualTo(100d).Within(1e-12));Assert.That(document.RootElement.GetProperty("tracking").GetDouble(),Is.EqualTo(100d).Within(1e-12));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        private static IReadOnlyList<ShotScoringObservation> Shots(int count,bool hit,double? time,double precision,double? submovement)=>Enumerable.Range(0,count).Select(_=>new ShotScoringObservation(hit,time,precision,submovement)).ToArray();
        private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
    }
}
