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
    public sealed class P4R2FarFlickTests
    {
        private FrozenCalibrationConfiguration configuration;
        private FarFlickMode mode;

        [SetUp]
        public void SetUp()
        {
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            mode = new FarFlickMode(new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)), 1);
        }

        [Test]
        public void FrozenFarContractLoadsOnlyAcceptedTravelAndMovementOnsetValues()
        {
            FrozenFarFlickContract contract = FrozenFarFlickContract.From(configuration);
            Assert.That(contract.ModeContractVersion, Is.EqualTo("sc8-mode-contract-v1"));
            Assert.That(contract.ShotTimeoutSeconds, Is.EqualTo(1.5d));
            Assert.That(contract.MovementOnsetThresholdDegPerSec, Is.EqualTo(8d));
            Assert.That(contract.CenterHitRadiusRatio, Is.EqualTo(0.5d));
        }

        [Test]
        public void CompleteFarFlickLifecycleSeparatesActivationMovementOnsetAndTravelTime()
        {
            var machine = new TestEngineStateMachine(mode, Session(), configuration);
            machine.Prepare();
            machine.Start();
            for (int index = 0; index < mode.Sequence.Conditions.Count; index++)
            {
                double preview = index * 3d;
                machine.Capture(Event("preview_target_visible", preview));
                TargetCondition condition = mode.CurrentCondition;
                machine.Capture(Event("center_reference_activated", preview + 0.2d));
                machine.Capture(Event("movement_onset", preview + 0.4d, velocity: 8d));
                machine.Capture(Event("click", preview + 0.7d, isHit: index % 2 == 0,
                    azimuth: condition.CenterAzimuthDeg.Value + 1d, elevation: condition.CenterElevationDeg.Value));
            }
            machine.End();
            TestEngineReport report = machine.Report();

            Assert.That(report.Completion.IsComplete, Is.True);
            Assert.That(mode.ResolvedOpportunities, Has.Count.EqualTo(30));
            Assert.That(mode.ResolvedOpportunities[0].InitialOffsetDistanceDeg, Is.InRange(20d, 40d));
            Assert.That(mode.ResolvedOpportunities[0].TravelTimeSeconds, Is.EqualTo(0.3d).Within(1e-12));
            Assert.That(mode.ResolvedOpportunities[0].SignedOverflickUnderflickDeg, Is.EqualTo(1d));
            Assert.That(typeof(TestEngineReport).GetProperty("PerformanceScore"), Is.Null);
        }

        [Test]
        public void MissingMovementOnsetIsPreservedAsNullRatherThanFabricated()
        {
            mode.Prepare(Session(), configuration);
            mode.Start();
            mode.Capture(Event("preview_target_visible", 1d));
            TargetCondition condition = mode.CurrentCondition;
            mode.Capture(Event("center_reference_activated", 1.2d));
            mode.Capture(Event("click", 1.4d, isHit: false, azimuth: condition.CenterAzimuthDeg.Value,
                elevation: condition.CenterElevationDeg.Value));

            FarFlickCaptureEvidence evidence = mode.ResolvedOpportunities[0];
            Assert.That(evidence.MovementOnsetTimestampSeconds, Is.Null);
            Assert.That(evidence.TravelTimeSeconds, Is.Null);
            Assert.That(evidence.OutcomeReason, Is.EqualTo("miss_click"));
        }

        [Test]
        public void LowVelocityOnsetAndClickOutsideActivationContractAreRejected()
        {
            mode.Prepare(Session(), configuration);
            mode.Start();
            mode.Capture(Event("preview_target_visible", 0d));
            Assert.That(() => mode.Capture(Event("click", 0.1d, true, 0d, 0d)),
                Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("far_flick_not_activated"));
            mode.Capture(Event("center_reference_activated", 0.2d));
            Assert.That(() => mode.Capture(Event("movement_onset", 0.3d, velocity: 7.99d)),
                Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("far_flick_movement_onset_invalid"));
            double timeout = FrozenFarFlickContract.From(configuration).ShotTimeoutSeconds;
            Assert.That(() => mode.Capture(Event("click", 0.2d + timeout + 0.001d, true, 0d, 0d)),
                Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("far_flick_click_outside_timeout"));
        }

        [Test]
        public void EvidenceMapperPersistsPreviewActivationOnsetAndSignedError()
        {
            string directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p4r2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string database = Path.Combine(directory, "far-flick.sqlite3");
                string native = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
                new SqliteDatabaseBootstrapper().Initialize(database, configuration, native);
                var connections = new SqliteConnectionFactory(database, native);
                ProfileRecord profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "far-flick-profile", "2026-07-16", 1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm", 5000d, 5000d, 1d, "2026-07-16"));
                var protocol = new ProtocolRepository(connections);
                CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
                ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-16"), new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
                ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value, cycle.Id.Value, candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-16", null));
                ResolveOne();
                IReadOnlyList<ShotCaptureRecord> shots = FarFlickEvidencePersistenceMapper.ToShotCaptureRecords(mode.ResolvedOpportunities, 0.175d);
                long configId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
                new SessionCaptureRepository(connections).Persist(new SessionCaptureRequest(new SessionRecord(profile.Id.Value, battery.Id.Value, configId, "2026-07-16", "flick_far", 1, null, false), Timing(), shots, Array.Empty<MouseSampleCaptureRecord>()));

                using SqliteDatabaseConnection connection = connections.Open();
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT preview_timestamp FROM shots;")), Is.EqualTo(1d));
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT spawn_timestamp FROM shots;")), Is.EqualTo(1.2d));
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT first_mouse_movement_timestamp FROM shots;")), Is.EqualTo(1.4d));
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT signed_overflick_underflick_deg FROM shots;")), Is.EqualTo(-0.25d));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private void ResolveOne()
        {
            mode.Prepare(Session(), configuration);
            mode.Start();
            mode.Capture(Event("preview_target_visible", 1d));
            TargetCondition condition = mode.CurrentCondition;
            mode.Capture(Event("center_reference_activated", 1.2d));
            mode.Capture(Event("movement_onset", 1.4d, velocity: 8d));
            mode.Capture(Event("click", 1.7d, isHit: true, azimuth: condition.CenterAzimuthDeg.Value - 0.25d,
                elevation: condition.CenterElevationDeg.Value + 0.5d));
        }

        private static TestModeCaptureEvent Event(string type, double timestamp, bool? isHit = null,
            double? azimuth = null, double? elevation = null, double? velocity = null)
        {
            var values = new Dictionary<string, string>();
            if (isHit.HasValue) values.Add("is_hit", isHit.Value ? "true" : "false");
            if (azimuth.HasValue) values.Add("final_aim_azimuth_deg", azimuth.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            if (elevation.HasValue) values.Add("final_aim_elevation_deg", elevation.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            if (velocity.HasValue) values.Add("angular_velocity_deg_per_sec", velocity.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            return new TestModeCaptureEvent(type, timestamp, values);
        }

        private EngineSessionContext Session()
        {
            var cycle = new EngineCycleContext(1, 10, 1);
            var candidate = new EngineCandidateContext(1, 10, 20, ProtocolPhase.PhaseOne, 280d, 0.175d);
            var battery = new EngineBatteryContext(1, 10, 20, 30, ProtocolPhase.PhaseOne, 0.175d);
            return new EngineSessionContext("p4-r2-far", cycle, candidate, battery, TestMode.FlickFar, configuration.ConfigVersion);
        }

        private static SessionTimingDiagnosticsRecord Timing() => new SessionTimingDiagnosticsRecord("sc8-signal-pipeline-v1", "mouse", 1000d, 1000d, 1d, 0d, 1d, 1d, 0, 0, 0, 1, 0, 0d, true, "accepted");
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
