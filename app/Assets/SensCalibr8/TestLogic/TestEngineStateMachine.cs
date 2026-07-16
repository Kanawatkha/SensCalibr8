using System;
using SensCalibr8.Core.Configuration;

namespace SensCalibr8.TestLogic
{
    public sealed class TestEngineStateMachine
    {
        private readonly ITestMode mode;
        private readonly EngineSessionContext session;
        private readonly FrozenCalibrationConfiguration configuration;
        private TestModeCompletion completion;

        public TestEngineStateMachine(ITestMode mode, EngineSessionContext session, FrozenCalibrationConfiguration configuration)
        {
            this.mode = mode ?? throw new ArgumentNullException(nameof(mode));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.configuration = ValidateConfiguration(configuration, session);
            if (mode.Mode != session.Mode) throw new TestEngineLifecycleException("engine_mode_mismatch");
            State = TestEngineSessionState.Created;
        }

        public TestEngineSessionState State { get; private set; }
        public EngineSessionContext Session => session;

        public void Prepare()
        {
            Require(TestEngineSessionState.Created);
            Execute(() => mode.Prepare(session, configuration), TestEngineSessionState.Prepared);
        }

        public void Start()
        {
            Require(TestEngineSessionState.Prepared);
            Execute(mode.Start, TestEngineSessionState.Capturing);
        }

        public void Capture(TestModeCaptureEvent captureEvent)
        {
            if (captureEvent == null) throw new ArgumentNullException(nameof(captureEvent));
            Require(TestEngineSessionState.Capturing);
            Execute(() => mode.Capture(captureEvent), TestEngineSessionState.Capturing);
        }

        public void End()
        {
            Require(TestEngineSessionState.Capturing);
            try
            {
                completion = mode.End() ?? throw new TestEngineLifecycleException("engine_mode_completion_missing");
                if (!completion.IsComplete) throw new TestEngineLifecycleException("engine_mode_completion_incomplete");
                State = TestEngineSessionState.Ending;
            }
            catch (Exception exception)
            {
                State = TestEngineSessionState.Faulted;
                if (exception is TestEngineLifecycleException) throw;
                throw new TestEngineLifecycleException("engine_mode_end_failed", exception);
            }
        }

        public TestEngineReport Report()
        {
            Require(TestEngineSessionState.Ending);
            try
            {
                TestModeReport modeReport = mode.Report() ?? throw new TestEngineLifecycleException("engine_mode_report_missing");
                State = TestEngineSessionState.Completed;
                return new TestEngineReport(session, completion, modeReport);
            }
            catch (Exception exception)
            {
                State = TestEngineSessionState.Faulted;
                if (exception is TestEngineLifecycleException) throw;
                throw new TestEngineLifecycleException("engine_mode_report_failed", exception);
            }
        }

        public void Cancel(string reason)
        {
            if (State == TestEngineSessionState.Completed || State == TestEngineSessionState.Cancelled)
                throw new TestEngineLifecycleException("engine_terminal_state");
            string requiredReason = Required(reason, nameof(reason));
            Execute(() => mode.Cancel(requiredReason), TestEngineSessionState.Cancelled);
        }

        public void Recover(string reason)
        {
            Require(TestEngineSessionState.Faulted);
            string requiredReason = Required(reason, nameof(reason));
            Execute(() => mode.Recover(requiredReason), TestEngineSessionState.Prepared);
            completion = null;
        }

        private void Execute(Action action, TestEngineSessionState successState)
        {
            try
            {
                action();
                State = successState;
            }
            catch (Exception exception)
            {
                State = TestEngineSessionState.Faulted;
                if (exception is TestEngineLifecycleException) throw;
                throw new TestEngineLifecycleException("engine_mode_callback_failed", exception);
            }
        }

        private void Require(TestEngineSessionState expected)
        {
            if (State != expected)
                throw new TestEngineLifecycleException("engine_invalid_transition_" + State.ToString().ToLowerInvariant() + "_to_" + expected.ToString().ToLowerInvariant());
        }

        private static FrozenCalibrationConfiguration ValidateConfiguration(FrozenCalibrationConfiguration value, EngineSessionContext session)
        {
            if (value == null || value.Record == null || value.SourceContracts == null || value.SourceContracts.Count == 0 ||
                string.IsNullOrWhiteSpace(value.ContractId) || string.IsNullOrWhiteSpace(value.Sha256) ||
                string.IsNullOrWhiteSpace(value.ConfigVersion.Value) || string.IsNullOrWhiteSpace(value.FormulaVersion.Value) ||
                !string.Equals(value.ConfigVersion.Value, value.Record.ConfigVersion, StringComparison.Ordinal))
                throw new TestEngineLifecycleException("engine_configuration_incomplete");
            if (!value.ConfigVersion.Equals(session.CalibrationConfigVersion))
                throw new TestEngineLifecycleException("engine_configuration_version_mismatch");
            return value;
        }

        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(name + " is required.", name);
    }
}
