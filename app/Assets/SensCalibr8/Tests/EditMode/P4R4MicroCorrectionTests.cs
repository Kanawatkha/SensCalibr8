using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P4R4MicroCorrectionTests
    {
        private FrozenCalibrationConfiguration configuration;private FrozenMicroCorrectionContract contract;private MicroCorrectionMode mode;
        [SetUp]public void SetUp(){configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());contract=FrozenMicroCorrectionContract.From(configuration);mode=new MicroCorrectionMode(new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)),1);}

        [Test]public void FrozenMicroContractLoadsAcceptedOffsetTargetTimeoutAndSignalIdentity(){Assert.That(contract.ModeContractVersion,Is.EqualTo("sc8-mode-contract-v1"));Assert.That(contract.SignalPipelineVersion,Is.EqualTo("sc8-signal-pipeline-v1"));Assert.That(contract.MinimumOffsetPx,Is.EqualTo(5d));Assert.That(contract.MaximumOffsetPx,Is.EqualTo(20d));Assert.That(contract.TargetSize,Is.EqualTo("small"));Assert.That(contract.ShotTimeoutSeconds,Is.EqualTo(1.5d));}

        [Test]public void SignalDetectorHonorsExactThresholdAndRefractoryBoundaries()
        {
            var processor=new SubmovementSignalProcessor(FrozenSubmovementContract.From(configuration));double[] below=Enumerable.Repeat(7.999d,200).ToArray(),threshold=new double[220],merged=new double[300],exact=new double[300];for(int i=20;i<80;i++)threshold[i]=8d;for(int i=80;i<threshold.Length;i++)threshold[i]=3.999d;for(int i=10;i<20;i++){merged[i]=9d;exact[i]=9d;}for(int i=50;i<60;i++)merged[i]=9d;for(int i=100;i<110;i++)exact[i]=9d;
            Assert.That(processor.DetectVelocityForVerification(below),Is.Empty);Assert.That(processor.DetectVelocityForVerification(threshold),Has.Count.EqualTo(1));Assert.That(processor.DetectVelocityForVerification(merged),Has.Count.EqualTo(1));Assert.That(processor.DetectVelocityForVerification(exact),Has.Count.EqualTo(2));
        }

        [Test]public void FullSignalPipelineRejectsShortSegmentAndAcceptsFilterableTrace()
        {
            var processor=new SubmovementSignalProcessor(FrozenSubmovementContract.From(configuration));SubmovementAnalysisResult shortResult=processor.Analyze(new[]{Segment(19)},Timing(true)),accepted=processor.Analyze(new[]{Segment(20)},Timing(true)),movement=processor.Analyze(new[]{MovementSegment()},Timing(true)),timingRejected=processor.Analyze(new[]{Segment(20)},Timing(false));
            Assert.That(shortResult.SignalEligible,Is.False);Assert.That(shortResult.Count,Is.Null);Assert.That(accepted.SignalEligible,Is.True);Assert.That(accepted.Count,Is.EqualTo(0));
            Assert.That(movement.SignalEligible,Is.True);Assert.That(movement.Count,Is.EqualTo(1));
            Assert.That(timingRejected.SignalEligible,Is.False);Assert.That(timingRejected.Disposition,Is.EqualTo("signal-ineligible-timing-contract"));
        }

        [Test]public void CompleteLifecycleResolvesThirtyDeterministicSmallOffsetOpportunities()
        {
            var machine=new TestEngineStateMachine(mode,Session(),configuration);machine.Prepare();machine.Start();SubmovementAnalysisResult signal=AcceptedSignal();
            for(int index=0;index<mode.Sequence.Conditions.Count;index++){double preview=index*3d;machine.Capture(Event("preview_target_visible",preview));TargetCondition condition=mode.CurrentCondition;double offset=Offset(condition);Assert.That(condition.TargetSize,Is.EqualTo("small"));Assert.That(offset,Is.InRange(contract.MinimumOffsetPx,contract.MaximumOffsetPx));machine.Capture(Event("center_reference_activated",preview+0.1d));mode.CaptureSignal(signal);(double azimuth,double elevation)=TargetAngles(condition);machine.Capture(Event("click",preview+0.3d,true,azimuth,elevation));}
            machine.End();TestEngineReport report=machine.Report();Assert.That(report.Completion.IsComplete,Is.True);Assert.That(mode.ResolvedOpportunities,Has.Count.EqualTo(30));Assert.That(mode.ResolvedOpportunities.All(value=>value.MicroAdjustmentCount==0&&value.SubmovementCount==0&&value.IsCenterHit),Is.True);Assert.That(typeof(TestEngineReport).GetProperty("PerformanceScore"),Is.Null);
        }

        [Test]public void HitRequiresEligibleSignalWhileMissKeepsCountsNull()
        {
            mode.Prepare(Session(),configuration);mode.Start();mode.Capture(Event("preview_target_visible",0d));TargetCondition condition=mode.CurrentCondition;mode.Capture(Event("center_reference_activated",0.1d));(double azimuth,double elevation)=TargetAngles(condition);Assert.That(()=>mode.Capture(Event("click",0.2d,true,azimuth,elevation)),Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("micro_hit_requires_eligible_signal"));mode.Capture(Event("click",0.2d,false,azimuth,elevation));Assert.That(mode.ResolvedOpportunities[0].MicroAdjustmentCount,Is.Null);Assert.That(mode.ResolvedOpportunities[0].SubmovementCount,Is.Null);
        }

        [Test]public void PersistenceMapperKeepsPixelOffsetAngularPrecisionAndCountsSeparateFromRawTrace()
        {
            mode.Prepare(Session(),configuration);mode.Start();mode.Capture(Event("preview_target_visible",1d));TargetCondition condition=mode.CurrentCondition;mode.Capture(Event("center_reference_activated",1.1d));mode.CaptureSignal(AcceptedSignal());(double azimuth,double elevation)=TargetAngles(condition);mode.Capture(Event("movement_onset",1.15d));mode.Capture(Event("click",1.3d,true,azimuth+0.1d,elevation));ShotCaptureRecord record=MicroCorrectionEvidencePersistenceMapper.ToShotCaptureRecords(mode.ResolvedOpportunities,0.175d)[0];
            Assert.That(record.DistanceZone,Is.EqualTo("micro"));Assert.That(record.TargetSize,Is.EqualTo("small"));Assert.That(record.InitialOffsetDistance,Is.InRange(5d,20d));Assert.That(record.MicroAdjustmentCount,Is.EqualTo(0));Assert.That(record.SubmovementCount,Is.EqualTo(0));Assert.That(record.FinalPrecisionError,Is.EqualTo(0.1d).Within(1e-12));Assert.That(record.IsAdaptationShot,Is.Null);Assert.That(record.PreviewTimestamp,Is.EqualTo(1d));
        }

        private SubmovementAnalysisResult AcceptedSignal()=>new SubmovementSignalProcessor(FrozenSubmovementContract.From(configuration)).Analyze(new[]{Segment(20)},Timing(true));
        private static UniformAngularSegment Segment(int count){var times=new List<double>(count);var axis=new List<double>(count);for(int i=0;i<count;i++){times.Add(i/1000d);axis.Add(0d);}return new UniformAngularSegment(0,count,times,axis,axis,count>=20);}
        private static UniformAngularSegment MovementSegment(){const int count=400;var times=new List<double>(count);var azimuth=new List<double>(count);var elevation=new List<double>(count);for(int i=0;i<count;i++){times.Add(i/1000d);azimuth.Add(i<100?0d:i<200?(i-99)*0.01d:1d);elevation.Add(0d);}return new UniformAngularSegment(0,count,times,azimuth,elevation,true);}
        private static InputTimingDiagnostics Timing(bool passed)=>new InputTimingDiagnostics("sc8-signal-pipeline-v1","mouse",1000d,1d,0d,1d,1d,0,0,0,19,0,0d,passed,passed?"accepted-integrity-modal-cadence":"rejected-timing-contract");
        private (double azimuth,double elevation) TargetAngles(TargetCondition condition)=>(Math.Atan((condition.CenterXpx.Value-contract.ViewportWidthPx/2d)/contract.FocalLengthPx)*180d/Math.PI,Math.Atan((contract.ViewportHeightPx/2d-condition.CenterYpx.Value)/contract.FocalLengthPx)*180d/Math.PI);
        private double Offset(TargetCondition condition){double x=condition.CenterXpx.Value-contract.ViewportWidthPx/2d,y=condition.CenterYpx.Value-contract.ViewportHeightPx/2d;return Math.Sqrt(x*x+y*y);}
        private static TestModeCaptureEvent Event(string type,double timestamp,bool? hit=null,double? azimuth=null,double? elevation=null){var metadata=new Dictionary<string,string>();if(hit.HasValue)metadata.Add("is_hit",hit.Value?"true":"false");if(azimuth.HasValue)metadata.Add("final_aim_azimuth_deg",azimuth.Value.ToString("G17",System.Globalization.CultureInfo.InvariantCulture));if(elevation.HasValue)metadata.Add("final_aim_elevation_deg",elevation.Value.ToString("G17",System.Globalization.CultureInfo.InvariantCulture));return new TestModeCaptureEvent(type,timestamp,metadata);}
        private EngineSessionContext Session(){var cycle=new EngineCycleContext(1,10,1);var candidate=new EngineCandidateContext(1,10,20,ProtocolPhase.PhaseOne,280d,0.175d);var battery=new EngineBatteryContext(1,10,20,30,ProtocolPhase.PhaseOne,0.175d);return new EngineSessionContext("p4-r4-micro",cycle,candidate,battery,TestMode.MicroCorrection,configuration.ConfigVersion);}
        private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
    }
}
