using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class PhaseOneNarrowingDecision
    {
        public PhaseOneNarrowingDecision(long significanceTestId, long profileId, long cycleId,
            double candidateAEdpi, double candidateBEdpi, string result)
        {
            SignificanceTestId = Positive(significanceTestId, nameof(significanceTestId));
            ProfileId = Positive(profileId, nameof(profileId));
            CycleId = Positive(cycleId, nameof(cycleId));
            CandidateAEdpi = PositiveFinite(candidateAEdpi, nameof(candidateAEdpi));
            CandidateBEdpi = PositiveFinite(candidateBEdpi, nameof(candidateBEdpi));
            Result = !string.IsNullOrWhiteSpace(result) ? result : throw new ArgumentException(nameof(result));
        }

        public long SignificanceTestId { get; }
        public long ProfileId { get; }
        public long CycleId { get; }
        public double CandidateAEdpi { get; }
        public double CandidateBEdpi { get; }
        public string Result { get; }

        public IReadOnlyList<double> Anchors => Result == "candidate_a" ? new[] { CandidateAEdpi } :
            Result == "candidate_b" ? new[] { CandidateBEdpi } :
            Result == "statistical_tie" ? new[] { CandidateAEdpi, CandidateBEdpi } :
            throw new InvalidOperationException("Unsupported Phase 1 decision.");

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static double PositiveFinite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class NarrowingRepository
    {
        private readonly RepositoryExecution execution;

        public NarrowingRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        {
            execution = new RepositoryExecution(connectionFactory, failureReporter);
        }

        public PhaseOneNarrowingDecision RequirePhaseOneDecision(long profileId, long cycleId)
        {
            if (profileId <= 0 || cycleId <= 0) throw new ArgumentOutOfRangeException();
            return execution.Read("read Phase 1 decision", connection =>
            {
                IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query(@"SELECT id,profile_id,cycle_id,candidate_a_edpi,candidate_b_edpi,result
FROM significance_tests WHERE profile_id=@profile_id AND cycle_id=@cycle_id AND phase=1;",
                    new Dictionary<string, object> { ["@profile_id"] = profileId, ["@cycle_id"] = cycleId });
                if (rows.Count != 1) throw new InvalidOperationException("Exactly one completed Phase 1 significance decision is required.");
                IReadOnlyDictionary<string, object> row = rows[0];
                return new PhaseOneNarrowingDecision(Convert.ToInt64(row["id"]), Convert.ToInt64(row["profile_id"]),
                    Convert.ToInt64(row["cycle_id"]), Convert.ToDouble(row["candidate_a_edpi"]),
                    Convert.ToDouble(row["candidate_b_edpi"]), Convert.ToString(row["result"]));
            });
        }

        public IReadOnlyList<ProtocolCandidateRecord> ListPhaseTwoCandidates(long profileId, long cycleId)
        {
            return ListPhaseCandidates(profileId, cycleId, 2);
        }

        public IReadOnlyList<ProtocolCandidateRecord> ListPhaseCandidates(long profileId, long cycleId, long phase)
        {
            if (profileId <= 0 || cycleId <= 0 || phase <= 0) throw new ArgumentOutOfRangeException();
            return execution.Read("read Phase 2 candidates", connection => connection.Query(@"SELECT * FROM protocol_candidates
WHERE profile_id=@profile_id AND cycle_id=@cycle_id AND phase=@phase ORDER BY edpi;",
                new Dictionary<string, object> { ["@profile_id"] = profileId, ["@cycle_id"] = cycleId, ["@phase"] = phase })
                .Select(MapCandidate).ToArray());
        }

        public IReadOnlyList<double> ListCompletedNarrowingScores(ProtocolCandidateRecord candidate,
            long calibrationConfigId, string formulaVersion)
        {
            if (candidate == null || candidate.Id == null) throw new ArgumentNullException(nameof(candidate));
            if (calibrationConfigId <= 0 || string.IsNullOrWhiteSpace(formulaVersion)) throw new ArgumentException("Score lineage is required.");
            return execution.Read("read completed narrowing scores", connection => connection.Query(@"SELECT s.avg_performance_score FROM sensitivity_tests s
JOIN protocol_batteries b ON b.id=s.battery_id
WHERE b.candidate_id=@candidate_id AND b.profile_id=@profile_id AND b.cycle_id=@cycle_id AND b.phase=@phase
AND b.purpose='narrowing' AND b.completed_date IS NOT NULL AND s.calibration_config_id=@config_id
AND s.formula_version=@formula_version AND s.phase=@phase AND s.edpi=@edpi ORDER BY s.id;",
                new Dictionary<string, object> { ["@candidate_id"] = candidate.Id.Value, ["@profile_id"] = candidate.ProfileId,
                    ["@cycle_id"] = candidate.CycleId, ["@config_id"] = calibrationConfigId,
                    ["@formula_version"] = formulaVersion, ["@edpi"] = candidate.Edpi, ["@phase"] = candidate.Phase })
                .Select(row => Convert.ToDouble(row["avg_performance_score"])).ToArray());
        }

        private static ProtocolCandidateRecord MapCandidate(IReadOnlyDictionary<string, object> row) =>
            new ProtocolCandidateRecord(Convert.ToInt64(row["id"]), Convert.ToInt64(row["profile_id"]),
                Convert.ToInt64(row["cycle_id"]), Convert.ToInt64(row["phase"]), Convert.ToDouble(row["edpi"]),
                Convert.ToDouble(row["sensitivity_value"]), Convert.ToString(row["generation_rule"]),
                Convert.ToString(row["created_date"]));
    }
}
