using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class ActiveProfileStateRepository
    {
        private readonly RepositoryExecution execution;

        public ActiveProfileStateRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        { execution = new RepositoryExecution(connectionFactory, failureReporter); }

        public long? GetActiveProfileId()
        {
            return execution.Read("read active profile state", connection =>
            {
                object value = connection.Scalar("SELECT profile_id FROM application_state WHERE state_key='active_profile';");
                return value == null ? (long?)null : Convert.ToInt64(value);
            });
        }

        public void Select(long profileId, string updatedDate)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            if (string.IsNullOrWhiteSpace(updatedDate)) throw new ArgumentException("Updated date is required.", nameof(updatedDate));
            execution.Write("persist active profile state", connection =>
            {
                connection.Execute(@"INSERT INTO application_state(state_key,profile_id,updated_date)
VALUES ('active_profile',@profile_id,@updated_date)
ON CONFLICT(state_key) DO UPDATE SET profile_id=excluded.profile_id, updated_date=excluded.updated_date;",
                    new Dictionary<string, object> { ["@profile_id"] = profileId, ["@updated_date"] = updatedDate });
                return 0;
            });
        }

        public void Clear()
        {
            execution.Write("clear active profile state", connection =>
            {
                connection.Execute("DELETE FROM application_state WHERE state_key='active_profile';");
                return 0;
            });
        }
    }
}
