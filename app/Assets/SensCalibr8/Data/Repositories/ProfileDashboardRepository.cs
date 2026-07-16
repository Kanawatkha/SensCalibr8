using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class ProfileDashboardRecord
    {
        public ProfileDashboardRecord(long completedSessionCount, string latestSessionDate, string latestGrade)
        { CompletedSessionCount = completedSessionCount; LatestSessionDate = latestSessionDate; LatestGrade = latestGrade; }
        public long CompletedSessionCount { get; } public string LatestSessionDate { get; } public string LatestGrade { get; }
    }

    public sealed class ProfileDashboardRepository
    {
        private readonly RepositoryExecution execution;
        public ProfileDashboardRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        { execution = new RepositoryExecution(connectionFactory, failureReporter); }

        public ProfileDashboardRecord Get(long profileId)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            return execution.Read("read profile dashboard summary", connection =>
            {
                var parameters = new Dictionary<string, object> { ["@profile_id"] = profileId };
                long sessionCount = Convert.ToInt64(connection.Scalar("SELECT COUNT(*) FROM sessions WHERE profile_id=@profile_id;", parameters));
                object date = connection.Scalar("SELECT date FROM sessions WHERE profile_id=@profile_id ORDER BY id DESC LIMIT 1;", parameters);
                object grade = connection.Scalar("SELECT grade FROM sensitivity_tests WHERE profile_id=@profile_id ORDER BY id DESC LIMIT 1;", parameters);
                return new ProfileDashboardRecord(sessionCount, date == null ? null : Convert.ToString(date), grade == null ? null : Convert.ToString(grade));
            });
        }
    }
}
