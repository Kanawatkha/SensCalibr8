using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class AnalysisReadRepository
    {
        private readonly RepositoryExecution execution;

        public AnalysisReadRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        {
            execution = new RepositoryExecution(connectionFactory, failureReporter);
        }

        public AnalysisProfileDataset LoadProfileDataset(long profileId)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            return execution.Read("read versioned analysis dataset", connection =>
            {
                AnalysisProfileIdentity profile = RequireProfile(connection, profileId);
                RequireFinalizedEvidence(connection, profileId);
                return new AnalysisProfileDataset(profile, ReadAuthoritativeScores(connection, profileId),
                    ReadSessions(connection, profileId), ReadOutlierAggregates(connection, profileId));
            });
        }

        private static AnalysisProfileIdentity RequireProfile(SqliteDatabaseConnection connection, long profileId)
        {
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(
                "SELECT id,name,mouse_dpi FROM profiles WHERE id=@profile_id;",
                new Dictionary<string, object> { ["@profile_id"] = profileId });
            if (rows.Count != 1) throw new InvalidOperationException("Analysis dataset profile was not found.");
            IReadOnlyDictionary<string, object> row = rows[0];
            return new AnalysisProfileIdentity(Convert.ToInt64(row["id"]), Convert.ToString(row["name"]), Convert.ToInt64(row["mouse_dpi"]));
        }

        private static void RequireFinalizedEvidence(SqliteDatabaseConnection connection, long profileId)
        {
            long pendingShots = Convert.ToInt64(connection.Scalar(
                "SELECT COUNT(*) FROM shots WHERE profile_id=@profile_id AND is_adaptation_shot IS NULL;",
                new Dictionary<string, object> { ["@profile_id"] = profileId }));
            long pendingTracking = Convert.ToInt64(connection.Scalar(
                "SELECT COUNT(*) FROM tracking_data WHERE profile_id=@profile_id AND is_adaptation_trial IS NULL;",
                new Dictionary<string, object> { ["@profile_id"] = profileId }));
            if (pendingShots != 0 || pendingTracking != 0)
                throw new InvalidOperationException("Analysis rejects profile evidence with unfinalized adaptation status.");
        }

        private static IReadOnlyList<AnalysisScoreRecord> ReadAuthoritativeScores(SqliteDatabaseConnection connection, long profileId)
        {
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"
SELECT st.id,st.cycle_id,st.battery_id,st.phase,st.edpi,st.cm_360,st.avg_performance_score,
st.performance_score_by_mode,st.grade,st.formula_version,cfg.config_version,b.completed_date
FROM sensitivity_tests st
JOIN protocol_batteries b ON b.id=st.battery_id
JOIN calibration_configs cfg ON cfg.id=st.calibration_config_id
WHERE st.profile_id=@profile_id AND b.profile_id=@profile_id AND b.completed_date IS NOT NULL
AND (SELECT COUNT(*) FROM sessions s WHERE s.battery_id=b.id)=4
AND (SELECT COUNT(DISTINCT s.mode) FROM sessions s WHERE s.battery_id=b.id)=4
ORDER BY st.id;",
                new Dictionary<string, object> { ["@profile_id"] = profileId });
            return rows.Select(row => new AnalysisScoreRecord(
                Convert.ToInt64(row["id"]), Convert.ToInt64(row["cycle_id"]), Convert.ToInt64(row["battery_id"]),
                Convert.ToInt64(row["phase"]), Convert.ToDouble(row["edpi"]), Convert.ToDouble(row["cm_360"]),
                Convert.ToDouble(row["avg_performance_score"]), Convert.ToString(row["performance_score_by_mode"]),
                row["grade"] == null ? null : Convert.ToString(row["grade"]), Convert.ToString(row["formula_version"]),
                Convert.ToString(row["config_version"]), Convert.ToString(row["completed_date"]))).ToArray();
        }

        private static IReadOnlyList<AnalysisSessionRecord> ReadSessions(SqliteDatabaseConnection connection, long profileId)
        {
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"
SELECT s.id,b.cycle_id,b.id AS battery_id,b.phase,s.mode,s.date,b.sensitivity_value,
s.fatigue_flag,s.fatigue_score_change_percentage,cfg.config_version,
CASE WHEN b.completed_date IS NOT NULL
 AND (SELECT COUNT(*) FROM sessions complete_sessions WHERE complete_sessions.battery_id=b.id)=4
 AND (SELECT COUNT(DISTINCT complete_modes.mode) FROM sessions complete_modes WHERE complete_modes.battery_id=b.id)=4
THEN 1 ELSE 0 END AS complete_battery
FROM sessions s
JOIN protocol_batteries b ON b.id=s.battery_id
JOIN calibration_configs cfg ON cfg.id=s.calibration_config_id
WHERE s.profile_id=@profile_id AND b.profile_id=@profile_id
ORDER BY s.id;",
                new Dictionary<string, object> { ["@profile_id"] = profileId });
            return rows.Select(row => new AnalysisSessionRecord(
                Convert.ToInt64(row["id"]), Convert.ToInt64(row["cycle_id"]), Convert.ToInt64(row["battery_id"]),
                Convert.ToInt64(row["phase"]), Convert.ToString(row["mode"]), Convert.ToString(row["date"]),
                Convert.ToDouble(row["sensitivity_value"]), Convert.ToInt64(row["complete_battery"]) == 1,
                Convert.ToInt64(row["fatigue_flag"]) == 1,
                row["fatigue_score_change_percentage"] == null ? (double?)null : Convert.ToDouble(row["fatigue_score_change_percentage"]),
                Convert.ToString(row["config_version"]))).ToArray();
        }

        private static IReadOnlyList<AnalysisOutlierAggregateRecord> ReadOutlierAggregates(SqliteDatabaseConnection connection, long profileId)
        {
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"
SELECT r.id,r.cycle_id,r.phase,r.mode,r.sensitivity_value,r.metric_name,r.inclusive_mean,
r.flagged_excluded_mean,r.observation_count,r.flagged_count,r.algorithm_version,cfg.config_version
FROM outlier_analysis_runs r
JOIN calibration_configs cfg ON cfg.id=r.calibration_config_id
WHERE r.profile_id=@profile_id
ORDER BY r.id;",
                new Dictionary<string, object> { ["@profile_id"] = profileId });
            return rows.Select(row => new AnalysisOutlierAggregateRecord(
                Convert.ToInt64(row["id"]), Convert.ToInt64(row["cycle_id"]), Convert.ToInt64(row["phase"]),
                Convert.ToString(row["mode"]), Convert.ToDouble(row["sensitivity_value"]), Convert.ToString(row["metric_name"]),
                Convert.ToDouble(row["inclusive_mean"]), Convert.ToDouble(row["flagged_excluded_mean"]),
                Convert.ToInt64(row["observation_count"]), Convert.ToInt64(row["flagged_count"]),
                Convert.ToString(row["algorithm_version"]), Convert.ToString(row["config_version"]))).ToArray();
        }
    }
}
