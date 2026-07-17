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
    public sealed class P5R4PhaseTwoNarrowingTests
    {
        private string directory;
        private FrozenCalibrationConfiguration configuration;
        private PhaseTwoProtocolContract contract;
        private SqliteConnectionFactory connections;
        private ProtocolRepository protocolRepository;
        private NarrowingRepository narrowingRepository;
        private NarrowingStabilizationService stabilization;
        private ProfileRecord profile;
        private CycleRecord cycle;
        private long configId;

        [SetUp]
        public void SetUp()
        {
            string root = RepositoryRoot();
            directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p5r4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(root);
            contract = PhaseTwoProtocolContractLoader.LoadFromRepository(root);
            string database = Path.Combine(directory, "narrowing.sqlite3");
            string native = Path.Combine(root, "app", "Assets", "Plugins", "sqlite3.dll");
            new SqliteDatabaseBootstrapper().Initialize(database, configuration, native);
            connections = new SqliteConnectionFactory(database, native);
            protocolRepository = new ProtocolRepository(connections);
            narrowingRepository = new NarrowingRepository(connections);
            profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "narrowing-profile",
                "2026-07-17", 1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm",
                50d, 50d, 1d, "2026-07-17"));
            cycle = protocolRepository.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-17", null, null));
            configId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
            stabilization = new NarrowingStabilizationService(narrowingRepository,
                new CalibrationConfigurationRepository(connections), configuration, contract);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void AcceptedContractIsImmutableAndMatchesResearchBoundaries()
        {
            Assert.That(contract.Version, Is.EqualTo("sc8-phase-two-protocol-v1"));
            Assert.That(contract.OffsetsPercent, Is.EqualTo(new[] { -10d, 0d, 10d }));
            Assert.That(contract.MinimumCompleteBatteries, Is.EqualTo(5));
            Assert.That(contract.MaximumCompleteBatteries, Is.EqualTo(10));
            Assert.That(contract.StabilizationCvExclusiveUpper, Is.EqualTo(10d));
            Assert.That(typeof(PhaseTwoProtocolContract).GetProperties().All(value => !value.CanWrite), Is.True);
        }

        [Test]
        public void SignificantWinnerGeneratesExactThreeValueNarrowingSetWithoutRounding()
        {
            InsertDecision("candidate_a", 280d, 294d);
            PhaseTwoNarrowingPlan plan = Workflow().CreatePlan(profile.Id.Value, cycle.Id.Value, 1600, "2026-07-17");
            IReadOnlyList<ProtocolCandidateRecord> candidates = narrowingRepository.ListPhaseTwoCandidates(profile.Id.Value, cycle.Id.Value);
            Assert.That(candidates.Select(value => value.Edpi), Is.EqualTo(new[] { 252d, 280d, 308d }).Within(1e-12));
            Assert.That(candidates.Select(value => value.SensitivityValue), Is.EqualTo(new[] { 0.1575d, 0.175d, 0.1925d }).Within(1e-12));
            Assert.That(candidates.All(value => value.GenerationRule == "single_anchor"), Is.True);
            Assert.That(plan.FloorNotifications, Is.Empty);
            Assert.That(typeof(BlindNarrowingCandidate).GetProperty("Edpi"), Is.Null);
            Assert.That(typeof(BlindNarrowingCandidate).GetProperty("SensitivityValue"), Is.Null);
        }

        [Test]
        public void StatisticalTieAppliesFloorBeforeDeduplicationAndRetainsAllSixSources()
        {
            InsertDecision("statistical_tie", 170d, 160d);
            PhaseTwoNarrowingPlan plan = Workflow().CreatePlan(profile.Id.Value, cycle.Id.Value, 1600, "2026-07-17");
            IReadOnlyList<ProtocolCandidateRecord> candidates = narrowingRepository.ListPhaseTwoCandidates(profile.Id.Value, cycle.Id.Value);
            Assert.That(candidates.Select(value => value.Edpi), Is.EqualTo(new[] { 160d, 170d, 176d, 187d }).Within(1e-12));
            Assert.That(candidates.All(value => value.GenerationRule == "tie_union"), Is.True);
            Assert.That(plan.FloorNotifications.Count, Is.EqualTo(2));
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources WHERE candidate_id IN (SELECT id FROM protocol_candidates WHERE phase=2);"), Is.EqualTo(6));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources s JOIN protocol_candidates c ON c.id=s.candidate_id WHERE c.phase=2 AND c.edpi=160;"), Is.EqualTo(3));
        }

        [Test]
        public void FiveBatteryWorkedExampleProducesExactFivePercentCvAndStabilizes()
        {
            CreateNormalPlan();
            foreach (ProtocolCandidateRecord candidate in Candidates())
            {
                foreach (double score in new[] { 95d, 95d, 100d, 105d, 105d }) SeedScore(candidate, score);
            }
            PhaseTwoStabilizationEvaluation result = stabilization.Evaluate(profile.Id.Value, cycle.Id.Value);
            Assert.That(result.AllCandidatesStabilized, Is.True);
            Assert.That(result.Candidates.All(value => value.CompletedBatteryCount == 5), Is.True);
            Assert.That(result.Candidates.All(value => Math.Abs(value.SampleStandardDeviation.Value - 5d) < 1e-12), Is.True);
            Assert.That(result.Candidates.All(value => Math.Abs(value.CoefficientOfVariationPercent.Value - 5d) < 1e-12), Is.True);
        }

        [Test]
        public void BlindLaunchCreatesOnlyNarrowingBatteryAndCounterbalancedFourModeOrder()
        {
            PhaseTwoNarrowingPlan plan = CreateNormalPlan();
            PhaseTwoNarrowingWorkflow workflow = Workflow();
            string label = plan.Candidates[0].BlindLabel;
            NarrowingBatteryLaunch launch = workflow.Launch(plan, label, 1, "2026-07-17");
            Assert.That(launch.BlindLabel, Is.EqualTo(label));
            Assert.That(launch.OrderedModes.Distinct().Count(), Is.EqualTo(4));
            Assert.That(typeof(NarrowingBatteryLaunch).GetProperty("SensitivityValue"), Is.Null);
            Assert.That(() => workflow.Launch(plan, label, 1, "2026-07-17"), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => workflow.Launch(plan, label, 11, "2026-07-17"), Throws.TypeOf<ArgumentOutOfRangeException>());
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Convert.ToString(connection.Scalar("SELECT purpose FROM protocol_batteries WHERE id=@id;",
                new Dictionary<string, object> { ["@id"] = launch.BatteryId })), Is.EqualTo("narrowing"));
        }

        [Test]
        public void CvAtTenPercentIsExclusiveAndRequiresMoreEvidence()
        {
            CreateNormalPlan();
            ProtocolCandidateRecord target = Candidates()[0];
            foreach (double score in new[] { 90d, 90d, 100d, 110d, 110d }) SeedScore(target, score);
            NarrowingCandidateEvaluation result = stabilization.Evaluate(profile.Id.Value, cycle.Id.Value)
                .Candidates.Single(value => value.CandidateId == target.Id.Value);
            Assert.That(result.CoefficientOfVariationPercent, Is.EqualTo(10d).Within(1e-12));
            Assert.That(result.State, Is.EqualTo(NarrowingStabilizationState.RequiresMoreEvidence));
            Assert.That(result.CanCollectAnother, Is.True);
        }

        [Test]
        public void NearZeroMeanIsUndefinedAndIncompleteBatteryDoesNotCount()
        {
            CreateNormalPlan();
            ProtocolCandidateRecord target = Candidates()[0];
            foreach (double score in new[] { -2d, -1d, 0d, 1d, 2d }) SeedScore(target, score);
            SeedScore(target, 100d, false);
            NarrowingCandidateEvaluation result = stabilization.Evaluate(profile.Id.Value, cycle.Id.Value)
                .Candidates.Single(value => value.CandidateId == target.Id.Value);
            Assert.That(result.CompletedBatteryCount, Is.EqualTo(5));
            Assert.That(result.MeanScore, Is.Zero.Within(1e-12));
            Assert.That(result.CoefficientOfVariationPercent, Is.Null);
            Assert.That(result.State, Is.EqualTo(NarrowingStabilizationState.RequiresMoreEvidence));
        }

        [Test]
        public void TenUnstableBatteriesReachMaximumFailureAndBlockFurtherLaunch()
        {
            PhaseTwoNarrowingPlan plan = CreateNormalPlan();
            ProtocolCandidateRecord target = Candidates()[0];
            for (int index = 0; index < contract.MaximumCompleteBatteries; index++) SeedScore(target, index % 2 == 0 ? 0d : 200d);
            NarrowingCandidateEvaluation result = stabilization.Evaluate(profile.Id.Value, cycle.Id.Value)
                .Candidates.Single(value => value.CandidateId == target.Id.Value);
            Assert.That(result.State, Is.EqualTo(NarrowingStabilizationState.MaximumReachedWithoutStabilization));
            string label = plan.Candidates.Single(value => value.CandidateId == target.Id.Value).BlindLabel;
            Assert.That(() => Workflow().Launch(plan, label, 10, "2026-07-17"), Throws.TypeOf<InvalidOperationException>());
        }

        private PhaseTwoNarrowingPlan CreateNormalPlan()
        {
            InsertDecision("candidate_a", 280d, 294d);
            return Workflow().CreatePlan(profile.Id.Value, cycle.Id.Value, 1600, "2026-07-17");
        }

        private PhaseTwoNarrowingWorkflow Workflow()
        {
            var service = new PhaseTwoNarrowingService(ResearchConstantsLoader.LoadFromRepository(RepositoryRoot()),
                contract, protocolRepository, narrowingRepository);
            return new PhaseTwoNarrowingWorkflow(service, stabilization,
                new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)));
        }

        private IReadOnlyList<ProtocolCandidateRecord> Candidates() => narrowingRepository.ListPhaseTwoCandidates(profile.Id.Value, cycle.Id.Value);

        private void InsertDecision(string result, double candidateA, double candidateB)
        {
            bool significant = result != "statistical_tie";
            double effect = result == "candidate_a" ? 5d : result == "candidate_b" ? -5d : 0d;
            using SqliteDatabaseConnection connection = connections.Open();
            connection.Execute(@"INSERT INTO significance_tests(profile_id,cycle_id,calibration_config_id,phase,candidate_a_edpi,candidate_b_edpi,test_method,alternative,alpha,p_value,effect_estimate,confidence_level,confidence_interval_lower,confidence_interval_upper,paired_sample_size,is_significant,formula_version,result)
VALUES (@profile,@cycle,@config,1,@a,@b,'exact-sign-flip','two-sided',0.05,@p,@effect,0.95,@effect,@effect,10,@significant,@formula,@result);",
                new Dictionary<string, object> { ["@profile"] = profile.Id.Value, ["@cycle"] = cycle.Id.Value,
                    ["@config"] = configId, ["@a"] = candidateA, ["@b"] = candidateB,
                    ["@p"] = significant ? 0.001953125d : 1d, ["@effect"] = effect,
                    ["@significant"] = significant, ["@formula"] = configuration.FormulaVersion.Value, ["@result"] = result });
        }

        private void SeedScore(ProtocolCandidateRecord candidate, double score, bool complete = true)
        {
            ProtocolBatteryRecord battery = protocolRepository.CreateBattery(new ProtocolBatteryRecord(null,
                profile.Id.Value, cycle.Id.Value, candidate.Id.Value, candidate.SensitivityValue,
                (long)ProtocolPhase.PhaseTwo, "narrowing", "2026-07-17", complete ? "2026-07-17" : null));
            if (!complete) return;
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
                configuration.FormulaVersion.Value, (long)ProtocolPhase.PhaseTwo, 1));
        }

        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
