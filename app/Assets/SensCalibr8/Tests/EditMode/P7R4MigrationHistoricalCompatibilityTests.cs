using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Migrations;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P7R4MigrationHistoricalCompatibilityTests
    {
        private string directory;
        private string nativeLibrary;
        private FrozenCalibrationConfiguration configuration;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p7r4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            nativeLibrary = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void EverySupportedSchemaStateUpgradesToTheCurrentCatalogWithoutReplacingMigrationHistory()
        {
            int latestVersion = SchemaMigrationCatalog.All.Last().Version;
            for (int sourceVersion = 0; sourceVersion <= latestVersion; sourceVersion++)
            {
                string database = Path.Combine(directory, "schema-v" + sourceVersion + ".sqlite3");
                CreateDatabaseAtVersion(database, sourceVersion);
                new SqliteDatabaseBootstrapper().Initialize(database, configuration, nativeLibrary);

                using SqliteDatabaseConnection connection = Open(database);
                Assert.That(Scalar(connection, "PRAGMA user_version;"), Is.EqualTo(latestVersion), "source schema " + sourceVersion);
                Assert.That(Scalar(connection, "SELECT COUNT(*) FROM schema_migrations;"), Is.EqualTo(SchemaMigrationCatalog.All.Count), "source schema " + sourceVersion);
                Assert.That(Scalar(connection, "SELECT COUNT(*) FROM calibration_configs WHERE config_version='calibration_config_v1';"), Is.EqualTo(1), "source schema " + sourceVersion);
                Assert.That(TextScalar(connection, "PRAGMA integrity_check;"), Is.EqualTo("ok"), "source schema " + sourceVersion);
                Assert.That(Scalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;"), Is.Zero, "source schema " + sourceVersion);
            }
        }

        [Test]
        public void HistoricalScoreKeepsFormulaAndCalibrationLineageInAnalysisReportAndExportReads()
        {
            string database = InitializeCurrentDatabase("historical.sqlite3");
            var connections = new SqliteConnectionFactory(database, nativeLibrary);
            ProfileRecord profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "historical-profile", "2026-07-19", 1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm", 50d, 50d, 1d, "2026-07-19"));
            var protocol = new ProtocolRepository(connections);
            CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-19", null, null));
            ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-19"), new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
            ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value, cycle.Id.Value, candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-19", "2026-07-19"));
            long configurationId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
            using (SqliteDatabaseConnection connection = connections.Open())
                foreach (string mode in new[] { "flick_close", "flick_far", "tracking", "micro_correction" })
                    connection.Execute("INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag) VALUES(@profile,@battery,@config,'2026-07-19',@mode,1,0);", new Dictionary<string, object> { ["@profile"] = profile.Id.Value, ["@battery"] = battery.Id.Value, ["@config"] = configurationId, ["@mode"] = mode });
            new SensitivityTestRepository(connections).Create(new SensitivityTestRecord(null, profile.Id.Value, cycle.Id.Value, configurationId, battery.Id.Value, 280d, 46.384d, 77d, "{}", "A", configuration.FormulaVersion.Value, 1, 1));

            AnalysisProfileDataset analysis = new AnalysisDatasetService(new AnalysisReadRepository(connections)).ReadProfileDataset(profile.Id.Value);
            HtmlReportInput report = new HtmlReportInputService(new HtmlReportInputRepository(connections)).Read(profile.Id.Value);
            ProfileDataExportDataset export = new ProfileDataExportRepository(connections).Read(profile.Id.Value);

            Assert.That(analysis.AuthoritativeScores.Single().FormulaVersion, Is.EqualTo(configuration.FormulaVersion.Value));
            Assert.That(analysis.AuthoritativeScores.Single().CalibrationConfigurationVersion, Is.EqualTo(configuration.ConfigVersion.Value));
            Assert.That(report.Scores.Single().FormulaVersion, Is.EqualTo(configuration.FormulaVersion.Value));
            Assert.That(report.Scores.Single().ConfigurationVersion, Is.EqualTo(configuration.ConfigVersion.Value));
            ProfileDataExportTable scores = export.Tables.Single(table => table.Name == "sensitivity_tests");
            Assert.That(Convert.ToString(scores.Rows.Single()["formula_version"]), Is.EqualTo(configuration.FormulaVersion.Value));
            Assert.That(Convert.ToInt64(scores.Rows.Single()["calibration_config_id"]), Is.EqualTo(configurationId));
        }

        [Test]
        public void UnsupportedAndInconsistentMigrationMetadataIsRejectedWithoutRemovingExistingRows()
        {
            string database = InitializeCurrentDatabase("corrupt.sqlite3");
            var connections = new SqliteConnectionFactory(database, nativeLibrary);
            ProfileRecord profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "preserve-on-reject", "2026-07-19", 1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm", 50d, 50d, 1d, "2026-07-19"));
            using (SqliteDatabaseConnection connection = connections.Open())
            {
                connection.Execute("INSERT INTO schema_migrations(version,name,checksum,applied_utc) VALUES(999,'unsupported','unsupported','2026-07-19T00:00:00Z');");
                connection.ExecuteScript("PRAGMA user_version = 999;");
            }

            Assert.That(() => new SqliteDatabaseBootstrapper().Initialize(database, configuration, nativeLibrary), Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection preserved = connections.Open();
            Assert.That(Scalar(preserved, "SELECT COUNT(*) FROM profiles WHERE id=" + profile.Id.Value + ";"), Is.EqualTo(1));
            Assert.That(Scalar(preserved, "SELECT COUNT(*) FROM schema_migrations WHERE version=999;"), Is.EqualTo(1));
        }

        [Test]
        public void GappedMigrationHistoryIsRejectedWithoutRemovingExistingRows()
        {
            string database = InitializeCurrentDatabase("gapped.sqlite3");
            var connections = new SqliteConnectionFactory(database, nativeLibrary);
            ProfileRecord profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "preserve-gap", "2026-07-19", 1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm", 50d, 50d, 1d, "2026-07-19"));
            using (SqliteDatabaseConnection connection = connections.Open())
                connection.Execute("DELETE FROM schema_migrations WHERE version=4;");

            Assert.That(() => new SqliteDatabaseBootstrapper().Initialize(database, configuration, nativeLibrary), Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection preserved = connections.Open();
            Assert.That(Scalar(preserved, "SELECT COUNT(*) FROM profiles WHERE id=" + profile.Id.Value + ";"), Is.EqualTo(1));
            Assert.That(Scalar(preserved, "SELECT COUNT(*) FROM schema_migrations WHERE version=4;"), Is.Zero);
        }

        private string InitializeCurrentDatabase(string fileName)
        {
            string database = Path.Combine(directory, fileName);
            new SqliteDatabaseBootstrapper().Initialize(database, configuration, nativeLibrary);
            return database;
        }

        private void CreateDatabaseAtVersion(string database, int version)
        {
            using SqliteDatabaseConnection connection = Open(database);
            connection.ExecuteScript("CREATE TABLE schema_migrations (version INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE, checksum TEXT NOT NULL, applied_utc TEXT NOT NULL);");
            foreach (SchemaMigration migration in SchemaMigrationCatalog.All.Where(item => item.Version <= version))
            {
                connection.ExecuteScript("BEGIN IMMEDIATE;");
                try
                {
                    connection.ExecuteScript(migration.Sql);
                    connection.Execute("INSERT INTO schema_migrations(version,name,checksum,applied_utc) VALUES(@version,@name,@checksum,@utc);", new Dictionary<string, object> { ["@version"] = migration.Version, ["@name"] = migration.Name, ["@checksum"] = migration.Checksum, ["@utc"] = "2026-07-19T00:00:00Z" });
                    connection.ExecuteScript("COMMIT;");
                }
                catch
                {
                    connection.ExecuteScript("ROLLBACK;");
                    throw;
                }
            }
        }

        private SqliteDatabaseConnection Open(string database) => new SqliteConnectionFactory(database, nativeLibrary).Open();
        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));
        private static string TextScalar(SqliteDatabaseConnection connection, string sql) => Convert.ToString(connection.Scalar(sql));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
