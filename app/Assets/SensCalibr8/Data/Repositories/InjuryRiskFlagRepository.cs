using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class InjuryRiskFlagRepository
    {
        private readonly RepositoryExecution execution;

        public InjuryRiskFlagRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        { execution = new RepositoryExecution(connectionFactory, failureReporter); }

        public IReadOnlyList<InjuryRiskFlagRecord> ListForProfile(long profileId)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            return execution.Read("list injury risk flags", connection =>
            {
                var result = new List<InjuryRiskFlagRecord>();
                foreach (IReadOnlyDictionary<string, object> row in connection.Query("SELECT * FROM injury_risk_flags WHERE profile_id=@profile_id ORDER BY id;", new Dictionary<string, object> { ["@profile_id"] = profileId })) result.Add(Map(row));
                return result;
            });
        }

        public bool ExistsUnacknowledged(long profileId, string flagType, double edpiAtTrigger)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            if (string.IsNullOrWhiteSpace(flagType)) throw new ArgumentException("Flag type is required.", nameof(flagType));
            return execution.Read("find active injury risk flag", connection => Convert.ToInt64(connection.Scalar(@"SELECT COUNT(*) FROM injury_risk_flags
WHERE profile_id=@profile_id AND flag_type=@flag_type AND edpi_at_trigger=@edpi_at_trigger AND acknowledged=0;",
                new Dictionary<string, object> { ["@profile_id"] = profileId, ["@flag_type"] = flagType, ["@edpi_at_trigger"] = edpiAtTrigger })) > 0);
        }

        public InjuryRiskFlagRecord Create(InjuryRiskFlagRecord flag)
        {
            if (flag == null) throw new ArgumentNullException(nameof(flag));
            return execution.Write("create injury risk flag", connection =>
            {
                connection.Execute(@"INSERT INTO injury_risk_flags(profile_id,flag_type,triggered_date,edpi_at_trigger,acknowledged)
VALUES (@profile_id,@flag_type,@triggered_date,@edpi_at_trigger,@acknowledged);", Parameters(flag));
                return new InjuryRiskFlagRecord(connection.LastInsertRowId(), flag.ProfileId, flag.FlagType, flag.TriggeredDate, flag.EdpiAtTrigger, flag.Acknowledged);
            });
        }

        public bool Acknowledge(long profileId, long flagId)
        {
            if (profileId <= 0 || flagId <= 0) throw new ArgumentOutOfRangeException(profileId <= 0 ? nameof(profileId) : nameof(flagId));
            return execution.Write("acknowledge injury risk flag", connection => connection.Execute(@"UPDATE injury_risk_flags SET acknowledged=1
WHERE id=@id AND profile_id=@profile_id AND acknowledged=0;", new Dictionary<string, object> { ["@id"] = flagId, ["@profile_id"] = profileId }) == 1);
        }

        private static IReadOnlyDictionary<string, object> Parameters(InjuryRiskFlagRecord value) => new Dictionary<string, object>
        {
            ["@profile_id"] = value.ProfileId, ["@flag_type"] = value.FlagType, ["@triggered_date"] = value.TriggeredDate,
            ["@edpi_at_trigger"] = value.EdpiAtTrigger, ["@acknowledged"] = value.Acknowledged
        };

        private static InjuryRiskFlagRecord Map(IReadOnlyDictionary<string, object> row) => new InjuryRiskFlagRecord(Convert.ToInt64(row["id"]),
            Convert.ToInt64(row["profile_id"]), Convert.ToString(row["flag_type"]), Convert.ToString(row["triggered_date"]),
            Convert.ToDouble(row["edpi_at_trigger"]), Convert.ToInt64(row["acknowledged"]) != 0);
    }
}
