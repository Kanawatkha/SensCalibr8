using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public sealed class P5R6ScientificRigorTests
    {
        private string directory;private SqliteConnectionFactory connections;private FrozenCalibrationConfiguration configuration;private ScientificRigorContract contract;private ScientificRigorService rigor;private ScientificRigorPersistenceService persistence;private ProfileRecord profile;private CycleRecord cycle;private ProtocolBatteryRecord battery;private long configId;private long closeSessionId;private long sensitivityTestId;private IReadOnlyList<long> shotIds;
        [SetUp]public void SetUp()
        {
            string root=RepositoryRoot();configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(root);ResearchConstants research=ResearchConstantsLoader.LoadFromRepository(root);contract=ScientificRigorContractLoader.LoadFromRepository(root,configuration,research);rigor=new ScientificRigorService(configuration,contract);
            directory=Path.Combine(Path.GetTempPath(),"senscalibr8-p5r6-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string native=Path.Combine(root,"app","Assets","Plugins","sqlite3.dll"),database=Path.Combine(directory,"rigor.sqlite3");new SqliteDatabaseBootstrapper().Initialize(database,configuration,native);connections=new SqliteConnectionFactory(database,native);persistence=new ScientificRigorPersistenceService(new ScientificRigorRepository(connections));
            profile=new ProfileRepository(connections).Create(new ProfileRecord(null,"p5-r6-profile","2026-07-17",1600,0.175d,1000d,"right","#FFE600","claw","arm",50d,50d,1d,"2026-07-17"));var protocol=new ProtocolRepository(connections);cycle=protocol.CreateCycle(new CycleRecord(null,profile.Id.Value,1,"2026-07-17",null,null));ProtocolCandidateRecord candidate=protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null,profile.Id.Value,cycle.Id.Value,3,280d,0.175d,"single_anchor","2026-07-17"),new[]{new ProtocolCandidateSourceRecord(280d,0d,280d,false)});battery=protocol.CreateBattery(new ProtocolBatteryRecord(null,profile.Id.Value,cycle.Id.Value,candidate.Id.Value,0.175d,3,"narrowing","2026-07-17",null));configId=new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);CreateCompletedBattery();
            var score=new SensitivityTestRepository(connections).Create(new SensitivityTestRecord(null,profile.Id.Value,cycle.Id.Value,configId,battery.Id.Value,280d,46.384d,75d,"{\"flick_close\":75,\"flick_far\":75,\"micro_correction\":75,\"tracking\":75}",null,configuration.FormulaVersion.Value,3,60));sensitivityTestId=score.Id.Value;
        }
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

        [Test]public void ContractPinsAdaptationOutlierFatigueAndGradeIdentities()
        {Assert.That(contract.AdaptationFraction,Is.EqualTo(0.5d));Assert.That(contract.TrackingAdaptationBlocks,Is.EqualTo(1));Assert.That(contract.OutlierSampleSdMultiplier,Is.EqualTo(3d));Assert.That(contract.FatigueDeclineThresholdPercent,Is.EqualTo(15d));Assert.That(contract.OutlierAlgorithmVersion,Is.EqualTo("sc8-outlier-3sd-v1"));Assert.That(contract.GradeContractVersion,Is.EqualTo("sc8-grade-v1"));}

        [Test]public void OnePassThreeSdFlagsStrictExtremesAndKeepsInclusiveSensitivityAnalysis()
        {
            MetricObservation[] values=shotIds.Select((id,index)=>new MetricObservation(closeSessionId,id,OutlierObservationKind.Shot,index==shotIds.Count-1?100d:0d)).ToArray();OutlierAnalysisResult result=rigor.AnalyzeOutliers(values);
            Assert.That(result.FlaggedCount,Is.EqualTo(1));Assert.That(result.InclusiveMean,Is.EqualTo(100d/11d).Within(1e-12));Assert.That(result.ExcludedMean,Is.Zero.Within(1e-12));Assert.That(result.Observations.Last().IsStatisticalOutlier,Is.True);Assert.That(Math.Abs(100d-result.Mean),Is.GreaterThan(result.Threshold));
            long runId=persistence.PersistOutlierRun(profile.Id.Value,cycle.Id.Value,configId,3,"flick_close",0.175d,"final_precision_error_deg",result);using SqliteDatabaseConnection connection=connections.Open();Assert.That(Scalar(connection,"SELECT flagged_count FROM outlier_analysis_runs WHERE id="+runId+";"),Is.EqualTo(1));Assert.That(Scalar(connection,"SELECT COUNT(*) FROM outlier_flags WHERE excluded_from_authoritative_score=0;"),Is.EqualTo(1));Assert.That(Scalar(connection,"SELECT COUNT(*) FROM shots WHERE is_outlier=1;"),Is.EqualTo(1));Assert.That(()=>persistence.PersistOutlierRun(profile.Id.Value,cycle.Id.Value,configId,3,"flick_close",0.175d,"final_precision_error_deg",result),Throws.TypeOf<DataAccessException>());
        }

        [Test]public void DataQualityExclusionRequiresSeparateDocumentedConfirmation()
        {
            OutlierAnalysisResult result=rigor.AnalyzeOutliers(shotIds.Select((id,index)=>new MetricObservation(closeSessionId,id,OutlierObservationKind.Shot,index==10?100d:0d)).ToArray());persistence.PersistOutlierRun(profile.Id.Value,cycle.Id.Value,configId,3,"flick_close",0.175d,"final_precision_error_deg",result);using SqliteDatabaseConnection connection=connections.Open();long flagId=Convert.ToInt64(connection.Scalar("SELECT id FROM outlier_flags;"));Assert.That(()=>persistence.ConfirmDataQualityExclusion(flagId," "),Throws.TypeOf<ArgumentException>());persistence.ConfirmDataQualityExclusion(flagId,"Confirmed duplicate device packet in raw acquisition log.");Assert.That(Scalar(connection,"SELECT excluded_from_authoritative_score FROM outlier_flags WHERE id="+flagId+";"),Is.EqualTo(1));Assert.That(Text(connection,"SELECT disposition_reason FROM outlier_flags WHERE id="+flagId+";"),Does.Contain("duplicate device packet"));
        }

        [Test]public void OutlierPersistenceRejectsAdaptationObservationOutsideEligibleScope()
        {using(SqliteDatabaseConnection connection=connections.Open())connection.Execute("UPDATE shots SET is_adaptation_shot=1 WHERE id=@id;",new Dictionary<string,object>{{"@id",shotIds[0]}});OutlierAnalysisResult result=rigor.AnalyzeOutliers(shotIds.Select(id=>new MetricObservation(closeSessionId,id,OutlierObservationKind.Shot,0d)).ToArray());Assert.That(()=>persistence.PersistOutlierRun(profile.Id.Value,cycle.Id.Value,configId,3,"flick_close",0.175d,"final_precision_error_deg",result),Throws.TypeOf<DataAccessException>());}

        [Test]public void OutlierAuditRejectsAValueThatDoesNotMatchPersistedRawEvidence()
        {OutlierAnalysisResult result=rigor.AnalyzeOutliers(shotIds.Select((id,index)=>new MetricObservation(closeSessionId,id,OutlierObservationKind.Shot,index==10?99d:0d)).ToArray());Assert.That(()=>persistence.PersistOutlierRun(profile.Id.Value,cycle.Id.Value,configId,3,"flick_close",0.175d,"final_precision_error_deg",result),Throws.TypeOf<DataAccessException>());}

        [Test]public void FatigueUsesChronologicalPostAdaptationHalvesAndStrictThreshold()
        {
            Assert.That(rigor.EvaluateFatigueScores(100d,85d).IsFlagged,Is.False);Assert.That(rigor.EvaluateFatigueScores(100d,84d).IsFlagged,Is.True);Assert.That(rigor.EvaluateFatigueScores(0d,0d).DeclinePercent,Is.Null);
            var observations=new List<ShotScoringObservation>();for(int index=0;index<7;index++)observations.Add(new ShotScoringObservation(true,100d,0d,1d));for(int index=0;index<8;index++)observations.Add(new ShotScoringObservation(false,500d,15d,null));FatigueResult result=rigor.EvaluateShotFatigue(TestMode.FlickClose,observations);Assert.That(result.FirstHalfScore,Is.EqualTo(100d).Within(1e-12));Assert.That(result.SecondHalfScore,Is.EqualTo(25d).Within(1e-12));Assert.That(result.IsFlagged,Is.True);persistence.PersistFatigue(closeSessionId,result);using SqliteDatabaseConnection connection=connections.Open();Assert.That(Scalar(connection,"SELECT fatigue_flag FROM sessions WHERE id="+closeSessionId+";"),Is.EqualTo(1));Assert.That(Text(connection,"SELECT fatigue_algorithm_version FROM sessions WHERE id="+closeSessionId+";"),Is.EqualTo("sc8-fatigue-halves-v1"));
        }

        [Test]public void AuthoritativeScoreIncludesFlagsWhileSensitivityAnalysisReportsExclusion()
        {var observations=Enumerable.Range(0,15).Select(index=>new ShotScoringObservation(index!=14,index==14?500d:100d,index==14?15d:0d,index==14?(double?)null:1d)).ToArray();ScoreSensitivityAnalysisResult result=rigor.ScoreShotFlagSensitivity(TestMode.FlickClose,observations,new[]{14});Assert.That(result.InclusiveCount,Is.EqualTo(15));Assert.That(result.ExcludedCount,Is.EqualTo(1));Assert.That(result.InclusivePerformanceScore,Is.LessThan(result.FlaggedExcludedPerformanceScore));Assert.That(result.FlaggedExcludedPerformanceScore,Is.EqualTo(100d).Within(1e-12));}

        [TestCase(199.9,"S")][TestCase(200,"A")][TestCase(250,"B")][TestCase(350,"C")][TestCase(500,"C")][TestCase(500.1,"D")]
        public void ReactionTierBoundariesAreExact(double reaction,string expected){GradeResult grade=rigor.AssignGrade(reaction,Modes(1d));Assert.That(grade.ReactionTier,Is.EqualTo(expected));Assert.That(grade.FinalGrade,Is.EqualTo(expected));}

        [TestCase(0.8,"S")][TestCase(0.6,"A")][TestCase(0.4,"B")][TestCase(0.2,"C")][TestCase(0.0,"D")]
        public void ConsistencyBandsAndWorseTierDetermineGrade(double consistency,string expected){GradeResult grade=rigor.AssignGrade(100d,Modes(consistency));Assert.That(grade.ConsistencyTier,Is.EqualTo(expected));Assert.That(grade.FinalGrade,Is.EqualTo(expected));}

        [Test]public void GradeAuditPersistsBothInputsAndCannotBeOverwritten()
        {GradeResult grade=rigor.AssignGrade(240d,new[]{Mode(TestMode.FlickClose,0.9d),Mode(TestMode.FlickFar,0.7d),Mode(TestMode.Tracking,0.5d),Mode(TestMode.MicroCorrection,0.3d)});Assert.That(grade.ReactionTier,Is.EqualTo("A"));Assert.That(grade.BatteryConsistencyUtility,Is.EqualTo(0.6d).Within(1e-12));Assert.That(grade.ConsistencyTier,Is.EqualTo("A"));Assert.That(grade.FinalGrade,Is.EqualTo("A"));persistence.PersistGrade(sensitivityTestId,grade);using SqliteDatabaseConnection connection=connections.Open();Assert.That(Text(connection,"SELECT grade FROM sensitivity_tests WHERE id="+sensitivityTestId+";"),Is.EqualTo("A"));Assert.That(Text(connection,"SELECT grade_contract_version FROM sensitivity_tests WHERE id="+sensitivityTestId+";"),Is.EqualTo("sc8-grade-v1"));Assert.That(()=>persistence.PersistGrade(sensitivityTestId,grade),Throws.TypeOf<DataAccessException>());}

        private void CreateCompletedBattery()
        {
            var ids=new List<long>();using SqliteDatabaseConnection connection=connections.Open();foreach(string mode in new[]{"flick_close","flick_far","tracking","micro_correction"}){connection.Execute("INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag) VALUES(@profile,@battery,@config,'2026-07-17',@mode,60,0);",new Dictionary<string,object>{{"@profile",profile.Id.Value},{"@battery",battery.Id.Value},{"@config",configId},{"@mode",mode}});long session=connection.LastInsertRowId();if(mode=="flick_close")closeSessionId=session;}for(int index=0;index<11;index++){connection.Execute(@"INSERT INTO shots(session_id,profile_id,target_id,distance_zone,target_size,spawn_position,spawn_timestamp,resolution_timestamp,is_hit,outcome_reason,final_aim_position,is_outlier,is_adaptation_shot,sensitivity_value,initial_offset_distance,submovement_count,final_precision_error,is_center_hit)
VALUES(@session,@profile,@target,'close','small','0,0',@time,@resolved,1,'hit','0,0',NULL,0,0.175,5,1,@precision,1);",new Dictionary<string,object>{{"@session",closeSessionId},{"@profile",profile.Id.Value},{"@target",index+1},{"@time",index},{"@resolved",index+0.1d},{"@precision",index==10?100d:0d}});ids.Add(connection.LastInsertRowId());}connection.Execute("UPDATE protocol_batteries SET completed_date='2026-07-17' WHERE id=@id;",new Dictionary<string,object>{{"@id",battery.Id.Value}});shotIds=ids.AsReadOnly();
        }
        private IReadOnlyList<ModeScoreResult> Modes(double consistency)=>new[]{Mode(TestMode.FlickClose,consistency),Mode(TestMode.FlickFar,consistency),Mode(TestMode.Tracking,consistency),Mode(TestMode.MicroCorrection,consistency)};
        private ModeScoreResult Mode(TestMode mode,double consistency)=>new ModeScoreResult(mode,75d,consistency,0.75d,mode==TestMode.Tracking?(double?)null:0.75d,0.75d,mode==TestMode.Tracking?(double?)null:0.25d,configuration.FormulaVersion.Value,configuration.Record.NormalizationVersion,mode==TestMode.Tracking?54:15);
        private static int Scalar(SqliteDatabaseConnection connection,string sql)=>Convert.ToInt32(connection.Scalar(sql));private static string Text(SqliteDatabaseConnection connection,string sql)=>Convert.ToString(connection.Scalar(sql));private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
    }
}
