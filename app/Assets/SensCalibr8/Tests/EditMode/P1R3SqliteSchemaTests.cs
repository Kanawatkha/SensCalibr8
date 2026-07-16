using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P1R3SqliteSchemaTests
    {
        private string tempDirectory;
        private string databasePath;
        private string nativeLibraryPath;
        private FrozenCalibrationConfiguration configuration;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p1r3-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            databasePath = Path.Combine(tempDirectory, "schema-tests.sqlite3");
            nativeLibraryPath = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void MigrationCreatesTheCompleteTableSetAndVersionMetadata()
        {
            using SqliteDatabaseConnection connection = Open();
            var tables = new HashSet<string>(StringComparer.Ordinal);
            foreach (IReadOnlyDictionary<string, object> row in connection.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';"))
                tables.Add(Convert.ToString(row["name"]));
            CollectionAssert.AreEquivalent(new[]
            {
                "schema_migrations", "profiles", "calibration_configs", "cycles", "protocol_candidates",
                "protocol_candidate_sources", "protocol_batteries", "sessions", "session_timing_diagnostics", "shots",
                "tracking_data", "tracking_windows", "mouse_samples", "outlier_flags", "sensitivity_tests",
                "significance_tests", "significance_test_pairs", "phase_history", "injury_risk_flags", "application_state",
                "session_attempts", "session_sequence_audits"
            }, tables);
            Assert.That(Scalar(connection, "PRAGMA user_version;"), Is.EqualTo(3));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM schema_migrations WHERE version=1 AND name='initial_schema';"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM schema_migrations WHERE version=2 AND name='active_profile_state';"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM schema_migrations WHERE version=3 AND name='session_battery_lifecycle';"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND sql IS NOT NULL;"), Is.EqualTo(27));
        }

        [Test]
        public void EveryConnectionEnablesForeignKeyEnforcement()
        {
            using SqliteDatabaseConnection first = Open();
            using SqliteDatabaseConnection second = Open();
            Assert.That(Scalar(first, "PRAGMA foreign_keys;"), Is.EqualTo(1));
            Assert.That(Scalar(second, "PRAGMA foreign_keys;"), Is.EqualTo(1));
        }

        [Test]
        public void EveryDeclaredForeignKeyCascadesOnDelete()
        {
            using SqliteDatabaseConnection connection = Open();
            int foreignKeyCount = 0;
            foreach (string table in UserTables(connection))
            {
                foreach (IReadOnlyDictionary<string, object> row in connection.Query("PRAGMA foreign_key_list(" + table + ");"))
                {
                    foreignKeyCount++;
                    Assert.That(Convert.ToString(row["on_delete"]), Is.EqualTo("CASCADE"), table);
                }
            }
            Assert.That(foreignKeyCount, Is.EqualTo(41));
        }

        [Test]
        public void RequiredFieldsAndUniquenessAreEnforcedBySQLite()
        {
            using SqliteDatabaseConnection connection = Open();
            Assert.That(() => Execute(connection, "INSERT INTO profiles(name) VALUES (NULL);"), Throws.TypeOf<InvalidOperationException>());
            InsertProfile(connection, "unique-profile");
            Assert.That(() => InsertProfile(connection, "unique-profile"), Throws.TypeOf<InvalidOperationException>());

            int nonIdFields = 0;
            int requiredFields = 0;
            foreach (IReadOnlyDictionary<string, object> row in connection.Query("PRAGMA table_info(calibration_configs);"))
            {
                if (Convert.ToString(row["name"]) == "id") continue;
                nonIdFields++;
                if (Convert.ToInt32(row["notnull"]) == 1) requiredFields++;
            }
            Assert.That(nonIdFields, Is.EqualTo(20));
            Assert.That(requiredFields, Is.EqualTo(20));
        }

        [Test]
        public void ProfileDeletionCascadesToDependentRows()
        {
            using SqliteDatabaseConnection connection = Open();
            InsertProfile(connection, "cascade-profile");
            Execute(connection, "INSERT INTO cycles(profile_id, cycle_number, start_date) SELECT id, 1, '2026-07-16' FROM profiles WHERE name='cascade-profile';");
            Execute(connection, "DELETE FROM profiles WHERE name='cascade-profile';");
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM cycles;"), Is.EqualTo(0));
        }

        [Test]
        public void BootstrapSeedsAllTwentyAcceptedCalibrationFieldsAndIsIdempotent()
        {
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath);
            using SqliteDatabaseConnection connection = Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM calibration_configs;"), Is.EqualTo(1));
            Assert.That(TextScalar(connection, "SELECT config_version FROM calibration_configs;"), Is.EqualTo(configuration.Record.ConfigVersion));
            Assert.That(TextScalar(connection, "SELECT tracking_contract_json FROM calibration_configs;"), Is.EqualTo(configuration.Record.TrackingContractJson));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM schema_migrations;"), Is.EqualTo(3));
        }

        [Test]
        public void BootstrapRejectsStoredCalibrationDrift()
        {
            using (SqliteDatabaseConnection connection = Open())
                Execute(connection, "UPDATE calibration_configs SET timing_acceptance_policy='mutated';");
            Assert.That(() => new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath),
                Throws.TypeOf<DataAccessException>().With.Message.Contains("initialize"));
        }

        [Test]
        public void MigrationRunnerRejectsAppliedChecksumDrift()
        {
            using (SqliteDatabaseConnection connection = Open())
                Execute(connection, "UPDATE schema_migrations SET checksum='mutated';");
            Assert.That(() => new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath),
                Throws.TypeOf<DataAccessException>().With.Message.Contains("migrations"));
        }

        private SqliteDatabaseConnection Open() => new SqliteConnectionFactory(databasePath, nativeLibraryPath).Open();

        private static IReadOnlyList<string> UserTables(SqliteDatabaseConnection connection)
        {
            var tables = new List<string>();
            foreach (IReadOnlyDictionary<string, object> row in connection.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> 'schema_migrations';"))
                tables.Add(Convert.ToString(row["name"]));
            return tables;
        }

        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));

        private static string TextScalar(SqliteDatabaseConnection connection, string sql) => Convert.ToString(connection.Scalar(sql));

        private static void Execute(SqliteDatabaseConnection connection, string sql) => connection.ExecuteScript(sql);

        private static void InsertProfile(SqliteDatabaseConnection connection, string name)
        {
            connection.Execute(@"INSERT INTO profiles(name, created_date, mouse_dpi, current_sensitivity,
configured_polling_rate_hz, dominant_hand, crosshair_config, grip_style, movement_strategy,
mousepad_width_cm, mousepad_height_cm, ads_multiplier, last_active_date)
VALUES (@name, '2026-07-16', 1600, 0.175, 1000, 'right', '#FFFFFF', 'claw', 'wrist', 45, 40, 1, '2026-07-16');",
                new Dictionary<string, object> { ["@name"] = name });
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
