using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Integration;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P4R6ModeAcceptanceTests
    {
        private FrozenCalibrationConfiguration configuration;
        private FrozenSequenceContract sequenceContract;

        [SetUp]
        public void SetUp()
        {
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            sequenceContract = FrozenSequenceContract.From(configuration);
        }

        [Test]
        public void ProductionFactoryCreatesAllFourImplementedModes()
        {
            var factory = new ProductionBatteryTestModeFactory(new DeterministicTargetSequencer(sequenceContract));
            Assert.That(factory.Create(TestMode.FlickClose, 1), Is.TypeOf<CloseFlickMode>());
            Assert.That(factory.Create(TestMode.FlickFar, 1), Is.TypeOf<FarFlickMode>());
            Assert.That(factory.Create(TestMode.Tracking, 1), Is.TypeOf<TrackingMode>());
            Assert.That(factory.Create(TestMode.MicroCorrection, 1), Is.TypeOf<MicroCorrectionMode>());
        }

        [Test]
        public void RepeatedRunsWithTheSameContextReproduceEveryModeAuditAndCondition()
        {
            foreach (TestMode mode in Enum.GetValues(typeof(TestMode)))
            {
                SequenceSeedContext context = new SequenceSeedContext(7, 11, ProtocolPhase.PhaseOne, mode, 2);
                DeterministicTargetSequence first = new DeterministicTargetSequencer(sequenceContract).Create(context);
                DeterministicTargetSequence second = new DeterministicTargetSequencer(sequenceContract).Create(context);

                Assert.That(second.Audit.SeedSha256, Is.EqualTo(first.Audit.SeedSha256));
                Assert.That(second.Audit.SeedMaterial, Is.EqualTo(first.Audit.SeedMaterial));
                Assert.That(second.Conditions.Count, Is.EqualTo(first.Conditions.Count));
                for (int index = 0; index < first.Conditions.Count; index++)
                    AssertConditionEqual(first.Conditions[index], second.Conditions[index]);
            }
        }

        [Test]
        public void EveryGeneratedStationaryTargetRemainsInsideTheFrozenHudSafeViewport()
        {
            foreach (TestMode mode in new[] { TestMode.FlickClose, TestMode.FlickFar, TestMode.MicroCorrection })
            {
                DeterministicTargetSequence generated = new DeterministicTargetSequencer(sequenceContract).Create(
                    new SequenceSeedContext(7, 11, ProtocolPhase.PhaseOne, mode, 1));
                foreach (TargetCondition condition in generated.Conditions)
                {
                    Assert.That(condition.CenterXpx, Is.Not.Null);
                    Assert.That(condition.CenterYpx, Is.Not.Null);
                    double radius = sequenceContract.TargetPixelDiameters[condition.TargetSize] / 2d;
                    Assert.That(condition.CenterXpx.Value - radius, Is.GreaterThanOrEqualTo(sequenceContract.EdgeMarginPx));
                    Assert.That(condition.CenterXpx.Value + radius, Is.LessThanOrEqualTo(sequenceContract.ViewportWidthPx - sequenceContract.EdgeMarginPx));
                    Assert.That(condition.CenterYpx.Value - radius, Is.GreaterThanOrEqualTo(sequenceContract.HudReservePx));
                    Assert.That(condition.CenterYpx.Value + radius, Is.LessThanOrEqualTo(sequenceContract.ViewportHeightPx - sequenceContract.EdgeMarginPx));
                }
            }
        }

        [Test]
        public void CancellationIsTerminalForEveryProductionModeAndCannotResumeCapture()
        {
            foreach (TestMode mode in Enum.GetValues(typeof(TestMode)))
            {
                ITestMode implementation = new ProductionBatteryTestModeFactory(new DeterministicTargetSequencer(sequenceContract)).Create(mode, 1);
                var engine = new TestEngineStateMachine(implementation, Session(mode), configuration);
                engine.Prepare();
                engine.Start();
                engine.Cancel("acceptance-cancel");

                Assert.That(engine.State, Is.EqualTo(TestEngineSessionState.Cancelled));
                Assert.That(() => engine.Capture(new TestModeCaptureEvent("after-cancel", 0d)),
                    Throws.TypeOf<TestEngineLifecycleException>());
                Assert.That(() => engine.Recover("cannot-recover-cancelled"),
                    Throws.TypeOf<TestEngineLifecycleException>());
            }
        }

        [Test]
        public void FailedModeEndRequiresAFreshEngineAfterRecovery()
        {
            var mode = new CloseFlickMode(new DeterministicTargetSequencer(sequenceContract), 1);
            var engine = new TestEngineStateMachine(mode, Session(TestMode.FlickClose), configuration);
            engine.Prepare();
            engine.Start();
            Assert.That(() => engine.End(), Throws.TypeOf<TestEngineLifecycleException>());
            Assert.That(engine.State, Is.EqualTo(TestEngineSessionState.Faulted));
            engine.Recover("acceptance-restart");
            Assert.That(engine.State, Is.EqualTo(TestEngineSessionState.Prepared));
            engine.Start();
            Assert.That(engine.State, Is.EqualTo(TestEngineSessionState.Capturing));
        }

        [Test]
        public void AcceptedCrosshairPaletteRejectsUnsupportedColorsAndKeepsFixedGeometry()
        {
            GameObject objectUnderTest = new GameObject("p4-r6-crosshair");
            try
            {
                var crosshair = objectUnderTest.AddComponent<FixedDotCrosshair>();
                crosshair.Configure("#FFE600", FrozenArenaGeometry.From(configuration).CrosshairDiameter);
                Assert.That(() => crosshair.Configure("#123456", 4f), Throws.TypeOf<ArgumentException>());
            }
            finally { UnityEngine.Object.DestroyImmediate(objectUnderTest); }
        }

        [Test]
        public void AcceptanceEvidenceContainsNoScoreOrWinnerSurface()
        {
            Assert.That(typeof(TestEngineReport).GetProperty("PerformanceScore"), Is.Null);
            Assert.That(typeof(CrossModeBatteryPlan).GetProperty("Candidate"), Is.Null);
            Assert.That(typeof(CrossModeBatteryPlan).GetProperty("SensitivityValue"), Is.Null);
        }

        private EngineSessionContext Session(TestMode mode)
        {
            var cycle = new EngineCycleContext(1, 11, 1);
            var candidate = new EngineCandidateContext(1, 11, 21, ProtocolPhase.PhaseOne, 280d, 0.175d);
            var battery = new EngineBatteryContext(1, 11, 21, 31, ProtocolPhase.PhaseOne, 0.175d);
            return new EngineSessionContext("p4-r6-acceptance", cycle, candidate, battery, mode, configuration.ConfigVersion);
        }

        private static void AssertConditionEqual(TargetCondition first, TargetCondition second)
        {
            Assert.That(second.TrialIndex, Is.EqualTo(first.TrialIndex));
            Assert.That(second.BlockIndex, Is.EqualTo(first.BlockIndex));
            Assert.That(second.Mode, Is.EqualTo(first.Mode));
            Assert.That(second.TargetSize, Is.EqualTo(first.TargetSize));
            Assert.That(second.Pattern, Is.EqualTo(first.Pattern));
            Assert.That(second.CenterOffsetDeg, Is.EqualTo(first.CenterOffsetDeg));
            Assert.That(second.CenterAzimuthDeg, Is.EqualTo(first.CenterAzimuthDeg));
            Assert.That(second.CenterElevationDeg, Is.EqualTo(first.CenterElevationDeg));
            Assert.That(second.CenterXpx, Is.EqualTo(first.CenterXpx));
            Assert.That(second.CenterYpx, Is.EqualTo(first.CenterYpx));
            Assert.That(second.ForeperiodMs, Is.EqualTo(first.ForeperiodMs));
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
