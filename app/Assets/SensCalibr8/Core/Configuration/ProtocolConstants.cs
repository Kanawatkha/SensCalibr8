using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SensCalibr8.Core.Configuration
{
    public sealed class ProtocolConstants
    {
        public ProtocolConstants(string version,int phaseOneCandidateCount,IReadOnlyList<double> phaseOneOffsetsPercent,string phaseOneGenerationRule)
        {
            Version=Required(version,nameof(version));PhaseOneCandidateCount=phaseOneCandidateCount>0?phaseOneCandidateCount:throw new ArgumentOutOfRangeException(nameof(phaseOneCandidateCount));if(phaseOneOffsetsPercent==null||phaseOneOffsetsPercent.Count!=phaseOneCandidateCount)throw new ArgumentException("Phase 1 offsets must match the candidate count.",nameof(phaseOneOffsetsPercent));var copy=new List<double>(phaseOneOffsetsPercent.Count);var unique=new HashSet<double>();foreach(double value in phaseOneOffsetsPercent){if(double.IsNaN(value)||double.IsInfinity(value)||!unique.Add(value))throw new ArgumentException("Phase 1 offsets must be finite and unique.",nameof(phaseOneOffsetsPercent));copy.Add(value);}PhaseOneOffsetsPercent=new ReadOnlyCollection<double>(copy);PhaseOneGenerationRule=Required(phaseOneGenerationRule,nameof(phaseOneGenerationRule));
        }
        public string Version{get;}public int PhaseOneCandidateCount{get;}public IReadOnlyList<double> PhaseOneOffsetsPercent{get;}public string PhaseOneGenerationRule{get;}
        private static string Required(string value,string field)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException(field+" is required.",field);
    }
}
