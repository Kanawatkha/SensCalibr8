using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P1R5DataIntegrityTests
    {
        private string tempDirectory;
        private string databasePath;
        private string nativeLibraryPath;
        private SqliteConnectionFactory connectionFactory;
        private Fixture fixture;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p1r5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            databasePath = Path.Combine(tempDirectory, "integrity.sqlite3");
            nativeLibraryPath = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            FrozenCalibrationConfiguration configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath);
            connectionFactory = new SqliteConnectionFactory(databasePath, nativeLibraryPath);
            fixture = CreateProtocolFixture();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void OneModeCannotBePersistedTwiceInOneBattery()
        {
            long configId = new CalibrationConfigurationRepository(connectionFactory).RequireId("calibration_config_v1");
            SessionCaptureRequest request = CreateCapture(fixture.Profile.Id.Value, fixture.Battery.Id.Value, configId);
            var repository = new SessionCaptureRepository(connectionFactory);
            repository.Persist(request);
            Assert.That(() => repository.Persist(request), Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM sessions;"), Is.EqualTo(1));
        }

        [Test]
        public void ProfileDeletionCascadesTheCompletePersistedAggregate()
        {
            long configId = new CalibrationConfigurationRepository(connectionFactory).RequireId("calibration_config_v1");
            new SessionCaptureRepository(connectionFactory).Persist(CreateCapture(fixture.Profile.Id.Value, fixture.Battery.Id.Value, configId));
            using (SqliteDatabaseConnection connection = connectionFactory.Open())
            {
                connection.ExecuteScript("DELETE FROM profiles WHERE id=" + fixture.Profile.Id.Value + ";");
                foreach (string table in new[] { "cycles", "protocol_candidates", "protocol_candidate_sources", "protocol_batteries", "sessions", "session_timing_diagnostics", "shots", "mouse_samples" })
                    Assert.That(Scalar(connection, "SELECT COUNT(*) FROM " + table + ";"), Is.EqualTo(0), table);
            }
        }

        [Test]
        public void FormulaAndCalibrationVersionsRemainAttachedToHistoricalResult()
        {
            long configId = new CalibrationConfigurationRepository(connectionFactory).RequireId("calibration_config_v1");
            using SqliteDatabaseConnection connection = connectionFactory.Open();
            connection.Execute(@"INSERT INTO sensitivity_tests(profile_id,cycle_id,calibration_config_id,edpi,cm_360,avg_performance_score,performance_score_by_mode,grade,formula_version,phase,sample_size)
VALUES (@profile_id,@cycle_id,@config_id,280,30,'77','{}','A','sc8-performance-score-v1',1,1);", new Dictionary<string, object> { ["@profile_id"] = fixture.Profile.Id.Value, ["@cycle_id"] = fixture.Cycle.Id.Value, ["@config_id"] = configId });
            Assert.That(TextScalar(connection, "SELECT formula_version FROM sensitivity_tests;"), Is.EqualTo("sc8-performance-score-v1"));
            Assert.That(Scalar(connection, "SELECT calibration_config_id FROM sensitivity_tests;"), Is.EqualTo(configId));
            Assert.That(TextScalar(connection, "SELECT config_version FROM calibration_configs WHERE id=" + configId + ";"), Is.EqualTo("calibration_config_v1"));
        }

        [Test]
        public void InvalidDatabaseLocationIsClassifiedAsRetryableUnavailableFailure()
        {
            string invalidLocation = Path.Combine(tempDirectory, "existing-directory");
            Directory.CreateDirectory(invalidLocation);
            var reporter = new RecordingFailureReporter();
            Assert.That(() => new ProfileRepository(new SqliteConnectionFactory(invalidLocation, nativeLibraryPath), reporter).List(), Throws.TypeOf<DataAccessException>());
            Assert.That(reporter.Last.FailureKind, Is.EqualTo(DataFailureKind.Unavailable));
            Assert.That(reporter.Last.RecoveryAction, Is.EqualTo(DataRecoveryAction.Retry));
        }

        [Test]
        public void ForeignKeyViolationIsRejectedWithoutPartialChildData()
        {
            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(() => connection.Execute("INSERT INTO cycles(profile_id,cycle_number,start_date) VALUES (999999,1,'2026-07-16');"), Throws.TypeOf<InvalidOperationException>());
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM cycles;"), Is.EqualTo(1));
        }

        [Test]
        public void SQLiteIntegrityAndForeignKeyChecksAreCleanAfterRepositoryWrites()
        {
            long configId = new CalibrationConfigurationRepository(connectionFactory).RequireId("calibration_config_v1");
            new SessionCaptureRepository(connectionFactory).Persist(CreateCapture(fixture.Profile.Id.Value, fixture.Battery.Id.Value, configId));
            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(TextScalar(connection, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;"), Is.EqualTo(0));
        }

        private Fixture CreateProtocolFixture()
        {
            var profiles = new ProfileRepository(connectionFactory);
            ProfileRecord profile = profiles.Create(new ProfileRecord(null, "integrity-" + Guid.NewGuid().ToString("N"), "2026-07-16", 1600, 0.175d, 1000d, "right", "#FFFFFF", "claw", "wrist", 45d, 40d, 1d, "2026-07-16"));
            var protocol = new ProtocolRepository(connectionFactory);
            CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
            ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-16"), new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
            ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value, cycle.Id.Value, candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-16", null));
            return new Fixture(profile, cycle, battery);
        }

        private static SessionCaptureRequest CreateCapture(long profileId, long batteryId, long configId)
        {
            var session = new SessionRecord(profileId, batteryId, configId, "2026-07-16", "flick_close", 1, null, false);
            var timing = new SessionTimingDiagnosticsRecord("sc8-signal-pipeline-v1", "integrity-test", 1000d, 1000d, 1d, 0d, 1d, 1d, 0, 0, 0, 1, 0, 0d, true, "test");
            var shot = new ShotCaptureRecord(1, "close", "small", "0,0,0", 0d, null, 1d, 1d, "0,0,0", true, "hit", "0,0,0", null, null, 0.175d, 1d, null, 1, 0d, true);
            return new SessionCaptureRequest(session, timing, new[] { shot }, new[] { new MouseSampleCaptureRecord(1, 0d, 0d, 0d, 0d, 0d, 0) });
        }

        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));
        private static string TextScalar(SqliteDatabaseConnection connection, string sql) => Convert.ToString(connection.Scalar(sql));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private sealed class Fixture { public Fixture(ProfileRecord profile, CycleRecord cycle, ProtocolBatteryRecord battery) { Profile = profile; Cycle = cycle; Battery = battery; } public ProfileRecord Profile { get; } public CycleRecord Cycle { get; } public ProtocolBatteryRecord Battery { get; } }
        private sealed class RecordingFailureReporter : IDataFailureReporter { public DataAccessException Last { get; private set; } public void Report(DataAccessException failure) { Last = failure; } }
    }
}
