using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class SessionLifecycleRepository
    {
        private readonly RepositoryExecution execution;
        public SessionLifecycleRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null) { execution=new RepositoryExecution(connectionFactory,failureReporter); }

        public SessionAttemptRecord Begin(SessionAttemptStartRecord start)
        {
            if(start==null)throw new ArgumentNullException(nameof(start));
            return execution.Write("begin session attempt",connection=>
            {
                using SqliteTransaction transaction=connection.BeginImmediateTransaction(); VerifyLineage(connection,start);
                long ordinal=Convert.ToInt64(connection.Scalar("SELECT COALESCE(MAX(attempt_ordinal),0)+1 FROM session_attempts WHERE battery_id=@battery_id AND mode=@mode;",new Dictionary<string,object>{{"@battery_id",start.BatteryId},{"@mode",start.Mode}}));
                connection.Execute(@"INSERT INTO session_attempts(profile_id,cycle_id,candidate_id,battery_id,calibration_config_id,mode,attempt_ordinal,state,disposition_reason,started_date,updated_date,sequence_contract_version,sequence_generator,sequence_seed_sha256,sequence_seed_material,blind_candidate_label)
VALUES(@profile_id,@cycle_id,@candidate_id,@battery_id,@calibration_config_id,@mode,@attempt_ordinal,'capturing','capture-started',@started_date,@updated_date,@sequence_contract_version,@sequence_generator,@sequence_seed_sha256,@sequence_seed_material,@blind_candidate_label);",Parameters(start,ordinal,"@started_date",start.StartedDate));
                long id=connection.LastInsertRowId();transaction.Commit();return new SessionAttemptRecord(id,start,ordinal,"capturing","capture-started",start.StartedDate,null);
            });
        }

        public SessionAttemptRecord Transition(SessionAttemptRecord attempt,string nextState,string dispositionReason,string updatedDate)
        {
            if(attempt==null)throw new ArgumentNullException(nameof(attempt));if(string.IsNullOrWhiteSpace(nextState)||string.IsNullOrWhiteSpace(dispositionReason)||string.IsNullOrWhiteSpace(updatedDate))throw new ArgumentException("Lifecycle transition fields are required.");
            return execution.Write("transition session attempt",connection=>
            {
                string current=Convert.ToString(connection.Scalar("SELECT state FROM session_attempts WHERE id=@id;",new Dictionary<string,object>{{"@id",attempt.Id}}));
                if(!Allowed(current,nextState))throw new InvalidOperationException("Invalid session attempt transition: "+current+" to "+nextState+".");
                connection.Execute("UPDATE session_attempts SET state=@state,disposition_reason=@reason,updated_date=@updated_date WHERE id=@id;",new Dictionary<string,object>{{"@state",nextState},{"@reason",dispositionReason},{"@updated_date",updatedDate},{"@id",attempt.Id}});
                return new SessionAttemptRecord(attempt.Id,attempt.Start,attempt.AttemptOrdinal,nextState,dispositionReason,updatedDate,attempt.CompletedSessionId);
            });
        }

        private static bool Allowed(string current,string next) => (current=="capturing"&&(next=="paused"||next=="cancelled"||next=="faulted"))||(current=="paused"&&(next=="capturing"||next=="cancelled"||next=="faulted"));
        private static void VerifyLineage(SqliteDatabaseConnection connection,SessionAttemptStartRecord start)
        {
            object count=connection.Scalar(@"SELECT COUNT(*) FROM protocol_batteries b JOIN protocol_candidates c ON c.id=b.candidate_id
WHERE b.id=@battery_id AND b.profile_id=@profile_id AND b.cycle_id=@cycle_id AND b.candidate_id=@candidate_id AND b.completed_date IS NULL
AND c.sensitivity_value=b.sensitivity_value AND NOT EXISTS(SELECT 1 FROM sessions s WHERE s.battery_id=b.id AND s.mode=@mode);",new Dictionary<string,object>{{"@battery_id",start.BatteryId},{"@profile_id",start.ProfileId},{"@cycle_id",start.CycleId},{"@candidate_id",start.CandidateId},{"@mode",start.Mode}});
            if(Convert.ToInt64(count)!=1)throw new InvalidOperationException("Session attempt lineage is invalid or its battery is complete.");
            object config=connection.Scalar("SELECT COUNT(*) FROM calibration_configs WHERE id=@id;",new Dictionary<string,object>{{"@id",start.CalibrationConfigId}});if(Convert.ToInt64(config)!=1)throw new InvalidOperationException("Session attempt configuration is invalid.");
        }
        private static IReadOnlyDictionary<string,object> Parameters(SessionAttemptStartRecord start,long ordinal,string dateKey,string date)
        { var audit=start.SequenceAudit;return new Dictionary<string,object>{{"@profile_id",start.ProfileId},{"@cycle_id",start.CycleId},{"@candidate_id",start.CandidateId},{"@battery_id",start.BatteryId},{"@calibration_config_id",start.CalibrationConfigId},{"@mode",start.Mode},{"@attempt_ordinal",ordinal},{dateKey,date},{"@updated_date",date},{"@sequence_contract_version",audit.ContractVersion},{"@sequence_generator",audit.Generator},{"@sequence_seed_sha256",audit.SeedSha256},{"@sequence_seed_material",audit.SeedMaterial},{"@blind_candidate_label",audit.BlindCandidateLabel}}; }
    }
}
