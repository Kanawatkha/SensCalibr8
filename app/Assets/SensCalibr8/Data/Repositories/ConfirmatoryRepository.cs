using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class ConfirmatoryRepository
    {
        private readonly RepositoryExecution execution;

        public ConfirmatoryRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        {
            execution = new RepositoryExecution(connectionFactory, failureReporter);
        }

        public IReadOnlyList<RankedProtocolCandidate> SelectTopExploratoryCandidates(long profileId, long cycleId, long phase, int count)
        {
            if (profileId <= 0 || cycleId <= 0 || phase <= 0 || count <= 0) throw new ArgumentOutOfRangeException();
            return execution.Read("select exploratory candidates", connection =>
            {
                IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"SELECT c.id,c.profile_id,c.cycle_id,c.phase,c.edpi,c.sensitivity_value,c.generation_rule,c.created_date,
AVG(s.avg_performance_score) AS mean_score,COUNT(s.id) AS score_count
FROM protocol_candidates c JOIN sensitivity_tests s
ON s.profile_id=c.profile_id AND s.cycle_id=c.cycle_id AND s.phase=c.phase AND s.edpi=c.edpi
JOIN protocol_batteries b ON b.id=s.battery_id AND b.purpose='exploratory' AND b.completed_date IS NOT NULL
WHERE c.profile_id=@profile_id AND c.cycle_id=@cycle_id AND c.phase=@phase
GROUP BY c.id,c.profile_id,c.cycle_id,c.phase,c.edpi,c.sensitivity_value,c.generation_rule,c.created_date
ORDER BY mean_score DESC,c.edpi ASC LIMIT @count;", new Dictionary<string, object>
                {
                    ["@profile_id"] = profileId, ["@cycle_id"] = cycleId, ["@phase"] = phase, ["@count"] = count
                });
                return rows.Select(MapRankedCandidate).ToArray();
            });
        }

        public ConfirmatoryBatteryPairRecord CreateBatteryPair(ProtocolCandidateRecord candidateA,
            ProtocolCandidateRecord candidateB, string startedDate)
        {
            if (candidateA == null || candidateB == null) throw new ArgumentNullException();
            if (candidateA.Id == null || candidateB.Id == null || candidateA.Id == candidateB.Id ||
                candidateA.ProfileId != candidateB.ProfileId || candidateA.CycleId != candidateB.CycleId || candidateA.Phase != candidateB.Phase)
                throw new ArgumentException("Confirmatory candidates must be distinct persisted peers.");
            if (string.IsNullOrWhiteSpace(startedDate)) throw new ArgumentException("Started date is required.", nameof(startedDate));
            return execution.Write("create confirmatory battery pair", connection =>
            {
                using SqliteTransaction transaction = connection.BeginImmediateTransaction();
                VerifyCandidate(connection, candidateA);
                VerifyCandidate(connection, candidateB);
                ProtocolBatteryRecord batteryA = InsertBattery(connection, candidateA, startedDate);
                ProtocolBatteryRecord batteryB = InsertBattery(connection, candidateB, startedDate);
                transaction.Commit();
                return new ConfirmatoryBatteryPairRecord(batteryA, batteryB);
            });
        }

        public PersistedSignificanceTest CreateWithPairs(SignificanceTestRecord test,
            IReadOnlyList<SignificanceTestPairRecord> pairs)
        {
            if (test == null) throw new ArgumentNullException(nameof(test));
            if (pairs == null || pairs.Count == 0) throw new ArgumentException("Significance pairs are required.", nameof(pairs));
            return execution.Write("create significance test", connection =>
            {
                using SqliteTransaction transaction = connection.BeginImmediateTransaction();
                VerifyResultLineage(connection, test);
                var batteryIds = new HashSet<long>();
                foreach (SignificanceTestPairRecord pair in pairs)
                {
                    if (pair == null) throw new ArgumentException("Significance pairs cannot contain null.", nameof(pairs));
                    if (!batteryIds.Add(pair.CandidateABatteryId) || !batteryIds.Add(pair.CandidateBBatteryId))
                        throw new InvalidOperationException("Every confirmatory battery may appear in exactly one pair.");
                    VerifyCompleteFreshBattery(connection, pair.CandidateABatteryId, pair.CandidateAScore, test, test.CandidateAEdpi);
                    VerifyCompleteFreshBattery(connection, pair.CandidateBBatteryId, pair.CandidateBScore, test, test.CandidateBEdpi);
                }

                connection.Execute(@"INSERT INTO significance_tests(profile_id,cycle_id,calibration_config_id,phase,candidate_a_edpi,candidate_b_edpi,test_method,alternative,alpha,p_value,effect_estimate,confidence_level,confidence_interval_lower,confidence_interval_upper,paired_sample_size,is_significant,formula_version,result)
VALUES (@profile_id,@cycle_id,@calibration_config_id,@phase,@candidate_a_edpi,@candidate_b_edpi,@test_method,@alternative,@alpha,@p_value,@effect_estimate,@confidence_level,@confidence_interval_lower,@confidence_interval_upper,@paired_sample_size,@is_significant,@formula_version,@result);",
                    TestParameters(test));
                long testId = connection.LastInsertRowId();
                foreach (SignificanceTestPairRecord pair in pairs)
                {
                    connection.Execute(@"INSERT INTO significance_test_pairs(significance_test_id,pair_index,first_candidate,pairing_seed,matched_condition_key,candidate_a_battery_id,candidate_b_battery_id,candidate_a_score,candidate_b_score)
VALUES (@test_id,@pair_index,@first_candidate,@pairing_seed,@matched_condition_key,@candidate_a_battery_id,@candidate_b_battery_id,@candidate_a_score,@candidate_b_score);",
                        PairParameters(testId, pair));
                }
                transaction.Commit();
                return new PersistedSignificanceTest(WithId(test, testId), pairs.ToArray());
            });
        }

        private static RankedProtocolCandidate MapRankedCandidate(IReadOnlyDictionary<string, object> row)
        {
            var candidate = new ProtocolCandidateRecord(Convert.ToInt64(row["id"]), Convert.ToInt64(row["profile_id"]),
                Convert.ToInt64(row["cycle_id"]), Convert.ToInt64(row["phase"]), Convert.ToDouble(row["edpi"]),
                Convert.ToDouble(row["sensitivity_value"]), Convert.ToString(row["generation_rule"]), Convert.ToString(row["created_date"]));
            return new RankedProtocolCandidate(candidate, Convert.ToDouble(row["mean_score"]), Convert.ToInt64(row["score_count"]));
        }

        private static ProtocolBatteryRecord InsertBattery(SqliteDatabaseConnection connection, ProtocolCandidateRecord candidate, string startedDate)
        {
            connection.Execute(@"INSERT INTO protocol_batteries(profile_id,cycle_id,candidate_id,sensitivity_value,phase,purpose,started_date,completed_date)
VALUES (@profile_id,@cycle_id,@candidate_id,@sensitivity_value,@phase,'confirmatory',@started_date,NULL);", new Dictionary<string, object>
            {
                ["@profile_id"] = candidate.ProfileId, ["@cycle_id"] = candidate.CycleId, ["@candidate_id"] = candidate.Id.Value,
                ["@sensitivity_value"] = candidate.SensitivityValue, ["@phase"] = candidate.Phase, ["@started_date"] = startedDate
            });
            return new ProtocolBatteryRecord(connection.LastInsertRowId(), candidate.ProfileId, candidate.CycleId,
                candidate.Id.Value, candidate.SensitivityValue, candidate.Phase, "confirmatory", startedDate, null);
        }

        private static void VerifyCandidate(SqliteDatabaseConnection connection, ProtocolCandidateRecord candidate)
        {
            long count = Convert.ToInt64(connection.Scalar(@"SELECT COUNT(*) FROM protocol_candidates
WHERE id=@id AND profile_id=@profile_id AND cycle_id=@cycle_id AND phase=@phase AND edpi=@edpi AND sensitivity_value=@sensitivity_value;",
                new Dictionary<string, object> { ["@id"] = candidate.Id.Value, ["@profile_id"] = candidate.ProfileId,
                    ["@cycle_id"] = candidate.CycleId, ["@phase"] = candidate.Phase, ["@edpi"] = candidate.Edpi,
                    ["@sensitivity_value"] = candidate.SensitivityValue }));
            if (count != 1) throw new InvalidOperationException("Confirmatory candidate lineage is invalid.");
        }

        private static void VerifyResultLineage(SqliteDatabaseConnection connection, SignificanceTestRecord test)
        {
            long count = Convert.ToInt64(connection.Scalar(@"SELECT COUNT(*) FROM cycles y JOIN calibration_configs c
WHERE y.id=@cycle_id AND y.profile_id=@profile_id AND c.id=@config_id;",
                new Dictionary<string, object> { ["@cycle_id"] = test.CycleId, ["@profile_id"] = test.ProfileId,
                    ["@config_id"] = test.CalibrationConfigId }));
            if (count != 1) throw new InvalidOperationException("Significance result lineage is invalid.");
        }

        private static void VerifyCompleteFreshBattery(SqliteDatabaseConnection connection, long batteryId,
            double batteryScore, SignificanceTestRecord test, double candidateEdpi)
        {
            long valid = Convert.ToInt64(connection.Scalar(@"SELECT COUNT(*) FROM protocol_batteries b
JOIN protocol_candidates c ON c.id=b.candidate_id
WHERE b.id=@battery_id AND b.profile_id=@profile_id AND b.cycle_id=@cycle_id AND b.phase=@phase
AND b.purpose='confirmatory' AND b.completed_date IS NOT NULL AND c.edpi=@candidate_edpi
AND (SELECT COUNT(*) FROM sessions s WHERE s.battery_id=b.id)=4
AND (SELECT COUNT(DISTINCT s.mode) FROM sessions s WHERE s.battery_id=b.id)=4
AND (SELECT COUNT(*) FROM sessions s WHERE s.battery_id=b.id AND s.calibration_config_id=@config_id)=4
AND (SELECT COUNT(*) FROM sensitivity_tests t WHERE t.battery_id=b.id AND t.calibration_config_id=@config_id
AND t.phase=@phase AND t.edpi=@candidate_edpi AND t.avg_performance_score=@battery_score AND t.formula_version=@formula_version)=1;",
                new Dictionary<string, object> { ["@battery_id"] = batteryId, ["@profile_id"] = test.ProfileId,
                    ["@cycle_id"] = test.CycleId, ["@phase"] = test.Phase, ["@candidate_edpi"] = candidateEdpi,
                    ["@config_id"] = test.CalibrationConfigId, ["@battery_score"] = batteryScore,
                    ["@formula_version"] = test.FormulaVersion }));
            if (valid != 1) throw new InvalidOperationException("Confirmatory battery must be fresh, complete, and lineage-matched.");
            long reused = Convert.ToInt64(connection.Scalar(@"SELECT COUNT(*) FROM significance_test_pairs
WHERE candidate_a_battery_id=@battery_id OR candidate_b_battery_id=@battery_id;",
                new Dictionary<string, object> { ["@battery_id"] = batteryId }));
            if (reused != 0) throw new InvalidOperationException("Confirmatory battery evidence cannot be reused.");
        }

        private static IReadOnlyDictionary<string, object> TestParameters(SignificanceTestRecord value) => new Dictionary<string, object>
        {
            ["@profile_id"] = value.ProfileId, ["@cycle_id"] = value.CycleId, ["@calibration_config_id"] = value.CalibrationConfigId,
            ["@phase"] = value.Phase, ["@candidate_a_edpi"] = value.CandidateAEdpi, ["@candidate_b_edpi"] = value.CandidateBEdpi,
            ["@test_method"] = value.TestMethod, ["@alternative"] = value.Alternative, ["@alpha"] = value.Alpha,
            ["@p_value"] = value.PValue, ["@effect_estimate"] = value.EffectEstimate, ["@confidence_level"] = value.ConfidenceLevel,
            ["@confidence_interval_lower"] = value.ConfidenceIntervalLower, ["@confidence_interval_upper"] = value.ConfidenceIntervalUpper,
            ["@paired_sample_size"] = value.PairedSampleSize, ["@is_significant"] = value.IsSignificant,
            ["@formula_version"] = value.FormulaVersion, ["@result"] = value.Result
        };

        private static IReadOnlyDictionary<string, object> PairParameters(long testId, SignificanceTestPairRecord value) => new Dictionary<string, object>
        {
            ["@test_id"] = testId, ["@pair_index"] = value.PairIndex, ["@first_candidate"] = value.FirstCandidate,
            ["@pairing_seed"] = value.PairingSeed, ["@matched_condition_key"] = value.MatchedConditionKey,
            ["@candidate_a_battery_id"] = value.CandidateABatteryId, ["@candidate_b_battery_id"] = value.CandidateBBatteryId,
            ["@candidate_a_score"] = value.CandidateAScore, ["@candidate_b_score"] = value.CandidateBScore
        };

        private static SignificanceTestRecord WithId(SignificanceTestRecord value, long id) => new SignificanceTestRecord(id,
            value.ProfileId, value.CycleId, value.CalibrationConfigId, value.Phase, value.CandidateAEdpi, value.CandidateBEdpi,
            value.TestMethod, value.Alternative, value.Alpha, value.PValue, value.EffectEstimate, value.ConfidenceLevel,
            value.ConfidenceIntervalLower, value.ConfidenceIntervalUpper, value.PairedSampleSize, value.IsSignificant,
            value.FormulaVersion, value.Result);
    }
}
