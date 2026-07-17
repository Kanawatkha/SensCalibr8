using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Integration;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P5R3ConfirmatorySignificanceTests
    {
        private string directory;
        private FrozenCalibrationConfiguration configuration;
        private ConfirmatoryStatisticsContract contract;
        private SqliteConnectionFactory connections;
        private ConfirmatorySignificanceWorkflow workflow;
        private ProfileRecord profile;
        private CycleRecord cycle;
        private long configId;

        [SetUp]
        public void SetUp()
        {
            string root = RepositoryRoot();
            directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p5r3-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(root);
            contract = ConfirmatoryStatisticsContractLoader.From(configuration);
            string database = Path.Combine(directory, "confirmatory.sqlite3");
            string native = Path.Combine(root, "app", "Assets", "Plugins", "sqlite3.dll");
            new SqliteDatabaseBootstrapper().Initialize(database, configuration, native);
            connections = new SqliteConnectionFactory(database, native);
            var profiles = new ProfileRepository(connections);
            var protocol = new ProtocolRepository(connections);
            profile = profiles.Create(new ProfileRecord(null, "confirmatory-profile", "2026-07-17", 1600,
                0.175d, 1000d, "right", "#FFE600", "claw", "arm", 50d, 50d, 1d, "2026-07-17"));
            cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-17", null, null));
            configId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
            SeedExploratoryCandidate(protocol, 280d, 0.175d, 80d);
            SeedExploratoryCandidate(protocol, 294d, 0.18375d, 90d);
            SeedExploratoryCandidate(protocol, 308d, 0.1925d, 70d);
            workflow = new ConfirmatorySignificanceWorkflow(new ConfirmatoryRepository(connections),
                new CalibrationConfigurationRepository(connections), configuration);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void FrozenContractRejectsReuseAndEarlyStoppingAndMatchesAcceptedEnumeration()
        {
            Assert.That(contract.Version, Is.EqualTo("sc8-confirmatory-v1"));
            Assert.That(contract.FreshPairsRequired, Is.EqualTo(10));
            Assert.That(contract.EnumerationCount, Is.EqualTo(1024));
            Assert.That(contract.Alpha, Is.EqualTo(0.05d));
            Assert.That(contract.ConfidenceLevel, Is.EqualTo(0.95d));
            Assert.That(contract.ReuseExploratoryData, Is.False);
            Assert.That(contract.EarlyStopping, Is.False);
            Assert.That(typeof(ConfirmatoryStatisticsContract).GetProperties().All(value => !value.CanWrite), Is.True);
        }

        [Test]
        public void RankingSelectsTopTwoAndBlindSurfaceDoesNotExposeScoresOrSensitivity()
        {
            ConfirmatoryCandidateSelection selection = workflow.SelectTopTwo(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseOne);
            Assert.That(selection.CandidateAId, Is.Not.EqualTo(selection.CandidateBId));
            Assert.That(selection.CandidateALabel, Is.EqualTo("Candidate-A"));
            Assert.That(selection.CandidateBLabel, Is.EqualTo("Candidate-B"));
            Assert.That(typeof(ConfirmatoryCandidateSelection).GetProperty("Edpi"), Is.Null);
            Assert.That(typeof(ConfirmatoryCandidateSelection).GetProperty("SensitivityValue"), Is.Null);
            Assert.That(typeof(ConfirmatoryCandidateSelection).GetProperty("ExploratoryMeanScore"), Is.Null);
        }

        [Test]
        public void ExactPositiveWorkedFixturePersistsWinnerAndEveryFreshPair()
        {
            ConfirmatoryCandidateSelection selection = workflow.SelectTopTwo(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseOne);
            IReadOnlyList<CompletedConfirmatoryPair> completed = LaunchAndComplete(selection, true);
            PersistedSignificanceTest persisted = workflow.CompleteAndPersist(selection, completed);

            Assert.That(completed.Count(value => value.Launch.FirstCandidateLabel == "Candidate-A"), Is.EqualTo(5));
            Assert.That(completed.Count(value => value.Launch.FirstCandidateLabel == "Candidate-B"), Is.EqualTo(5));
            Assert.That(completed.Select(value => value.Launch.PairingSeed).Distinct().Count(), Is.EqualTo(1));
            Assert.That(completed.Select(value => value.Launch.MatchedConditionKey).Distinct().Count(), Is.EqualTo(10));
            Assert.That(persisted.Test.EffectEstimate, Is.EqualTo(5d).Within(1e-12));
            Assert.That(persisted.Test.PValue, Is.EqualTo(0.001953125d).Within(1e-15));
            Assert.That(persisted.Test.ConfidenceIntervalLower, Is.EqualTo(5d).Within(1e-12));
            Assert.That(persisted.Test.ConfidenceIntervalUpper, Is.EqualTo(5d).Within(1e-12));
            Assert.That(persisted.Test.IsSignificant, Is.True);
            Assert.That(persisted.Test.Result, Is.EqualTo("candidate_a"));
            Assert.That(persisted.Test.FormulaVersion, Is.EqualTo(configuration.FormulaVersion.Value));
            Assert.That(persisted.Pairs.Count, Is.EqualTo(10));
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_tests;"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_test_pairs;"), Is.EqualTo(10));
        }

        [Test]
        public void SymmetricAndNegativeFixturesProduceTieAndCandidateB()
        {
            var calculator = new ConfirmatorySignificanceCalculator(contract);
            ConfirmatorySignificanceResult tie = calculator.Calculate(Enumerable.Range(1, 10)
                .Select(index => new ConfirmatoryScorePair(index, index % 2 == 0 ? 79d : 81d, 80d)).ToArray());
            ConfirmatorySignificanceResult candidateB = calculator.Calculate(Enumerable.Range(1, 10)
                .Select(index => new ConfirmatoryScorePair(index, 75d, 80d)).ToArray());
            Assert.That(tie.PValue, Is.EqualTo(1d));
            Assert.That(tie.Result, Is.EqualTo("statistical_tie"));
            Assert.That(tie.Differences.Count(value => value == 0d), Is.Zero);
            Assert.That(candidateB.PValue, Is.EqualTo(0.001953125d).Within(1e-15));
            Assert.That(candidateB.EffectEstimate, Is.EqualTo(-5d).Within(1e-12));
            Assert.That(candidateB.Result, Is.EqualTo("candidate_b"));
        }

        [Test]
        public void MissingPairAndDuplicateIndexFailBeforeStatisticsCanRun()
        {
            var calculator = new ConfirmatorySignificanceCalculator(contract);
            ConfirmatoryScorePair[] nine = Enumerable.Range(1, 9).Select(index => new ConfirmatoryScorePair(index, 80d, 75d)).ToArray();
            ConfirmatoryScorePair[] duplicate = Enumerable.Range(1, 10).Select(index => new ConfirmatoryScorePair(index == 10 ? 9 : index, 80d, 75d)).ToArray();
            Assert.That(() => calculator.Calculate(nine), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => calculator.Calculate(duplicate), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void IncompleteBatteryRejectsWholeResultWithoutPartialPersistence()
        {
            ConfirmatoryCandidateSelection selection = workflow.SelectTopTwo(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseOne);
            List<CompletedConfirmatoryPair> completed = LaunchAndComplete(selection, false).ToList();
            Assert.That(() => workflow.CompleteAndPersist(selection, completed), Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_tests;"), Is.Zero);
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_test_pairs;"), Is.Zero);
        }

        [Test]
        public void SuccessfulEvidenceCannotBeReusedInAnotherSignificanceTest()
        {
            ConfirmatoryCandidateSelection selection = workflow.SelectTopTwo(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseOne);
            IReadOnlyList<CompletedConfirmatoryPair> completed = LaunchAndComplete(selection, true);
            workflow.CompleteAndPersist(selection, completed);
            Assert.That(() => workflow.CompleteAndPersist(selection, completed), Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_tests;"), Is.EqualTo(1));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_test_pairs;"), Is.EqualTo(10));
        }

        [Test]
        public void ScoreThatDoesNotMatchItsPersistedBatteryAggregateIsRejected()
        {
            ConfirmatoryCandidateSelection selection = workflow.SelectTopTwo(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseOne);
            List<CompletedConfirmatoryPair> completed = LaunchAndComplete(selection, true).ToList();
            CompletedConfirmatoryPair original = completed[0];
            completed[0] = new CompletedConfirmatoryPair(original.Launch, 81d, original.CandidateBScore);
            Assert.That(() => workflow.CompleteAndPersist(selection, completed), Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_tests;"), Is.Zero);
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM significance_test_pairs;"), Is.Zero);
        }

        private IReadOnlyList<CompletedConfirmatoryPair> LaunchAndComplete(ConfirmatoryCandidateSelection selection, bool completeLastB)
        {
            var completed = new List<CompletedConfirmatoryPair>();
            for (int index = 1; index <= contract.FreshPairsRequired; index++)
            {
                ConfirmatoryPairLaunch launch = workflow.LaunchPair(selection, index, "2026-07-17");
                CompleteBattery(launch.CandidateABatteryId);
                PersistBatteryScore(launch.CandidateABatteryId, 80d);
                if (completeLastB || index < contract.FreshPairsRequired)
                {
                    CompleteBattery(launch.CandidateBBatteryId);
                    PersistBatteryScore(launch.CandidateBBatteryId, 75d);
                }
                completed.Add(new CompletedConfirmatoryPair(launch, 80d, 75d));
            }
            return completed;
        }

        private void CompleteBattery(long batteryId)
        {
            using SqliteDatabaseConnection connection = connections.Open();
            foreach (string mode in new[] { "flick_close", "flick_far", "tracking", "micro_correction" })
                connection.Execute(@"INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_score_change_percentage,fatigue_flag)
VALUES (@profile_id,@battery_id,@config_id,'2026-07-17',@mode,1,NULL,0);", new Dictionary<string, object>
                {
                    ["@profile_id"] = profile.Id.Value, ["@battery_id"] = batteryId, ["@config_id"] = configId, ["@mode"] = mode
                });
            connection.Execute("UPDATE protocol_batteries SET completed_date='2026-07-17' WHERE id=@id;",
                new Dictionary<string, object> { ["@id"] = batteryId });
        }

        private void SeedExploratoryCandidate(ProtocolRepository protocol, double edpi, double sensitivity, double score)
        {
            ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(
                new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, (long)ProtocolPhase.PhaseOne,
                    edpi, sensitivity, "phase1_offsets", "2026-07-17"),
                new[] { new ProtocolCandidateSourceRecord(280d, 0d, edpi, false) });
            ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value,
                cycle.Id.Value, candidate.Id.Value, sensitivity, (long)ProtocolPhase.PhaseOne,
                "exploratory", "2026-07-17", null));
            CompleteBattery(battery.Id.Value);
            new SensitivityTestRepository(connections).Create(new SensitivityTestRecord(null, profile.Id.Value,
                cycle.Id.Value, configId, battery.Id.Value, edpi, 46.384d, score, "{}", null,
                configuration.FormulaVersion.Value, (long)ProtocolPhase.PhaseOne, 1));
            Assert.That(candidate.Id, Is.Not.Null);
        }

        private void PersistBatteryScore(long batteryId, double score)
        {
            using SqliteDatabaseConnection connection = connections.Open();
            double edpi = Convert.ToDouble(connection.Scalar(@"SELECT c.edpi FROM protocol_batteries b
JOIN protocol_candidates c ON c.id=b.candidate_id WHERE b.id=@id;", new Dictionary<string, object> { ["@id"] = batteryId }));
            new SensitivityTestRepository(connections).Create(new SensitivityTestRecord(null, profile.Id.Value,
                cycle.Id.Value, configId, batteryId, edpi, 46.384d, score, "{}", null,
                configuration.FormulaVersion.Value, (long)ProtocolPhase.PhaseOne, 1));
        }

        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
