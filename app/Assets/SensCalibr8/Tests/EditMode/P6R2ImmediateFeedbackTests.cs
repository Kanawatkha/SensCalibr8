using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P6R2ImmediateFeedbackTests
    {
        private string tempDirectory;
        private SqliteConnectionFactory connections;
        private FrozenCalibrationConfiguration configuration;
        private ResearchConstants constants;
        private long configurationId;
        private ProfileRecord profile;
        private CycleRecord cycle;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p6r2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            constants = ResearchConstantsLoader.LoadFromRepository(RepositoryRoot());
            string databasePath = Path.Combine(tempDirectory, "feedback.sqlite3");
            string nativeLibrary = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibrary);
            connections = new SqliteConnectionFactory(databasePath, nativeLibrary);
            configurationId = new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
            profile = new ProfileRepository(connections).Create(new ProfileRecord(null, "feedback-profile", "2026-07-17", 1600,
                0.175d, 1000d, "right", "#FFE600", "claw", "arm", 50d, 50d, 1d, "2026-07-17"));
            cycle = new ProtocolRepository(connections).CreateCycle(new CycleRecord(null, profile.Id.Value, 1, "2026-07-17", null, null));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void CurrentProtocolAccuracyChartUsesOnlyPostAdaptationEvidenceAndServiceComputedPercentages()
        {
            CreateShotSession(1, 0.15d, new[] { true, true });
            CreateShotSession(1, 0.175d, new[] { true, false });

            ImmediateFeedbackPresentation feedback = new ImmediateFeedbackService(new ImmediateFeedbackRepository(connections)).Load(profile.Id.Value);

            Assert.That(feedback.CurrentMode, Is.EqualTo("flick_close"));
            Assert.That(feedback.CurrentCycleId, Is.EqualTo(cycle.Id.Value));
            Assert.That(feedback.CurrentPhase, Is.EqualTo(1));
            Assert.That(feedback.AccuracyBars.Count, Is.EqualTo(2));
            Assert.That(feedback.AccuracyBars[0].SensitivityValue, Is.EqualTo(0.15d).Within(1e-12));
            Assert.That(feedback.AccuracyBars[0].HitCount, Is.EqualTo(2));
            Assert.That(feedback.AccuracyBars[0].ResolvedCount, Is.EqualTo(2));
            Assert.That(feedback.AccuracyBars[0].AccuracyPercent, Is.EqualTo(100d).Within(1e-12));
            Assert.That(feedback.AccuracyBars[0].FillFraction, Is.EqualTo(1d).Within(1e-12));
            Assert.That(feedback.AccuracyBars[1].SensitivityValue, Is.EqualTo(0.175d).Within(1e-12));
            Assert.That(feedback.AccuracyBars[1].AccuracyPercent, Is.EqualTo(50d).Within(1e-12));
            Assert.That(feedback.AccuracyBars[1].FillFraction, Is.EqualTo(0.5d).Within(1e-12));
        }

        [Test]
        public void DashboardUsesPersistedWinnerAndScoreAndNeverAsksUiToDeriveThem()
        {
            ProtocolCandidateRecord winner = CreateCandidate(3, 280d, 0.175d);
            ProtocolBatteryRecord battery = CreateCompleteBattery(winner, 3);
            CreateScore(battery, 77d);
            new PhaseHistoryRepository(connections).Create(new PhaseHistoryRecord(null, profile.Id.Value, cycle.Id.Value, 3, 280d, "2026-07-17"));

            var warnings = new ErgonomicWarningService(new InjuryRiskFlagRepository(connections),
                new SensitivityCalculationService(constants), constants, new FixedClock());
            ProfileDashboardPresentation dashboard = new ProfileDashboardService(new ProfileDashboardRepository(connections), warnings,
                new ImmediateFeedbackService(new ImmediateFeedbackRepository(connections))).Load(Present(profile));

            Assert.That(dashboard.ImmediateFeedback.BestSensitivity, Is.EqualTo(0.175d).Within(1e-12));
            Assert.That(dashboard.ImmediateFeedback.LatestPerformanceScore, Is.EqualTo(77d).Within(1e-12));
            Assert.That(dashboard.LatestGrade, Is.EqualTo("A"));
        }

        [Test]
        public void UnfinalizedShotEvidenceFailsClosedRatherThanFabricatingFeedback()
        {
            ProtocolCandidateRecord candidate = CreateCandidate(1, 240d, 0.15d);
            ProtocolBatteryRecord battery = new ProtocolRepository(connections).CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value,
                cycle.Id.Value, candidate.Id.Value, candidate.SensitivityValue, 1, "exploratory", "2026-07-17", null));
            long sessionId = InsertSession(battery, "flick_close");
            using (SqliteDatabaseConnection connection = connections.Open())
            {
                connection.Execute(@"INSERT INTO shots(session_id,profile_id,target_id,distance_zone,target_size,spawn_position,
spawn_timestamp,resolution_timestamp,is_hit,outcome_reason,final_aim_position,is_adaptation_shot,sensitivity_value,
initial_offset_distance,final_precision_error,is_center_hit)
VALUES(@session,@profile,1,'close','small','0,0',0,1,1,'hit','0,0',NULL,@sensitivity,0,0,1);",
                    new Dictionary<string, object> { ["@session"] = sessionId, ["@profile"] = profile.Id.Value, ["@sensitivity"] = candidate.SensitivityValue });
            }
            Assert.That(() => new ImmediateFeedbackService(new ImmediateFeedbackRepository(connections)).Load(profile.Id.Value),
                Throws.TypeOf<DataAccessException>());
        }

        private void CreateShotSession(long phase, double sensitivity, IReadOnlyList<bool> authoritativeHits)
        {
            ProtocolCandidateRecord candidate = CreateCandidate(phase, sensitivity * profile.MouseDpi, sensitivity);
            ProtocolBatteryRecord battery = new ProtocolRepository(connections).CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value,
                cycle.Id.Value, candidate.Id.Value, sensitivity, phase, "exploratory", "2026-07-17", null));
            long sessionId = InsertSession(battery, "flick_close");
            using SqliteDatabaseConnection connection = connections.Open();
            long targetId = 0;
            foreach (bool adaptation in new[] { true, true })
                InsertShot(connection, sessionId, ++targetId, sensitivity, true, adaptation);
            foreach (bool hit in authoritativeHits)
                InsertShot(connection, sessionId, ++targetId, sensitivity, hit, false);
        }

        private ProtocolCandidateRecord CreateCandidate(long phase, double edpi, double sensitivity)
        {
            return new ProtocolRepository(connections).CreateCandidateWithSources(
                new ProtocolCandidateRecord(null, profile.Id.Value, cycle.Id.Value, phase, edpi, sensitivity, "phase1_offsets", "2026-07-17"),
                new[] { new ProtocolCandidateSourceRecord(edpi, 0d, edpi, false) });
        }

        private ProtocolBatteryRecord CreateCompleteBattery(ProtocolCandidateRecord candidate, long phase)
        {
            ProtocolBatteryRecord battery = new ProtocolRepository(connections).CreateBattery(new ProtocolBatteryRecord(null, profile.Id.Value,
                cycle.Id.Value, candidate.Id.Value, candidate.SensitivityValue, phase, "narrowing", "2026-07-17", "2026-07-17"));
            foreach (string mode in new[] { "flick_close", "flick_far", "tracking", "micro_correction" }) InsertSession(battery, mode);
            return battery;
        }

        private long InsertSession(ProtocolBatteryRecord battery, string mode)
        {
            using SqliteDatabaseConnection connection = connections.Open();
            connection.Execute(@"INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag)
VALUES(@profile,@battery,@config,'2026-07-17',@mode,1,0);",
                new Dictionary<string, object>
                {
                    ["@profile"] = profile.Id.Value, ["@battery"] = battery.Id.Value, ["@config"] = configurationId, ["@mode"] = mode
                });
            return connection.LastInsertRowId();
        }

        private void InsertShot(SqliteDatabaseConnection connection, long sessionId, long targetId, double sensitivity, bool hit, bool adaptation)
        {
            connection.Execute(@"INSERT INTO shots(session_id,profile_id,target_id,distance_zone,target_size,spawn_position,
spawn_timestamp,resolution_timestamp,is_hit,outcome_reason,final_aim_position,is_adaptation_shot,sensitivity_value,
initial_offset_distance,final_precision_error,is_center_hit)
VALUES(@session,@profile,@target,'close','small','0,0',0,1,@hit,@outcome,'0,0',@adaptation,@sensitivity,0,0,@hit);",
                new Dictionary<string, object>
                {
                    ["@session"] = sessionId, ["@profile"] = profile.Id.Value, ["@target"] = targetId, ["@hit"] = hit,
                    ["@outcome"] = hit ? "hit" : "miss_click", ["@adaptation"] = adaptation, ["@sensitivity"] = sensitivity
                });
        }

        private void CreateScore(ProtocolBatteryRecord battery, double score)
        {
            using SqliteDatabaseConnection connection = connections.Open();
            connection.Execute(@"INSERT INTO sensitivity_tests(profile_id,cycle_id,calibration_config_id,battery_id,edpi,cm_360,
avg_performance_score,performance_score_by_mode,grade,formula_version,phase,sample_size)
VALUES(@profile,@cycle,@config,@battery,280,46.384,@score,'{}','A','sc8-performance-score-v1',3,1);",
                new Dictionary<string, object>
                {
                    ["@profile"] = profile.Id.Value, ["@cycle"] = cycle.Id.Value, ["@config"] = configurationId,
                    ["@battery"] = battery.Id.Value, ["@score"] = score
                });
        }

        private static ProfileSetupPresentation Present(ProfileRecord value) => new ProfileSetupPresentation(value.Id.Value, value.Name,
            value.MouseDpi, value.CurrentSensitivity, value.ConfiguredPollingRateHz, value.DominantHand, value.CrosshairConfig,
            value.GripStyle, value.MovementStrategy, value.MousepadWidthCm, value.MousepadHeightCm, value.AdsMultiplier);

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class FixedClock : IProfileClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        }
    }
}
