using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P1R4RepositoryTransactionTests
    {
        private string tempDirectory;
        private string databasePath;
        private string nativeLibraryPath;
        private SqliteConnectionFactory connectionFactory;
        private RecordingFailureReporter failureReporter;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p1r4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            databasePath = Path.Combine(tempDirectory, "repositories.sqlite3");
            nativeLibraryPath = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            FrozenCalibrationConfiguration configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath);
            connectionFactory = new SqliteConnectionFactory(databasePath, nativeLibraryPath);
            failureReporter = new RecordingFailureReporter();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void RepositoriesPersistAndMapProfileCandidateSourcesAndBatteryProvenance()
        {
            ProfileRecord profile = CreateProfile(new ProfileRepository(connectionFactory, failureReporter), "repository-profile");
            var protocol = new ProtocolRepository(connectionFactory, failureReporter);
            CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
            ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(
                new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-16"),
                new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
            ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value, cycle.Id.Value, candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-16", null));

            Assert.That(new ProfileRepository(connectionFactory).FindById(profile.Id.Value).Name, Is.EqualTo("repository-profile"));
            Assert.That(candidate.Id, Is.Not.Null);
            Assert.That(battery.Id, Is.Not.Null);
            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources;"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_batteries;"), Is.EqualTo(1));
        }

        [Test]
        public void CompletedShotCaptureCommitsAsOneAggregate()
        {
            Fixture fixture = CreateProtocolFixture();
            long configId = new CalibrationConfigurationRepository(connectionFactory).RequireId("calibration_config_v1");
            long sessionId = new SessionCaptureRepository(connectionFactory, failureReporter).Persist(CreateCapture(fixture.Profile.Id.Value, fixture.Battery.Id.Value, configId, false));

            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(sessionId, Is.GreaterThan(0));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM sessions;"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM session_timing_diagnostics;"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM shots;"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM mouse_samples WHERE shot_id IS NOT NULL;"), Is.EqualTo(1));
        }

        [Test]
        public void FailedCaptureRollsBackAllRowsAndReportsPreservationRecovery()
        {
            Fixture fixture = CreateProtocolFixture();
            long configId = new CalibrationConfigurationRepository(connectionFactory).RequireId("calibration_config_v1");
            Assert.That(() => new SessionCaptureRepository(connectionFactory, failureReporter).Persist(CreateCapture(fixture.Profile.Id.Value, fixture.Battery.Id.Value, configId, true)), Throws.TypeOf<DataAccessException>());

            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM sessions;"), Is.EqualTo(0));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM session_timing_diagnostics;"), Is.EqualTo(0));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM shots;"), Is.EqualTo(0));
            Assert.That(failureReporter.Last.Operation, Is.EqualTo("persist completed session capture"));
            Assert.That(failureReporter.Last.FailureKind, Is.EqualTo(DataFailureKind.ConstraintViolation));
            Assert.That(failureReporter.Last.RecoveryAction, Is.EqualTo(DataRecoveryAction.PreserveInMemorySession));
        }

        [Test]
        public void CompletedTrackingCaptureCommitsTrialsAndWindowsInTheSameTransaction()
        {
            Fixture fixture = CreateProtocolFixture();
            long configId = new CalibrationConfigurationRepository(connectionFactory).RequireId("calibration_config_v1");
            var session = new SessionRecord(fixture.Profile.Id.Value, fixture.Battery.Id.Value, configId, "2026-07-16", "tracking", 1, null, false);
            var timing = new SessionTimingDiagnosticsRecord("sc8-signal-pipeline-v1", "test-mouse", 1000d, 1000d, 1d, 0d, 1d, 1d, 0, 0, 0, 1, 0, 0d, true, "test");
            var trial = new TrackingTrialCaptureRecord(0.175d, 1, 1, null, "linear", "small", "sc8-mode-contract-v1", "{}", 1, "[]", 1d, 1d);
            var window = new TrackingWindowCaptureRecord(0, 1, 0, 1, 1d, 1d, 0d);
            new SessionCaptureRepository(connectionFactory).Persist(new SessionCaptureRequest(session, timing, Array.Empty<ShotCaptureRecord>(), Array.Empty<MouseSampleCaptureRecord>(), new[] { trial }, new[] { window }));

            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM tracking_data;"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM tracking_windows;"), Is.EqualTo(1));
        }

        [Test]
        public void CandidateSourceFailureRollsBackTheParentCandidate()
        {
            ProfileRecord profile = CreateProfile(new ProfileRepository(connectionFactory), "rollback-candidate");
            var protocol = new ProtocolRepository(connectionFactory);
            CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
            var duplicateSources = new[]
            {
                new ProtocolCandidateSourceRecord(280d, 0d, 280d, false),
                new ProtocolCandidateSourceRecord(280d, 0d, 280d, false)
            };
            Assert.That(() => protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-16"), duplicateSources), Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidates;"), Is.EqualTo(0));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources;"), Is.EqualTo(0));
        }

        [Test]
        public void RawSqlMethodsAreInternalToTheDataAssembly()
        {
            Type type = typeof(SqliteDatabaseConnection);
            Assert.That(type.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(type.GetMethod("Query", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(type.GetMethod("ExecuteScript", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(type.GetMethod("Execute", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        }

        private Fixture CreateProtocolFixture()
        {
            ProfileRecord profile = CreateProfile(new ProfileRepository(connectionFactory), "session-profile-" + Guid.NewGuid().ToString("N"));
            var protocol = new ProtocolRepository(connectionFactory);
            CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
            ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-16"), new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
            ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value, cycle.Id.Value, candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-16", null));
            return new Fixture(profile, battery);
        }

        private static ProfileRecord CreateProfile(ProfileRepository repository, string name) => repository.Create(new ProfileRecord(null, name, "2026-07-16", 1600, 0.175d, 1000d, "right", "#FFFFFF", "claw", "wrist", 45d, 40d, 1d, "2026-07-16"));

        private static SessionCaptureRequest CreateCapture(long profileId, long batteryId, long configId, bool duplicateSample)
        {
            var session = new SessionRecord(profileId, batteryId, configId, "2026-07-16", "flick_close", 1, null, false);
            var timing = new SessionTimingDiagnosticsRecord("sc8-signal-pipeline-v1", "test-mouse", 1000d, 1000d, 1d, 0d, 1d, 1d, 0, 0, 0, 1, 0, 0d, true, "test");
            var shot = new ShotCaptureRecord(1, "close", "small", "0,0,0", 0d, null, 1d, 1d, "0,0,0", true, "hit", "0,0,0", null, null, 0.175d, 1d, null, 1, 0d, true);
            var samples = new List<MouseSampleCaptureRecord> { new MouseSampleCaptureRecord(1, 0d, 0d, 0d, 0d, 0d, 0) };
            if (duplicateSample) samples.Add(new MouseSampleCaptureRecord(1, 1d, 0d, 0d, 0d, 0d, 0));
            return new SessionCaptureRequest(session, timing, new[] { shot }, samples);
        }

        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private sealed class Fixture { public Fixture(ProfileRecord profile, ProtocolBatteryRecord battery) { Profile = profile; Battery = battery; } public ProfileRecord Profile { get; } public ProtocolBatteryRecord Battery { get; } }
        private sealed class RecordingFailureReporter : IDataFailureReporter { public DataAccessException Last { get; private set; } public void Report(DataAccessException failure) { Last = failure; } }
    }
}
