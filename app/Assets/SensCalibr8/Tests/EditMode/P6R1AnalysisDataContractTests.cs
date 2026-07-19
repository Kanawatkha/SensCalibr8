using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;
using SensCalibr8.Services.Configuration;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P6R1AnalysisDataContractTests
    {
        private string tempDirectory;
        private SqliteConnectionFactory connections;
        private FrozenCalibrationConfiguration configuration;
        private long configurationId;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p6r1-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            string databasePath = Path.Combine(tempDirectory, "analysis.sqlite3");
            string nativeLibrary = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibrary);
            connections = new SqliteConnectionFactory(databasePath, nativeLibrary);
            configurationId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void VersionedDatasetKeepsProfilesIsolatedAndPreservesAuthoritativeScoreLineage()
        {
            ProfileRecord first = CreateProfile("analysis-first");
            ProfileRecord second = CreateProfile("analysis-second");
            BatteryFixture complete = CreateBattery(first, true);
            BatteryFixture incomplete = CreateAdditionalBattery(first, complete.Cycle, complete.Candidate, false);
            CreateScore(complete, 77d);
            CreateScore(incomplete, 99d);
            CreateOutlierAggregate(first, complete.Cycle.Id.Value);

            var service = new AnalysisDatasetService(new AnalysisReadRepository(connections));
            AnalysisProfileDataset dataset = service.ReadProfileDataset(first.Id.Value);
            AnalysisProfileDataset other = service.ReadProfileDataset(second.Id.Value);

            Assert.That(dataset.AnalysisDatasetVersion, Is.EqualTo(AnalysisDatasetContract.Version));
            Assert.That(dataset.Profile.ProfileId, Is.EqualTo(first.Id.Value));
            Assert.That(dataset.Profile.MouseDpi, Is.EqualTo(1600));
            Assert.That(dataset.EdpiUnit, Is.EqualTo(AnalysisDatasetContract.EdpiUnit));
            Assert.That(dataset.Cm360Unit, Is.EqualTo(AnalysisDatasetContract.Cm360Unit));
            Assert.That(dataset.PerformanceScoreUnit, Is.EqualTo(AnalysisDatasetContract.PerformanceScoreUnit));
            Assert.That(dataset.ReactionTimeUnit, Is.EqualTo(AnalysisDatasetContract.ReactionTimeUnit));
            Assert.That(dataset.AuthoritativeScores.Count, Is.EqualTo(1));
            Assert.That(dataset.AuthoritativeScores[0].PerformanceScore, Is.EqualTo(77d).Within(1e-12));
            Assert.That(dataset.AuthoritativeScores[0].FormulaVersion, Is.EqualTo("sc8-performance-score-v1"));
            Assert.That(dataset.AuthoritativeScores[0].CalibrationConfigurationVersion, Is.EqualTo(configuration.ConfigVersion.Value));
            Assert.That(dataset.Sessions.Count, Is.EqualTo(5));
            Assert.That(dataset.Sessions.Count(value => value.IsCompleteBattery), Is.EqualTo(4));
            Assert.That(dataset.Sessions.Count(value => !value.IsCompleteBattery), Is.EqualTo(1));
            Assert.That(dataset.OutlierAggregates.Count, Is.EqualTo(1));
            Assert.That(dataset.OutlierAggregates[0].InclusiveMean, Is.EqualTo(77d).Within(1e-12));
            Assert.That(dataset.OutlierAggregates[0].FlaggedExcludedMean, Is.EqualTo(80d).Within(1e-12));
            Assert.That(other.AuthoritativeScores.Count, Is.Zero);
            Assert.That(other.Sessions.Count, Is.Zero);
            Assert.That(other.OutlierAggregates.Count, Is.Zero);
            Assert.That(service.SerializeProfileDataset(first.Id.Value), Does.Contain("\"analysisDatasetVersion\""));
        }

        [Test]
        public void UnfinalizedAdaptationEvidenceFailsClosedUntilTheCaptureIsFinalized()
        {
            ProfileRecord profile = CreateProfile("analysis-pending");
            BatteryFixture incomplete = CreateBattery(profile, false);
            long sessionId = incomplete.SessionIds[0];
            using (SqliteDatabaseConnection connection = connections.Open())
            {
                connection.Execute(@"INSERT INTO shots(session_id,profile_id,target_id,distance_zone,target_size,spawn_position,
spawn_timestamp,resolution_timestamp,is_hit,outcome_reason,final_aim_position,is_adaptation_shot,sensitivity_value,
initial_offset_distance,final_precision_error,is_center_hit)
VALUES(@session,@profile,1,'close','small','0,0',0,1,1,'hit','0,0',NULL,0.175,0,0,1);",
                    new Dictionary<string, object> { ["@session"] = sessionId, ["@profile"] = profile.Id.Value });
            }
            var repository = new AnalysisReadRepository(connections);
            Assert.That(() => repository.LoadProfileDataset(profile.Id.Value), Throws.TypeOf<DataAccessException>());
            using (SqliteDatabaseConnection connection = connections.Open())
                connection.Execute("UPDATE shots SET is_adaptation_shot=0 WHERE session_id=@session;",
                    new Dictionary<string, object> { ["@session"] = sessionId });
            Assert.That(repository.LoadProfileDataset(profile.Id.Value).Sessions.Count, Is.EqualTo(1));
        }

        private ProfileRecord CreateProfile(string name)
        {
            return new ProfileRepository(connections).Create(new ProfileRecord(null, name, "2026-07-17", 1600, 0.175d,
                1000d, "right", "#FFE600", "claw", "arm", 50d, 50d, 1d, "2026-07-17"));
        }

        private BatteryFixture CreateBattery(ProfileRecord profile, bool complete)
        {
            var protocol = new ProtocolRepository(connections);
            CycleRecord cycle = protocol.CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-17", null, null));
            ProtocolCandidateRecord candidate = protocol.CreateCandidateWithSources(
                new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, 1, 280d, 0.175d, "phase1_offsets", "2026-07-17"),
                new[] { new ProtocolCandidateSourceRecord(280d, 0d, 280d, false) });
            return CreateAdditionalBattery(profile, cycle, candidate, complete);
        }

        private BatteryFixture CreateAdditionalBattery(ProfileRecord profile, CycleRecord cycle, ProtocolCandidateRecord candidate, bool complete)
        {
            var protocol = new ProtocolRepository(connections);
            ProtocolBatteryRecord battery = protocol.CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value, cycle.Id.Value,
                candidate.Id.Value, 0.175d, 1, "exploratory", "2026-07-17", complete ? "2026-07-17" : null));
            string[] modes = complete
                ? new[] { "flick_close", "flick_far", "tracking", "micro_correction" }
                : new[] { "flick_close" };
            var sessionIds = new List<long>();
            using (SqliteDatabaseConnection connection = connections.Open())
            {
                foreach (string mode in modes)
                {
                    connection.Execute(@"INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag)
VALUES(@profile,@battery,@config,'2026-07-17',@mode,1,0);",
                        new Dictionary<string, object>
                        {
                            ["@profile"] = profile.Id.Value, ["@battery"] = battery.Id.Value, ["@config"] = configurationId, ["@mode"] = mode
                        });
                    sessionIds.Add(connection.LastInsertRowId());
                }
            }
            return new BatteryFixture(cycle, candidate, battery, sessionIds);
        }

        private void CreateScore(BatteryFixture fixture, double score)
        {
            using SqliteDatabaseConnection connection = connections.Open();
            connection.Execute(@"INSERT INTO sensitivity_tests(profile_id,cycle_id,calibration_config_id,battery_id,edpi,cm_360,
avg_performance_score,performance_score_by_mode,grade,formula_version,phase,sample_size)
SELECT b.profile_id,b.cycle_id,@config,b.id,280,46.384,@score,'{}','A','sc8-performance-score-v1',1,1
FROM protocol_batteries b WHERE b.id=@battery;",
                new Dictionary<string, object> { ["@config"] = configurationId, ["@score"] = score, ["@battery"] = fixture.Battery.Id.Value });
        }

        private void CreateOutlierAggregate(ProfileRecord profile, long cycleId)
        {
            using SqliteDatabaseConnection connection = connections.Open();
            connection.Execute(@"INSERT INTO outlier_analysis_runs(profile_id,cycle_id,calibration_config_id,phase,mode,sensitivity_value,
metric_name,scope_key,group_mean,sample_sd,threshold_value,inclusive_mean,flagged_excluded_mean,observation_count,flagged_count,algorithm_version)
VALUES(@profile,@cycle,@config,1,'flick_close',0.175,'reaction_time_ms','analysis-scope',77,1,3,77,80,2,1,'sc8-outlier-v1');",
                new Dictionary<string, object> { ["@profile"] = profile.Id.Value, ["@cycle"] = cycleId, ["@config"] = configurationId });
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class BatteryFixture
        {
            public BatteryFixture(CycleRecord cycle, ProtocolCandidateRecord candidate, ProtocolBatteryRecord battery, IReadOnlyList<long> sessionIds)
            {
                Cycle = cycle;
                Candidate = candidate;
                Battery = battery;
                SessionIds = sessionIds;
            }

            public CycleRecord Cycle { get; }
            public ProtocolCandidateRecord Candidate { get; }
            public ProtocolBatteryRecord Battery { get; }
            public IReadOnlyList<long> SessionIds { get; }
        }
    }
}
