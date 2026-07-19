using System;
using System.Collections.Generic;

namespace SensCalibr8.Data.Repositories
{
    public static class HtmlReportInputContract { public const string Version = "sc8-html-report-input-v3"; }

    public sealed class HtmlReportScoreRecord
    {
        public HtmlReportScoreRecord(long cycleId,long phase,double edpi,double sensitivityValue,double performanceScore,string grade,string formulaVersion,string configurationVersion,string completedDate)
        { CycleId=Positive(cycleId);Phase=Positive(phase);Edpi=Finite(edpi);SensitivityValue=Finite(sensitivityValue);PerformanceScore=Finite(performanceScore);Grade=grade;FormulaVersion=Required(formulaVersion);ConfigurationVersion=Required(configurationVersion);CompletedDate=Required(completedDate); }
        public long CycleId{get;} public long Phase{get;} public double Edpi{get;} public double SensitivityValue{get;} public double PerformanceScore{get;} public string Grade{get;} public string FormulaVersion{get;} public string ConfigurationVersion{get;} public string CompletedDate{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportShotRecord
    {
        public HtmlReportShotRecord(long sessionId,string date,long cycleId,long phase,string mode,double sensitivityValue,double edpi,double finalPrecisionErrorDeg,double? signedAimErrorDeg,long? submovementCount,string targetCenterPosition,string configurationVersion)
        { SessionId=Positive(sessionId);Date=Required(date);CycleId=Positive(cycleId);Phase=Positive(phase);Mode=Required(mode);SensitivityValue=Finite(sensitivityValue);Edpi=Finite(edpi);FinalPrecisionErrorDeg=Finite(finalPrecisionErrorDeg);SignedAimErrorDeg=signedAimErrorDeg;SubmovementCount=submovementCount;TargetCenterPosition=Required(targetCenterPosition);ConfigurationVersion=Required(configurationVersion); }
        public long SessionId{get;}public string Date{get;}public long CycleId{get;}public long Phase{get;}public string Mode{get;}public double SensitivityValue{get;}public double Edpi{get;}public double FinalPrecisionErrorDeg{get;}public double? SignedAimErrorDeg{get;}public long? SubmovementCount{get;}public string TargetCenterPosition{get;}public string ConfigurationVersion{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportTrackingErrorRecord
    {
        public HtmlReportTrackingErrorRecord(string date,long cycleId,long phase,double sensitivityValue,double deviationRmsDeg,string configurationVersion)
        { Date=Required(date);CycleId=Positive(cycleId);Phase=Positive(phase);SensitivityValue=Finite(sensitivityValue);DeviationRmsDeg=Finite(deviationRmsDeg);ConfigurationVersion=Required(configurationVersion); }
        public string Date{get;}public long CycleId{get;}public long Phase{get;}public double SensitivityValue{get;}public double DeviationRmsDeg{get;}public string ConfigurationVersion{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportWinnerRecord
    {
        public HtmlReportWinnerRecord(long cycleNumber,long phase,double edpi,double sensitivityValue,string timestamp)
        { CycleNumber=Positive(cycleNumber);Phase=Positive(phase);Edpi=Finite(edpi);SensitivityValue=Finite(sensitivityValue);Timestamp=Required(timestamp); }
        public long CycleNumber{get;}public long Phase{get;}public double Edpi{get;}public double SensitivityValue{get;}public string Timestamp{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportReactionRecord
    {
        public HtmlReportReactionRecord(long sessionId,string date,long cycleId,long phase,string mode,double sensitivityValue,double reactionTimeMs,string configurationVersion)
        {SessionId=Positive(sessionId);Date=Required(date);CycleId=Positive(cycleId);Phase=Positive(phase);Mode=Required(mode);SensitivityValue=Finite(sensitivityValue);ReactionTimeMs=NonNegative(reactionTimeMs);ConfigurationVersion=Required(configurationVersion);}
        public long SessionId{get;}public string Date{get;}public long CycleId{get;}public long Phase{get;}public string Mode{get;}public double SensitivityValue{get;}public double ReactionTimeMs{get;}public string ConfigurationVersion{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static double NonNegative(double value)=>Finite(value)>=0d?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportProfileComparisonRecord
    {
        public HtmlReportProfileComparisonRecord(long profileId,string profileName,long cycleNumber,double winnerEdpi)
        {ProfileId=Positive(profileId);ProfileName=Required(profileName);CycleNumber=Positive(cycleNumber);WinnerEdpi=Finite(winnerEdpi);}
        public long ProfileId{get;}public string ProfileName{get;}public long CycleNumber{get;}public double WinnerEdpi{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportOutlierRecord
    {
        public HtmlReportOutlierRecord(long phase,string mode,double sensitivityValue,string metricName,double inclusiveMean,double flaggedExcludedMean,long observationCount,long flaggedCount,string algorithmVersion,string configurationVersion)
        {Phase=Positive(phase);Mode=Required(mode);SensitivityValue=Finite(sensitivityValue);MetricName=Required(metricName);InclusiveMean=Finite(inclusiveMean);FlaggedExcludedMean=Finite(flaggedExcludedMean);ObservationCount=Positive(observationCount);FlaggedCount=NonNegative(flaggedCount);AlgorithmVersion=Required(algorithmVersion);ConfigurationVersion=Required(configurationVersion);}
        public long Phase{get;}public string Mode{get;}public double SensitivityValue{get;}public string MetricName{get;}public double InclusiveMean{get;}public double FlaggedExcludedMean{get;}public long ObservationCount{get;}public long FlaggedCount{get;}public string AlgorithmVersion{get;}public string ConfigurationVersion{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static long NonNegative(long value)=>value>=0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportFatigueRecord
    {
        public HtmlReportFatigueRecord(long sessionId,string date,string mode,bool fatigueFlag,double? scoreChangePercentage,string algorithmVersion)
        {SessionId=Positive(sessionId);Date=Required(date);Mode=Required(mode);FatigueFlag=fatigueFlag;ScoreChangePercentage=scoreChangePercentage;AlgorithmVersion=algorithmVersion;}
        public long SessionId{get;}public string Date{get;}public string Mode{get;}public bool FatigueFlag{get;}public double? ScoreChangePercentage{get;}public string AlgorithmVersion{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportWarningRecord
    {
        public HtmlReportWarningRecord(string flagType,string triggeredDate,double edpi,bool acknowledged){FlagType=Required(flagType);TriggeredDate=Required(triggeredDate);Edpi=Finite(edpi);Acknowledged=acknowledged;}
        public string FlagType{get;}public string TriggeredDate{get;}public double Edpi{get;}public bool Acknowledged{get;}
        private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportSignificanceRecord
    {
        public HtmlReportSignificanceRecord(long phase,double candidateAEdpi,double candidateBEdpi,double alpha,double pValue,bool isSignificant,string result,string method,string formulaVersion)
        {Phase=Positive(phase);CandidateAEdpi=Finite(candidateAEdpi);CandidateBEdpi=Finite(candidateBEdpi);Alpha=Finite(alpha);PValue=Finite(pValue);IsSignificant=isSignificant;Result=Required(result);Method=Required(method);FormulaVersion=Required(formulaVersion);}
        public long Phase{get;}public double CandidateAEdpi{get;}public double CandidateBEdpi{get;}public double Alpha{get;}public double PValue{get;}public bool IsSignificant{get;}public string Result{get;}public string Method{get;}public string FormulaVersion{get;}
        private static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)?value:throw new ArgumentOutOfRangeException();private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Report field is required.");
    }
    public sealed class HtmlReportInput
    {
        public HtmlReportInput(AnalysisProfileIdentity profile,IReadOnlyList<HtmlReportScoreRecord> scores,IReadOnlyList<HtmlReportShotRecord> shots,IReadOnlyList<HtmlReportTrackingErrorRecord> trackingErrors,IReadOnlyList<HtmlReportWinnerRecord> winners,IReadOnlyList<HtmlReportReactionRecord> reactions,IReadOnlyList<HtmlReportProfileComparisonRecord> profileComparisons,IReadOnlyList<HtmlReportOutlierRecord> outliers,IReadOnlyList<HtmlReportFatigueRecord> fatigue,IReadOnlyList<HtmlReportWarningRecord> warnings,IReadOnlyList<HtmlReportSignificanceRecord> significance)
        { ReportInputVersion=HtmlReportInputContract.Version;Profile=profile??throw new ArgumentNullException(nameof(profile));Scores=scores??throw new ArgumentNullException(nameof(scores));Shots=shots??throw new ArgumentNullException(nameof(shots));TrackingErrors=trackingErrors??throw new ArgumentNullException(nameof(trackingErrors));Winners=winners??throw new ArgumentNullException(nameof(winners));Reactions=reactions??throw new ArgumentNullException(nameof(reactions));ProfileComparisons=profileComparisons??throw new ArgumentNullException(nameof(profileComparisons));Outliers=outliers??throw new ArgumentNullException(nameof(outliers));Fatigue=fatigue??throw new ArgumentNullException(nameof(fatigue));Warnings=warnings??throw new ArgumentNullException(nameof(warnings));Significance=significance??throw new ArgumentNullException(nameof(significance)); }
        public string ReportInputVersion{get;}public AnalysisProfileIdentity Profile{get;}public IReadOnlyList<HtmlReportScoreRecord> Scores{get;}public IReadOnlyList<HtmlReportShotRecord> Shots{get;}public IReadOnlyList<HtmlReportTrackingErrorRecord> TrackingErrors{get;}public IReadOnlyList<HtmlReportWinnerRecord> Winners{get;}public IReadOnlyList<HtmlReportReactionRecord> Reactions{get;}public IReadOnlyList<HtmlReportProfileComparisonRecord> ProfileComparisons{get;}public IReadOnlyList<HtmlReportOutlierRecord> Outliers{get;}public IReadOnlyList<HtmlReportFatigueRecord> Fatigue{get;}public IReadOnlyList<HtmlReportWarningRecord> Warnings{get;}public IReadOnlyList<HtmlReportSignificanceRecord> Significance{get;}
    }
}
