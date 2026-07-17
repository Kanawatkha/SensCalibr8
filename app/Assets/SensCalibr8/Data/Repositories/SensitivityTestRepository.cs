using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class SensitivityTestRepository
    {
        private readonly RepositoryExecution execution;
        public SensitivityTestRepository(SqliteConnectionFactory connectionFactory,IDataFailureReporter failureReporter=null){execution=new RepositoryExecution(connectionFactory,failureReporter);}
        public SensitivityTestRecord Create(SensitivityTestRecord value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            return execution.Write("create sensitivity test",connection=>
            {
                object lineage=connection.Scalar("SELECT COUNT(*) FROM cycles WHERE id=@cycle_id AND profile_id=@profile_id;",new Dictionary<string,object>{{"@cycle_id",value.CycleId},{"@profile_id",value.ProfileId}});
                if(Convert.ToInt64(lineage)!=1)throw new InvalidOperationException("Sensitivity-test cycle does not belong to the profile.");
                object batteryLineage=connection.Scalar(@"SELECT COUNT(*) FROM protocol_batteries b JOIN protocol_candidates c ON c.id=b.candidate_id
WHERE b.id=@battery_id AND b.profile_id=@profile_id AND b.cycle_id=@cycle_id AND b.phase=@phase AND c.edpi=@edpi
AND b.completed_date IS NOT NULL AND (SELECT COUNT(*) FROM sessions s WHERE s.battery_id=b.id)=4
AND (SELECT COUNT(DISTINCT s.mode) FROM sessions s WHERE s.battery_id=b.id)=4
AND (SELECT COUNT(*) FROM sessions s WHERE s.battery_id=b.id AND s.calibration_config_id=@config_id)=4;",new Dictionary<string,object>{{"@battery_id",value.BatteryId},{"@profile_id",value.ProfileId},{"@cycle_id",value.CycleId},{"@phase",value.Phase},{"@edpi",value.Edpi},{"@config_id",value.CalibrationConfigId}});
                if(Convert.ToInt64(batteryLineage)!=1)throw new InvalidOperationException("Sensitivity-test battery lineage is invalid.");
                connection.Execute(@"INSERT INTO sensitivity_tests(profile_id,cycle_id,calibration_config_id,battery_id,edpi,cm_360,avg_performance_score,performance_score_by_mode,grade,formula_version,phase,sample_size)
VALUES (@profile_id,@cycle_id,@calibration_config_id,@battery_id,@edpi,@cm_360,@avg_performance_score,@performance_score_by_mode,@grade,@formula_version,@phase,@sample_size);",new Dictionary<string,object>{{"@profile_id",value.ProfileId},{"@cycle_id",value.CycleId},{"@calibration_config_id",value.CalibrationConfigId},{"@battery_id",value.BatteryId},{"@edpi",value.Edpi},{"@cm_360",value.Cm360},{"@avg_performance_score",value.AveragePerformanceScore},{"@performance_score_by_mode",value.PerformanceScoreByModeJson},{"@grade",value.Grade},{"@formula_version",value.FormulaVersion},{"@phase",value.Phase},{"@sample_size",value.SampleSize}});
                return new SensitivityTestRecord(connection.LastInsertRowId(),value.ProfileId,value.CycleId,value.CalibrationConfigId,value.BatteryId,value.Edpi,value.Cm360,value.AveragePerformanceScore,value.PerformanceScoreByModeJson,value.Grade,value.FormulaVersion,value.Phase,value.SampleSize);
            });
        }
    }
}
