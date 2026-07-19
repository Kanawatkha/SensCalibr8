using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class ProfileDataExportRepository
    {
        private sealed class TableSpec { public TableSpec(string name,string query){Name=name;Query=query;}public string Name{get;}public string Query{get;} }
        private static readonly TableSpec[] Tables={
            new TableSpec("profiles","SELECT * FROM profiles WHERE id=@id;"),
            new TableSpec("calibration_configs",@"SELECT * FROM calibration_configs WHERE id IN (SELECT calibration_config_id FROM sessions WHERE profile_id=@id UNION SELECT calibration_config_id FROM sensitivity_tests WHERE profile_id=@id UNION SELECT calibration_config_id FROM outlier_analysis_runs WHERE profile_id=@id UNION SELECT calibration_config_id FROM significance_tests WHERE profile_id=@id) ORDER BY id;"),
            new TableSpec("cycles","SELECT * FROM cycles WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("protocol_candidates","SELECT * FROM protocol_candidates WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("protocol_candidate_sources","SELECT * FROM protocol_candidate_sources WHERE candidate_id IN (SELECT id FROM protocol_candidates WHERE profile_id=@id) ORDER BY id;"),
            new TableSpec("protocol_batteries","SELECT * FROM protocol_batteries WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("sessions","SELECT * FROM sessions WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("session_timing_diagnostics","SELECT * FROM session_timing_diagnostics WHERE session_id IN (SELECT id FROM sessions WHERE profile_id=@id) ORDER BY id;"),
            new TableSpec("shots","SELECT * FROM shots WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("tracking_data","SELECT * FROM tracking_data WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("tracking_windows","SELECT * FROM tracking_windows WHERE tracking_trial_id IN (SELECT id FROM tracking_data WHERE profile_id=@id) ORDER BY id;"),
            new TableSpec("mouse_samples","SELECT * FROM mouse_samples WHERE session_id IN (SELECT id FROM sessions WHERE profile_id=@id) ORDER BY id;"),
            new TableSpec("outlier_analysis_runs","SELECT * FROM outlier_analysis_runs WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("outlier_flags","SELECT * FROM outlier_flags WHERE session_id IN (SELECT id FROM sessions WHERE profile_id=@id) ORDER BY id;"),
            new TableSpec("sensitivity_tests","SELECT * FROM sensitivity_tests WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("significance_tests","SELECT * FROM significance_tests WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("significance_test_pairs","SELECT * FROM significance_test_pairs WHERE significance_test_id IN (SELECT id FROM significance_tests WHERE profile_id=@id) ORDER BY id;"),
            new TableSpec("phase_history","SELECT * FROM phase_history WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("injury_risk_flags","SELECT * FROM injury_risk_flags WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("cycle_checkpoints","SELECT * FROM cycle_checkpoints WHERE profile_id=@id ORDER BY id;"),
            new TableSpec("recalibration_events","SELECT * FROM recalibration_events WHERE source_cycle_id IN (SELECT id FROM cycles WHERE profile_id=@id) OR destination_cycle_id IN (SELECT id FROM cycles WHERE profile_id=@id) ORDER BY id;")
        };
        private readonly RepositoryExecution execution;
        public ProfileDataExportRepository(SqliteConnectionFactory factory,IDataFailureReporter reporter=null){execution=new RepositoryExecution(factory,reporter);}
        public ProfileDataExportDataset Read(long profileId)
        {
            if(profileId<=0)throw new ArgumentOutOfRangeException(nameof(profileId));
            return execution.Read("read profile data export",connection=>{AnalysisProfileIdentity profile=Profile(connection,profileId);Finalized(connection,profileId);return new ProfileDataExportDataset(profile,Tables.Select(table=>ReadTable(connection,table,profileId)).ToArray());});
        }
        private static AnalysisProfileIdentity Profile(SqliteDatabaseConnection c,long id){var rows=c.Query("SELECT id,name,mouse_dpi FROM profiles WHERE id=@id;",Parameters(id));if(rows.Count!=1)throw new InvalidOperationException("Export profile was not found.");var row=rows[0];return new AnalysisProfileIdentity(Convert.ToInt64(row["id"]),Convert.ToString(row["name"]),Convert.ToInt64(row["mouse_dpi"]));}
        private static void Finalized(SqliteDatabaseConnection c,long id){long shots=Convert.ToInt64(c.Scalar("SELECT COUNT(*) FROM shots WHERE profile_id=@id AND is_adaptation_shot IS NULL;",Parameters(id))),tracking=Convert.ToInt64(c.Scalar("SELECT COUNT(*) FROM tracking_data WHERE profile_id=@id AND is_adaptation_trial IS NULL;",Parameters(id)));if(shots!=0||tracking!=0)throw new InvalidOperationException("Data Export rejects unfinalized adaptation evidence.");}
        private static ProfileDataExportTable ReadTable(SqliteDatabaseConnection c,TableSpec spec,long id){var columns=c.Query("PRAGMA table_info("+spec.Name+");").OrderBy(row=>Convert.ToInt64(row["cid"])).Select(row=>Convert.ToString(row["name"])).ToArray();return new ProfileDataExportTable(spec.Name,columns,c.Query(spec.Query,Parameters(id)));}
        private static IReadOnlyDictionary<string,object> Parameters(long id)=>new Dictionary<string,object>{{"@id",id}};
    }
}
