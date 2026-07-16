using System;
using System.Collections.Generic;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Migrations;

namespace SensCalibr8.Data.Persistence
{
    public sealed class SqliteDatabaseBootstrapper
    {
        private readonly SqliteMigrationRunner migrationRunner;

        public SqliteDatabaseBootstrapper() : this(new SqliteMigrationRunner()) { }
        public SqliteDatabaseBootstrapper(SqliteMigrationRunner migrationRunner) { this.migrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner)); }

        public void Initialize(string databasePath, FrozenCalibrationConfiguration configuration, string nativeLibraryPath = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            try
            {
                var factory = new SqliteConnectionFactory(databasePath, nativeLibraryPath);
                using SqliteDatabaseConnection connection = factory.Open();
                migrationRunner.ApplyAll(connection);
                InsertOrVerifyCalibrationConfiguration(connection, configuration.Record);
            }
            catch (DataAccessException) { throw; }
            catch (Exception exception) { throw new DataAccessException("Failed to initialize the SensCalibr8 database.", exception); }
        }

        private static void InsertOrVerifyCalibrationConfiguration(SqliteDatabaseConnection connection, CalibrationConfigurationRecord record)
        {
            var key = new Dictionary<string, object> { ["@config_version"] = record.ConfigVersion };
            if (Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM calibration_configs WHERE config_version=@config_version;", key)) > 0)
            {
                VerifyExisting(connection, record);
                return;
            }
            connection.ExecuteScript("BEGIN IMMEDIATE;");
            try
            {
                int changed = connection.Execute(@"INSERT INTO calibration_configs (
config_version, normalization_version, signal_pipeline_version, test_geometry_version, created_date,
input_sampling_rate_hz, resampling_tolerance_ms, timing_acceptance_policy, butterworth_order,
cutoff_frequency_hz, submovement_start_deg_per_sec, submovement_end_deg_per_sec, refractory_period_ms,
normalization_bounds_json, submovement_bounds_by_mode_json, consistency_tier_cutpoints_json,
scoring_zero_tolerance, target_geometry_json, tracking_contract_json, confirmatory_contract_json)
VALUES (@config_version,@normalization_version,@signal_pipeline_version,@test_geometry_version,@created_date,
@input_sampling_rate_hz,@resampling_tolerance_ms,@timing_acceptance_policy,@butterworth_order,
@cutoff_frequency_hz,@submovement_start_deg_per_sec,@submovement_end_deg_per_sec,@refractory_period_ms,
@normalization_bounds_json,@submovement_bounds_by_mode_json,@consistency_tier_cutpoints_json,
@scoring_zero_tolerance,@target_geometry_json,@tracking_contract_json,@confirmatory_contract_json);", Parameters(record));
                if (changed != 1) throw new InvalidOperationException("Calibration configuration insert did not affect exactly one row.");
                connection.ExecuteScript("COMMIT;");
            }
            catch
            {
                connection.ExecuteScript("ROLLBACK;");
                throw;
            }
        }

        private static void VerifyExisting(SqliteDatabaseConnection connection, CalibrationConfigurationRecord record)
        {
            object count = connection.Scalar(@"SELECT COUNT(*) FROM calibration_configs WHERE
config_version=@config_version AND normalization_version=@normalization_version AND signal_pipeline_version=@signal_pipeline_version AND
test_geometry_version=@test_geometry_version AND created_date=@created_date AND input_sampling_rate_hz=@input_sampling_rate_hz AND
resampling_tolerance_ms=@resampling_tolerance_ms AND timing_acceptance_policy=@timing_acceptance_policy AND butterworth_order=@butterworth_order AND
cutoff_frequency_hz=@cutoff_frequency_hz AND submovement_start_deg_per_sec=@submovement_start_deg_per_sec AND
submovement_end_deg_per_sec=@submovement_end_deg_per_sec AND refractory_period_ms=@refractory_period_ms AND
normalization_bounds_json=@normalization_bounds_json AND submovement_bounds_by_mode_json=@submovement_bounds_by_mode_json AND
consistency_tier_cutpoints_json=@consistency_tier_cutpoints_json AND scoring_zero_tolerance=@scoring_zero_tolerance AND
target_geometry_json=@target_geometry_json AND tracking_contract_json=@tracking_contract_json AND confirmatory_contract_json=@confirmatory_contract_json;", Parameters(record));
            if (Convert.ToInt32(count) != 1) throw new InvalidOperationException("Stored calibration configuration differs from the accepted immutable projection.");
        }

        private static IReadOnlyDictionary<string, object> Parameters(CalibrationConfigurationRecord record)
        {
            return new Dictionary<string, object>
            {
                ["@config_version"] = record.ConfigVersion, ["@normalization_version"] = record.NormalizationVersion,
                ["@signal_pipeline_version"] = record.SignalPipelineVersion, ["@test_geometry_version"] = record.TestGeometryVersion,
                ["@created_date"] = record.CreatedDate, ["@input_sampling_rate_hz"] = record.InputSamplingRateHz,
                ["@resampling_tolerance_ms"] = record.ResamplingToleranceMs, ["@timing_acceptance_policy"] = record.TimingAcceptancePolicy,
                ["@butterworth_order"] = record.ButterworthOrder, ["@cutoff_frequency_hz"] = record.CutoffFrequencyHz,
                ["@submovement_start_deg_per_sec"] = record.SubmovementStartDegPerSec, ["@submovement_end_deg_per_sec"] = record.SubmovementEndDegPerSec,
                ["@refractory_period_ms"] = record.RefractoryPeriodMs, ["@normalization_bounds_json"] = record.NormalizationBoundsJson,
                ["@submovement_bounds_by_mode_json"] = record.SubmovementBoundsByModeJson, ["@consistency_tier_cutpoints_json"] = record.ConsistencyTierCutpointsJson,
                ["@scoring_zero_tolerance"] = record.ScoringZeroTolerance, ["@target_geometry_json"] = record.TargetGeometryJson,
                ["@tracking_contract_json"] = record.TrackingContractJson, ["@confirmatory_contract_json"] = record.ConfirmatoryContractJson
            };
        }
    }
}
