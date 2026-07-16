using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P3R6CoreEngineVerificationTests
    {
        private FrozenCalibrationConfiguration configuration;
        private FrozenSequenceContract sequenceContract;
        private ResearchConstants constants;

        [SetUp]
        public void SetUp()
        {
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            sequenceContract = FrozenSequenceContract.From(configuration);
            constants = ResearchConstantsLoader.LoadFromRepository(RepositoryRoot());
        }

        [Test]
        public void IdenticalRawInputReplayProducesIdenticalTraceAndTimingEvidence()
        {
            IReadOnlyList<CapturedMouseSample> first = CaptureTrace();
            IReadOnlyList<CapturedMouseSample> second = CaptureTrace();

            Assert.That(second, Has.Count.EqualTo(first.Count));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].SampleIndex, Is.EqualTo(first[index].SampleIndex));
                Assert.That(second[index].SessionTimestampSeconds, Is.EqualTo(first[index].SessionTimestampSeconds));
                Assert.That(second[index].Source.RawDeltaX, Is.EqualTo(first[index].Source.RawDeltaX));
                Assert.That(second[index].Source.RawDeltaY, Is.EqualTo(first[index].Source.RawDeltaY));
                Assert.That(second[index].CumulativeAzimuthDeg, Is.EqualTo(first[index].CumulativeAzimuthDeg));
                Assert.That(second[index].CumulativeElevationDeg, Is.EqualTo(first[index].CumulativeElevationDeg));
            }
        }

        [Test]
        public void SequenceReplayIsStableAcrossSensitivityCandidatesAndChangesWithMode()
        {
            var first = new DeterministicTargetSequencer(sequenceContract).Create(
                new SequenceSeedContext(11, 22, ProtocolPhase.PhaseOne, TestMode.FlickClose, 1));
            var replay = new DeterministicTargetSequencer(sequenceContract).Create(
                new SequenceSeedContext(11, 22, ProtocolPhase.PhaseOne, TestMode.FlickClose, 1));
            var otherMode = new DeterministicTargetSequencer(sequenceContract).Create(
                new SequenceSeedContext(11, 22, ProtocolPhase.PhaseOne, TestMode.FlickFar, 1));

            Assert.That(replay.Audit.SeedSha256, Is.EqualTo(first.Audit.SeedSha256));
            Assert.That(Signature(replay), Is.EqualTo(Signature(first)));
            Assert.That(otherMode.Audit.SeedSha256, Is.Not.EqualTo(first.Audit.SeedSha256));
            Assert.That(otherMode.Conditions, Is.Not.Empty);
        }

        [Test]
        public void FrozenGeometryAndLetterboxPolicyRemainResolutionInvariant()
        {
            FrozenArenaGeometry geometry = FrozenArenaGeometry.From(configuration);
            Assert.That(geometry.TargetDiameters, Is.Not.Empty);
            Assert.That(geometry.ArenaDimensions.x, Is.GreaterThan(0f));
            Assert.That(geometry.ArenaDimensions.y, Is.GreaterThan(0f));
            Assert.That(geometry.ArenaDimensions.z, Is.GreaterThan(0f));

            Rect native = LetterboxedViewport.Calculate(1920f, 1080f, geometry.ReferenceAspect);
            Rect scaled = LetterboxedViewport.Calculate(3840f, 2160f, geometry.ReferenceAspect);
            Assert.That(scaled.x, Is.EqualTo(native.x).Within(1e-6f));
            Assert.That(scaled.y, Is.EqualTo(native.y).Within(1e-6f));
            Assert.That(scaled.width, Is.EqualTo(native.width).Within(1e-6f));
            Assert.That(scaled.height, Is.EqualTo(native.height).Within(1e-6f));
        }

        [Test]
        public void CoreVerificationDoesNotIntroduceRenderFrameInputDependencies()
        {
            string root = Path.Combine(RepositoryRoot(), "app", "Assets", "SensCalibr8");
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.Contains("TestLogic", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            string source = string.Join("\n", files.Select(File.ReadAllText));
            Assert.That(source, Does.Not.Contain("Time.deltaTime"));
            Assert.That(source, Does.Not.Contain("Time.time"));
            Assert.That(source, Does.Not.Contain("Input.GetAxis"));
        }

        private IReadOnlyList<CapturedMouseSample> CaptureTrace()
        {
            var source = new FakeRawMouseSource();
            using var capture = new DeterministicInputCapture(source, new InputAngularConverter(0.175d, constants));
            capture.Start();
            source.Emit(new RawMouseInputEvent(1, 10d, 20d, 3d, -2d, "mouse-1"));
            source.Emit(new RawMouseInputEvent(2, 10.001d, 20.001d, -1d, 4d, "mouse-1"));
            source.Emit(new RawMouseInputEvent(3, 10.002d, 20.002d, 2d, 1d, "mouse-1"));
            return capture.Stop();
        }

        private static string Signature(DeterministicTargetSequence sequence) => string.Join("|", sequence.Conditions.Select(condition => string.Join(":",
            condition.TrialIndex, condition.BlockIndex, condition.Mode, condition.TargetSize, condition.Pattern,
            condition.CenterOffsetDeg, condition.CenterAzimuthDeg, condition.CenterElevationDeg, condition.CenterXpx,
            condition.CenterYpx, condition.ForeperiodMs)));

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
