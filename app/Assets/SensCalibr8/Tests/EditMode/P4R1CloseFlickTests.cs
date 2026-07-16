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
    public sealed class P4R1CloseFlickTests
    {
        private FrozenCalibrationConfiguration configuration;
        private CloseFlickMode mode;

        [SetUp]
        public void SetUp()
        {
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            mode = new CloseFlickMode(new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)), 1);
        }

        [Test]
        public void FrozenCloseContractLoadsOnlyAcceptedTimingAndCenterHitValues()
        {
            FrozenCloseFlickContract contract = FrozenCloseFlickContract.From(configuration);
            Assert.That(contract.ModeContractVersion, Is.EqualTo("sc8-mode-contract-v1"));
            Assert.That(contract.ShotTimeoutSeconds, Is.EqualTo(1.5d));
            Assert.That(contract.CenterHitRadiusRatio, Is.EqualTo(0.5d));
            Assert.That(contract.TargetAngularDiameters["small"], Is.EqualTo(0.75d));
        }

        [Test]
        public void CompleteCloseFlickLifecycleUsesThirtyFrozenConditionsAndNoScoring()
        {
            var machine = new TestEngineStateMachine(mode, Session(), configuration);
            machine.Prepare();
            machine.Start();
            for (int index = 0; index < mode.Sequence.Conditions.Count; index++)
            {
                double visible = index * 2d;
                machine.Capture(Event("target_visible", visible));
                TargetCondition condition = mode.CurrentCondition;
                Assert.That(mode.CurrentForeperiodMs, Is.InRange(500d, 1000d));
                if (index == 0) machine.Capture(Event("first_mouse_movement", visible + 0.01d));
                machine.Capture(Event("click", visible + 0.2d, index % 2 == 0,
                    condition.CenterAzimuthDeg.Value + 1d, condition.CenterElevationDeg.Value));
            }
            machine.End();
            TestEngineReport report = machine.Report();

            Assert.That(report.Completion.IsComplete, Is.True);
            Assert.That(mode.ResolvedOpportunities, Has.Count.EqualTo(30));
            Assert.That(mode.ResolvedOpportunities[0].FirstMouseMovementTimestampSeconds, Is.EqualTo(0.01d));
            Assert.That(mode.ResolvedOpportunities[0].SignedOverflickUnderflickDeg, Is.EqualTo(1d));
            Assert.That(mode.ResolvedOpportunities[0].FinalPrecisionErrorDeg, Is.EqualTo(1d));
            Assert.That(typeof(TestEngineReport).GetProperty("PerformanceScore"), Is.Null);
        }

        [Test]
        public void TimeoutIsRecordedAsMissWithFinalAimEvidenceAndConfiguredLimit()
        {
            mode.Prepare(Session(), configuration);
            mode.Start();
            mode.Capture(Event("target_visible", 10d));
            TargetCondition condition = mode.CurrentCondition;
            double timeout = FrozenCloseFlickContract.From(configuration).ShotTimeoutSeconds;
            mode.Capture(Event("timeout", 10d + timeout, false,
                condition.CenterAzimuthDeg.Value, condition.CenterElevationDeg.Value));

            CloseFlickCaptureEvidence opportunity = mode.ResolvedOpportunities[0];
            Assert.That(opportunity.OutcomeReason, Is.EqualTo("timeout"));
            Assert.That(opportunity.IsHit, Is.False);
            Assert.That(opportunity.FinalPrecisionErrorDeg, Is.EqualTo(0d));
            Assert.That(opportunity.IsCenterHit, Is.True);
        }

        [Test]
        public void ClickAfterFrozenTimeoutAndMalformedEvidenceAreRejected()
        {
            mode.Prepare(Session(), configuration);
            mode.Start();
            mode.Capture(Event("target_visible", 0d));
            double timeout = FrozenCloseFlickContract.From(configuration).ShotTimeoutSeconds;
            Assert.That(() => mode.Capture(Event("click", timeout + 0.001d, true, 0d, 0d)),
                Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("close_flick_click_outside_timeout"));
            Assert.That(() => mode.Capture(new TestModeCaptureEvent("click", 0.1d)),
                Throws.TypeOf<TestEngineLifecycleException>().With.Property("ErrorCode").EqualTo("close_flick_metadata_is_hit_invalid"));
        }

        [Test]
        public void EvidenceMapperPreservesCloseMetricsAndRawSignedError()
        {
            mode.Prepare(Session(), configuration);
            mode.Start();
            mode.Capture(Event("target_visible", 1d));
            TargetCondition condition = mode.CurrentCondition;
            mode.Capture(Event("click", 1.2d, true, condition.CenterAzimuthDeg.Value - 0.25d,
                condition.CenterElevationDeg.Value + 0.5d));
            IReadOnlyList<SensCalibr8.Data.Repositories.ShotCaptureRecord> records =
                CloseFlickEvidencePersistenceMapper.ToShotCaptureRecords(mode.ResolvedOpportunities, 0.175d);

            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(records[0].DistanceZone, Is.EqualTo("close"));
            Assert.That(records[0].HitTimestamp, Is.EqualTo(1.2d));
            Assert.That(records[0].SignedOverflickUnderflickDeg, Is.EqualTo(-0.25d));
            Assert.That(records[0].FinalPrecisionError, Is.EqualTo(Math.Sqrt(0.3125d)).Within(1e-12));
            Assert.That(records[0].IsAdaptationShot, Is.Null);
        }

        [Test]
        public void MappedCloseEvidencePersistsItsRawSignedAimError()
        {
            string directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p4r1-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string database = Path.Combine(directory, "close-flick.sqlite3");
                string native = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
                new SqliteDatabaseBootstrapper().Initialize(database, configuration, native);
                var connections = new SqliteConnectionFactory(database, native);
                ProfileRecord profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "close-flick-profile", "2026-07-16", 1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm", 5000d, 5000d, 1d, "2026-07-16"));
                var protocol = new ProtocolRepository(connections);
                CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
                ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-16"), new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
                ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value, cycle.Id.Value, candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-16", null));
                ResolveOne(0.5d, -0.25d);
                IReadOnlyList<ShotCaptureRecord> shots = CloseFlickEvidencePersistenceMapper.ToShotCaptureRecords(mode.ResolvedOpportunities, 0.175d);
                long configId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
                new SessionCaptureRepository(connections).Persist(new SessionCaptureRequest(new SessionRecord(profile.Id.Value, battery.Id.Value, configId, "2026-07-16", "flick_close", 1, null, false), Timing(), shots, Array.Empty<MouseSampleCaptureRecord>()));

                using SqliteDatabaseConnection connection = connections.Open();
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT signed_overflick_underflick_deg FROM shots;")), Is.EqualTo(-0.25d));
                Assert.That(Convert.ToDouble(connection.Scalar("SELECT final_precision_error FROM shots;")), Is.EqualTo(Math.Sqrt(0.3125d)).Within(1e-12));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static TestModeCaptureEvent Event(string type, double timestamp, bool? isHit = null,
            double? azimuth = null, double? elevation = null)
        {
            var values = new Dictionary<string, string>();
            if (isHit.HasValue) values.Add("is_hit", isHit.Value ? "true" : "false");
            if (azimuth.HasValue) values.Add("final_aim_azimuth_deg", azimuth.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            if (elevation.HasValue) values.Add("final_aim_elevation_deg", elevation.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            return new TestModeCaptureEvent(type, timestamp, values);
        }

        private void ResolveOne(double elevationDelta, double azimuthDelta)
        {
            mode.Prepare(Session(), configuration);
            mode.Start();
            mode.Capture(Event("target_visible", 1d));
            TargetCondition condition = mode.CurrentCondition;
            mode.Capture(Event("click", 1.2d, true, condition.CenterAzimuthDeg.Value + azimuthDelta,
                condition.CenterElevationDeg.Value + elevationDelta));
        }

        private static SessionTimingDiagnosticsRecord Timing() => new SessionTimingDiagnosticsRecord(
            "sc8-signal-pipeline-v1", "mouse", 1000d, 1000d, 1d, 0d, 1d, 1d, 0, 0, 0, 1, 0, 0d, true, "accepted");

        private EngineSessionContext Session()
        {
            var cycle = new EngineCycleContext(1, 10, 1);
            var candidate = new EngineCandidateContext(1, 10, 20, ProtocolPhase.PhaseOne, 280d, 0.175d);
            var battery = new EngineBatteryContext(1, 10, 20, 30, ProtocolPhase.PhaseOne, 0.175d);
            return new EngineSessionContext("p4-r1-close", cycle, candidate, battery, TestMode.FlickClose, configuration.ConfigVersion);
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
