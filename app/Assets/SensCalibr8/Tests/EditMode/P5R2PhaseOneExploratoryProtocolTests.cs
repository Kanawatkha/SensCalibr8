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
    public sealed class P5R2PhaseOneExploratoryProtocolTests
    {
        private string directory;
        private FrozenCalibrationConfiguration configuration;
        private ResearchConstants research;
        private ProtocolConstants protocolConstants;
        private SqliteConnectionFactory connections;
        private ProtocolRepository protocolRepository;
        private PhaseOneExploratoryProtocolService protocolService;
        private ProfileRecord profile;

        [SetUp]
        public void SetUp()
        {
            string root = RepositoryRoot();
            directory = Path.Combine(Path.GetTempPath(), "senscalibr8-p5r2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(root);
            research = ResearchConstantsLoader.LoadFromRepository(root);
            protocolConstants = ProtocolConstantsLoader.LoadFromRepository(root);
            string database = Path.Combine(directory, "phase-one.sqlite3");
            string native = Path.Combine(root, "app", "Assets", "Plugins", "sqlite3.dll");
            new SqliteDatabaseBootstrapper().Initialize(database, configuration, native);
            connections = new SqliteConnectionFactory(database, native);
            protocolRepository = new ProtocolRepository(connections);
            protocolService = new PhaseOneExploratoryProtocolService(research, protocolConstants, protocolRepository);
            profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "phase-one-profile", "2026-07-16",
                1600, 0.175d, 1000d, "right", "#FFE600", "claw", "arm", 50d, 50d, 1d, "2026-07-16"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void AcceptedProtocolConstantsAreImmutableAndComplete()
        {
            Assert.That(protocolConstants.Version, Is.EqualTo("sc8-protocol-constants-v1"));
            Assert.That(protocolConstants.PhaseOneCandidateCount, Is.EqualTo(7));
            Assert.That(protocolConstants.PhaseOneOffsetsPercent, Is.EqualTo(new[] { -20d, -10d, -5d, 0d, 5d, 10d, 20d }));
            Assert.That(typeof(ProtocolConstants).GetProperties().All(value => !value.CanWrite), Is.True);
        }

        [Test]
        public void PsaBaselineAtDpi1600ProducesTheSevenExactWorkedValuesWithoutIntermediateRounding()
        {
            PhaseOneCandidateGeneration generated = protocolService.Generate(1600);
            Assert.That(generated.Candidates.Select(value => value.Edpi),
                Is.EqualTo(new[] { 224d, 252d, 266d, 280d, 294d, 308d, 336d }).Within(1e-12));
            Assert.That(generated.Candidates.Select(value => value.Sensitivity),
                Is.EqualTo(new[] { 0.14d, 0.1575d, 0.16625d, 0.175d, 0.18375d, 0.1925d, 0.21d }).Within(1e-12));
            Assert.That(generated.FloorNotifications, Is.Empty);
        }

        [Test]
        public void EdpiFloorDeduplicatesEffectiveCandidatesAndPreservesEverySource()
        {
            PhaseOneCandidateGeneration generated = protocolService.GenerateAround(170d, 1600);
            PhaseOneCandidateDefinition floor = generated.Candidates.Single(value => value.Edpi == research.EdpiFloor);
            Assert.That(generated.Candidates.Count, Is.EqualTo(6));
            Assert.That(floor.Sources.Select(value => value.OffsetPercent), Is.EqualTo(new[] { -20d, -10d }));
            Assert.That(floor.Sources.Select(value => value.PreFloorEdpi), Is.EqualTo(new[] { 136d, 153d }).Within(1e-12));
            Assert.That(floor.Sources.All(value => value.FloorApplied), Is.True);
            Assert.That(generated.FloorNotifications.Count, Is.EqualTo(2));
            Assert.That(generated.FloorNotifications.All(value => value.AdjustedEdpi == research.EdpiFloor), Is.True);
        }

        [Test]
        public void WorkflowPersistsCanonicalCandidatesAndExposesOnlyBlindLabelsToLaunchSurface()
        {
            PhaseOneExploratoryWorkflow workflow = Workflow();
            PhaseOneExploratoryPlan plan = workflow.CreatePlan(profile.Id.Value, 1, 1600, "2026-07-16");

            Assert.That(plan.Candidates.Select(value => value.BlindLabel),
                Is.EqualTo(Enumerable.Range(1, protocolConstants.PhaseOneCandidateCount).Select(value => "Candidate-" + value.ToString("D2"))));
            Assert.That(plan.Candidates.Select(value => value.CandidateId).Distinct().Count(), Is.EqualTo(protocolConstants.PhaseOneCandidateCount));
            Assert.That(typeof(BlindPhaseOneCandidate).GetProperty("SensitivityValue"), Is.Null);
            Assert.That(typeof(BlindPhaseOneCandidate).GetProperty("Edpi"), Is.Null);
            Assert.That(typeof(PhaseOneBatteryLaunch).GetProperty("SensitivityValue"), Is.Null);

            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidates;"), Is.EqualTo(7));
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources;"), Is.EqualTo(7));
            Assert.That(Convert.ToString(connection.Scalar("SELECT generation_rule FROM protocol_candidates LIMIT 1;")),
                Is.EqualTo(protocolConstants.PhaseOneGenerationRule));
        }

        [Test]
        public void LaunchCreatesExploratoryBatteryAndCounterbalancesAllFourModes()
        {
            PhaseOneExploratoryWorkflow workflow = Workflow();
            PhaseOneExploratoryPlan plan = workflow.CreatePlan(profile.Id.Value, 1, 1600, "2026-07-16");
            string label = plan.Candidates[0].BlindLabel;
            var repetitions = new List<IReadOnlyList<TestMode>>();
            for (int repetition = 1; repetition <= 4; repetition++)
                repetitions.Add(workflow.Launch(plan, label, repetition, "2026-07-16").OrderedModes);

            Assert.That(repetitions.All(order => order.Distinct().Count() == 4), Is.True);
            for (int position = 0; position < 4; position++)
                Assert.That(repetitions.Select(order => order[position]).Distinct().Count(), Is.EqualTo(4));
            Assert.That(() => workflow.Launch(plan, label, 1, "2026-07-16"), Throws.TypeOf<InvalidOperationException>());
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_batteries WHERE purpose='exploratory';"), Is.EqualTo(4));
        }

        [Test]
        public void SampleCompletionContractComesFromFrozenAcceptedConfiguration()
        {
            PhaseOneSampleContract contract = PhaseOneSampleContract.From(configuration);
            Assert.That(contract.ShotTotal, Is.EqualTo(30));
            Assert.That(contract.ShotAdaptation, Is.EqualTo(15));
            Assert.That(contract.ShotAuthoritative, Is.EqualTo(15));
            Assert.That(contract.TrackingTrials, Is.EqualTo(18));
            Assert.That(contract.TrackingAdaptationTrials, Is.EqualTo(9));
            Assert.That(contract.TrackingWindows, Is.EqualTo(108));
            Assert.That(contract.TrackingAuthoritativeWindows, Is.EqualTo(54));
            Assert.That(contract.IsSatisfied(TestMode.FlickClose, 30, 15, 15), Is.True);
            Assert.That(contract.IsSatisfied(TestMode.FlickFar, 30, 15, 15), Is.True);
            Assert.That(contract.IsSatisfied(TestMode.MicroCorrection, 30, 15, 15), Is.True);
            Assert.That(contract.IsSatisfied(TestMode.Tracking, 18, 9, 9), Is.True);
            Assert.That(contract.TrackingWindowsSatisfied(108, 54), Is.True);
            Assert.That(contract.IsSatisfied(TestMode.FlickClose, 29, 15, 14), Is.False);
            Assert.That(contract.TrackingWindowsSatisfied(107, 54), Is.False);
        }

        [Test]
        public void CandidateBatchFailureRollsBackEveryCandidateAndSource()
        {
            CycleRecord cycle = protocolRepository.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-16", null, null));
            ProtocolCandidateCreateRequest request = CandidateRequest(cycle.Id.Value, 280d, 0d);
            Assert.That(() => protocolRepository.CreateCandidateSetWithSources(new[] { request, request }),
                Throws.TypeOf<DataAccessException>());
            using SqliteDatabaseConnection connection = connections.Open();
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidates;"), Is.Zero);
            Assert.That(Scalar(connection, "SELECT COUNT(*) FROM protocol_candidate_sources;"), Is.Zero);
        }

        private PhaseOneExploratoryWorkflow Workflow()
        {
            return new PhaseOneExploratoryWorkflow(protocolService,
                new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)));
        }

        private ProtocolCandidateCreateRequest CandidateRequest(long cycleId, double edpi, double offset)
        {
            return new ProtocolCandidateCreateRequest(
                new ProtocolCandidateRecord(null, profile.Id.Value, cycleId, (long)ProtocolPhase.PhaseOne,
                    edpi, edpi / profile.MouseDpi, protocolConstants.PhaseOneGenerationRule, "2026-07-16"),
                new[] { new ProtocolCandidateSourceRecord(research.PsaBaselineEdpi, offset, edpi, false) });
        }

        private static int Scalar(SqliteDatabaseConnection connection, string sql) => Convert.ToInt32(connection.Scalar(sql));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
