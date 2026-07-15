using System;
using System.IO;
using UnityEngine;

namespace SensCalibr8.Calibration
{
    public sealed class CalibrationRunStore : IDisposable
    {
        private readonly CalibrationRunManifest startedManifest;
        private readonly AppendOnlyJsonLinesWriter mouseWriter;
        private readonly AppendOnlyJsonLinesWriter frameWriter;
        private readonly AppendOnlyJsonLinesWriter targetCameraWriter;
        private long nextMouseSequence;
        private long nextFrameSequence;
        private long nextTargetCameraSequence;
        private long lastMouseTimestampTicks = -1;
        private long lastFrameTimestampTicks = -1;
        private long lastTargetCameraTimestampTicks = -1;
        private bool writersDisposed;
        private bool finalized;

        public CalibrationRunStore(
            string artifactRoot,
            CalibrationEnvironmentManifest environment,
            CalibrationCapturePlanManifest capturePlan,
            CalibrationRunManifest runManifest)
        {
            if (environment == null)
            {
                throw new ArgumentNullException("environment");
            }

            if (capturePlan == null)
            {
                throw new ArgumentNullException("capturePlan");
            }

            if (runManifest == null)
            {
                throw new ArgumentNullException("runManifest");
            }

            ValidateRelationships(environment, capturePlan, runManifest);
            startedManifest = runManifest;
            RunDirectory = Path.Combine(artifactRoot, CalibrationIdFactory.NormalizeToken(runManifest.RunId));
            Directory.CreateDirectory(RunDirectory);

            EnvironmentManifestPath = ArtifactPath("environment-manifest", "json");
            CapturePlanManifestPath = ArtifactPath("capture-plan", "json");
            StartedManifestPath = ArtifactPath("run-started", "json");
            FinalManifestPath = ArtifactPath("run-final", "json");
            RawMouseEventsPath = ArtifactPath("raw-mouse-events", "jsonl");
            FrameTimingPath = ArtifactPath("frame-timing", "jsonl");
            TargetCameraEventsPath = ArtifactPath("target-camera-events", "jsonl");
            IntegrityManifestPath = ArtifactPath("integrity-manifest", "json");

            CalibrationFileSystem.WriteNewJson(EnvironmentManifestPath, environment);
            CalibrationFileSystem.WriteNewJson(CapturePlanManifestPath, capturePlan);
            CalibrationFileSystem.WriteNewJson(StartedManifestPath, runManifest);

            mouseWriter = new AppendOnlyJsonLinesWriter(RawMouseEventsPath);
            frameWriter = new AppendOnlyJsonLinesWriter(FrameTimingPath);
            targetCameraWriter = new AppendOnlyJsonLinesWriter(TargetCameraEventsPath);
        }

        public string RunDirectory { get; private set; }

        public string EnvironmentManifestPath { get; private set; }

        public string CapturePlanManifestPath { get; private set; }

        public string StartedManifestPath { get; private set; }

        public string FinalManifestPath { get; private set; }

        public string RawMouseEventsPath { get; private set; }

        public string FrameTimingPath { get; private set; }

        public string TargetCameraEventsPath { get; private set; }

        public string IntegrityManifestPath { get; private set; }

        public void RecordMouseEvent(RawMouseEventRecord record)
        {
            EnsureOpen();
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            ValidateRecordIdentity(record.RunId, record.TraceId, startedManifest.TraceId, "mouse trace");
            ValidateSequenceAndTimestamp(
                record.Sequence,
                nextMouseSequence,
                record.MonotonicTimestampTicks,
                lastMouseTimestampTicks,
                "mouse trace");
            mouseWriter.Append(JsonUtility.ToJson(record));
            nextMouseSequence++;
            lastMouseTimestampTicks = record.MonotonicTimestampTicks;
        }

        public void RecordFrameTiming(FrameTimingRecord record)
        {
            EnsureOpen();
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            ValidateRecordIdentity(record.RunId, record.EnvironmentId, startedManifest.EnvironmentId, "frame trace");
            ValidateSequenceAndTimestamp(
                record.Sequence,
                nextFrameSequence,
                record.MonotonicTimestampTicks,
                lastFrameTimestampTicks,
                "frame trace");
            frameWriter.Append(JsonUtility.ToJson(record));
            nextFrameSequence++;
            lastFrameTimestampTicks = record.MonotonicTimestampTicks;
        }

        public void RecordTargetCameraEvent(TargetCameraEventRecord record)
        {
            EnsureOpen();
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            ValidateRecordIdentity(record.RunId, record.ConditionId, startedManifest.ConditionId, "target/camera trace");
            ValidateSequenceAndTimestamp(
                record.Sequence,
                nextTargetCameraSequence,
                record.MonotonicTimestampTicks,
                lastTargetCameraTimestampTicks,
                "target/camera trace");
            targetCameraWriter.Append(JsonUtility.ToJson(record));
            nextTargetCameraSequence++;
            lastTargetCameraTimestampTicks = record.MonotonicTimestampTicks;
        }

        public IntegrityManifest Complete(string status, string reason)
        {
            if (finalized)
            {
                throw new InvalidOperationException("The calibration run has already been finalized.");
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("A final run status is required.", "status");
            }

            DisposeWriters();

            CalibrationRunManifest finalManifest = new CalibrationRunManifest
            {
                ProtocolId = startedManifest.ProtocolId,
                EnvironmentId = startedManifest.EnvironmentId,
                CapturePlanId = startedManifest.CapturePlanId,
                ConditionId = startedManifest.ConditionId,
                RunId = startedManifest.RunId,
                TraceId = startedManifest.TraceId,
                HarnessVersion = startedManifest.HarnessVersion,
                HarnessChecksum = startedManifest.HarnessChecksum,
                Status = status,
                Reason = NormalizeReason(reason),
                StartedUtc = startedManifest.StartedUtc,
                EndedUtc = DateTime.UtcNow.ToString("o"),
                StopwatchFrequency = startedManifest.StopwatchFrequency
            };

            CalibrationFileSystem.WriteNewJson(FinalManifestPath, finalManifest);
            IntegrityManifest integrity = CalibrationFileSystem.CreateIntegrityManifest(
                RunDirectory,
                IntegrityManifestPath,
                finalManifest);
            finalized = true;
            return integrity;
        }

        public IntegrityManifest Interrupt(string reason)
        {
            return Complete("interrupted", reason);
        }

        public void Dispose()
        {
            if (!finalized)
            {
                Interrupt("disposed-without-completion");
            }
        }

        private string ArtifactPath(string artifactType, string extension)
        {
            string fileName = CalibrationIdFactory.BuildArtifactFileName(
                startedManifest.ProtocolId,
                startedManifest.EnvironmentId,
                startedManifest.ConditionId,
                startedManifest.RunId,
                artifactType,
                extension);
            return Path.Combine(RunDirectory, fileName);
        }

        private void DisposeWriters()
        {
            if (writersDisposed)
            {
                return;
            }

            mouseWriter.Dispose();
            frameWriter.Dispose();
            targetCameraWriter.Dispose();
            writersDisposed = true;
        }

        private void EnsureOpen()
        {
            if (writersDisposed || finalized)
            {
                throw new InvalidOperationException("Cannot append to a finalized calibration run.");
            }
        }

        private static string NormalizeReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? CalibrationHarnessMetadata.UnknownValue : reason.Trim();
        }

        private void ValidateRecordIdentity(
            string recordRunId,
            string recordRelationshipId,
            string expectedRelationshipId,
            string streamName)
        {
            if (!string.Equals(recordRunId, startedManifest.RunId, StringComparison.Ordinal) ||
                !string.Equals(recordRelationshipId, expectedRelationshipId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The " + streamName + " record does not match the active run relationships.");
            }
        }

        private static void ValidateSequenceAndTimestamp(
            long sequence,
            long expectedSequence,
            long timestampTicks,
            long previousTimestampTicks,
            string streamName)
        {
            if (sequence != expectedSequence)
            {
                throw new InvalidOperationException("The " + streamName + " sequence is not contiguous.");
            }

            if (timestampTicks < 0 || timestampTicks < previousTimestampTicks)
            {
                throw new InvalidOperationException("The " + streamName + " timestamp is not monotonic.");
            }
        }

        private static void ValidateRelationships(
            CalibrationEnvironmentManifest environment,
            CalibrationCapturePlanManifest capturePlan,
            CalibrationRunManifest runManifest)
        {
            CalibrationIdFactory.NormalizeToken(runManifest.TraceId);

            if (!string.Equals(environment.HarnessVersion, runManifest.HarnessVersion, StringComparison.Ordinal) ||
                !string.Equals(environment.HarnessChecksum, runManifest.HarnessChecksum, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(runManifest.HarnessChecksum))
            {
                throw new InvalidOperationException("Harness version/checksum does not match the environment manifest.");
            }

            if (!string.Equals(environment.ProtocolId, capturePlan.ProtocolId, StringComparison.Ordinal) ||
                !string.Equals(environment.ProtocolId, runManifest.ProtocolId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Protocol IDs do not match across calibration manifests.");
            }

            if (!string.Equals(environment.EnvironmentId, capturePlan.EnvironmentId, StringComparison.Ordinal) ||
                !string.Equals(environment.EnvironmentId, runManifest.EnvironmentId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Environment IDs do not match across calibration manifests.");
            }

            if (!string.Equals(capturePlan.CapturePlanId, runManifest.CapturePlanId, StringComparison.Ordinal) ||
                !string.Equals(capturePlan.ConditionId, runManifest.ConditionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Capture-plan relationships do not match the run manifest.");
            }

            if (capturePlan.PlannedRepeatCount <= 0 || capturePlan.RepetitionOrdinal <= 0 ||
                capturePlan.RepetitionOrdinal > capturePlan.PlannedRepeatCount)
            {
                throw new InvalidOperationException("The capture plan must contain a valid repetition contract.");
            }
        }
    }

    public static class CalibrationRecoveryService
    {
        public static int RecoverAbandonedRuns(string artifactRoot)
        {
            if (!Directory.Exists(artifactRoot))
            {
                return 0;
            }

            int recoveredCount = 0;
            string[] runDirectories = Directory.GetDirectories(artifactRoot);
            for (int index = 0; index < runDirectories.Length; index++)
            {
                string runDirectory = runDirectories[index];
                string[] startedFiles = Directory.GetFiles(runDirectory, "*_run-started.json");
                string[] finalFiles = Directory.GetFiles(runDirectory, "*_run-final.json");
                if (startedFiles.Length != 1 || finalFiles.Length != 0)
                {
                    continue;
                }

                CalibrationRunManifest started = JsonUtility.FromJson<CalibrationRunManifest>(
                    File.ReadAllText(startedFiles[0]));
                string finalPath = Path.Combine(
                    runDirectory,
                    CalibrationIdFactory.BuildArtifactFileName(
                        started.ProtocolId,
                        started.EnvironmentId,
                        started.ConditionId,
                        started.RunId,
                        "run-final",
                        "json"));
                CalibrationRunManifest final = new CalibrationRunManifest
                {
                    ProtocolId = started.ProtocolId,
                    EnvironmentId = started.EnvironmentId,
                    CapturePlanId = started.CapturePlanId,
                    ConditionId = started.ConditionId,
                    RunId = started.RunId,
                    TraceId = started.TraceId,
                    HarnessVersion = started.HarnessVersion,
                    HarnessChecksum = started.HarnessChecksum,
                    Status = "interrupted",
                    Reason = "recovered-after-process-interruption",
                    StartedUtc = started.StartedUtc,
                    EndedUtc = DateTime.UtcNow.ToString("o"),
                    StopwatchFrequency = started.StopwatchFrequency
                };
                CalibrationFileSystem.WriteNewJson(finalPath, final);

                string integrityPath = Path.Combine(
                    runDirectory,
                    CalibrationIdFactory.BuildArtifactFileName(
                        started.ProtocolId,
                        started.EnvironmentId,
                        started.ConditionId,
                        started.RunId,
                        "integrity-manifest",
                        "json"));
                CalibrationFileSystem.CreateIntegrityManifest(
                    runDirectory,
                    integrityPath,
                    final);
                recoveredCount++;
            }

            return recoveredCount;
        }
    }
}
