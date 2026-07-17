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
    public sealed class MetricBound
    {
        public MetricBound(double lower, double upper)
        {
            if (!Finite(lower) || !Finite(upper) || upper <= lower) throw new InvalidDataException("Metric bound requires finite L < U.");
            Lower = lower; Upper = upper;
        }
        public double Lower { get; } public double Upper { get; }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class FrozenScoringRules
    {
        private FrozenScoringRules(string normalizationVersion, IReadOnlyDictionary<string, IReadOnlyDictionary<string, MetricBound>> bounds,
            IReadOnlyDictionary<string, MetricBound> submovementBounds)
        { NormalizationVersion=normalizationVersion;Bounds=bounds;SubmovementBounds=submovementBounds; }
        public string NormalizationVersion { get; }
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, MetricBound>> Bounds { get; }
        public IReadOnlyDictionary<string, MetricBound> SubmovementBounds { get; }

        public static FrozenScoringRules From(FrozenCalibrationConfiguration configuration)
        {
            if(configuration==null)throw new ArgumentNullException(nameof(configuration));
            using JsonDocument normalization=JsonDocument.Parse(configuration.Record.NormalizationBoundsJson);
            using JsonDocument submovement=JsonDocument.Parse(configuration.Record.SubmovementBoundsByModeJson);
            string version=normalization.RootElement.GetProperty("normalization_version").GetString();
            if(!string.Equals(version,configuration.Record.NormalizationVersion,StringComparison.Ordinal)||
               !string.Equals(submovement.RootElement.GetProperty("normalization_version").GetString(),version,StringComparison.Ordinal))
                throw new InvalidDataException("Scoring normalization identity mismatch.");
            var all=new Dictionary<string,IReadOnlyDictionary<string,MetricBound>>(StringComparer.Ordinal);
            foreach(JsonProperty mode in normalization.RootElement.GetProperty("bounds").EnumerateObject())
            { var metrics=new Dictionary<string,MetricBound>(StringComparer.Ordinal);foreach(JsonProperty metric in mode.Value.EnumerateObject()){double[] pair=metric.Value.EnumerateArray().Select(value=>value.GetDouble()).ToArray();if(pair.Length!=2)throw new InvalidDataException("Metric bound pair is invalid.");metrics.Add(metric.Name,new MetricBound(pair[0],pair[1]));}all.Add(mode.Name,new ReadOnlyDictionary<string,MetricBound>(metrics)); }
            var sub=new Dictionary<string,MetricBound>(StringComparer.Ordinal);foreach(JsonProperty mode in submovement.RootElement.GetProperty("bounds_by_mode").EnumerateObject()){double[] pair=mode.Value.EnumerateArray().Select(value=>value.GetDouble()).ToArray();if(pair.Length!=2)throw new InvalidDataException("Submovement bound pair is invalid.");sub.Add(mode.Name,new MetricBound(pair[0],pair[1]));}
            return new FrozenScoringRules(version,new ReadOnlyDictionary<string,IReadOnlyDictionary<string,MetricBound>>(all),new ReadOnlyDictionary<string,MetricBound>(sub));
        }
        public MetricBound Require(string mode,string metric){if(!Bounds.TryGetValue(mode,out IReadOnlyDictionary<string,MetricBound> metrics)||!metrics.TryGetValue(metric,out MetricBound value))throw new InvalidDataException("Required scoring bound is missing: "+mode+"/"+metric);return value;}
    }

    public sealed class ShotScoringObservation
    {
        public ShotScoringObservation(bool isHit,double? primaryTimeMs,double finalPrecisionErrorDeg,double? submovementCount)
        { IsHit=isHit;PrimaryTimeMs=primaryTimeMs;FinalPrecisionErrorDeg=FiniteNonNegative(finalPrecisionErrorDeg,nameof(finalPrecisionErrorDeg));if(submovementCount.HasValue)SubmovementCount=FiniteNonNegative(submovementCount.Value,nameof(submovementCount)); }
        public bool IsHit{get;}public double? PrimaryTimeMs{get;}public double FinalPrecisionErrorDeg{get;}public double? SubmovementCount{get;}
        private static double FiniteNonNegative(double value,string field)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0d?value:throw new ArgumentOutOfRangeException(field);
    }

    public sealed class TrackingScoringWindow
    {
        public TrackingScoringWindow(double timeOnTargetPercent,double trackingDeviationRmsDeg)
        { TimeOnTargetPercent=FiniteNonNegative(timeOnTargetPercent,nameof(timeOnTargetPercent));TrackingDeviationRmsDeg=FiniteNonNegative(trackingDeviationRmsDeg,nameof(trackingDeviationRmsDeg)); }
        public double TimeOnTargetPercent{get;}public double TrackingDeviationRmsDeg{get;}
        private static double FiniteNonNegative(double value,string field)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0d?value:throw new ArgumentOutOfRangeException(field);
    }

    public sealed class ModeScoreResult
    {
        public ModeScoreResult(TestMode mode,double performanceScore,double consistencyUtility,double accuracyOrTimeOnTargetUtility,double? reactionUtility,double precisionUtility,double? submovementPenalty,string formulaVersion,string normalizationVersion,int sampleSize)
        { Mode=mode;PerformanceScore=performanceScore;ConsistencyUtility=consistencyUtility;AccuracyOrTimeOnTargetUtility=accuracyOrTimeOnTargetUtility;ReactionUtility=reactionUtility;PrecisionUtility=precisionUtility;SubmovementPenalty=submovementPenalty;FormulaVersion=formulaVersion;NormalizationVersion=normalizationVersion;SampleSize=sampleSize; }
        public TestMode Mode{get;}public double PerformanceScore{get;}public double ConsistencyUtility{get;}public double AccuracyOrTimeOnTargetUtility{get;}public double? ReactionUtility{get;}public double PrecisionUtility{get;}public double? SubmovementPenalty{get;}public string FormulaVersion{get;}public string NormalizationVersion{get;}public int SampleSize{get;}
    }

    public sealed class BatteryScoreResult
    {
        public BatteryScoreResult(double average,IReadOnlyDictionary<TestMode,double> byMode,string formulaVersion,string normalizationVersion)
        { AveragePerformanceScore=average;PerformanceScoreByMode=byMode;FormulaVersion=formulaVersion;NormalizationVersion=normalizationVersion; }
        public double AveragePerformanceScore{get;}public IReadOnlyDictionary<TestMode,double> PerformanceScoreByMode{get;}public string FormulaVersion{get;}public string NormalizationVersion{get;}
    }

    public sealed class PerformanceScoringService
    {
        private readonly FrozenCalibrationConfiguration configuration;private readonly ScoringFormulaContract formula;private readonly FrozenScoringRules rules;
        public PerformanceScoringService(FrozenCalibrationConfiguration configuration){this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));formula=configuration.ScoringFormula;rules=FrozenScoringRules.From(configuration);if(!string.Equals(formula.FormulaVersion,configuration.FormulaVersion.Value,StringComparison.Ordinal))throw new InvalidDataException("Scoring formula identity mismatch.");}
        public double NormalizeHigherIsBetter(double value,MetricBound bound)=>Clamp((Finite(value)-bound.Lower)/(bound.Upper-bound.Lower));
        public double NormalizeLowerIsBetter(double value,MetricBound bound)=>1d-NormalizeHigherIsBetter(value,bound);
        public double ComposeShotScore(double consistencyUtility,double accuracyUtility,double reactionUtility,double precisionUtility,double submovementPenalty)
        { return formula.Multiplier*(Unit(consistencyUtility)*formula.ShotConsistencyWeight+Unit(accuracyUtility)*formula.ShotAccuracyWeight+Unit(reactionUtility)*formula.ShotReactionWeight+Unit(precisionUtility)*formula.ShotPrecisionWeight-Unit(submovementPenalty)*formula.ShotSubmovementPenaltyWeight); }
        public double ComposeTrackingScore(double consistencyUtility,double timeOnTargetUtility,double precisionUtility)
        { return formula.Multiplier*(Unit(consistencyUtility)*formula.TrackingConsistencyWeight+Unit(timeOnTargetUtility)*formula.TrackingTimeOnTargetWeight+Unit(precisionUtility)*formula.TrackingPrecisionWeight); }
        public ModeScoreResult ScoreShotMode(TestMode mode,IReadOnlyList<ShotScoringObservation> observations)
        { return ScoreShot(mode,observations,true); }
        public ModeScoreResult ScoreShotSubset(TestMode mode,IReadOnlyList<ShotScoringObservation> observations)
        { return ScoreShot(mode,observations,false); }
        private ModeScoreResult ScoreShot(TestMode mode,IReadOnlyList<ShotScoringObservation> observations,bool requireAuthoritativeCount)
        {
            string name=ModeName(mode);if(mode==TestMode.Tracking)throw new ArgumentException("Tracking requires window scoring.",nameof(mode));if(observations==null||(requireAuthoritativeCount?observations.Count!=formula.AuthoritativeShotObservations:observations.Count<2))throw new InvalidDataException(requireAuthoritativeCount?"Shot scoring requires the frozen authoritative observation count.":"Shot subset scoring requires at least two observations for sample SD.");
            MetricBound reaction=rules.Require(name,ReactionMetric(mode)),precision=rules.Require(name,"final_precision_error_deg"),consistency=rules.Require(name,"consistency_sample_sd_deg"),accuracy=rules.Require(name,"accuracy_percent");
            var times=new List<double>(observations.Count);var errors=new List<double>(observations.Count);var hitCounts=new List<double>();int hits=0;
            foreach(ShotScoringObservation value in observations){if(value==null)throw new InvalidDataException("Shot observation is missing.");if(value.IsHit){hits++;if(!value.SubmovementCount.HasValue)throw new InvalidDataException("Every authoritative hit requires Submovement Count.");hitCounts.Add(value.SubmovementCount.Value);}double time=value.PrimaryTimeMs??(mode==TestMode.FlickFar?reaction.Upper:throw new InvalidDataException("Required primary time is missing."));times.Add(FiniteNonNegative(time));errors.Add(value.FinalPrecisionErrorDeg);}
            double consistencyUtility=NormalizeLowerIsBetter(SampleSd(errors),consistency),accuracyUtility=NormalizeHigherIsBetter(formula.Multiplier*hits/observations.Count,accuracy),reactionUtility=NormalizeLowerIsBetter(times.Average(),reaction),precisionUtility=NormalizeLowerIsBetter(errors.Average(),precision);
            double penalty=hitCounts.Count==0?1d:NormalizeHigherIsBetter(hitCounts.Average(),rules.SubmovementBounds[name]);
            double score=ComposeShotScore(consistencyUtility,accuracyUtility,reactionUtility,precisionUtility,penalty);
            return new ModeScoreResult(mode,score,consistencyUtility,accuracyUtility,reactionUtility,precisionUtility,penalty,formula.FormulaVersion,rules.NormalizationVersion,observations.Count);
        }
        public ModeScoreResult ScoreTracking(IReadOnlyList<TrackingScoringWindow> windows)
        { return ScoreTrackingWindows(windows,true); }
        public ModeScoreResult ScoreTrackingSubset(IReadOnlyList<TrackingScoringWindow> windows)
        { return ScoreTrackingWindows(windows,false); }
        private ModeScoreResult ScoreTrackingWindows(IReadOnlyList<TrackingScoringWindow> windows,bool requireAuthoritativeCount)
        {
            if(windows==null||(requireAuthoritativeCount?windows.Count!=formula.AuthoritativeTrackingWindows:windows.Count<2))throw new InvalidDataException(requireAuthoritativeCount?"Tracking scoring requires the frozen authoritative window count.":"Tracking subset scoring requires at least two windows for sample SD.");var totals=new List<double>(windows.Count);var deviations=new List<double>(windows.Count);foreach(TrackingScoringWindow value in windows){if(value==null)throw new InvalidDataException("Tracking window is missing.");totals.Add(value.TimeOnTargetPercent);deviations.Add(value.TrackingDeviationRmsDeg);}double consistency=NormalizeLowerIsBetter(SampleSd(deviations),rules.Require("tracking","consistency_sample_sd_deg")),time=NormalizeHigherIsBetter(totals.Average(),rules.Require("tracking","time_on_target_percent")),precision=NormalizeLowerIsBetter(deviations.Average(),rules.Require("tracking","tracking_deviation_rms_deg"));double score=ComposeTrackingScore(consistency,time,precision);return new ModeScoreResult(TestMode.Tracking,score,consistency,time,null,precision,null,formula.FormulaVersion,rules.NormalizationVersion,windows.Count);
        }
        public BatteryScoreResult ScoreBattery(IReadOnlyList<ModeScoreResult> modes)
        { if(modes==null||modes.Count!=Enum.GetValues(typeof(TestMode)).Length||modes.Select(value=>value.Mode).Distinct().Count()!=modes.Count)throw new InvalidDataException("Battery scoring requires each mode exactly once.");foreach(ModeScoreResult value in modes)if(value==null||value.FormulaVersion!=formula.FormulaVersion||value.NormalizationVersion!=rules.NormalizationVersion)throw new InvalidDataException("Mode score version mismatch.");var byMode=new ReadOnlyDictionary<TestMode,double>(modes.ToDictionary(value=>value.Mode,value=>value.PerformanceScore));return new BatteryScoreResult(modes.Average(value=>value.PerformanceScore),byMode,formula.FormulaVersion,rules.NormalizationVersion); }
        private static double SampleSd(IReadOnlyList<double> values){if(values==null||values.Count<2)throw new InvalidDataException("Sample SD requires at least two observations.");double mean=values.Average(),sum=0d;foreach(double value in values){double delta=Finite(value)-mean;sum+=delta*delta;}return Math.Sqrt(sum/(values.Count-1));}
        private static double Clamp(double value)=>value<0d?0d:value>1d?1d:value;private static double Unit(double value)=>Finite(value)>=0d&&value<=1d?value:throw new ArgumentOutOfRangeException(nameof(value));private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new InvalidDataException("Scoring value must be finite.");private static double FiniteNonNegative(double value)=>Finite(value)>=0d?value:throw new InvalidDataException("Scoring value must be non-negative.");
        private static string ReactionMetric(TestMode mode){switch(mode){case TestMode.FlickClose:return "reaction_time_ms";case TestMode.FlickFar:return "travel_time_ms";case TestMode.MicroCorrection:return "correction_time_ms";default:throw new ArgumentOutOfRangeException(nameof(mode));}}
        private static string ModeName(TestMode mode){switch(mode){case TestMode.FlickClose:return "flick_close";case TestMode.FlickFar:return "flick_far";case TestMode.MicroCorrection:return "micro_correction";case TestMode.Tracking:return "tracking";default:throw new ArgumentOutOfRangeException(nameof(mode));}}
    }
}
