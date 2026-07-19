using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Analysis
{
    public sealed class ProfileComparisonPresentation
    {
        public ProfileComparisonPresentation(CrossProfileComparisonData source){if(source==null)throw new ArgumentNullException(nameof(source));ProfileId=source.ProfileId;ProfileName=source.ProfileName;Edpi=source.Edpi;ConsistencyUtility=source.ConsistencyUtility;ReactionTier=source.ReactionTier;PerformanceScore=source.PerformanceScore;FormulaVersion=source.FormulaVersion;ConfigurationVersion=source.ConfigurationVersion;CompletedDate=source.CompletedDate;}
        public long ProfileId{get;}public string ProfileName{get;}public double? Edpi{get;}public double? ConsistencyUtility{get;}public string ReactionTier{get;}public double? PerformanceScore{get;}public string FormulaVersion{get;}public string ConfigurationVersion{get;}public string CompletedDate{get;}public bool HasComparableResult=>Edpi.HasValue&&ConsistencyUtility.HasValue&&!string.IsNullOrWhiteSpace(ReactionTier)&&PerformanceScore.HasValue;
    }
    public sealed class CrossProfileComparisonService
    {
        private readonly ICrossProfileComparisonReader reader;
        public CrossProfileComparisonService(ICrossProfileComparisonReader reader){this.reader=reader??throw new ArgumentNullException(nameof(reader));}
        public IReadOnlyList<ProfileComparisonPresentation> LoadExplicit(IReadOnlyList<long> profileIds)
        {
            if(profileIds==null||profileIds.Count<2)throw new ArgumentException("Select at least two profiles for comparison.",nameof(profileIds));
            if(profileIds.Any(id=>id<=0)||profileIds.Distinct().Count()!=profileIds.Count)throw new ArgumentException("Comparison profile ids must be positive and unique.",nameof(profileIds));
            return reader.ReadExplicit(profileIds).Select(value=>new ProfileComparisonPresentation(value)).ToArray();
        }
    }
}
