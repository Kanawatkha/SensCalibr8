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
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P5R5PhaseThreeFinalNarrowingTests
    {
        private string directory;
        private FrozenCalibrationConfiguration configuration;
        private PhaseTwoProtocolContract phaseTwoContract;
        private PhaseThreeProtocolContract phaseThreeContract;
        private SqliteConnectionFactory connections;
        private ProtocolRepository protocolRepository;
        private NarrowingRepository narrowingRepository;
        private PhaseHistoryRepository historyRepository;
        private NarrowingStabilizationService stabilization;
        private NarrowingWinnerSelectionService winnerSelection;
        private ProfileRecord profile;
        private CycleRecord cycle;
        private long configId;

        [SetUp]
        public void SetUp()
        {
            string root = RepositoryRoot();
            directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p5r5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(root);
            phaseTwoContract = PhaseTwoProtocolContractLoader.LoadFromRepository(root);
            phaseThreeContract = PhaseThreeProtocolContractLoader.LoadFromRepository(root);
            string database = Path.Combine(directory, "final-narrowing.sqlite3");
            string native = Path.Combine(root, "app", "Assets", "Plugins", "sqlite3.dll");
            new SqliteDatabaseBootstrapper().Initialize(database, configuration, native);
            connections = new SqliteConnectionFactory(database, native);
            protocolRepository = new ProtocolRepository(connections);
            narrowingRepository = new NarrowingRepository(connections);
            historyRepository = new PhaseHistoryRepository(connections);
            profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "final-narrowing-profile",
                "2026-07-17", 1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm",
                50d, 50d, 1d, "2026-07-17"));
            cycle = protocolRepository.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-17", null, null));
            configId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
            stabilization = new NarrowingStabilizationService(narrowingRepository,
                new CalibrationConfigurationRepository(connections), configuration, phaseTwoContract);
            winnerSelection = new NarrowingWinnerSelectionService(stabilization, narrowingRepository, historyRepository);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void PhaseTwoHighestStableMeanPersistsWinnerAndExactEqualMeanDoesNot()
        {
            PhaseTwoNarrowingPlan plan = CreateAndStabilizePhaseTwo(new[] { 90d, 100d, 80d });
            NarrowingWinnerSelection selected = winnerSelection.SelectAndPersist(profile.Id.Value, cycle.Id.Value,
                ProtocolPhase.PhaseTwo, "2026-07-17T00:00:00Z");
            Assert.That(selected.HasWinner, Is.True);
            Assert.That(selected.IsExactMeanTie, Is.False);
            Assert.That(selected.PersistedWinner.WinnerEdpi, Is.EqualTo(280d).Within(1e-12));
            Assert.That(historyRepository.Require(profile.Id.Value, cycle.Id.Value, 2).WinnerEdpi, Is.EqualTo(280d).Within(1e-12));
            Assert.That(plan.Candidates.Count, Is.EqualTo(3));

            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM phase_history WHERE phase_number=2;"), Is.EqualTo(1));
        }

        [Test]
        public void ExactEqualHighestStableMeansReturnTieWithoutPersistingPhaseWinner()
        {
            CreateAndStabilizePhaseTwo(new[] { 100d, 100d, 90d });
            NarrowingWinnerSelection selected = winnerSelection.SelectAndPersist(profile.Id.Value, cycle.Id.Value,
                ProtocolPhase.PhaseTwo, "2026-07-17T00:00:00Z");
            Assert.That(selected.HasWinner, Is.False);
            Assert.That(selected.IsExactMeanTie, Is.True);
            Assert.That(selected.TiedCandidateIds.Count, Is.EqualTo(2));
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM phase_history;"), Is.Zero);
        }

        [Test]
        public void PhaseThreeCreatesExactPlusMinusFiveSetAndBlindCounterbalancedLaunch()
        {
            CreateAndStabilizePhaseTwo(new[] { 90d, 100d, 80d });
            winnerSelection.SelectAndPersist(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseTwo, "2026-07-17T00:00:00Z");
            PhaseThreeFinalPlan plan = PhaseThreeWorkflow().CreatePlan(profile.Id.Value, cycle.Id.Value, 1600, "2026-07-17");
            IReadOnlyList<ProtocolCandidateRecord> candidates = narrowingRepository.ListPhaseCandidates(profile.Id.Value, cycle.Id.Value, 3);
            Assert.That(phaseThreeContract.OffsetsPercent, Is.EqualTo(new[] { -5d, 0d, 5d }));
            Assert.That(candidates.Select(value => value.Edpi), Is.EqualTo(new[] { 266d, 280d, 294d }).Within(1e-12));
            Assert.That(candidates.Select(value => value.SensitivityValue), Is.EqualTo(new[] { 0.16625d, 0.175d, 0.18375d }).Within(1e-12));
            Assert.That(candidates.All(value => value.GenerationRule == "single_anchor"), Is.True);
            string label = plan.Candidates[0].BlindLabel;
            NarrowingBatteryLaunch launch = PhaseThreeWorkflow().Launch(plan, label, 1, "2026-07-17");
            Assert.That(launch.OrderedModes.Distinct().Count(), Is.EqualTo(4));
            Assert.That(typeof(PhaseThreeFinalPlan).GetProperty("Edpi"), Is.Null);
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Convert.ToString(connection.Scalar("SELECT purpose FROM protocol_batteries WHERE id=@id;", new Dictionary<string, object> { ["@id"] = launch.BatteryId })), Is.EqualTo("narrowing"));
        }

        [Test]
        public void PhaseThreeStableHighestMeanPersistsFinalBestWithoutChangingProfileCurrentSensitivity()
        {
            CreateAndStabilizePhaseTwo(new[] { 90d, 100d, 80d });
            winnerSelection.SelectAndPersist(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseTwo, "2026-07-17T00:00:00Z");
            PhaseThreeWorkflow().CreatePlan(profile.Id.Value, cycle.Id.Value, 1600, "2026-07-17");
            IReadOnlyList<ProtocolCandidateRecord> phaseThree = narrowingRepository.ListPhaseCandidates(profile.Id.Value, cycle.Id.Value, 3);
            foreach (ProtocolCandidateRecord candidate in phaseThree)
            {
                double score = candidate.Edpi.Equals(294d) ? 100d : 90d;
                for (int index = 0; index < phaseTwoContract.MinimumCompleteBatteries; index++) SeedScore(candidate, score);
            }
            NarrowingWinnerSelection final = winnerSelection.SelectAndPersist(profile.Id.Value, cycle.Id.Value,
                ProtocolPhase.PhaseThree, "2026-07-17T01:00:00Z");
            Assert.That(final.HasWinner, Is.True);
            Assert.That(final.PersistedWinner.WinnerEdpi, Is.EqualTo(294d).Within(1e-12));
            Assert.That(new ProfileRepository(connections).FindById(profile.Id.Value).CurrentSensitivity, Is.EqualTo(0.175d));
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM phase_history WHERE phase_number=3;"), Is.EqualTo(1));
        }

        [Test]
        public void PhaseThreeFloorDeduplicatesAndPreservesEverySource()
        {
            InsertPhaseTwoCandidate(160d, 0.1d);
            historyRepository.Create(new PhaseHistoryRecord(null, profile.Id.Value, cycle.Id.Value, 2, 160d, "2026-07-17T00:00:00Z"));
            PhaseThreeFinalPlan plan = PhaseThreeWorkflow().CreatePlan(profile.Id.Value, cycle.Id.Value, 1600, "2026-07-17");
            IReadOnlyList<ProtocolCandidateRecord> candidates = narrowingRepository.ListPhaseCandidates(profile.Id.Value, cycle.Id.Value, 3);
            Assert.That(candidates.Select(value => value.Edpi), Is.EqualTo(new[] { 160d, 168d }).Within(1e-12));
            Assert.That(plan.FloorNotifications.Count, Is.EqualTo(1));
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources WHERE candidate_id IN (SELECT id FROM protocol_candidates WHERE phase=3);"), Is.EqualTo(3));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources s JOIN protocol_candidates c ON c.id=s.candidate_id WHERE c.phase=3 AND c.edpi=160;"), Is.EqualTo(2));
        }

        [Test]
        public void WinnerSelectionRejectsUnstableOrIncompletePhaseEvidence()
        {
            InsertPhaseTwoCandidate(280d, 0.175d);
            Assert.That(() => winnerSelection.SelectAndPersist(profile.Id.Value, cycle.Id.Value, ProtocolPhase.PhaseTwo, "2026-07-17T00:00:00Z"), Throws.TypeOf<InvalidOperationException>());
        }

        private PhaseTwoNarrowingPlan CreateAndStabilizePhaseTwo(double[] candidateMeans)
        {
            InsertPhaseOneDecision();
            PhaseTwoNarrowingPlan plan = PhaseTwoWorkflow().CreatePlan(profile.Id.Value, cycle.Id.Value, 1600, "2026-07-17");
            IReadOnlyList<ProtocolCandidateRecord> candidates = narrowingRepository.ListPhaseCandidates(profile.Id.Value, cycle.Id.Value, 2);
            for (int position = 0; position < candidates.Count; position++)
                for (int index = 0; index < phaseTwoContract.MinimumCompleteBatteries; index++) SeedScore(candidates[position], candidateMeans[position]);
            return plan;
        }

        private PhaseTwoNarrowingWorkflow PhaseTwoWorkflow()
        {
            return new PhaseTwoNarrowingWorkflow(new PhaseTwoNarrowingService(ResearchConstantsLoader.LoadFromRepository(RepositoryRoot()),
                phaseTwoContract, protocolRepository, narrowingRepository), stabilization,
                new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)));
        }

        private PhaseThreeFinalNarrowingWorkflow PhaseThreeWorkflow()
        {
            return new PhaseThreeFinalNarrowingWorkflow(new PhaseThreeFinalNarrowingService(
                ResearchConstantsLoader.LoadFromRepository(RepositoryRoot()), phaseThreeContract, phaseTwoContract,
                protocolRepository, narrowingRepository, historyRepository), stabilization,
                new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)));
        }

        private void InsertPhaseOneDecision()
        {
            using SqliteDatabaseConnection connection = connections.Open();
            connection.Execute(@"INSERT INTO significance_tests(profile_id,cycle_id,calibration_config_id,phase,candidate_a_edpi,candidate_b_edpi,test_method,alternative,alpha,p_value,effect_estimate,confidence_level,confidence_interval_lower,confidence_interval_upper,paired_sample_size,is_significant,formula_version,result)
VALUES (@profile,@cycle,@config,1,280,294,'exact-sign-flip','two-sided',0.05,0.001953125,5,0.95,5,5,10,1,@formula,'candidate_a');",
                new Dictionary<string, object> { ["@profile"] = profile.Id.Value, ["@cycle"] = cycle.Id.Value,
                    ["@config"] = configId, ["@formula"] = configuration.FormulaVersion.Value });
        }

        private void InsertPhaseTwoCandidate(double edpi, double sensitivity)
        {
            protocolRepository.CreateCandidateWithSources(new ProtocolCandidateRecord(null, profile.Id.Value,
                cycle.Id.Value, 2, edpi, sensitivity, "single_anchor", "2026-07-17"),
                new[] { new ProtocolCandidateSourceRecord(edpi, 0d, edpi, false) });
        }

        private void SeedScore(ProtocolCandidateRecord candidate, double score)
        {
            ProtocolBatteryRecord battery = protocolRepository.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value,
                cycle.Id.Value, candidate.Id.Value, candidate.SensitivityValue, candidate.Phase,
                "narrowing", "2026-07-17", "2026-07-17"));
            using (SqliteDatabaseConnection connection = connections.Open())
            {
                foreach (string mode in new[] { "flick_close", "flick_far", "tracking", "micro_correction" })
                    connection.Execute(@"INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag)
VALUES (@profile,@battery,@config,'2026-07-17',@mode,1,0);", new Dictionary<string, object>
                    {
                        ["@profile"] = profile.Id.Value, ["@battery"] = battery.Id.Value,
                        ["@config"] = configId, ["@mode"] = mode
                    });
            }
            new SensitivityTestRepository(connections).Create(new SensitivityTestRecord(null, profile.Id.Value,
                cycle.Id.Value, configId, battery.Id.Value, candidate.Edpi, 46.384d, score, "{}", null,
                configuration.FormulaVersion.Value, candidate.Phase, 1));
        }

        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
