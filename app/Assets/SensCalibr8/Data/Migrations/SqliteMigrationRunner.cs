using System;
using System.Collections.Generic;
using System.IO;
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
