using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class PhaseHistoryRecord
    {
        public PhaseHistoryRecord(long? id, long profileId, long cycleId, long phaseNumber, double winnerEdpi, string timestamp)
        {
            Id = id;
            ProfileId = Positive(profileId, nameof(profileId));
            CycleId = Positive(cycleId, nameof(cycleId));
            PhaseNumber = Positive(phaseNumber, nameof(phaseNumber));
            WinnerEdpi = PositiveFinite(winnerEdpi, nameof(winnerEdpi));
            Timestamp = !string.IsNullOrWhiteSpace(timestamp) ? timestamp : throw new ArgumentException("Timestamp is required.", nameof(timestamp));
        }

        public long? Id { get; }
        public long ProfileId { get; }
        public long CycleId { get; }
        public long PhaseNumber { get; }
        public double WinnerEdpi { get; }
        public string Timestamp { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double PositiveFinite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class PhaseHistoryRepository
    {
        private readonly RepositoryExecution execution;

        public PhaseHistoryRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        {
            execution = new RepositoryExecution(connectionFactory, failureReporter);
        }

        public PhaseHistoryRecord Create(PhaseHistoryRecord value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return execution.Write("create phase history", connection =>
            {
                long valid = Convert.ToInt64(connection.Scalar(@"SELECT COUNT(*) FROM cycles WHERE id=@cycle_id AND profile_id=@profile_id;",
                    new Dictionary<string, object> { ["@cycle_id"] = value.CycleId, ["@profile_id"] = value.ProfileId }));
                if (valid != 1) throw new InvalidOperationException("Phase history cycle lineage is invalid.");
                long duplicate = Convert.ToInt64(connection.Scalar(@"SELECT COUNT(*) FROM phase_history WHERE profile_id=@profile_id AND cycle_id=@cycle_id AND phase_number=@phase;",
                    new Dictionary<string, object> { ["@profile_id"] = value.ProfileId, ["@cycle_id"] = value.CycleId, ["@phase"] = value.PhaseNumber }));
                if (duplicate != 0) throw new InvalidOperationException("A winner is already recorded for this cycle phase.");
                connection.Execute(@"INSERT INTO phase_history(profile_id,cycle_id,phase_number,winner_edpi,timestamp)
VALUES (@profile_id,@cycle_id,@phase,@winner_edpi,@timestamp);", new Dictionary<string, object>
                {
                    ["@profile_id"] = value.ProfileId, ["@cycle_id"] = value.CycleId, ["@phase"] = value.PhaseNumber,
                    ["@winner_edpi"] = value.WinnerEdpi, ["@timestamp"] = value.Timestamp
                });
                return new PhaseHistoryRecord(connection.LastInsertRowId(), value.ProfileId, value.CycleId,
                    value.PhaseNumber, value.WinnerEdpi, value.Timestamp);
            });
        }

        public PhaseHistoryRecord Require(long profileId, long cycleId, long phaseNumber)
        {
            return execution.Read("read phase history", connection =>
            {
                IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"SELECT * FROM phase_history
WHERE profile_id=@profile_id AND cycle_id=@cycle_id AND phase_number=@phase;", new Dictionary<string, object>
                {
                    ["@profile_id"] = profileId, ["@cycle_id"] = cycleId, ["@phase"] = phaseNumber
                });
                if (rows.Count != 1) throw new InvalidOperationException("Exactly one phase winner is required.");
                IReadOnlyDictionary<string, object> row = rows[0];
                return new PhaseHistoryRecord(Convert.ToInt64(row["id"]), Convert.ToInt64(row["profile_id"]),
                    Convert.ToInt64(row["cycle_id"]), Convert.ToInt64(row["phase_number"]),
                    Convert.ToDouble(row["winner_edpi"]), Convert.ToString(row["timestamp"]));
            });
        }
    }
}
