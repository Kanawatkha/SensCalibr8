using System;
namespace SensCalibr8.Core.Configuration
{
    public sealed class ContinuousCycleContract
    {
        public ContinuousCycleContract(string version,int minimumSessions,int maximumSessions,int plateauCycles,double plateauChangePercent,double percentageScale,string trainingPurpose,string generationRule)
        {Version=Required(version);MinimumSessions=Positive(minimumSessions);MaximumSessions=maximumSessions>=minimumSessions?maximumSessions:throw new ArgumentOutOfRangeException(nameof(maximumSessions));PlateauCycles=Positive(plateauCycles);PlateauChangePercent=Positive(plateauChangePercent);PercentageScale=Positive(percentageScale);TrainingPurpose=Required(trainingPurpose);GenerationRule=Required(generationRule);}
        public string Version{get;}public int MinimumSessions{get;}public int MaximumSessions{get;}public int PlateauCycles{get;}public double PlateauChangePercent{get;}public double PercentageScale{get;}public string TrainingPurpose{get;}public string GenerationRule{get;}
        private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Required contract field is missing.");private static int Positive(int value)=>value>0?value:throw new ArgumentOutOfRangeException();private static double Positive(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>0d?value:throw new ArgumentOutOfRangeException();
    }
}
