using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SensCalibr8.Calibration.Tests
{
    public sealed class CalibrationHarnessTests
    {
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "SensCalibr8CalibrationTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void NormalizeToken_ProducesLowercaseAsciiToken()
        {
            Assert.That(CalibrationIdFactory.NormalizeToken("  Input Timing / Candidate A  "),
                Is.EqualTo("input-timing-candidate-a"));
        }

        [Test]
        public void CreateRunId_RejectsNonUtcTimestamp()
        {
            Assert.Throws<ArgumentException>(() =>
                CalibrationIdFactory.CreateRunId(DateTime.Now, 1));
        }

        [Test]
        public void ApprovedArtifactPath_FitsLegacyWindowsPathLimit()
        {
            const string RunId = "20260715t0412205342860z-r1";
            string runDirectory = Path.Combine(CalibrationArtifactLocation.CaptureRoot, RunId);
            string fileName = CalibrationIdFactory.BuildArtifactFileName(
                "sc8-p0-r1-protocol-v1",
                "sc8-env-zulartan-wg903-v1",
                "wg903-1600dpi-1000hz-user-bluetooth",
                RunId,
                "environment-manifest",
                "json");
            string artifactPath = Path.Combine(runDirectory, fileName);

            Assert.That(artifactPath.Length, Is.LessThan(260), artifactPath);
        }

        [Test]
        public void AppendOnlyWriter_DrainsQueuedLinesAndRefusesOverwrite()
        {
            string path = Path.Combine(temporaryRoot, "events.jsonl");
            using (AppendOnlyJsonLinesWriter writer = new AppendOnlyJsonLinesWriter(path))
            {
                writer.Append("{\"Sequence\":0}");
                writer.Append("{\"Sequence\":1}");
            }

            string[] lines = File.ReadAllLines(path);
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Is.EqualTo("{\"Sequence\":0}"));
            Assert.That(lines[1], Is.EqualTo("{\"Sequence\":1}"));
            Assert.Throws<IOException>(() => new AppendOnlyJsonLinesWriter(path));
        }

        [Test]
        public void CalibrationClock_UsesPositiveHighResolutionFrequencyAndOrderedTicks()
        {
            CalibrationClock clock = new CalibrationClock();
            CalibrationTimestamp first = clock.Capture();
            CalibrationTimestamp second = clock.Capture();

            Assert.That(clock.Frequency, Is.GreaterThan(0));
            Assert.That(second.Ticks, Is.GreaterThanOrEqualTo(first.Ticks));
            Assert.That(second.Seconds, Is.GreaterThanOrEqualTo(first.Seconds));
        }

        [Test]
        public void EnvironmentFactory_CapturesRuntimeIdentityChecksumAndExplicitUnknowns()
        {
            CalibrationEnvironmentManifest environment = CalibrationEnvironmentFactory.Capture(
                "protocol-a",
                "environment-a",
                null);

            Assert.That(environment.HarnessVersion, Is.EqualTo(CalibrationHarnessMetadata.HarnessVersion));
            Assert.That(environment.HarnessChecksum, Is.EqualTo(CalibrationFileSystem.ComputeSha256(
                typeof(CalibrationHarnessMetadata).Assembly.Location)));
            Assert.That(environment.RuntimeBuildType, Is.EqualTo("unity-editor"));
            Assert.That(environment.ExecutableName, Is.Not.Empty);
            Assert.That(environment.ExecutableChecksum, Is.Not.Empty);
            Assert.That(environment.UnityVersion, Is.Not.Empty);
            Assert.That(environment.InputSystemVersion, Is.Not.Empty);
            Assert.That(environment.InputUpdateMode, Is.Not.Empty);
            Assert.That(environment.RedundantEventMergingDisabled,
                Is.EqualTo(UnityEngine.InputSystem.InputSystem.settings.disableRedundantEventsMerging));
            Assert.That(environment.Manual.MouseDpi, Is.EqualTo(CalibrationHarnessMetadata.UnknownValue));
            Assert.That(environment.Manual.ConfiguredPollingRate, Is.EqualTo(CalibrationHarnessMetadata.UnknownValue));
        }

        [Test]
        public void P0R3CaptureGate_RejectsUnknownAcceptanceEnvironment()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                P0R3CaptureGate.ValidatePlan(
                    "protocol-a",
                    "environment-a",
                    "plan-a",
                    "condition-a",
                    2,
                    1,
                    1.0,
                    "pilot",
                    "reviewer-a",
                    "counterbalanced",
                    "continuous fixture motion",
                    "{\"fixture\":true}",
                    new CalibrationManualEnvironment()));

            Assert.That(error.Message, Does.Contain("VSync state"));
        }

        [Test]
        public void P0R3CaptureGate_AllowsUnknownAuditOnlyMouseMetadata()
        {
            CalibrationManualEnvironment environment = CreateKnownManualEnvironment();
            environment.MouseManufacturer = CalibrationHarnessMetadata.UnknownValue;
            environment.MouseModel = CalibrationHarnessMetadata.UnknownValue;
            environment.MouseFirmware = CalibrationHarnessMetadata.UnknownValue;
            environment.MousepadDescription = CalibrationHarnessMetadata.UnknownValue;
            environment.PostureNotes = CalibrationHarnessMetadata.UnknownValue;

            Assert.DoesNotThrow(() => P0R3CaptureGate.ValidatePlan(
                "protocol-a",
                "environment-a",
                "plan-a",
                "condition-a",
                5,
                1,
                30.0,
                "pilot",
                "reviewer-a",
                "sequential",
                "continuous fixture motion",
                "{\"fixture\":true}",
                environment));
        }

        [Test]
        public void P0R3CaptureGate_AcceptsCompleteFrozenPlan()
        {
            Assert.DoesNotThrow(() => P0R3CaptureGate.ValidatePlan(
                "protocol-a",
                "environment-a",
                "plan-a",
                "condition-a",
                2,
                2,
                1.0,
                "confirmation",
                "reviewer-a",
                "counterbalanced",
                "continuous fixture motion",
                "{\"fixture\":true}",
                CreateKnownManualEnvironment()));
        }

        [Test]
        public void P0R3CaptureGate_RequiresUnmergedMouseEvents()
        {
            Assert.Throws<InvalidOperationException>(() =>
                P0R3CaptureGate.ValidateInputCaptureRuntime(false));
            Assert.DoesNotThrow(() =>
                P0R3CaptureGate.ValidateInputCaptureRuntime(true));
        }

        [Test]
        public void CompletedRun_WritesImmutableArtifactsAndValidIntegrityManifest()
        {
            CalibrationRunStore store = CreateRunStore("completed-run");
            store.RecordMouseEvent(new RawMouseEventRecord
            {
                TraceId = "completed-run-mouse",
                RunId = "completed-run",
                Sequence = 0,
                MonotonicTimestampTicks = 1,
                MonotonicTimestampSeconds = 1.0,
                InputEventTimestampSeconds = 1.0,
                DeviceId = 1,
                DeviceLayout = "Mouse",
                DeviceInterface = "Fixture",
                DeviceProduct = "Fixture Mouse",
                DeviceManufacturer = "Fixture",
                DeviceVersion = "Fixture",
                EventType = "Fixture",
                RawDeltaX = 2.0f,
                RawDeltaY = -1.0f
            });
            store.RecordFrameTiming(new FrameTimingRecord
            {
                RunId = "completed-run",
                EnvironmentId = "environment-a",
                Sequence = 0,
                MonotonicTimestampTicks = 1,
                MonotonicTimestampSeconds = 1.0,
                UnityFrameIndex = 1,
                UnscaledDeltaTimeSeconds = 0.01f,
                ApplicationFocused = true,
                ScreenWidth = 1,
                ScreenHeight = 1,
                RefreshRateHz = 1.0,
                FullScreenMode = "Fixture"
            });
            store.RecordTargetCameraEvent(new TargetCameraEventRecord
            {
                RunId = "completed-run",
                ConditionId = "condition-a",
                Sequence = 0,
                MonotonicTimestampTicks = 1,
                MonotonicTimestampSeconds = 1.0,
                EventType = "fixture",
                TargetId = "target-a"
            });

            IntegrityManifest integrity = store.Complete("completed", "fixture-complete");

            Assert.That(File.Exists(store.FinalManifestPath), Is.True);
            Assert.That(File.Exists(store.IntegrityManifestPath), Is.True);
            Assert.That(File.ReadAllLines(store.RawMouseEventsPath), Has.Length.EqualTo(1));
            Assert.That(integrity.Artifacts, Is.Not.Empty);
            for (int index = 0; index < integrity.Artifacts.Length; index++)
            {
                FileIntegrityRecord record = integrity.Artifacts[index];
                string sourcePath = Path.Combine(store.RunDirectory, record.RelativePath);
                Assert.That(File.Exists(sourcePath), Is.True);
                Assert.That(new FileInfo(sourcePath).Length, Is.EqualTo(record.ByteSize));
                Assert.That(CalibrationFileSystem.ComputeSha256(sourcePath), Is.EqualTo(record.Sha256));
                Assert.That(record.ArtifactId, Is.Not.Empty);
                Assert.That(record.CreatedUtc, Is.Not.Empty);
                Assert.That(record.ProducerVersion, Is.EqualTo(CalibrationHarnessMetadata.HarnessVersion));
                Assert.That(record.ProducerChecksum, Is.EqualTo("fixture-harness-sha256"));
                Assert.That(record.ProtocolId, Is.EqualTo("protocol-a"));
                Assert.That(record.EnvironmentId, Is.EqualTo("environment-a"));
                Assert.That(record.CapturePlanId, Is.EqualTo("plan-a"));
                Assert.That(record.ConditionId, Is.EqualTo("condition-a"));
                Assert.That(record.RunId, Is.EqualTo("completed-run"));
                Assert.That(record.TraceId, Is.EqualTo("completed-run-mouse"));
            }

            Assert.Throws<InvalidOperationException>(() =>
                store.RecordMouseEvent(new RawMouseEventRecord()));
        }

        [Test]
        public void RunStore_RejectsBrokenRelationshipsAndNonContiguousSequences()
        {
            CalibrationRunStore store = CreateRunStore("ordered-run");
            Assert.Throws<InvalidOperationException>(() => store.RecordMouseEvent(new RawMouseEventRecord
            {
                TraceId = "wrong-trace",
                RunId = "ordered-run",
                Sequence = 0,
                MonotonicTimestampTicks = 1
            }));
            Assert.Throws<InvalidOperationException>(() => store.RecordFrameTiming(new FrameTimingRecord
            {
                RunId = "ordered-run",
                EnvironmentId = "environment-a",
                Sequence = 1,
                MonotonicTimestampTicks = 1
            }));
            Assert.Throws<InvalidOperationException>(() => store.RecordTargetCameraEvent(new TargetCameraEventRecord
            {
                RunId = "ordered-run",
                ConditionId = "condition-a",
                Sequence = 0,
                MonotonicTimestampTicks = -1
            }));
            store.Dispose();
        }

        [Test]
        public void DisposeWithoutCompletion_FinalizesRunAsInterrupted()
        {
            CalibrationRunStore store = CreateRunStore("disposed-run");
            string finalPath = store.FinalManifestPath;
            store.Dispose();

            CalibrationRunManifest final = JsonUtility.FromJson<CalibrationRunManifest>(
                File.ReadAllText(finalPath));
            Assert.That(final.Status, Is.EqualTo("interrupted"));
            Assert.That(final.Reason, Is.EqualTo("disposed-without-completion"));
        }

        [Test]
        public void RecoveryService_FinalizesAbandonedRunAndHashesPartialEvidence()
        {
            string runId = "abandoned-run";
            string runDirectory = Path.Combine(temporaryRoot, runId);
            Directory.CreateDirectory(runDirectory);
            CalibrationRunManifest started = CreateRunManifest(runId);
            string startedPath = Path.Combine(
                runDirectory,
                CalibrationIdFactory.BuildArtifactFileName(
                    started.ProtocolId,
                    started.EnvironmentId,
                    started.ConditionId,
                    started.RunId,
                    "run-started",
                    "json"));
            CalibrationFileSystem.WriteNewJson(startedPath, started);
            File.WriteAllText(Path.Combine(runDirectory, "partial-raw.jsonl"), "{\"partial\":true}");

            int recovered = CalibrationRecoveryService.RecoverAbandonedRuns(temporaryRoot);

            Assert.That(recovered, Is.EqualTo(1));
            string[] finalFiles = Directory.GetFiles(runDirectory, "*_run-final.json");
            string[] integrityFiles = Directory.GetFiles(runDirectory, "*_integrity-manifest.json");
            Assert.That(finalFiles, Has.Length.EqualTo(1));
            Assert.That(integrityFiles, Has.Length.EqualTo(1));
            CalibrationRunManifest final = JsonUtility.FromJson<CalibrationRunManifest>(
                File.ReadAllText(finalFiles[0]));
            Assert.That(final.Status, Is.EqualTo("interrupted"));
            Assert.That(final.Reason, Is.EqualTo("recovered-after-process-interruption"));
        }

        private CalibrationRunStore CreateRunStore(string runId)
        {
            CalibrationEnvironmentManifest environment = new CalibrationEnvironmentManifest
            {
                ProtocolId = "protocol-a",
                EnvironmentId = "environment-a",
                CapturedUtc = DateTime.UtcNow.ToString("o"),
                HarnessVersion = CalibrationHarnessMetadata.HarnessVersion,
                HarnessChecksum = "fixture-harness-sha256",
                Manual = new CalibrationManualEnvironment()
            };
            CalibrationCapturePlanManifest capturePlan = new CalibrationCapturePlanManifest
            {
                ProtocolId = environment.ProtocolId,
                CapturePlanId = "plan-a",
                EnvironmentId = environment.EnvironmentId,
                ConditionId = "condition-a",
                PlannedRepeatCount = 1,
                RepetitionOrdinal = 1,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            };
            return new CalibrationRunStore(
                temporaryRoot,
                environment,
                capturePlan,
                CreateRunManifest(runId));
        }

        private static CalibrationRunManifest CreateRunManifest(string runId)
        {
            return new CalibrationRunManifest
            {
                ProtocolId = "protocol-a",
                EnvironmentId = "environment-a",
                CapturePlanId = "plan-a",
                ConditionId = "condition-a",
                RunId = runId,
                TraceId = runId + "-mouse",
                HarnessVersion = CalibrationHarnessMetadata.HarnessVersion,
                HarnessChecksum = "fixture-harness-sha256",
                Status = "started",
                Reason = CalibrationHarnessMetadata.UnknownValue,
                StartedUtc = DateTime.UtcNow.ToString("o"),
                EndedUtc = CalibrationHarnessMetadata.UnknownValue,
                StopwatchFrequency = 1
            };
        }

        private static CalibrationManualEnvironment CreateKnownManualEnvironment()
        {
            const string Known = "fixture-known";
            return new CalibrationManualEnvironment
            {
                DisplayModel = Known,
                NativeResolution = Known,
                DisplayRefreshRate = Known,
                DisplayScaling = Known,
                VSyncState = Known,
                AdaptiveSyncState = Known,
                MouseManufacturer = Known,
                MouseModel = Known,
                MouseConnection = Known,
                MouseFirmware = Known,
                MouseDpi = Known,
                MouseDpiEvidenceSource = Known,
                ConfiguredPollingRate = Known,
                PollingRateEvidenceSource = Known,
                UsbPathOrHub = Known,
                MousePowerState = Known,
                PointerSpeed = Known,
                PointerAccelerationState = Known,
                MousepadDescription = Known,
                OperatorId = Known,
                DominantHand = Known,
                GripDescriptor = Known,
                MovementDescriptor = Known,
                PostureNotes = Known,
                WarmupProcedure = Known,
                PowerPlan = Known,
                BackgroundLoadPolicy = Known,
                ThermalPowerNotes = Known,
                NetworkOfflineState = Known
            };
        }
    }
}
