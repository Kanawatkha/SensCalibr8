using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Migrations
{
    public sealed class SqliteMigrationRunner
    {
        public void ApplyAll(SqliteDatabaseConnection connection)
        {
            if (connection == null || !connection.IsOpen) throw new ArgumentException("An open SQLite connection is required.", nameof(connection));
            try
            {
                connection.ExecuteScript(@"CREATE TABLE IF NOT EXISTS schema_migrations (
version INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE, checksum TEXT NOT NULL, applied_utc TEXT NOT NULL);"
                );
                Dictionary<int, AppliedMigration> applied = ReadApplied(connection);
                ValidateAppliedState(connection, applied);
                foreach (SchemaMigration migration in SchemaMigrationCatalog.All)
                {
                    if (applied.TryGetValue(migration.Version, out AppliedMigration existing))
                    {
                        if (!string.Equals(existing.Name, migration.Name, StringComparison.Ordinal) || !string.Equals(existing.Checksum, migration.Checksum, StringComparison.Ordinal))
                            throw new InvalidDataException("Applied migration metadata does not match the immutable catalog: " + migration.Version);
                        continue;
                    }
                    Apply(connection, migration);
                }
            }
            catch (DataAccessException) { throw; }
            catch (Exception exception) { throw new DataAccessException("Failed to apply SQLite schema migrations.", exception); }
        }

        private static Dictionary<int, AppliedMigration> ReadApplied(SqliteDatabaseConnection connection)
        {
            var result = new Dictionary<int, AppliedMigration>();
            foreach (IReadOnlyDictionary<string, object> row in connection.Query("SELECT version, name, checksum FROM schema_migrations ORDER BY version;"))
                result.Add(Convert.ToInt32(row["version"]), new AppliedMigration(Convert.ToString(row["name"]), Convert.ToString(row["checksum"])));
            return result;
        }

        private static void ValidateAppliedState(SqliteDatabaseConnection connection, IReadOnlyDictionary<int, AppliedMigration> applied)
        {
            var catalog = SchemaMigrationCatalog.All.ToDictionary(migration => migration.Version);
            foreach (int version in applied.Keys)
                if (!catalog.ContainsKey(version))
                    throw new InvalidDataException("Database contains an unsupported schema migration version: " + version);

            int highestApplied = applied.Count == 0 ? 0 : applied.Keys.Max();
            int userVersion = Convert.ToInt32(connection.Scalar("PRAGMA user_version;"));
            if (userVersion != highestApplied)
                throw new InvalidDataException("SQLite user_version does not match the applied schema migration history.");

            for (int version = 1; version <= highestApplied; version++)
                if (!applied.ContainsKey(version) || !catalog.ContainsKey(version))
                    throw new InvalidDataException("Database schema migration history is incomplete at version: " + version);
        }

        private static void Apply(SqliteDatabaseConnection connection, SchemaMigration migration)
        {
            connection.ExecuteScript("BEGIN IMMEDIATE;");
            try
            {
                connection.ExecuteScript(migration.Sql);
                connection.Execute("INSERT INTO schema_migrations(version, name, checksum, applied_utc) VALUES (@version, @name, @checksum, @applied_utc);",
                    new Dictionary<string, object> { ["@version"] = migration.Version, ["@name"] = migration.Name, ["@checksum"] = migration.Checksum, ["@applied_utc"] = DateTime.UtcNow.ToString("O") });
                connection.ExecuteScript("COMMIT;");
            }
            catch
            {
                connection.ExecuteScript("ROLLBACK;");
                throw;
            }
        }

        private sealed class AppliedMigration
        {
            public AppliedMigration(string name, string checksum) { Name = name; Checksum = checksum; }
            public string Name { get; }
            public string Checksum { get; }
        }
    }
}
