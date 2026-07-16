using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class SessionCaptureRepository
    {
        private readonly RepositoryExecution execution;
        public SessionCaptureRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null) { execution = new RepositoryExecution(connectionFactory, failureReporter); }

        public long Persist(SessionCaptureRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return execution.Write("persist completed session capture", connection =>
            {
                using SqliteTransaction transaction = connection.BeginImmediateTransaction();
                VerifySessionParents(connection, request.Session);
                long sessionId = InsertSession(connection, request.Session);
                InsertTimingDiagnostics(connection, sessionId, request.TimingDiagnostics);
                var shotIds = new List<long>(request.Shots.Count);
                foreach (ShotCaptureRecord shot in request.Shots) shotIds.Add(InsertShot(connection, sessionId, request.Session.ProfileId, shot));
                var trackingTrialIds = new List<long>(request.TrackingTrials.Count);
                foreach (TrackingTrialCaptureRecord trial in request.TrackingTrials) trackingTrialIds.Add(InsertTrackingTrial(connection, sessionId, request.Session.ProfileId, trial));
                foreach (TrackingWindowCaptureRecord window in request.TrackingWindows) InsertTrackingWindow(connection, trackingTrialIds, window);
                foreach (MouseSampleCaptureRecord sample in request.MouseSamples) InsertMouseSample(connection, sessionId, shotIds, sample);
                transaction.Commit();
                return sessionId;
            });
        }

        public SessionFinalizationResult PersistAndFinalize(SessionFinalizationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return execution.Write("persist and finalize completed session", connection =>
            {
                using SqliteTransaction transaction = connection.BeginImmediateTransaction();
                VerifySessionParents(connection, request.Capture.Session);
                VerifyActiveAttempt(connection, request);
                RequireUnsetAdaptationFlags(request.Capture);
                long sessionId = InsertSession(connection, request.Capture.Session);
                InsertTimingDiagnostics(connection, sessionId, request.Capture.TimingDiagnostics);
                var shotIds = new List<long>(request.Capture.Shots.Count);
                foreach (ShotCaptureRecord shot in request.Capture.Shots) shotIds.Add(InsertShot(connection, sessionId, request.Capture.Session.ProfileId, shot));
                var trackingTrialIds = new List<long>(request.Capture.TrackingTrials.Count);
                foreach (TrackingTrialCaptureRecord trial in request.Capture.TrackingTrials) trackingTrialIds.Add(InsertTrackingTrial(connection, sessionId, request.Capture.Session.ProfileId, trial));
                foreach (TrackingWindowCaptureRecord window in request.Capture.TrackingWindows) InsertTrackingWindow(connection, trackingTrialIds, window);
                foreach (MouseSampleCaptureRecord sample in request.Capture.MouseSamples) InsertMouseSample(connection, sessionId, shotIds, sample);
                InsertSequenceAudit(connection, sessionId, request.SequenceAudit);
                int shotAdaptationCount = FinalizeShotAdaptation(connection, shotIds, request.Adaptation);
                int trackingAdaptationCount = FinalizeTrackingAdaptation(connection, sessionId, request.Adaptation);
                MarkAttemptCompleted(connection, request.AttemptId, sessionId, request.CompletedDate);
                bool batteryCompleted = CompleteBatteryIfReady(connection, request.Capture.Session.BatteryId, request.CompletedDate);
                transaction.Commit();
                return new SessionFinalizationResult(sessionId, batteryCompleted, shotAdaptationCount, trackingAdaptationCount);
            });
        }

        private static void VerifySessionParents(SqliteDatabaseConnection connection, SessionRecord session)
        {
            object count = connection.Scalar(@"SELECT COUNT(*) FROM protocol_batteries b JOIN calibration_configs c ON c.id=@calibration_config_id
WHERE b.id=@battery_id AND b.profile_id=@profile_id;", new Dictionary<string, object> { ["@battery_id"] = session.BatteryId, ["@profile_id"] = session.ProfileId, ["@calibration_config_id"] = session.CalibrationConfigId });
            if (Convert.ToInt64(count) != 1) throw new InvalidOperationException("The session battery/profile/calibration references are invalid.");
        }

        private static void VerifyActiveAttempt(SqliteDatabaseConnection connection, SessionFinalizationRequest request)
        {
            SessionRecord session = request.Capture.Session;
            SessionSequenceAuditRecord audit = request.SequenceAudit;
            object count = connection.Scalar(@"SELECT COUNT(*) FROM session_attempts WHERE id=@attempt_id AND state='capturing'
AND profile_id=@profile_id AND battery_id=@battery_id AND calibration_config_id=@calibration_config_id AND mode=@mode
AND sequence_contract_version=@contract_version AND sequence_generator=@generator AND sequence_seed_sha256=@seed_sha256
AND sequence_seed_material=@seed_material AND blind_candidate_label=@blind_label;", new Dictionary<string, object>
            {
                ["@attempt_id"] = request.AttemptId, ["@profile_id"] = session.ProfileId, ["@battery_id"] = session.BatteryId,
                ["@calibration_config_id"] = session.CalibrationConfigId, ["@mode"] = session.Mode,
                ["@contract_version"] = audit.ContractVersion, ["@generator"] = audit.Generator, ["@seed_sha256"] = audit.SeedSha256,
                ["@seed_material"] = audit.SeedMaterial, ["@blind_label"] = audit.BlindCandidateLabel
            });
            if (Convert.ToInt64(count) != 1) throw new InvalidOperationException("The active attempt does not match the completed session evidence.");
        }

        private static void RequireUnsetAdaptationFlags(SessionCaptureRequest capture)
        {
            foreach (ShotCaptureRecord shot in capture.Shots)
                if (shot == null || shot.IsAdaptationShot.HasValue) throw new InvalidOperationException("Shot adaptation flags must remain null until session finalization.");
            foreach (TrackingTrialCaptureRecord trial in capture.TrackingTrials)
                if (trial == null || trial.IsAdaptationTrial.HasValue) throw new InvalidOperationException("Tracking adaptation flags must remain null until session finalization.");
        }

        private static void InsertSequenceAudit(SqliteDatabaseConnection connection, long sessionId, SessionSequenceAuditRecord value)
        {
            connection.Execute(@"INSERT INTO session_sequence_audits(session_id,sequence_contract_version,sequence_generator,sequence_seed_sha256,sequence_seed_material,blind_candidate_label)
VALUES(@session_id,@contract_version,@generator,@seed_sha256,@seed_material,@blind_label);", new Dictionary<string, object>
            {
                ["@session_id"] = sessionId, ["@contract_version"] = value.ContractVersion, ["@generator"] = value.Generator,
                ["@seed_sha256"] = value.SeedSha256, ["@seed_material"] = value.SeedMaterial, ["@blind_label"] = value.BlindCandidateLabel
            });
        }

        private static int FinalizeShotAdaptation(SqliteDatabaseConnection connection, IReadOnlyList<long> shotIds, SessionAdaptationFinalizationPolicy policy)
        {
            int cutoff = Convert.ToInt32(Math.Floor(shotIds.Count * policy.ShotAdaptationFraction));
            for (int index = 0; index < shotIds.Count; index++)
                connection.Execute("UPDATE shots SET is_adaptation_shot=@is_adaptation_shot WHERE id=@id AND is_adaptation_shot IS NULL;", new Dictionary<string, object> { ["@is_adaptation_shot"] = index < cutoff, ["@id"] = shotIds[index] });
            return cutoff;
        }

        private static int FinalizeTrackingAdaptation(SqliteDatabaseConnection connection, long sessionId, SessionAdaptationFinalizationPolicy policy)
        {
            connection.Execute("UPDATE tracking_data SET is_adaptation_trial=CASE WHEN block_index < @adaptation_blocks THEN 1 ELSE 0 END WHERE session_id=@session_id AND is_adaptation_trial IS NULL;", new Dictionary<string, object> { ["@adaptation_blocks"] = policy.TrackingAdaptationBlockCount, ["@session_id"] = sessionId });
            return Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM tracking_data WHERE session_id=@session_id AND is_adaptation_trial=1;", new Dictionary<string, object> { ["@session_id"] = sessionId }));
        }

        private static void MarkAttemptCompleted(SqliteDatabaseConnection connection, long attemptId, long sessionId, string completedDate)
        {
            int changed = connection.Execute("UPDATE session_attempts SET state='completed',disposition_reason='session-finalized',updated_date=@date,completed_session_id=@session_id WHERE id=@attempt_id AND state='capturing';", new Dictionary<string, object> { ["@date"] = completedDate, ["@session_id"] = sessionId, ["@attempt_id"] = attemptId });
            if (changed != 1) throw new InvalidOperationException("Session attempt could not be completed.");
        }

        private static bool CompleteBatteryIfReady(SqliteDatabaseConnection connection, long batteryId, string completedDate)
        {
            long count = Convert.ToInt64(connection.Scalar("SELECT COUNT(DISTINCT mode) FROM sessions WHERE battery_id=@battery_id;", new Dictionary<string, object> { ["@battery_id"] = batteryId }));
            if (count < 4L) return false;
            if (count != 4L) throw new InvalidOperationException("A protocol battery must contain exactly four distinct modes.");
            int changed = connection.Execute("UPDATE protocol_batteries SET completed_date=@date WHERE id=@battery_id AND completed_date IS NULL;", new Dictionary<string, object> { ["@date"] = completedDate, ["@battery_id"] = batteryId });
            if (changed != 1) throw new InvalidOperationException("Protocol battery completion is invalid.");
            return true;
        }

        private static long InsertSession(SqliteDatabaseConnection connection, SessionRecord value)
        {
            connection.Execute(@"INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_score_change_percentage,fatigue_flag)
VALUES (@profile_id,@battery_id,@calibration_config_id,@date,@mode,@duration_sec,@fatigue_score_change_percentage,@fatigue_flag);", new Dictionary<string, object> { ["@profile_id"] = value.ProfileId, ["@battery_id"] = value.BatteryId, ["@calibration_config_id"] = value.CalibrationConfigId, ["@date"] = value.Date, ["@mode"] = value.Mode, ["@duration_sec"] = value.DurationSec, ["@fatigue_score_change_percentage"] = value.FatigueScoreChangePercentage, ["@fatigue_flag"] = value.FatigueFlag });
            return connection.LastInsertRowId();
        }

        private static void InsertTimingDiagnostics(SqliteDatabaseConnection connection, long sessionId, SessionTimingDiagnosticsRecord value)
        {
            connection.Execute(@"INSERT INTO session_timing_diagnostics(session_id,signal_pipeline_version,input_device_identity,configured_polling_rate_hz,measured_event_rate_hz,median_interval_ms,interval_mad_ms,p95_interval_ms,p99_interval_ms,duplicate_timestamp_count,reverse_timestamp_count,burst_interval_count,single_cadence_interval_count,gap_interval_count,p99_single_cadence_residual_ms,timing_contract_passed,disposition_reason)
VALUES (@session_id,@signal_pipeline_version,@input_device_identity,@configured_polling_rate_hz,@measured_event_rate_hz,@median_interval_ms,@interval_mad_ms,@p95_interval_ms,@p99_interval_ms,@duplicate_timestamp_count,@reverse_timestamp_count,@burst_interval_count,@single_cadence_interval_count,@gap_interval_count,@p99_single_cadence_residual_ms,@timing_contract_passed,@disposition_reason);", new Dictionary<string, object> { ["@session_id"] = sessionId, ["@signal_pipeline_version"] = value.SignalPipelineVersion, ["@input_device_identity"] = value.InputDeviceIdentity, ["@configured_polling_rate_hz"] = value.ConfiguredPollingRateHz, ["@measured_event_rate_hz"] = value.MeasuredEventRateHz, ["@median_interval_ms"] = value.MedianIntervalMs, ["@interval_mad_ms"] = value.IntervalMadMs, ["@p95_interval_ms"] = value.P95IntervalMs, ["@p99_interval_ms"] = value.P99IntervalMs, ["@duplicate_timestamp_count"] = value.DuplicateTimestampCount, ["@reverse_timestamp_count"] = value.ReverseTimestampCount, ["@burst_interval_count"] = value.BurstIntervalCount, ["@single_cadence_interval_count"] = value.SingleCadenceIntervalCount, ["@gap_interval_count"] = value.GapIntervalCount, ["@p99_single_cadence_residual_ms"] = value.P99SingleCadenceResidualMs, ["@timing_contract_passed"] = value.TimingContractPassed, ["@disposition_reason"] = value.DispositionReason });
        }

        private static long InsertShot(SqliteDatabaseConnection connection, long sessionId, long profileId, ShotCaptureRecord value)
        {
            if (value == null) throw new ArgumentException("Shot captures cannot contain null.", nameof(value));
            connection.Execute(@"INSERT INTO shots(session_id,profile_id,target_id,distance_zone,target_size,spawn_position,spawn_timestamp,first_mouse_movement_timestamp,resolution_timestamp,hit_timestamp,hit_position,is_hit,outcome_reason,final_aim_position,is_outlier,is_adaptation_shot,sensitivity_value,initial_offset_distance,micro_adjustment_count,submovement_count,final_precision_error,is_center_hit)
VALUES (@session_id,@profile_id,@target_id,@distance_zone,@target_size,@spawn_position,@spawn_timestamp,@first_mouse_movement_timestamp,@resolution_timestamp,@hit_timestamp,@hit_position,@is_hit,@outcome_reason,@final_aim_position,@is_outlier,@is_adaptation_shot,@sensitivity_value,@initial_offset_distance,@micro_adjustment_count,@submovement_count,@final_precision_error,@is_center_hit);", new Dictionary<string, object> { ["@session_id"] = sessionId, ["@profile_id"] = profileId, ["@target_id"] = value.TargetId, ["@distance_zone"] = value.DistanceZone, ["@target_size"] = value.TargetSize, ["@spawn_position"] = value.SpawnPosition, ["@spawn_timestamp"] = value.SpawnTimestamp, ["@first_mouse_movement_timestamp"] = value.FirstMouseMovementTimestamp, ["@resolution_timestamp"] = value.ResolutionTimestamp, ["@hit_timestamp"] = value.HitTimestamp, ["@hit_position"] = value.HitPosition, ["@is_hit"] = value.IsHit, ["@outcome_reason"] = value.OutcomeReason, ["@final_aim_position"] = value.FinalAimPosition, ["@is_outlier"] = value.IsOutlier, ["@is_adaptation_shot"] = value.IsAdaptationShot, ["@sensitivity_value"] = value.SensitivityValue, ["@initial_offset_distance"] = value.InitialOffsetDistance, ["@micro_adjustment_count"] = value.MicroAdjustmentCount, ["@submovement_count"] = value.SubmovementCount, ["@final_precision_error"] = value.FinalPrecisionError, ["@is_center_hit"] = value.IsCenterHit });
            return connection.LastInsertRowId();
        }

        private static void InsertMouseSample(SqliteDatabaseConnection connection, long sessionId, IReadOnlyList<long> shotIds, MouseSampleCaptureRecord value)
        {
            if (value == null) throw new ArgumentException("Mouse samples cannot contain null.", nameof(value));
            long? shotId = null;
            if (value.ShotCaptureIndex.HasValue)
            {
                if (value.ShotCaptureIndex.Value < 0 || value.ShotCaptureIndex.Value >= shotIds.Count) throw new InvalidOperationException("Mouse sample references a shot outside the capture request.");
                shotId = shotIds[value.ShotCaptureIndex.Value];
            }
            connection.Execute(@"INSERT INTO mouse_samples(session_id,shot_id,sample_index,timestamp_sec,raw_delta_x,raw_delta_y,azimuth_deg,elevation_deg)
VALUES (@session_id,@shot_id,@sample_index,@timestamp_sec,@raw_delta_x,@raw_delta_y,@azimuth_deg,@elevation_deg);", new Dictionary<string, object> { ["@session_id"] = sessionId, ["@shot_id"] = shotId, ["@sample_index"] = value.SampleIndex, ["@timestamp_sec"] = value.TimestampSec, ["@raw_delta_x"] = value.RawDeltaX, ["@raw_delta_y"] = value.RawDeltaY, ["@azimuth_deg"] = value.AzimuthDeg, ["@elevation_deg"] = value.ElevationDeg });
        }

        private static long InsertTrackingTrial(SqliteDatabaseConnection connection, long sessionId, long profileId, TrackingTrialCaptureRecord value)
        {
            if (value == null) throw new ArgumentException("Tracking trials cannot contain null.", nameof(value));
            connection.Execute(@"INSERT INTO tracking_data(session_id,profile_id,sensitivity_value,trial_index,block_index,is_adaptation_trial,pattern_type,target_size,path_contract_id,path_parameters_json,duration_ms,deviation_samples,time_on_target_ms,time_on_target_percentage)
VALUES (@session_id,@profile_id,@sensitivity_value,@trial_index,@block_index,@is_adaptation_trial,@pattern_type,@target_size,@path_contract_id,@path_parameters_json,@duration_ms,@deviation_samples,@time_on_target_ms,@time_on_target_percentage);", new Dictionary<string, object> { ["@session_id"] = sessionId, ["@profile_id"] = profileId, ["@sensitivity_value"] = value.SensitivityValue, ["@trial_index"] = value.TrialIndex, ["@block_index"] = value.BlockIndex, ["@is_adaptation_trial"] = value.IsAdaptationTrial, ["@pattern_type"] = value.PatternType, ["@target_size"] = value.TargetSize, ["@path_contract_id"] = value.PathContractId, ["@path_parameters_json"] = value.PathParametersJson, ["@duration_ms"] = value.DurationMs, ["@deviation_samples"] = value.DeviationSamples, ["@time_on_target_ms"] = value.TimeOnTargetMs, ["@time_on_target_percentage"] = value.TimeOnTargetPercentage });
            return connection.LastInsertRowId();
        }

        private static void InsertTrackingWindow(SqliteDatabaseConnection connection, IReadOnlyList<long> trackingTrialIds, TrackingWindowCaptureRecord value)
        {
            if (value == null) throw new ArgumentException("Tracking windows cannot contain null.", nameof(value));
            if (value.TrackingTrialCaptureIndex < 0 || value.TrackingTrialCaptureIndex >= trackingTrialIds.Count) throw new InvalidOperationException("Tracking window references a trial outside the capture request.");
            connection.Execute(@"INSERT INTO tracking_windows(tracking_trial_id,window_index,window_start_ms,window_end_ms,time_on_target_ms,time_on_target_percentage,tracking_deviation_rms_deg)
VALUES (@tracking_trial_id,@window_index,@window_start_ms,@window_end_ms,@time_on_target_ms,@time_on_target_percentage,@tracking_deviation_rms_deg);", new Dictionary<string, object> { ["@tracking_trial_id"] = trackingTrialIds[value.TrackingTrialCaptureIndex], ["@window_index"] = value.WindowIndex, ["@window_start_ms"] = value.WindowStartMs, ["@window_end_ms"] = value.WindowEndMs, ["@time_on_target_ms"] = value.TimeOnTargetMs, ["@time_on_target_percentage"] = value.TimeOnTargetPercentage, ["@tracking_deviation_rms_deg"] = value.TrackingDeviationRmsDeg });
        }
    }
}
