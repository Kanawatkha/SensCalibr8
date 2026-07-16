using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P3R5SessionBatteryPersistenceTests
    {
        private string directory, database, native;
        private FrozenCalibrationConfiguration configuration;
        private SqliteConnectionFactory connections;
        private ProfileRecord profile;
        private CycleRecord cycle;
        private ProtocolCandidateRecord candidate;
        private ProtocolBatteryRecord battery;
        private long configId;
        private SessionBatteryPersistenceService service;

        [SetUp]
        public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"senscalibr8-p3r5-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);database=Path.Combine(directory,"engine.sqlite3");native=Path.Combine(RepositoryRoot(),"app","Assets","Plugins","sqlite3.dll");configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());new SqliteDatabaseBootstrapper().Initialize(database,configuration,native);connections=new SqliteConnectionFactory(database,native);
            profile=new ProfileRepository(connections).Create(new ProfileRecord(null,"engine-profile","2026-07-16",1600,0.175d,1000d,"right","#FFE600","claw","arm",5000d,5000d,1d,"2026-07-16"));var protocol=new ProtocolRepository(connections);cycle=protocol.CreateCycle(new CycleRecord(null,profile.Id.Value,1,"2026-07-16",null,null));candidate=protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null,profile.Id.Value,cycle.Id.Value,1,280d,0.175d,"phase1_offsets","2026-07-16"),new[]{new ProtocolCandidateSourceRecord(280d,0d,280d,false)});battery=protocol.CreateBattery(new ProtocolBatteryRecord(null,profile.Id.Value,cycle.Id.Value,candidate.Id.Value,0.175d,1,"exploratory","2026-07-16",null));configId=new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);service=new SessionBatteryPersistenceService(new SessionLifecycleRepository(connections),new SessionCaptureRepository(connections),new CalibrationConfigurationRepository(connections),configuration);
        }
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

        [Test]
        public void FrozenLifecyclePolicyComesOnlyFromAcceptedModeContract()
        { FrozenSessionLifecycleContract contract=FrozenSessionLifecycleContract.From(configuration);Assert.That(contract.ModeContractVersion,Is.EqualTo("sc8-mode-contract-v1"));Assert.That(contract.ShotAdaptationFraction,Is.EqualTo(0.5d));Assert.That(contract.TrackingAdaptationBlockCount,Is.EqualTo(1)); }

        [Test]
        public void PauseCancelAndFaultPersistTerminalDispositionWithoutCreatingCompletedSession()
        {
            SessionAttemptRecord paused=service.Pause(Begin("flick_close","Candidate-01"),"user-paused","2026-07-16T01:00:00Z");SessionAttemptRecord resumed=service.Resume(paused,"user-resumed","2026-07-16T01:01:00Z");SessionAttemptRecord cancelled=service.Cancel(resumed,"user-cancelled","2026-07-16T01:02:00Z");
            Assert.That(cancelled.State,Is.EqualTo("cancelled"));Assert.That(() => service.Resume(cancelled,"bad","2026-07-16T01:03:00Z"),Throws.TypeOf<DataAccessException>());
            SessionAttemptRecord faulted=service.Fault(Begin("flick_far","Candidate-02"),"capture-fault","2026-07-16T01:04:00Z");Assert.That(faulted.State,Is.EqualTo("faulted"));using SqliteDatabaseConnection connection=connections.Open();Assert.That(Count(connection,"sessions"),Is.EqualTo(0));Assert.That(Count(connection,"session_attempts"),Is.EqualTo(2));
        }

        [Test]
        public void CompletionFinalizesShotAdaptationAndPersistsAuditAtomically()
        {
            SessionAttemptRecord attempt=Begin("flick_close","Candidate-01");SessionFinalizationResult result=service.Complete(attempt,Capture("flick_close",Shots(30),Array.Empty<TrackingTrialCaptureRecord>(),Array.Empty<TrackingWindowCaptureRecord>(),false),Audit("Candidate-01"),"2026-07-16T02:00:00Z");
            Assert.That(result.BatteryCompleted,Is.False);Assert.That(result.ShotAdaptationCount,Is.EqualTo(15));using SqliteDatabaseConnection connection=connections.Open();Assert.That(Scalar(connection,"SELECT COUNT(*) FROM shots WHERE is_adaptation_shot=1;"),Is.EqualTo(15));Assert.That(Scalar(connection,"SELECT COUNT(*) FROM shots WHERE is_adaptation_shot=0;"),Is.EqualTo(15));Assert.That(Text(connection,"SELECT state FROM session_attempts WHERE id="+attempt.Id+";"),Is.EqualTo("completed"));Assert.That(Text(connection,"SELECT blind_candidate_label FROM session_sequence_audits;"),Is.EqualTo("Candidate-01"));
            Assert.That(()=>Begin("flick_close","Candidate-02"),Throws.TypeOf<DataAccessException>());
        }

        [Test]
        public void TrackingAdaptationIsFinalizedOnlyAfterSessionEnds()
        {
            SessionAttemptRecord attempt=Begin("tracking","Candidate-03");var trials=new[]{Trial(0,0),Trial(1,1)};SessionFinalizationResult result=service.Complete(attempt,Capture("tracking",Array.Empty<ShotCaptureRecord>(),trials,Array.Empty<TrackingWindowCaptureRecord>(),false),Audit("Candidate-03"),"2026-07-16T03:00:00Z");using SqliteDatabaseConnection connection=connections.Open();Assert.That(result.TrackingAdaptationCount,Is.EqualTo(1));Assert.That(Scalar(connection,"SELECT COUNT(*) FROM tracking_data WHERE is_adaptation_trial=1;"),Is.EqualTo(1));Assert.That(Scalar(connection,"SELECT COUNT(*) FROM tracking_data WHERE is_adaptation_trial=0;"),Is.EqualTo(1));
        }

        [Test]
        public void FourthDistinctCompletedModeCompletesBatteryExactlyOnce()
        {
            CompleteMode("flick_close","Candidate-01");CompleteMode("flick_far","Candidate-02");CompleteMode("tracking","Candidate-03");SessionFinalizationResult fourth=CompleteMode("micro_correction","Candidate-04");Assert.That(fourth.BatteryCompleted,Is.True);using SqliteDatabaseConnection connection=connections.Open();Assert.That(Scalar(connection,"SELECT COUNT(DISTINCT mode) FROM sessions WHERE battery_id="+battery.Id+";"),Is.EqualTo(4));Assert.That(Text(connection,"SELECT completed_date FROM protocol_batteries WHERE id="+battery.Id+";"),Is.EqualTo("2026-07-16T04:00:00Z"));Assert.That(() => Begin("flick_close","Candidate-05"),Throws.TypeOf<DataAccessException>());
        }

        [Test]
        public void FinalizationRejectsPreflaggedAdaptationAndRollsBackTheWholeCapture()
        {
            SessionAttemptRecord attempt=Begin("flick_close","Candidate-01");Assert.That(()=>service.Complete(attempt,Capture("flick_close",Shots(1,true),Array.Empty<TrackingTrialCaptureRecord>(),Array.Empty<TrackingWindowCaptureRecord>(),true),Audit("Candidate-01"),"2026-07-16T05:00:00Z"),Throws.TypeOf<DataAccessException>());using SqliteDatabaseConnection connection=connections.Open();Assert.That(Count(connection,"sessions"),Is.EqualTo(0));Assert.That(Count(connection,"shots"),Is.EqualTo(0));Assert.That(Text(connection,"SELECT state FROM session_attempts WHERE id="+attempt.Id+";"),Is.EqualTo("capturing"));
        }

        [Test]
        public void MismatchedAuditCannotAttachToAttempt()
        { SessionAttemptRecord attempt=Begin("flick_close","Candidate-01");Assert.That(()=>service.Complete(attempt,Capture("flick_close",Array.Empty<ShotCaptureRecord>(),Array.Empty<TrackingTrialCaptureRecord>(),Array.Empty<TrackingWindowCaptureRecord>(),false),Audit("Candidate-02"),"2026-07-16T06:00:00Z"),Throws.TypeOf<InvalidOperationException>()); }

        [Test]
        public void FailureAfterRawRowsBeginRollsBackTheEntireCapture()
        {
            SessionAttemptRecord attempt=Begin("flick_close","Candidate-01");
            var samples=new[]{new MouseSampleCaptureRecord(0,0d,3d,-2d,0.5d,-0.25d,99)};
            Assert.That(()=>service.Complete(attempt,Capture("flick_close",Shots(1),Array.Empty<TrackingTrialCaptureRecord>(),Array.Empty<TrackingWindowCaptureRecord>(),false,samples),Audit("Candidate-01"),"2026-07-16T07:00:00Z"),Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection connection=connections.Open();
            Assert.That(Count(connection,"sessions"),Is.EqualTo(0));Assert.That(Count(connection,"shots"),Is.EqualTo(0));Assert.That(Count(connection,"mouse_samples"),Is.EqualTo(0));
            Assert.That(Text(connection,"SELECT state FROM session_attempts WHERE id="+attempt.Id+";"),Is.EqualTo("capturing"));
        }

        private SessionFinalizationResult CompleteMode(string mode,string label)
        { SessionAttemptRecord attempt=Begin(mode,label);if(mode=="tracking")return service.Complete(attempt,Capture(mode,Array.Empty<ShotCaptureRecord>(),new[]{Trial(0,0),Trial(1,1)},Array.Empty<TrackingWindowCaptureRecord>(),false),Audit(label),"2026-07-16T04:00:00Z");return service.Complete(attempt,Capture(mode,Array.Empty<ShotCaptureRecord>(),Array.Empty<TrackingTrialCaptureRecord>(),Array.Empty<TrackingWindowCaptureRecord>(),false),Audit(label),"2026-07-16T04:00:00Z"); }
        private SessionAttemptRecord Begin(string mode,string label)=>service.Begin(new SessionAttemptStartRecord(profile.Id.Value,cycle.Id.Value,candidate.Id.Value,battery.Id.Value,configId,mode,"2026-07-16T00:00:00Z",Audit(label)));
        private SessionCaptureRequest Capture(string mode,IReadOnlyList<ShotCaptureRecord> shots,IReadOnlyList<TrackingTrialCaptureRecord> trials,IReadOnlyList<TrackingWindowCaptureRecord> windows,bool preflagged)=>new SessionCaptureRequest(new SessionRecord(profile.Id.Value,battery.Id.Value,configId,"2026-07-16",mode,1,null,false),Timing(),shots,Array.Empty<MouseSampleCaptureRecord>(),trials,windows);
        private SessionCaptureRequest Capture(string mode,IReadOnlyList<ShotCaptureRecord> shots,IReadOnlyList<TrackingTrialCaptureRecord> trials,IReadOnlyList<TrackingWindowCaptureRecord> windows,bool preflagged,IReadOnlyList<MouseSampleCaptureRecord> samples=null)=>new SessionCaptureRequest(new SessionRecord(profile.Id.Value,battery.Id.Value,configId,"2026-07-16",mode,1,null,false),Timing(),shots,samples??Array.Empty<MouseSampleCaptureRecord>(),trials,windows);
        private static SessionTimingDiagnosticsRecord Timing()=>new SessionTimingDiagnosticsRecord("sc8-signal-pipeline-v1","mouse",1000d,1000d,1d,0d,1d,1d,0,0,0,1,0,0d,true,"accepted");
        private static IReadOnlyList<ShotCaptureRecord> Shots(int count,bool preflagged=false){var values=new List<ShotCaptureRecord>();for(int index=0;index<count;index++)values.Add(new ShotCaptureRecord(index,"close","small","0,0",index,null,index+.1d,null,null,true,"hit","0,0",null,preflagged ? true : (bool?)null,0.175d,0d,0,1,0d,true));return values;}
        private static TrackingTrialCaptureRecord Trial(int index,int block)=>new TrackingTrialCaptureRecord(0.175d,index,block,null,"linear","small","sc8-mode-contract-v1","{}",6000,"[]",0d,0d);
        private static SessionSequenceAuditRecord Audit(string label)=>new SessionSequenceAuditRecord("sc8-mode-contract-v1","deterministic-versioned-seed","abc123","seed-material",label);
        private static int Count(SqliteDatabaseConnection connection,string table)=>Scalar(connection,"SELECT COUNT(*) FROM "+table+";");private static int Scalar(SqliteDatabaseConnection connection,string sql)=>Convert.ToInt32(connection.Scalar(sql));private static string Text(SqliteDatabaseConnection connection,string sql)=>Convert.ToString(connection.Scalar(sql));private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
    }
}
