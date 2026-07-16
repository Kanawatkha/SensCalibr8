using System;

namespace SensCalibr8.Data.Persistence
{
    internal sealed class SqliteTransaction : IDisposable
    {
        private readonly SqliteDatabaseConnection connection;
        private bool completed;

        internal SqliteTransaction(SqliteDatabaseConnection connection)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        internal void Commit()
        {
            EnsureActive();
            connection.ExecuteScript("COMMIT;");
            completed = true;
        }

        public void Dispose()
        {
            if (completed) return;
            try { connection.ExecuteScript("ROLLBACK;"); }
            catch { }
            completed = true;
        }

        private void EnsureActive()
        {
            if (completed) throw new InvalidOperationException("The SQLite transaction has already completed.");
        }
    }
}
