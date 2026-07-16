using System;
using System.IO;

namespace SensCalibr8.Data.Persistence
{
    public sealed class SqliteConnectionFactory
    {
        private readonly string databasePath;
        private readonly string nativeLibraryPath;

        public SqliteConnectionFactory(string databasePath, string nativeLibraryPath = null)
        {
            if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("Database path is required.", nameof(databasePath));
            this.databasePath = Path.GetFullPath(databasePath);
            this.nativeLibraryPath = nativeLibraryPath;
        }

        public SqliteDatabaseConnection Open()
        {
            try
            {
                string directory = Path.GetDirectoryName(databasePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                SqliteNativeRuntime.EnsureLoaded(nativeLibraryPath);
                var connection = new SqliteDatabaseConnection(databasePath);
                connection.Open();
                connection.ExecuteScript("PRAGMA foreign_keys = ON;");
                if (Convert.ToInt32(connection.Scalar("PRAGMA foreign_keys;")) != 1)
                {
                    connection.Dispose();
                    throw new InvalidOperationException("SQLite foreign-key enforcement could not be enabled.");
                }
                return connection;
            }
            catch (DataAccessException) { throw; }
            catch (Exception exception)
            {
                throw new DataAccessException("Failed to open an enforced SQLite connection.", exception);
            }
        }
    }
}
