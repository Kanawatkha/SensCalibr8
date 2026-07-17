using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.Services.Calculations
{
    public enum OutlierObservationKind{Shot,TrackingTrial}
    public sealed class MetricObservation
    {
        public MetricObservation(long sessionId,long observationId,OutlierObservationKind kind,double value)
        {SessionId=Positive(sessionId,nameof(sessionId));ObservationId=Positive(observationId,nameof(observationId));Kind=kind;Value=Finite(value,nameof(value));}
        public long SessionId{get;}public long ObservationId{get;}public OutlierObservationKind Kind{get;}public double Value{get;}
        private static long Positive(long value,string field)=>value>0?value:throw new ArgumentOutOfRangeException(field);
        private static double Finite(double value,string field)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException(field);
    }
    public sealed class OutlierFlagResult
    {
        public OutlierFlagResult(MetricObservation observation,bool isStatisticalOutlier){Observation=observation??throw new ArgumentNullException(nameof(observation));IsStatisticalOutlier=isStatisticalOutlier;}
        public MetricObservation Observation{get;}public bool IsStatisticalOutlier{get;}
    }
    public sealed class OutlierAnalysisResult
    {
        public OutlierAnalysisResult(double mean,double sampleSd,double threshold,double inclusiveMean,double excludedMean,IReadOnlyList<OutlierFlagResult> observations,string algorithmVersion)
        {Mean=mean;SampleSd=sampleSd;Threshold=threshold;InclusiveMean=inclusiveMean;ExcludedMean=excludedMean;Observations=observations;AlgorithmVersion=algorithmVersion;}
        public double Mean{get;}public double SampleSd{get;}public double Threshold{get;}public double InclusiveMean{get;}public double ExcludedMean{get;}public IReadOnlyList<OutlierFlagResult> Observations{get;}public string AlgorithmVersion{get;}
        public int FlaggedCount=>Observations.Count(value=>value.IsStatisticalOutlier);
    }
    public sealed class FatigueResult
    {
        public FatigueResult(double firstHalfScore,double secondHalfScore,double? declinePercent,bool isFlagged,string algorithmVersion)
        {FirstHalfScore=firstHalfScore;SecondHalfScore=secondHalfScore;DeclinePercent=declinePercent;IsFlagged=isFlagged;AlgorithmVersion=algorithmVersion;}
        public double FirstHalfScore{get;}public double SecondHalfScore{get;}public double? DeclinePercent{get;}public bool IsFlagged{get;}public string AlgorithmVersion{get;}
    }
    public sealed class GradeResult
    {
        public GradeResult(string reactionTier,string consistencyTier,string finalGrade,double closeReactionTimeMs,double batteryConsistencyUtility,string contractVersion)
        {ReactionTier=reactionTier;ConsistencyTier=consistencyTier;FinalGrade=finalGrade;CloseReactionTimeMs=closeReactionTimeMs;BatteryConsistencyUtility=batteryConsistencyUtility;ContractVersion=contractVersion;}
        public string ReactionTier{get;}public string ConsistencyTier{get;}public string FinalGrade{get;}public double CloseReactionTimeMs{get;}public double BatteryConsistencyUtility{get;}public string ContractVersion{get;}
    }
    public sealed class ScoreSensitivityAnalysisResult
    {
        public ScoreSensitivityAnalysisResult(double inclusivePerformanceScore,double flaggedExcludedPerformanceScore,int inclusiveCount,int excludedCount)
        {InclusivePerformanceScore=inclusivePerformanceScore;FlaggedExcludedPerformanceScore=flaggedExcludedPerformanceScore;InclusiveCount=inclusiveCount;ExcludedCount=excludedCount;}
        public double InclusivePerformanceScore{get;}public double FlaggedExcludedPerformanceScore{get;}public int InclusiveCount{get;}public int ExcludedCount{get;}
    }
    internal sealed class GradeBand
    {
        public GradeBand(string grade,double? minimum,bool minimumInclusive,double? maximum,bool maximumInclusive){Grade=grade;Minimum=minimum;MinimumInclusive=minimumInclusive;Maximum=maximum;MaximumInclusive=maximumInclusive;}
        public string Grade{get;}public double? Minimum{get;}public bool MinimumInclusive{get;}public double? Maximum{get;}public bool MaximumInclusive{get;}
        public bool Contains(double value)=>(!Minimum.HasValue||(MinimumInclusive?value>=Minimum:value>Minimum))&&(!Maximum.HasValue||(MaximumInclusive?value<=Maximum:value<Maximum));
    }
    public sealed class ScientificRigorService
    {
        private static readonly string[] Grades={"S","A","B","C","D"};
        private readonly FrozenCalibrationConfiguration configuration;private readonly ScientificRigorContract contract;private readonly PerformanceScoringService scoring;private readonly IReadOnlyList<GradeBand> reactionBands;private readonly IReadOnlyList<GradeBand> consistencyBands;
        public ScientificRigorService(FrozenCalibrationConfiguration configuration,ScientificRigorContract contract)
        {this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));this.contract=contract??throw new ArgumentNullException(nameof(contract));scoring=new PerformanceScoringService(configuration);ParseBands(configuration.Record.ConsistencyTierCutpointsJson,out reactionBands,out consistencyBands);}
        public OutlierAnalysisResult AnalyzeOutliers(IReadOnlyList<MetricObservation> observations)
        {
            if(observations==null||observations.Count<2)throw new InvalidDataException("Outlier analysis requires at least two homogeneous post-adaptation observations.");
            if(observations.Any(value=>value==null))throw new InvalidDataException("Outlier observation is missing.");
            double mean=observations.Average(value=>value.Value),sum=observations.Sum(value=>(value.Value-mean)*(value.Value-mean)),sd=Math.Sqrt(sum/(observations.Count-1)),threshold=contract.OutlierSampleSdMultiplier*sd;
            var flags=observations.Select(value=>new OutlierFlagResult(value,Math.Abs(value.Value-mean)>threshold)).ToArray();double[] included=flags.Where(value=>!value.IsStatisticalOutlier).Select(value=>value.Observation.Value).ToArray();
            return new OutlierAnalysisResult(mean,sd,threshold,mean,included.Length==0?mean:included.Average(),new ReadOnlyCollection<OutlierFlagResult>(flags),contract.OutlierAlgorithmVersion);
        }
        public FatigueResult EvaluateShotFatigue(TestMode mode,IReadOnlyList<ShotScoringObservation> postAdaptationObservations)
        {if(mode==TestMode.Tracking)throw new ArgumentException("Tracking requires window fatigue.",nameof(mode));Split(postAdaptationObservations,out IReadOnlyList<ShotScoringObservation> first,out IReadOnlyList<ShotScoringObservation> second);return EvaluateFatigueScores(scoring.ScoreShotSubset(mode,first).PerformanceScore,scoring.ScoreShotSubset(mode,second).PerformanceScore);}
        public FatigueResult EvaluateTrackingFatigue(IReadOnlyList<TrackingScoringWindow> postAdaptationWindows)
        {Split(postAdaptationWindows,out IReadOnlyList<TrackingScoringWindow> first,out IReadOnlyList<TrackingScoringWindow> second);return EvaluateFatigueScores(scoring.ScoreTrackingSubset(first).PerformanceScore,scoring.ScoreTrackingSubset(second).PerformanceScore);}
        public FatigueResult EvaluateFatigueScores(double firstHalfScore,double secondHalfScore)
        {Finite(firstHalfScore);Finite(secondHalfScore);if(Math.Abs(firstHalfScore)<=configuration.Record.ScoringZeroTolerance)return new FatigueResult(firstHalfScore,secondHalfScore,null,false,contract.FatigueAlgorithmVersion);double decline=(firstHalfScore-secondHalfScore)/firstHalfScore*contract.FatiguePercentageScale;return new FatigueResult(firstHalfScore,secondHalfScore,decline,decline>contract.FatigueDeclineThresholdPercent,contract.FatigueAlgorithmVersion);}
        public ScoreSensitivityAnalysisResult ScoreShotFlagSensitivity(TestMode mode,IReadOnlyList<ShotScoringObservation> authoritative,IReadOnlyCollection<int> statisticallyFlaggedIndexes)
        {if(authoritative==null)throw new ArgumentNullException(nameof(authoritative));IReadOnlyList<ShotScoringObservation> retained=Exclude(authoritative,statisticallyFlaggedIndexes);return new ScoreSensitivityAnalysisResult(scoring.ScoreShotMode(mode,authoritative).PerformanceScore,scoring.ScoreShotSubset(mode,retained).PerformanceScore,authoritative.Count,authoritative.Count-retained.Count);}
        public ScoreSensitivityAnalysisResult ScoreTrackingFlagSensitivity(IReadOnlyList<TrackingScoringWindow> authoritative,IReadOnlyCollection<int> statisticallyFlaggedIndexes)
        {if(authoritative==null)throw new ArgumentNullException(nameof(authoritative));IReadOnlyList<TrackingScoringWindow> retained=Exclude(authoritative,statisticallyFlaggedIndexes);return new ScoreSensitivityAnalysisResult(scoring.ScoreTracking(authoritative).PerformanceScore,scoring.ScoreTrackingSubset(retained).PerformanceScore,authoritative.Count,authoritative.Count-retained.Count);}
        public GradeResult AssignGrade(double closeFlickAuthoritativeMeanReactionMs,IReadOnlyList<ModeScoreResult> modeScores)
        {
            Finite(closeFlickAuthoritativeMeanReactionMs);if(closeFlickAuthoritativeMeanReactionMs<0d)throw new ArgumentOutOfRangeException(nameof(closeFlickAuthoritativeMeanReactionMs));
            if(modeScores==null||modeScores.Count!=Enum.GetValues(typeof(TestMode)).Length||modeScores.Any(value=>value==null)||modeScores.Select(value=>value.Mode).Distinct().Count()!=modeScores.Count)throw new InvalidDataException("Grade assignment requires each mode exactly once.");
            foreach(ModeScoreResult value in modeScores)if(value.FormulaVersion!=configuration.FormulaVersion.Value||value.NormalizationVersion!=configuration.Record.NormalizationVersion)throw new InvalidDataException("Grade mode-score version mismatch.");
            double consistency=modeScores.Average(value=>value.ConsistencyUtility);string reaction=Tier(reactionBands,closeFlickAuthoritativeMeanReactionMs),consistencyTier=Tier(consistencyBands,consistency);string final=Grades[Math.Max(Array.IndexOf(Grades,reaction),Array.IndexOf(Grades,consistencyTier))];
            return new GradeResult(reaction,consistencyTier,final,closeFlickAuthoritativeMeanReactionMs,consistency,contract.GradeContractVersion);
        }
        private static void Split<T>(IReadOnlyList<T> values,out IReadOnlyList<T> first,out IReadOnlyList<T> second)
        {if(values==null||values.Count<4)throw new InvalidDataException("Fatigue scoring requires enough post-adaptation observations for sample SD in both halves.");int midpoint=values.Count/2;first=values.Take(midpoint).ToArray();second=values.Skip(midpoint).ToArray();}
        private static IReadOnlyList<T> Exclude<T>(IReadOnlyList<T> values,IReadOnlyCollection<int> indexes)
        {if(indexes==null)throw new ArgumentNullException(nameof(indexes));var unique=new HashSet<int>();foreach(int index in indexes)if(index<0||index>=values.Count||!unique.Add(index))throw new InvalidDataException("Sensitivity-analysis indexes must be unique and within the authoritative set.");T[] retained=values.Where((value,index)=>!unique.Contains(index)).ToArray();if(retained.Length<2)throw new InvalidDataException("Flag-excluded sensitivity analysis requires at least two retained observations.");return retained;}
        private static string Tier(IReadOnlyList<GradeBand> bands,double value){foreach(GradeBand band in bands)if(band.Contains(value))return band.Grade;throw new InvalidDataException("Grade value is outside the frozen tier contract.");}
        private static void ParseBands(string json,out IReadOnlyList<GradeBand> reaction,out IReadOnlyList<GradeBand> consistency)
        {using JsonDocument document=JsonDocument.Parse(json);var reactionValues=new List<GradeBand>();var consistencyValues=new List<GradeBand>();JsonElement reactionRoot=document.RootElement.GetProperty("reaction_tiers_ms"),consistencyRoot=document.RootElement.GetProperty("consistency_tiers");foreach(string grade in Grades){JsonElement band=reactionRoot.GetProperty(grade);reactionValues.Add(new GradeBand(grade,NullableNumber(band,"minimum"),band.GetProperty("minimum_inclusive").GetBoolean(),NullableNumber(band,"maximum"),band.GetProperty("maximum_inclusive").GetBoolean()));band=consistencyRoot.GetProperty(grade);double minimum=band.GetProperty("minimum_inclusive").GetDouble();if(band.TryGetProperty("maximum_exclusive",out JsonElement exclusive))consistencyValues.Add(new GradeBand(grade,minimum,true,exclusive.GetDouble(),false));else consistencyValues.Add(new GradeBand(grade,minimum,true,band.GetProperty("maximum_inclusive").GetDouble(),true));}reaction=reactionValues.AsReadOnly();consistency=consistencyValues.AsReadOnly();}
        private static double? NullableNumber(JsonElement element,string name){if(string.IsNullOrEmpty(name)||!element.TryGetProperty(name,out JsonElement value)||value.ValueKind==JsonValueKind.Null)return null;return value.GetDouble();}
        private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new InvalidDataException("Scientific-rigor value must be finite.");
    }
}
