using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class CrossProfileComparisonData
    {
        public CrossProfileComparisonData(long profileId,string profileName,double? edpi,double? consistencyUtility,string reactionTier,double? performanceScore,string formulaVersion,string configurationVersion,string completedDate)
        {ProfileId=profileId>0?profileId:throw new ArgumentOutOfRangeException(nameof(profileId));ProfileName=!string.IsNullOrWhiteSpace(profileName)?profileName:throw new ArgumentException("Profile name is required.");Edpi=edpi;ConsistencyUtility=consistencyUtility;ReactionTier=reactionTier;PerformanceScore=performanceScore;FormulaVersion=formulaVersion;ConfigurationVersion=configurationVersion;CompletedDate=completedDate;}
        public long ProfileId{get;}public string ProfileName{get;}public double? Edpi{get;}public double? ConsistencyUtility{get;}public string ReactionTier{get;}public double? PerformanceScore{get;}public string FormulaVersion{get;}public string ConfigurationVersion{get;}public string CompletedDate{get;}
    }
    public interface ICrossProfileComparisonReader { IReadOnlyList<CrossProfileComparisonData> ReadExplicit(IReadOnlyList<long> profileIds); }
    public sealed class CrossProfileComparisonRepository : ICrossProfileComparisonReader
    {
        private readonly RepositoryExecution execution;
        public CrossProfileComparisonRepository(SqliteConnectionFactory factory,IDataFailureReporter reporter=null){execution=new RepositoryExecution(factory,reporter);}
        public IReadOnlyList<CrossProfileComparisonData> ReadExplicit(IReadOnlyList<long> profileIds)
        {
            Validate(profileIds);
            return execution.Read("read explicit profile comparison",connection=>profileIds.Select(id=>Read(connection,id)).ToArray());
        }
        private static CrossProfileComparisonData Read(SqliteDatabaseConnection c,long id)
        {
            var rows=c.Query(@"SELECT p.id,p.name,st.edpi,st.battery_consistency_utility,st.reaction_tier,st.avg_performance_score,st.formula_version,cfg.config_version,b.completed_date
FROM profiles p LEFT JOIN sensitivity_tests st ON st.id=(SELECT candidate.id FROM sensitivity_tests candidate JOIN protocol_batteries candidate_battery ON candidate_battery.id=candidate.battery_id WHERE candidate.profile_id=p.id AND candidate.grade IS NOT NULL AND candidate_battery.completed_date IS NOT NULL AND (SELECT COUNT(*) FROM sessions s WHERE s.battery_id=candidate_battery.id)=4 AND (SELECT COUNT(DISTINCT s.mode) FROM sessions s WHERE s.battery_id=candidate_battery.id)=4 ORDER BY candidate_battery.completed_date DESC,candidate.id DESC LIMIT 1)
LEFT JOIN protocol_batteries b ON b.id=st.battery_id LEFT JOIN calibration_configs cfg ON cfg.id=st.calibration_config_id WHERE p.id=@id;",new Dictionary<string,object>{{"@id",id}});
            if(rows.Count!=1)throw new InvalidOperationException("Explicit comparison profile was not found.");var row=rows[0];return new CrossProfileComparisonData(Convert.ToInt64(row["id"]),Convert.ToString(row["name"]),Number(row,"edpi"),Number(row,"battery_consistency_utility"),Text(row,"reaction_tier"),Number(row,"avg_performance_score"),Text(row,"formula_version"),Text(row,"config_version"),Text(row,"completed_date"));
        }
        private static void Validate(IReadOnlyList<long> ids){if(ids==null||ids.Count==0)throw new ArgumentException("At least one explicit profile id is required.",nameof(ids));if(ids.Any(id=>id<=0)||ids.Distinct().Count()!=ids.Count)throw new ArgumentException("Comparison profile ids must be positive and unique.",nameof(ids));}
        private static double? Number(IReadOnlyDictionary<string,object> row,string key)=>row[key]==null?(double?)null:Convert.ToDouble(row[key]);private static string Text(IReadOnlyDictionary<string,object> row,string key)=>row[key]==null?null:Convert.ToString(row[key]);
    }
}
