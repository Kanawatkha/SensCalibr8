using System;

namespace SensCalibr8.Core.Configuration
{
    public sealed class ScientificRigorContract
    {
        public ScientificRigorContract(string version,double adaptationFraction,int trackingAdaptationBlocks,
            string outlierAlgorithmVersion,double outlierSampleSdMultiplier,string fatigueAlgorithmVersion,
            double fatigueDeclineThresholdPercent,double fatiguePercentageScale,string gradeContractVersion)
        {
            Version=Required(version,nameof(version));AdaptationFraction=Fraction(adaptationFraction,nameof(adaptationFraction));
            TrackingAdaptationBlocks=trackingAdaptationBlocks>=0?trackingAdaptationBlocks:throw new ArgumentOutOfRangeException(nameof(trackingAdaptationBlocks));
            OutlierAlgorithmVersion=Required(outlierAlgorithmVersion,nameof(outlierAlgorithmVersion));
            OutlierSampleSdMultiplier=Positive(outlierSampleSdMultiplier,nameof(outlierSampleSdMultiplier));
            FatigueAlgorithmVersion=Required(fatigueAlgorithmVersion,nameof(fatigueAlgorithmVersion));
            FatigueDeclineThresholdPercent=Positive(fatigueDeclineThresholdPercent,nameof(fatigueDeclineThresholdPercent));
            FatiguePercentageScale=Positive(fatiguePercentageScale,nameof(fatiguePercentageScale));
            GradeContractVersion=Required(gradeContractVersion,nameof(gradeContractVersion));
        }
        public string Version{get;} public double AdaptationFraction{get;} public int TrackingAdaptationBlocks{get;}
        public string OutlierAlgorithmVersion{get;} public double OutlierSampleSdMultiplier{get;}
        public string FatigueAlgorithmVersion{get;} public double FatigueDeclineThresholdPercent{get;}public double FatiguePercentageScale{get;}
        public string GradeContractVersion{get;}
        private static string Required(string value,string field)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException(field+" is required.",field);
        private static double Positive(double value,string field)=>Finite(value)&&value>0d?value:throw new ArgumentOutOfRangeException(field);
        private static double Fraction(double value,string field)=>Finite(value)&&value>0d&&value<1d?value:throw new ArgumentOutOfRangeException(field);
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
