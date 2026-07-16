using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class ProtocolRepository
    {
        private readonly RepositoryExecution execution;
        public ProtocolRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null) { execution = new RepositoryExecution(connectionFactory, failureReporter); }

        public CycleRecord CreateCycle(CycleRecord cycle)
        {
            if (cycle == null) throw new ArgumentNullException(nameof(cycle));
            return execution.Write("create cycle", connection =>
            {
                connection.Execute("INSERT INTO cycles(profile_id, cycle_number, start_date, end_date, outcome) VALUES (@profile_id,@cycle_number,@start_date,@end_date,@outcome);", new Dictionary<string, object> { ["@profile_id"] = cycle.ProfileId, ["@cycle_number"] = cycle.CycleNumber, ["@start_date"] = cycle.StartDate, ["@end_date"] = cycle.EndDate, ["@outcome"] = cycle.Outcome });
                return new CycleRecord(connection.LastInsertRowId(), cycle.ProfileId, cycle.CycleNumber, cycle.StartDate, cycle.EndDate, cycle.Outcome);
            });
        }

        public ProtocolCandidateRecord CreateCandidateWithSources(ProtocolCandidateRecord candidate, IReadOnlyList<ProtocolCandidateSourceRecord> sources)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            return execution.Write("create protocol candidate", connection =>
            {
                using SqliteTransaction transaction = connection.BeginImmediateTransaction();
                VerifyCycleBelongsToProfile(connection, candidate.CycleId, candidate.ProfileId);
                connection.Execute(@"INSERT INTO protocol_candidates(profile_id, cycle_id, phase, edpi, sensitivity_value, generation_rule, created_date)
VALUES (@profile_id,@cycle_id,@phase,@edpi,@sensitivity_value,@generation_rule,@created_date);", CandidateParameters(candidate));
                long id = connection.LastInsertRowId();
                foreach (ProtocolCandidateSourceRecord source in sources)
                {
                    if (source == null) throw new ArgumentException("Candidate sources cannot contain null.", nameof(sources));
                    connection.Execute(@"INSERT INTO protocol_candidate_sources(candidate_id, anchor_edpi, offset_percent, pre_floor_edpi, floor_applied)
VALUES (@candidate_id,@anchor_edpi,@offset_percent,@pre_floor_edpi,@floor_applied);", new Dictionary<string, object> { ["@candidate_id"] = id, ["@anchor_edpi"] = source.AnchorEdpi, ["@offset_percent"] = source.OffsetPercent, ["@pre_floor_edpi"] = source.PreFloorEdpi, ["@floor_applied"] = source.FloorApplied });
                }
                transaction.Commit();
                return new ProtocolCandidateRecord(id, candidate.ProfileId, candidate.CycleId, candidate.Phase, candidate.Edpi, candidate.SensitivityValue, candidate.GenerationRule, candidate.CreatedDate);
            });
        }

        public ProtocolBatteryRecord CreateBattery(ProtocolBatteryRecord battery)
        {
            if (battery == null) throw new ArgumentNullException(nameof(battery));
            return execution.Write("create protocol battery", connection =>
            {
                VerifyCandidateMatchesBattery(connection, battery);
                connection.Execute(@"INSERT INTO protocol_batteries(profile_id, cycle_id, candidate_id, sensitivity_value, phase, purpose, started_date, completed_date)
VALUES (@profile_id,@cycle_id,@candidate_id,@sensitivity_value,@phase,@purpose,@started_date,@completed_date);", new Dictionary<string, object> { ["@profile_id"] = battery.ProfileId, ["@cycle_id"] = battery.CycleId, ["@candidate_id"] = battery.CandidateId, ["@sensitivity_value"] = battery.SensitivityValue, ["@phase"] = battery.Phase, ["@purpose"] = battery.Purpose, ["@started_date"] = battery.StartedDate, ["@completed_date"] = battery.CompletedDate });
                return new ProtocolBatteryRecord(connection.LastInsertRowId(), battery.ProfileId, battery.CycleId, battery.CandidateId, battery.SensitivityValue, battery.Phase, battery.Purpose, battery.StartedDate, battery.CompletedDate);
            });
        }

        private static void VerifyCycleBelongsToProfile(SqliteDatabaseConnection connection, long cycleId, long profileId)
        {
            object count = connection.Scalar("SELECT COUNT(*) FROM cycles WHERE id=@cycle_id AND profile_id=@profile_id;", new Dictionary<string, object> { ["@cycle_id"] = cycleId, ["@profile_id"] = profileId });
            if (Convert.ToInt64(count) != 1) throw new InvalidOperationException("The cycle does not belong to the candidate profile.");
        }

        private static void VerifyCandidateMatchesBattery(SqliteDatabaseConnection connection, ProtocolBatteryRecord battery)
        {
            object count = connection.Scalar(@"SELECT COUNT(*) FROM protocol_candidates WHERE id=@candidate_id AND profile_id=@profile_id AND cycle_id=@cycle_id AND phase=@phase AND sensitivity_value=@sensitivity_value;", new Dictionary<string, object> { ["@candidate_id"] = battery.CandidateId, ["@profile_id"] = battery.ProfileId, ["@cycle_id"] = battery.CycleId, ["@phase"] = battery.Phase, ["@sensitivity_value"] = battery.SensitivityValue });
            if (Convert.ToInt64(count) != 1) throw new InvalidOperationException("The battery must match its canonical candidate profile, cycle, phase, and sensitivity.");
        }

        private static IReadOnlyDictionary<string, object> CandidateParameters(ProtocolCandidateRecord value) => new Dictionary<string, object> { ["@profile_id"] = value.ProfileId, ["@cycle_id"] = value.CycleId, ["@phase"] = value.Phase, ["@edpi"] = value.Edpi, ["@sensitivity_value"] = value.SensitivityValue, ["@generation_rule"] = value.GenerationRule, ["@created_date"] = value.CreatedDate };
    }
}
