using System;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P3R1EngineStateMachineTests
    {
        private FrozenCalibrationConfiguration configuration;

        [SetUp]
        public void SetUp()
        {
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
        }

        [Test]
        public void CompleteLifecycleUsesTheSharedModeContractWithoutScoring()
        {
            var mode = new StubMode(TestMode.FlickClose);
            var machine = new TestEngineStateMachine(mode, Session(TestMode.FlickClose), configuration);

            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Created));
            machine.Prepare();
            machine.Start();
            machine.Capture(new TestModeCaptureEvent("resolved_opportunity", 0.25d));
            machine.End();
            TestEngineReport report = machine.Report();

            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Completed));
            Assert.That(mode.CallTrace, Is.EqualTo("prepare,start,capture,end,report"));
            Assert.That(report.Session.RunId, Is.EqualTo("run-1"));
            Assert.That(report.ModeReport.Summary, Is.EqualTo("stub complete"));
            Assert.That(typeof(TestEngineReport).GetProperty("PerformanceScore"), Is.Null);
        }

        [Test]
        public void InvalidTransitionsAreRejectedWithoutInvokingTheMode()
        {
            var mode = new StubMode(TestMode.Tracking);
            var machine = new TestEngineStateMachine(mode, Session(TestMode.Tracking), configuration);
            AssertLifecycleError("engine_invalid_transition_created_to_prepared", machine.Start);
            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Created));
            Assert.That(mode.CallTrace, Is.Empty);

            machine.Prepare();
            AssertLifecycleError("engine_invalid_transition_prepared_to_capturing", machine.End);
            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Prepared));
        }

        [Test]
        public void CallbackFailureMovesToFaultedAndRecoveryRequiresAFreshStart()
        {
            var mode = new StubMode(TestMode.FlickFar) { ThrowOnCapture = true };
            var machine = new TestEngineStateMachine(mode, Session(TestMode.FlickFar), configuration);
            machine.Prepare();
            machine.Start();
            AssertLifecycleError("engine_mode_callback_failed", () => machine.Capture(new TestModeCaptureEvent("raw", 0d)));
            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Faulted));

            mode.ThrowOnCapture = false;
            machine.Recover("in-memory evidence retained");
            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Prepared));
            AssertLifecycleError("engine_invalid_transition_prepared_to_capturing", () => machine.Capture(new TestModeCaptureEvent("raw", 0d)));
            machine.Start();
            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Capturing));
        }

        [Test]
        public void CancellationIsTerminalAndAvailableBeforeCompletion()
        {
            var mode = new StubMode(TestMode.MicroCorrection);
            var machine = new TestEngineStateMachine(mode, Session(TestMode.MicroCorrection), configuration);
            machine.Prepare();
            machine.Start();
            machine.Cancel("user interrupted");
            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Cancelled));
            AssertLifecycleError("engine_terminal_state", () => machine.Cancel("again"));
            AssertLifecycleError("engine_invalid_transition_cancelled_to_prepared", machine.Start);
        }

        [Test]
        public void IncompleteCompletionCannotProduceAReport()
        {
            var mode = new StubMode(TestMode.Tracking) { IsComplete = false };
            var machine = new TestEngineStateMachine(mode, Session(TestMode.Tracking), configuration);
            machine.Prepare();
            machine.Start();
            AssertLifecycleError("engine_mode_completion_incomplete", machine.End);
            Assert.That(machine.State, Is.EqualTo(TestEngineSessionState.Faulted));
            AssertLifecycleError("engine_invalid_transition_faulted_to_ending", () => machine.Report());
        }

        [Test]
        public void ContextRejectsCrossProfileCycleCandidateBatteryAndSensitivityMismatch()
        {
            EngineCycleContext cycle = new EngineCycleContext(1, 10, 1);
            EngineCandidateContext candidate = new EngineCandidateContext(1, 10, 20, ProtocolPhase.PhaseOne, 280d, 0.175d);
            AssertLifecycleError("engine_profile_lineage_mismatch", () => new EngineSessionContext("run", cycle, candidate,
                new EngineBatteryContext(2, 10, 20, 30, ProtocolPhase.PhaseOne, 0.175d), TestMode.FlickClose, configuration.ConfigVersion));
            AssertLifecycleError("engine_candidate_lineage_mismatch", () => new EngineSessionContext("run", cycle, candidate,
                new EngineBatteryContext(1, 10, 21, 30, ProtocolPhase.PhaseOne, 0.175d), TestMode.FlickClose, configuration.ConfigVersion));
            AssertLifecycleError("engine_sensitivity_lineage_mismatch", () => new EngineSessionContext("run", cycle, candidate,
                new EngineBatteryContext(1, 10, 20, 30, ProtocolPhase.PhaseOne, 0.2d), TestMode.FlickClose, configuration.ConfigVersion));
        }

        [Test]
        public void EngineRejectsMissingMismatchedAndInternallyIncompleteConfiguration()
        {
            EngineSessionContext session = Session(TestMode.FlickClose);
            AssertLifecycleError("engine_configuration_incomplete", () => new TestEngineStateMachine(new StubMode(TestMode.FlickClose), session, null));

            var otherVersion = new CalibrationConfigVersion("other-version");
            EngineSessionContext mismatch = Session(TestMode.FlickClose, otherVersion);
            AssertLifecycleError("engine_configuration_version_mismatch", () => new TestEngineStateMachine(new StubMode(TestMode.FlickClose), mismatch, configuration));

            var incomplete = new FrozenCalibrationConfiguration(configuration.ConfigVersion, configuration.FormulaVersion,
                configuration.ContractId, configuration.Sha256, configuration.Record, Array.Empty<SourceContract>());
            AssertLifecycleError("engine_configuration_incomplete", () => new TestEngineStateMachine(new StubMode(TestMode.FlickClose), session, incomplete));
        }

        [Test]
        public void EngineRejectsAModeThatDoesNotMatchTheSessionContract()
        {
            AssertLifecycleError("engine_mode_mismatch", () => new TestEngineStateMachine(
                new StubMode(TestMode.FlickFar), Session(TestMode.FlickClose), configuration));
        }

        private EngineSessionContext Session(TestMode mode) => Session(mode, configuration.ConfigVersion);

        private static EngineSessionContext Session(TestMode mode, CalibrationConfigVersion version)
        {
            var cycle = new EngineCycleContext(1, 10, 1);
            var candidate = new EngineCandidateContext(1, 10, 20, ProtocolPhase.PhaseOne, 280d, 0.175d);
            var battery = new EngineBatteryContext(1, 10, 20, 30, ProtocolPhase.PhaseOne, 0.175d);
            return new EngineSessionContext("run-1", cycle, candidate, battery, mode, version);
        }

        private static void AssertLifecycleError(string errorCode, TestDelegate action)
        {
            TestEngineLifecycleException exception = Assert.Throws<TestEngineLifecycleException>(action);
            Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class StubMode : ITestMode
        {
            public StubMode(TestMode mode) { Mode = mode; }
            public TestMode Mode { get; }
            public string CallTrace { get; private set; } = string.Empty;
            public bool ThrowOnCapture { get; set; }
            public bool IsComplete { get; set; } = true;
            public void Prepare(EngineSessionContext session, FrozenCalibrationConfiguration configuration) => Add("prepare");
            public void Start() => Add("start");
            public void Capture(TestModeCaptureEvent captureEvent) { Add("capture"); if (ThrowOnCapture) throw new InvalidOperationException("capture failed"); }
            public TestModeCompletion End() { Add("end"); return new TestModeCompletion(IsComplete, IsComplete ? "complete" : "incomplete"); }
            public TestModeReport Report() { Add("report"); return new TestModeReport("stub complete"); }
            public void Cancel(string reason) => Add("cancel");
            public void Recover(string reason) => Add("recover");
            private void Add(string value) => CallTrace = string.IsNullOrEmpty(CallTrace) ? value : CallTrace + "," + value;
        }
    }
}
