using System;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public interface IDataFailureReporter
    {
        void Report(DataAccessException failure);
    }

    public sealed class NullDataFailureReporter : IDataFailureReporter
    {
        public static NullDataFailureReporter Instance { get; } = new NullDataFailureReporter();
        private NullDataFailureReporter() { }
        public void Report(DataAccessException failure) { }
    }

    internal sealed class RepositoryExecution
    {
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly IDataFailureReporter failureReporter;

        internal RepositoryExecution(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.failureReporter = failureReporter ?? NullDataFailureReporter.Instance;
        }

        internal T Read<T>(string operation, Func<SqliteDatabaseConnection, T> action) => Execute(operation, action);
        internal T Write<T>(string operation, Func<SqliteDatabaseConnection, T> action) => Execute(operation, action);

        private T Execute<T>(string operation, Func<SqliteDatabaseConnection, T> action)
        {
            try
            {
                using SqliteDatabaseConnection connection = connectionFactory.Open();
                return action(connection);
            }
            catch (DataAccessException failure)
            {
                var contextual = new DataAccessException(operation, failure.FailureKind, failure);
                failureReporter.Report(contextual);
                throw contextual;
            }
            catch (Exception exception)
            {
                var failure = new DataAccessException(operation, exception);
                failureReporter.Report(failure);
                throw failure;
            }
        }
    }
}
