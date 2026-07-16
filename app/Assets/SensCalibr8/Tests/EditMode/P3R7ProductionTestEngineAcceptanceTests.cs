using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P3R7ProductionTestEngineAcceptanceTests
    {
        private FrozenCalibrationConfiguration configuration;

        [SetUp]
        public void SetUp() => configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());

        [Test]
        public void FrozenProductionEnvelopeIsAcceptedByEverySharedRuntimeContract()
        {
            FrozenInputTimingContract timing = FrozenInputTimingContract.From(configuration);
            FrozenFramePolicy frame = FrozenFramePolicy.From(configuration);
            FrozenArenaGeometry geometry = FrozenArenaGeometry.From(configuration);
            FrozenSequenceContract sequence = FrozenSequenceContract.From(configuration);
            FrozenSessionLifecycleContract lifecycle = FrozenSessionLifecycleContract.From(configuration);
            ResearchConstants constants = ResearchConstantsLoader.LoadFromRepository(RepositoryRoot());

            Assert.That(configuration.Record.ConfigVersion, Is.EqualTo(configuration.ConfigVersion.Value));
            Assert.That(timing.SignalPipelineVersion, Is.EqualTo(configuration.Record.SignalPipelineVersion));
            Assert.That(frame.TargetFrameRateHz, Is.GreaterThan(0));
            Assert.That(geometry.TargetDiameters, Is.Not.Empty);
            Assert.That(sequence.ShotTrials, Is.GreaterThan(0));
            Assert.That(lifecycle.ModeContractVersion, Is.EqualTo("sc8-mode-contract-v1"));
            Assert.That(constants.ValorantYawMultiplier, Is.GreaterThan(0d));
        }

        [Test]
        public void AllFourModesUseTheSameCompleteLifecycleWithoutScoring()
        {
            foreach (TestMode mode in new[] { TestMode.FlickClose, TestMode.FlickFar, TestMode.Tracking, TestMode.MicroCorrection })
            {
                var stub = new AcceptanceStubMode(mode);
                var machine = new TestEngineStateMachine(stub, Session(mode), configuration);
                machine.Prepare();
                machine.Start();
                machine.Capture(new TestModeCaptureEvent("acceptance-event", 0.1d));
                machine.End();
                TestEngineReport report = machine.Report();

                Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Completed));
                Assert.That(stub.CallTrace, Is.EqualTo("prepare,start,capture,end,report"));
                Assert.That(report.Session.Mode, Is.EqualTo(mode));
                Assert.That(typeof(TestEngineReport).GetProperty("PerformanceScore"), Is.Null);
            }
        }

        [Test]
        public void TestLogicContainsOneSharedStateMachineBoundary()
        {
            string root = Path.Combine(RepositoryRoot(), "app", "Assets", "SensCalibr8", "TestLogic");
            string[] stateMachineFiles = Directory.GetFiles(root, "*StateMachine*.cs", SearchOption.AllDirectories);
            Assert.That(stateMachineFiles, Has.Length.EqualTo(1));
            Assert.That(Path.GetFileName(stateMachineFiles[0]), Is.EqualTo("TestEngineStateMachine.cs"));
        }

        private static EngineSessionContext Session(TestMode mode)
        {
            var cycle = new EngineCycleContext(1, 10, 1);
            var candidate = new EngineCandidateContext(1, 10, 20, ProtocolPhase.PhaseOne, 280d, 0.175d);
            var battery = new EngineBatteryContext(1, 10, 20, 30, ProtocolPhase.PhaseOne, 0.175d);
            return new EngineSessionContext("p3-r7-acceptance", cycle, candidate, battery, mode, new CalibrationConfigVersion("calibration_config_v1"));
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class AcceptanceStubMode : ITestMode
        {
            public AcceptanceStubMode(TestMode mode) => Mode = mode;
            public TestMode Mode { get; }
            public string CallTrace { get; private set; } = string.Empty;
            public void Prepare(EngineSessionContext session, FrozenCalibrationConfiguration value) => Add("prepare");
            public void Start() => Add("start");
            public void Capture(TestModeCaptureEvent value) => Add("capture");
            public TestModeCompletion End() { Add("end"); return new TestModeCompletion(true, "accepted"); }
            public TestModeReport Report() { Add("report"); return new TestModeReport("accepted"); }
            public void Cancel(string reason) => Add("cancel");
            public void Recover(string reason) => Add("recover");
            private void Add(string value) => CallTrace = string.IsNullOrEmpty(CallTrace) ? value : CallTrace + "," + value;
        }
    }
}
