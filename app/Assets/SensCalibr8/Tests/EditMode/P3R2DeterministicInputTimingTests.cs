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
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P3R2DeterministicInputTimingTests
    {
        private FrozenCalibrationConfiguration configuration;
        private FrozenInputTimingContract timing;
        private ResearchConstants constants;

        [SetUp]
        public void SetUp()
        {
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            timing = FrozenInputTimingContract.From(configuration);
            constants = ResearchConstantsLoader.LoadFromRepository(RepositoryRoot());
        }

        [Test]
        public void RuntimeContractsLoadTimingAndFramePolicyOnlyFromFrozenConfiguration()
        {
            Assert.That(timing.SignalPipelineVersion, Is.EqualTo(configuration.Record.SignalPipelineVersion));
            Assert.That(timing.SamplingRateHz, Is.EqualTo(configuration.Record.InputSamplingRateHz));
            Assert.That(timing.ResamplingToleranceMs, Is.EqualTo(configuration.Record.ResamplingToleranceMs));
            Assert.That(timing.AcceptancePolicy, Is.EqualTo(configuration.Record.TimingAcceptancePolicy));
            Assert.That(timing.MinimumFilterableSegmentSamples, Is.EqualTo(20));

            FrozenFramePolicy frame = FrozenFramePolicy.From(configuration);
            Assert.That(frame.TargetFrameRateHz, Is.EqualTo(144));
            Assert.That(frame.VSyncCount, Is.EqualTo(0));
            Assert.That(frame.AdaptiveSyncRequiredOff, Is.True);
        }

        [Test]
        public void CapturePreservesEveryRawDeltaAndConvertsCumulativeAngularPosition()
        {
            var source = new FakeRawMouseSource();
            var angular = new InputAngularConverter(0.175d, constants);
            using var capture = new DeterministicInputCapture(source, angular);
            capture.Start();
            source.Emit(Event(100, 10d, 20d, 5d, -2d));
            source.Emit(Event(101, 10.001d, 20.001d, -1d, 4d));
            IReadOnlyList<CapturedMouseSample> samples = capture.Stop();

            Assert.That(samples, Has.Count.EqualTo(2));
            Assert.That(samples[0].Source.RawDeltaX, Is.EqualTo(5d));
            Assert.That(samples[0].Source.InputEventTimestampSeconds, Is.EqualTo(20d));
            Assert.That(samples[1].SessionTimestampSeconds, Is.EqualTo(0.001d).Within(1e-12));
            Assert.That(angular.DegreesPerCount, Is.EqualTo(0.175d * constants.ValorantYawMultiplier));
            Assert.That(samples[0].CumulativeAzimuthDeg, Is.EqualTo(5d * angular.DegreesPerCount));
            Assert.That(samples[1].CumulativeAzimuthDeg, Is.EqualTo(4d * angular.DegreesPerCount));
            Assert.That(samples[1].CumulativeElevationDeg, Is.EqualTo(2d * angular.DegreesPerCount));
        }

        [Test]
        public void StableNominalCadencePassesWithExactPhaseZeroParityMetrics()
        {
            IReadOnlyList<CapturedMouseSample> samples = Samples(0d, 0.001d, 0.002d, 0.003d, 0.004d);
            InputTimingDiagnostics result = new InputTimingAnalyzer(timing).Analyze(samples);
            Assert.That(result.TimingContractPassed, Is.True);
            Assert.That(result.MeasuredEventRateHz, Is.EqualTo(1000d).Within(1e-9));
            Assert.That(result.MedianIntervalMs, Is.EqualTo(1d).Within(1e-12));
            Assert.That(result.IntervalMadMs, Is.EqualTo(0d).Within(1e-12));
            Assert.That(result.BurstIntervalCount, Is.EqualTo(0));
            Assert.That(result.SingleCadenceIntervalCount, Is.EqualTo(4));
            Assert.That(result.GapIntervalCount, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateAndReversedEventTimesAreRetainedButFailTheTimingGate()
        {
            IReadOnlyList<CapturedMouseSample> samples = Samples(0d, 0.001d, 0.001d, 0.0005d, 0.002d);
            InputTimingDiagnostics result = new InputTimingAnalyzer(timing).Analyze(samples);
            Assert.That(result.DuplicateTimestampCount, Is.EqualTo(1));
            Assert.That(result.ReverseTimestampCount, Is.EqualTo(1));
            Assert.That(result.TimingContractPassed, Is.False);
            Assert.That(result.DispositionReason, Is.EqualTo("rejected-non-increasing-timestamps"));
            Assert.That(() => new InputTimingAnalyzer(timing).ResampleGapSafe(samples),
                Throws.TypeOf<InputTimingException>().With.Property("ErrorCode").EqualTo("timing_non_increasing_cannot_resample"));
        }

        [Test]
        public void BurstsAndGapsRemainDiagnosticWhileGapSafeResamplingNeverBridgesThem()
        {
            IReadOnlyList<CapturedMouseSample> samples = Samples(0d, 0.0001d, 0.001d, 0.002d, 0.005d, 0.006d);
            var analyzer = new InputTimingAnalyzer(timing);
            InputTimingDiagnostics result = analyzer.Analyze(samples);
            Assert.That(result.TimingContractPassed, Is.True);
            Assert.That(result.BurstIntervalCount, Is.EqualTo(1));
            Assert.That(result.GapIntervalCount, Is.EqualTo(1));

            IReadOnlyList<UniformAngularSegment> segments = analyzer.ResampleGapSafe(samples);
            Assert.That(segments, Has.Count.EqualTo(2));
            Assert.That(segments[0].SourceStartIndex, Is.EqualTo(0));
            Assert.That(segments[0].SourceStopIndexExclusive, Is.EqualTo(4));
            Assert.That(segments[1].SourceStartIndex, Is.EqualTo(4));
            Assert.That(segments[0].FilterEligible, Is.False);
            Assert.That(segments[1].FilterEligible, Is.False);
            Assert.That(segments[0].TimeSeconds[^1], Is.LessThan(segments[1].TimeSeconds[0]));
        }

        [Test]
        public void SingleCadenceMustBeStrictlyModal()
        {
            IReadOnlyList<CapturedMouseSample> samples = Samples(0d, 0.0001d, 0.0002d, 0.0012d, 0.0022d);
            InputTimingDiagnostics result = new InputTimingAnalyzer(timing).Analyze(samples);
            Assert.That(result.BurstIntervalCount, Is.EqualTo(2));
            Assert.That(result.SingleCadenceIntervalCount, Is.EqualTo(2));
            Assert.That(result.TimingContractPassed, Is.False);
            Assert.That(result.DispositionReason, Is.EqualTo("rejected-single-cadence-not-modal"));
        }

        [Test]
        public void PersistenceMapperKeepsRawAndAngularEvidenceSeparateAndComplete()
        {
            IReadOnlyList<CapturedMouseSample> samples = Samples(0d, 0.001d, 0.002d);
            InputTimingDiagnostics diagnostics = new InputTimingAnalyzer(timing).Analyze(samples);
            IReadOnlyList<MouseSampleCaptureRecord> records = InputEvidencePersistenceMapper.ToMouseSampleRecords(samples);
            SessionTimingDiagnosticsRecord timingRecord = InputEvidencePersistenceMapper.ToTimingRecord(diagnostics, 1000d);

            Assert.That(records, Has.Count.EqualTo(samples.Count));
            Assert.That(records[1].RawDeltaX, Is.EqualTo(samples[1].Source.RawDeltaX));
            Assert.That(records[1].AzimuthDeg, Is.EqualTo(samples[1].CumulativeAzimuthDeg));
            Assert.That(records[1].TimestampSec, Is.EqualTo(samples[1].SessionTimestampSeconds));
            Assert.That(timingRecord.ConfiguredPollingRateHz, Is.EqualTo(1000d));
            Assert.That(timingRecord.MeasuredEventRateHz, Is.EqualTo(1000d).Within(1e-9));
            Assert.That(timingRecord.TimingContractPassed, Is.True);
        }

        [Test]
        public void PersistenceReadyEvidenceCommitsThroughTheExistingAtomicSessionBoundary()
        {
            string directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p3r2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string database = Path.Combine(directory, "input.sqlite3");
                string native = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
                new SqliteDatabaseBootstrapper().Initialize(database, configuration, native);
                var connections = new SqliteConnectionFactory(database, native);
                ProfileRecord profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "input-profile", "2026-07-16",
                    1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm", 5000d, 5000d, 1d, "2026-07-16"));
                var protocol = new ProtocolRepository(connections);
                CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
                ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(
                    new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-16"),
                    new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
                ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value,
                    cycle.Id.Value, candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-16", null));
                long configId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);

                IReadOnlyList<CapturedMouseSample> samples = Samples(0d, 0.001d, 0.002d);
                InputTimingDiagnostics diagnostics = new InputTimingAnalyzer(timing).Analyze(samples);
                var request = new SessionCaptureRequest(
                    new SessionRecord(profile.Id.Value, battery.Id.Value, configId, "2026-07-16", "flick_close", 1, null, false),
                    InputEvidencePersistenceMapper.ToTimingRecord(diagnostics, 1000d),
                    Array.Empty<ShotCaptureRecord>(), InputEvidencePersistenceMapper.ToMouseSampleRecords(samples));
                new SessionCaptureRepository(connections).Persist(request);

                using SqliteDatabaseConnection connection = connections.Open();
                Assert.That(Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM mouse_samples;")), Is.EqualTo(3));
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT raw_delta_x FROM mouse_samples WHERE sample_index=1;")),
                    Is.EqualTo(samples[1].Source.RawDeltaX));
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT azimuth_deg FROM mouse_samples WHERE sample_index=1;")),
                    Is.EqualTo(samples[1].CumulativeAzimuthDeg));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void FramePolicyRequiresAdaptiveSyncConfirmationAndRestoresRuntimeSettings()
        {
            FrozenFramePolicy policy = FrozenFramePolicy.From(configuration);
            Assert.That(() => new UnityFramePolicyScope(policy, false), Throws.TypeOf<InvalidOperationException>());
            int previousTarget = Application.targetFrameRate;
            int previousVSync = QualitySettings.vSyncCount;
            using (new UnityFramePolicyScope(policy, true))
            {
                Assert.That(Application.targetFrameRate, Is.EqualTo(policy.TargetFrameRateHz));
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(policy.VSyncCount));
            }
            Assert.That(Application.targetFrameRate, Is.EqualTo(previousTarget));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(previousVSync));
        }

        [Test]
        public void ProductionClockAndCaptureLogicDoNotDependOnRenderFrames()
        {
            var clock = new StopwatchHighResolutionClock();
            Assert.That(clock.Frequency, Is.GreaterThan(0));
            Assert.That(clock.TimestampTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(typeof(DeterministicInputCapture).BaseType, Is.EqualTo(typeof(object)));
            Assert.That(typeof(InputTimingAnalyzer).BaseType, Is.EqualTo(typeof(object)));
        }

        private static RawMouseInputEvent Event(long ticks, double monotonic, double eventTime, double x, double y) =>
            new RawMouseInputEvent(ticks, monotonic, eventTime, x, y, "mouse-1");

        private IReadOnlyList<CapturedMouseSample> Samples(params double[] eventTimes)
        {
            var source = new FakeRawMouseSource();
            var angular = new InputAngularConverter(0.175d, constants);
            using var capture = new DeterministicInputCapture(source, angular);
            capture.Start();
            for (int index = 0; index < eventTimes.Length; index++)
                source.Emit(Event(index, index * 0.001d, eventTimes[index], 1d, -0.5d));
            return capture.Stop();
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class FakeRawMouseSource : IRawMouseInputSource
        {
            public event Action<RawMouseInputEvent> DeltaReceived;
            public bool IsStarted { get; private set; }
            public void StartCapture() => IsStarted = true;
            public void StopCapture() => IsStarted = false;
            public void Emit(RawMouseInputEvent value)
            {
                if (!IsStarted) throw new InvalidOperationException("Fake source is not active.");
                DeltaReceived?.Invoke(value);
            }
        }
    }
}
