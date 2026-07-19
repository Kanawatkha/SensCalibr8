using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P7R3ReproducibilityTimingPerformanceTests
    {
        private string directory;
        private string database;
        private string native;
        private FrozenCalibrationConfiguration configuration;

        [SetUp]
        public void SetUp()
        {
            string root=RepositoryRoot();directory=Path.Combine(Path.GetTempPath(),"senscalibr8-p7r3-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            database=Path.Combine(directory,"p7r3.sqlite3");native=Path.Combine(root,"app","Assets","Plugins","sqlite3.dll");
            configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(root);new SqliteDatabaseBootstrapper().Initialize(database,configuration,native);
        }

        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

        [Test]
        public void RepeatedFreshSequencersProduceTheSameFrozenConditionsForEveryMode()
        {
            FrozenSequenceContract contract=FrozenSequenceContract.From(configuration);
            foreach(TestMode mode in (TestMode[])Enum.GetValues(typeof(TestMode)))
            {
                var context=new SequenceSeedContext(7,11,ProtocolPhase.PhaseOne,mode,1);
                DeterministicTargetSequence first=new DeterministicTargetSequencer(contract).Create(context);
                DeterministicTargetSequence second=new DeterministicTargetSequencer(contract).Create(context);
                Assert.That(second.Audit.SeedSha256,Is.EqualTo(first.Audit.SeedSha256));
                Assert.That(Signature(second),Is.EqualTo(Signature(first)));
            }
            FrozenInputTimingContract timing=FrozenInputTimingContract.From(configuration);FrozenFramePolicy frame=FrozenFramePolicy.From(configuration);
            Assert.That(timing.SamplingRateHz,Is.EqualTo(configuration.Record.InputSamplingRateHz));
            Assert.That(timing.ResamplingToleranceMs,Is.EqualTo(configuration.Record.ResamplingToleranceMs));
            Assert.That(frame.TargetFrameRateHz,Is.EqualTo(144));Assert.That(frame.VSyncCount,Is.EqualTo(0));
        }

        [Test]
        public void FullFrozenShotContractPreservesRawRowsAndReportsObservedPersistenceReadCost()
        {
            var connections=new SqliteConnectionFactory(database,native);ProfileRecord profile=new ProfileRepository(connections).Create(new ProfileRecord(null,"p7r3-profile","2026-07-19",1600,0.175d,1000d,"right","#FFE600","claw","arm",50d,50d,1d,"2026-07-19"));
            var protocol=new ProtocolRepository(connections);CycleRecord cycle=protocol.CreateCycle(new CycleRecord(null,profile.Id.Value,1,"2026-07-19",null,null));ProtocolCandidateRecord candidate=protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null,profile.Id.Value,cycle.Id.Value,1,280d,0.175d,"phase1_offsets","2026-07-19"),new[]{new ProtocolCandidateSourceRecord(280d,0d,280d,false)});ProtocolBatteryRecord battery=protocol.CreateBattery(new ProtocolBatteryRecord(null,profile.Id.Value,cycle.Id.Value,candidate.Id.Value,0.175d,1,"exploratory","2026-07-19",null));
            FrozenSequenceContract sequence=FrozenSequenceContract.From(configuration);FrozenInputTimingContract timing=FrozenInputTimingContract.From(configuration);long configId=new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);int shotCount=sequence.ShotTrials;
            IReadOnlyList<ShotCaptureRecord> shots=Enumerable.Range(0,shotCount).Select(index=>new ShotCaptureRecord(index+1,"close","small","0,0,0",index/timing.SamplingRateHz,index/timing.SamplingRateHz,index/timing.SamplingRateHz+0.001d,index/timing.SamplingRateHz+0.001d,"0,0,0",true,"hit","0,0,0",false,false,0.175d,1d,1,1,0.1d,true,0.01d)).ToArray();
            IReadOnlyList<MouseSampleCaptureRecord> samples=Enumerable.Range(0,shotCount).Select(index=>new MouseSampleCaptureRecord(index,index/timing.SamplingRateHz,1d,-1d,index*0.01d,0d,index)).ToArray();
            var diagnostics=new SessionTimingDiagnosticsRecord(timing.SignalPipelineVersion,"p7r3-fixture",timing.SamplingRateHz,timing.SamplingRateHz,1000d/timing.SamplingRateHz,0d,1000d/timing.SamplingRateHz,1000d/timing.SamplingRateHz,0,0,0,shotCount-1,0,0d,true,"p7r3-fixture-stable-cadence");
            var request=new SessionCaptureRequest(new SessionRecord(profile.Id.Value,battery.Id.Value,configId,"2026-07-19","flick_close",1,null,false),diagnostics,shots,samples);
            var persistenceTimer=Stopwatch.StartNew();new SessionCaptureRepository(connections).Persist(request);persistenceTimer.Stop();
            var analysisTimer=Stopwatch.StartNew();AnalysisProfileDataset dataset=new AnalysisDatasetService(new AnalysisReadRepository(connections)).ReadProfileDataset(profile.Id.Value);analysisTimer.Stop();
            using(SqliteDatabaseConnection connection=connections.Open())
            {
                Assert.That(Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM shots;")),Is.EqualTo(shotCount));
                Assert.That(Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM mouse_samples;")),Is.EqualTo(shotCount));
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT raw_delta_x FROM mouse_samples WHERE sample_index=0;")),Is.EqualTo(1d));
            }
            Assert.That(dataset.Sessions.Count,Is.EqualTo(1));Assert.That(new FileInfo(database).Length,Is.GreaterThan(0));
            UnityEngine.Debug.Log("P7-R3 observed baseline: shots="+shotCount+", mouse_samples="+shotCount+", database_bytes="+new FileInfo(database).Length+", persist_elapsed_ms="+persistenceTimer.Elapsed.TotalMilliseconds.ToString("F3")+", analysis_elapsed_ms="+analysisTimer.Elapsed.TotalMilliseconds.ToString("F3"));
        }

        private static string Signature(DeterministicTargetSequence sequence)=>string.Join(";",sequence.Conditions.Select(value=>value.TrialIndex+":"+value.BlockIndex+":"+value.TargetSize+":"+value.Pattern+":"+value.CenterOffsetDeg+":"+value.CenterAzimuthDeg+":"+value.CenterElevationDeg+":"+value.CenterXpx+":"+value.CenterYpx+":"+value.ForeperiodMs));
        private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
    }
}
