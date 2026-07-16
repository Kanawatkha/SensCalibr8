using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class P4R3TrackingTests
    {
        private FrozenCalibrationConfiguration configuration;
        private TrackingMode mode;

        [SetUp]
        public void SetUp()
        { configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());mode=new TrackingMode(new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)),1); }

        [Test]
        public void FrozenTrackingContractLoadsOnlyAcceptedDurationWindowAndBlocks()
        {
            FrozenTrackingContract contract=FrozenTrackingContract.From(configuration);
            Assert.That(contract.ModeContractVersion,Is.EqualTo("sc8-mode-contract-v1")); Assert.That(contract.TrialDurationSeconds,Is.EqualTo(6d)); Assert.That(contract.MetricWindowSeconds,Is.EqualTo(1d)); Assert.That(contract.Blocks,Is.EqualTo(2)); Assert.That(contract.AdaptationBlocks,Is.EqualTo(1));
        }

        [Test]
        public void AnalyticPathsMatchFrozenLinearCurvedAndVariableFixtures()
        {
            mode.Prepare(Session(),configuration);
            TrackingTargetPosition linear=mode.TargetPosition("linear",0d), curved=mode.TargetPosition("curved",0d), variable=mode.TargetPosition("variable_speed",1d);
            Assert.That(linear.AzimuthDeg,Is.EqualTo(-15d).Within(1e-12)); Assert.That(linear.ElevationDeg,Is.Zero);
            Assert.That(curved.AzimuthDeg,Is.EqualTo(15d).Within(1e-12)); Assert.That(curved.ElevationDeg,Is.Zero);
            Assert.That(variable.AzimuthDeg,Is.EqualTo(15d).Within(1e-12)); Assert.That(variable.ElevationDeg,Is.Zero);
        }

        [Test]
        public void CompleteTrackingLifecycleProducesEighteenTrialsAndIntervalWeightedWindows()
        {
            var machine=new TestEngineStateMachine(mode,Session(),configuration);machine.Prepare();machine.Start();
            for(int trial=0;trial<mode.Sequence.Conditions.Count;trial++) CapturePerfectTrial(machine,trial*7d);
            machine.End(); TestEngineReport report=machine.Report();
            Assert.That(report.Completion.IsComplete,Is.True); Assert.That(mode.ResolvedTrials,Has.Count.EqualTo(18)); Assert.That(mode.ResolvedTrials[0].Windows,Has.Count.EqualTo(6)); Assert.That(mode.ResolvedTrials[0].TimeOnTargetPercentage,Is.EqualTo(100d).Within(1e-10)); Assert.That(mode.ResolvedTrials[0].Windows[0].DeviationRmsDeg,Is.Zero.Within(1e-10)); Assert.That(typeof(TestEngineReport).GetProperty("PerformanceScore"),Is.Null);
        }

        [Test]
        public void IncompleteBoundaryCoverageIsRejectedRatherThanInvented()
        {
            mode.Prepare(Session(),configuration);mode.Start();mode.Capture(Event("trial_started",0d));TrackingTargetPosition target=mode.TargetPosition(mode.CurrentCondition.Pattern,0d);mode.Capture(Event("aim_sample",0d,target));
            Assert.That(()=>mode.Capture(Event("trial_completed",6d)),Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("tracking_sample_boundaries_invalid"));
        }

        [Test]
        public void EvidenceMapperPreservesTrialAndSixWindowMetricsWithoutAdaptationGuessing()
        {
            mode.Prepare(Session(),configuration);mode.Start();CapturePerfectTrialDirect(0d); TrackingCaptureEvidence evidence=mode.ResolvedTrials[0];
            IReadOnlyList<TrackingTrialCaptureRecord> trials=TrackingEvidencePersistenceMapper.ToTrialRecords(new[]{evidence}); IReadOnlyList<TrackingWindowCaptureRecord> windows=TrackingEvidencePersistenceMapper.ToWindowRecords(new[]{evidence});
            Assert.That(trials[0].IsAdaptationTrial,Is.Null); Assert.That(trials[0].DurationMs,Is.EqualTo(6000)); Assert.That(trials[0].TimeOnTargetPercentage,Is.EqualTo(100d)); Assert.That(windows,Has.Count.EqualTo(6)); Assert.That(windows[5].WindowStartMs,Is.EqualTo(5000)); Assert.That(windows[5].WindowEndMs,Is.EqualTo(6000));
        }

        private void CapturePerfectTrial(TestEngineStateMachine machine,double start){machine.Capture(Event("trial_started",start)); for(int second=0;second<=6;second++){TrackingTargetPosition target=mode.TargetPosition(mode.CurrentCondition.Pattern,second);machine.Capture(Event("aim_sample",start+second,target));}machine.Capture(Event("trial_completed",start+6d,sensitivity:0.175d));}
        private void CapturePerfectTrialDirect(double start){mode.Capture(Event("trial_started",start));for(int second=0;second<=6;second++){TrackingTargetPosition target=mode.TargetPosition(mode.CurrentCondition.Pattern,second);mode.Capture(Event("aim_sample",start+second,target));}mode.Capture(Event("trial_completed",start+6d,sensitivity:0.175d));}
        private static TestModeCaptureEvent Event(string type,double timestamp,TrackingTargetPosition target=null,double? sensitivity=null){var metadata=new Dictionary<string,string>();if(target!=null){metadata.Add("aim_azimuth_deg",target.AzimuthDeg.ToString("G17",System.Globalization.CultureInfo.InvariantCulture));metadata.Add("aim_elevation_deg",target.ElevationDeg.ToString("G17",System.Globalization.CultureInfo.InvariantCulture));}if(sensitivity.HasValue)metadata.Add("sensitivity_value",sensitivity.Value.ToString("G17",System.Globalization.CultureInfo.InvariantCulture));return new TestModeCaptureEvent(type,timestamp,metadata);}
        private EngineSessionContext Session(){var cycle=new EngineCycleContext(1,10,1);var candidate=new EngineCandidateContext(1,10,20,ProtocolPhase.PhaseOne,280d,0.175d);var battery=new EngineBatteryContext(1,10,20,30,ProtocolPhase.PhaseOne,0.175d);return new EngineSessionContext("p4-r3-tracking",cycle,candidate,battery,TestMode.Tracking,configuration.ConfigVersion);}
        private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
    }
}
