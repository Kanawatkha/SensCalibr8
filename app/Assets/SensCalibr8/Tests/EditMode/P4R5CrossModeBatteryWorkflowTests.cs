using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using SensCalibr8.Integration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P4R5CrossModeBatteryWorkflowTests
    {
        private string directory,database,native;private FrozenCalibrationConfiguration configuration;private SqliteConnectionFactory connections;private ProfileRecord profile;private CycleRecord cycle;private ProtocolCandidateRecord candidate;private ProtocolBatteryRecord battery;private CrossModeBatteryWorkflow workflow;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"senscalibr8-p4r5-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);database=Path.Combine(directory,"battery.sqlite3");native=Path.Combine(RepositoryRoot(),"app","Assets","Plugins","sqlite3.dll");configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());new SqliteDatabaseBootstrapper().Initialize(database,configuration,native);connections=new SqliteConnectionFactory(database,native);profile=new ProfileRepository(connections).Create(new ProfileRecord(null,"battery-profile","2026-07-16",1600,0.175d,1000d,"right","#FFE600","claw","arm",5000d,5000d,1d,"2026-07-16"));var protocol=new ProtocolRepository(connections);cycle=protocol.CreateCycle(new CycleRecord(null,profile.Id.Value,1,"2026-07-16",null,null));candidate=protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null,profile.Id.Value,cycle.Id.Value,1,280d,0.175d,"phase1_offsets","2026-07-16"),new[]{new ProtocolCandidateSourceRecord(280d,0d,280d,false)});battery=protocol.CreateBattery(new ProtocolBatteryRecord(null,profile.Id.Value,cycle.Id.Value,candidate.Id.Value,0.175d,1,"exploratory","2026-07-16",null));var persistence=new SessionBatteryPersistenceService(new SessionLifecycleRepository(connections),new SessionCaptureRepository(connections),new CalibrationConfigurationRepository(connections),configuration);workflow=new CrossModeBatteryWorkflow(new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)),new StubFactory(),persistence,configuration);}
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

        [Test]public void PlanUsesOpaqueBlindLabelAndEachModeExactlyOnceInCounterbalancedOrder()
        {
            CrossModeBatteryPlan plan=CreatePlan();Assert.That(plan.BlindCandidateLabel,Is.EqualTo("Candidate-01"));Assert.That(plan.OrderedModes,Has.Count.EqualTo(4));Assert.That(plan.OrderedModes,Is.Unique);Assert.That(typeof(CrossModeBatteryPlan).GetProperty("Candidate"),Is.Null);Assert.That(typeof(CrossModeBatteryPlan).GetProperty("SensitivityValue"),Is.Null);
        }

        [Test]public void CompleteFourModeBatteryPersistsSameBlindLabelAndMarksBatteryCompleteOnlyOnFourth()
        {
            CrossModeBatteryPlan plan=CreatePlan();SessionFinalizationResult final=null;for(int index=0;index<plan.OrderedModes.Count;index++){CrossModeBatteryRun run=workflow.BeginNext("2026-07-16T00:00:00Z");Assert.That(run.Mode,Is.EqualTo(plan.OrderedModes[index]));CompleteEngine(run);final=workflow.CompleteActive(Capture(run.Mode),"2026-07-16T00:10:00Z");Assert.That(final.BatteryCompleted,Is.EqualTo(index==3));}
            Assert.That(final.BatteryCompleted,Is.True);Assert.That(workflow.CompletedModes,Has.Count.EqualTo(4));using SqliteDatabaseConnection connection=connections.Open();Assert.That(Scalar(connection,"SELECT COUNT(DISTINCT mode) FROM sessions WHERE battery_id="+battery.Id+";"),Is.EqualTo(4));Assert.That(Scalar(connection,"SELECT COUNT(DISTINCT blind_candidate_label) FROM session_sequence_audits;"),Is.EqualTo(1));Assert.That(Text(connection,"SELECT blind_candidate_label FROM session_sequence_audits LIMIT 1;"),Is.EqualTo("Candidate-01"));Assert.That(()=>workflow.BeginNext("2026-07-16T00:11:00Z"),Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("battery_all_modes_completed"));
        }

        [Test]public void WorkflowRejectsMismatchedCaptureAndRequiresCompletedEngineReport()
        {
            CreatePlan();CrossModeBatteryRun run=workflow.BeginNext("2026-07-16T00:00:00Z");Assert.That(()=>workflow.CompleteActive(Capture(run.Mode),"2026-07-16T00:01:00Z"),Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("battery_mode_report_required"));CompleteEngine(run);TestMode wrong=run.Mode==TestMode.Tracking?TestMode.FlickClose:TestMode.Tracking;Assert.That(()=>workflow.CompleteActive(Capture(wrong),"2026-07-16T00:01:00Z"),Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("battery_capture_lineage_mismatch"));Assert.That(workflow.ActiveRun,Is.SameAs(run));
        }

        [Test]public void WorkflowRejectsIncompleteModeContractsAndFailedTiming()
        {
            CreatePlan();CrossModeBatteryRun run=workflow.BeginNext("2026-07-16T00:00:00Z");CompleteEngine(run);SessionCaptureRequest incomplete=Capture(run.Mode,validTiming:false);Assert.That(()=>workflow.CompleteActive(incomplete,"2026-07-16T00:01:00Z"),Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("battery_timing_contract_failed"));Assert.That(workflow.ActiveRun,Is.SameAs(run));
        }

        private CrossModeBatteryPlan CreatePlan()=>workflow.CreatePlan(new EngineCycleContext(profile.Id.Value,cycle.Id.Value,1),new EngineCandidateContext(profile.Id.Value,cycle.Id.Value,candidate.Id.Value,ProtocolPhase.PhaseOne,280d,0.175d),new EngineBatteryContext(profile.Id.Value,cycle.Id.Value,candidate.Id.Value,battery.Id.Value,ProtocolPhase.PhaseOne,0.175d),1,new[]{candidate.Id.Value});
        private static void CompleteEngine(CrossModeBatteryRun run){run.Engine.Capture(new TestModeCaptureEvent("complete",0d));run.Engine.End();run.Engine.Report();}
        private SessionCaptureRequest Capture(TestMode mode,bool validTiming=true){string name=ModeName(mode);var session=new SessionRecord(profile.Id.Value,battery.Id.Value,new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value),"2026-07-16",name,1,null,false);return mode==TestMode.Tracking?new SessionCaptureRequest(session,Timing(validTiming),Array.Empty<ShotCaptureRecord>(),Array.Empty<MouseSampleCaptureRecord>(),Trials(),Windows()):new SessionCaptureRequest(session,Timing(validTiming),Shots(),Array.Empty<MouseSampleCaptureRecord>());}
        private static IReadOnlyList<ShotCaptureRecord> Shots(){var values=new List<ShotCaptureRecord>();for(int index=0;index<30;index++)values.Add(new ShotCaptureRecord(index,"close","small","0,0",index,null,index+0.1d,index+0.1d,"0,0",true,"hit","0,0",null,null,0.175d,1d,1,1,0d,true));return values;}
        private static IReadOnlyList<TrackingTrialCaptureRecord> Trials(){var values=new List<TrackingTrialCaptureRecord>();for(int index=0;index<18;index++)values.Add(new TrackingTrialCaptureRecord(0.175d,index,index/9,null,"linear","small","sc8-mode-contract-v1","{}",6000,"[]",0d,0d));return values;}
        private static IReadOnlyList<TrackingWindowCaptureRecord> Windows(){var values=new List<TrackingWindowCaptureRecord>();for(int trial=0;trial<18;trial++)for(int window=0;window<6;window++)values.Add(new TrackingWindowCaptureRecord(trial,window,window*1000,(window+1)*1000,0d,0d,0d));return values;}
        private static SessionTimingDiagnosticsRecord Timing(bool passed)=>new SessionTimingDiagnosticsRecord("sc8-signal-pipeline-v1","mouse",1000d,1000d,1d,0d,1d,1d,0,0,0,1,0,0d,passed,passed?"accepted":"rejected");private static string ModeName(TestMode mode){switch(mode){case TestMode.FlickClose:return "flick_close";case TestMode.FlickFar:return "flick_far";case TestMode.Tracking:return "tracking";case TestMode.MicroCorrection:return "micro_correction";default:throw new ArgumentOutOfRangeException(nameof(mode));}}private static int Scalar(SqliteDatabaseConnection connection,string sql)=>Convert.ToInt32(connection.Scalar(sql));private static string Text(SqliteDatabaseConnection connection,string sql)=>Convert.ToString(connection.Scalar(sql));private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
        private sealed class StubFactory:IBatteryTestModeFactory{public ITestMode Create(TestMode mode,int ordinal)=>new StubMode(mode);}private sealed class StubMode:ITestMode{private readonly TestMode mode;private bool complete;public StubMode(TestMode mode){this.mode=mode;}public TestMode Mode=>mode;public void Prepare(EngineSessionContext session,FrozenCalibrationConfiguration configuration){}public void Start(){}public void Capture(TestModeCaptureEvent value){if(value.EventType=="complete")complete=true;}public TestModeCompletion End()=>new TestModeCompletion(complete,"complete");public TestModeReport Report()=>new TestModeReport("complete");public void Cancel(string reason){}public void Recover(string reason){}}
    }
}
