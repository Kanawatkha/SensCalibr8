using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class ImmediateFeedbackRepository
    {
        private readonly RepositoryExecution execution;

        public ImmediateFeedbackRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        {
            execution = new RepositoryExecution(connectionFactory, failureReporter);
        }

        public ImmediateFeedbackDataRecord ReadCurrentProtocolWindow(long profileId)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            return execution.Read("read immediate feedback", connection =>
            {
                RequireProfile(connection, profileId);
                RequireFinalizedShotEvidence(connection, profileId);
                FeedbackContext context = FindLatestShotContext(connection, profileId);
                return new ImmediateFeedbackDataRecord(ReadBestSensitivity(connection, profileId),
                    ReadLatestAuthoritativeScore(connection, profileId), context == null ? null : context.Mode,
                    context == null ? (long?)null : context.CycleId, context == null ? (long?)null : context.Phase,
                    context == null ? Array.Empty<AccuracyEvidenceRecord>() : ReadAccuracyEvidence(connection, profileId, context));
            });
        }

        private static void RequireProfile(SqliteDatabaseConnection connection, long profileId)
        {
            if (Convert.ToInt64(connection.Scalar("SELECT COUNT(*) FROM profiles WHERE id=@profile_id;",
                new Dictionary<string, object> { ["@profile_id"] = profileId })) != 1)
                throw new InvalidOperationException("Immediate-feedback profile was not found.");
        }

        private static void RequireFinalizedShotEvidence(SqliteDatabaseConnection connection, long profileId)
        {
            if (Convert.ToInt64(connection.Scalar("SELECT COUNT(*) FROM shots WHERE profile_id=@profile_id AND is_adaptation_shot IS NULL;",
                new Dictionary<string, object> { ["@profile_id"] = profileId })) != 0)
                throw new InvalidOperationException("Immediate feedback rejects shot evidence with unfinalized adaptation status.");
        }

        private static FeedbackContext FindLatestShotContext(SqliteDatabaseConnection connection, long profileId)
        {
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"
SELECT b.cycle_id,b.phase,s.mode
FROM sessions s
JOIN protocol_batteries b ON b.id=s.battery_id
WHERE s.profile_id=@profile_id
AND s.mode IN ('flick_close','flick_far','micro_correction')
AND EXISTS (SELECT 1 FROM shots sh WHERE sh.session_id=s.id AND sh.is_adaptation_shot=0)
ORDER BY s.id DESC LIMIT 1;",
                new Dictionary<string, object> { ["@profile_id"] = profileId });
            if (rows.Count == 0) return null;
            IReadOnlyDictionary<string, object> row = rows[0];
            return new FeedbackContext(Convert.ToInt64(row["cycle_id"]), Convert.ToInt64(row["phase"]), Convert.ToString(row["mode"]));
        }

        private static IReadOnlyList<AccuracyEvidenceRecord> ReadAccuracyEvidence(SqliteDatabaseConnection connection, long profileId, FeedbackContext context)
        {
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"
SELECT b.sensitivity_value,
SUM(CASE WHEN sh.is_hit=1 THEN 1 ELSE 0 END) AS hit_count,
COUNT(*) AS resolved_count
FROM sessions s
JOIN protocol_batteries b ON b.id=s.battery_id
JOIN shots sh ON sh.session_id=s.id
WHERE s.profile_id=@profile_id AND b.profile_id=@profile_id
AND b.cycle_id=@cycle_id AND b.phase=@phase AND s.mode=@mode
AND sh.is_adaptation_shot=0
GROUP BY b.sensitivity_value
ORDER BY b.sensitivity_value;",
                new Dictionary<string, object>
                {
                    ["@profile_id"] = profileId, ["@cycle_id"] = context.CycleId, ["@phase"] = context.Phase, ["@mode"] = context.Mode
                });
            return rows.Select(row => new AccuracyEvidenceRecord(Convert.ToDouble(row["sensitivity_value"]),
                Convert.ToInt64(row["hit_count"]), Convert.ToInt64(row["resolved_count"]))).ToArray();
        }

        private static double? ReadBestSensitivity(SqliteDatabaseConnection connection, long profileId)
        {
            object value = connection.Scalar(@"
SELECT c.sensitivity_value
FROM phase_history h
JOIN protocol_candidates c ON c.profile_id=h.profile_id AND c.cycle_id=h.cycle_id
AND c.phase=h.phase_number AND c.edpi=h.winner_edpi
WHERE h.profile_id=@profile_id AND h.phase_number=3
ORDER BY h.id DESC LIMIT 1;",
                new Dictionary<string, object> { ["@profile_id"] = profileId });
            return value == null ? (double?)null : Convert.ToDouble(value);
        }

        private static double? ReadLatestAuthoritativeScore(SqliteDatabaseConnection connection, long profileId)
        {
            object value = connection.Scalar(@"
SELECT st.avg_performance_score
FROM sensitivity_tests st
JOIN protocol_batteries b ON b.id=st.battery_id
WHERE st.profile_id=@profile_id AND b.profile_id=@profile_id AND b.completed_date IS NOT NULL
AND (SELECT COUNT(*) FROM sessions complete_sessions WHERE complete_sessions.battery_id=b.id)=4
AND (SELECT COUNT(DISTINCT complete_modes.mode) FROM sessions complete_modes WHERE complete_modes.battery_id=b.id)=4
ORDER BY st.id DESC LIMIT 1;",
                new Dictionary<string, object> { ["@profile_id"] = profileId });
            return value == null ? (double?)null : Convert.ToDouble(value);
        }

        private sealed class FeedbackContext
        {
            public FeedbackContext(long cycleId, long phase, string mode)
            {
                CycleId = cycleId;
                Phase = phase;
                Mode = mode;
            }

            public long CycleId { get; }
            public long Phase { get; }
            public string Mode { get; }
        }
    }
}
