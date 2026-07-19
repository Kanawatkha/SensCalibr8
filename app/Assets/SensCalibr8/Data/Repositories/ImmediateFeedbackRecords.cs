using System;
using System.Collections.Generic;

namespace SensCalibr8.Data.Repositories
{
    public sealed class AccuracyEvidenceRecord
    {
        public AccuracyEvidenceRecord(double sensitivityValue, long hitCount, long resolvedCount)
        {
            if (double.IsNaN(sensitivityValue) || double.IsInfinity(sensitivityValue)) throw new ArgumentOutOfRangeException(nameof(sensitivityValue));
            if (hitCount < 0 || resolvedCount <= 0 || hitCount > resolvedCount) throw new ArgumentOutOfRangeException(nameof(hitCount));
            SensitivityValue = sensitivityValue;
            HitCount = hitCount;
            ResolvedCount = resolvedCount;
        }

        public double SensitivityValue { get; }
        public long HitCount { get; }
        public long ResolvedCount { get; }
    }

    public sealed class ImmediateFeedbackDataRecord
    {
        public ImmediateFeedbackDataRecord(double? bestSensitivity, double? latestPerformanceScore, string currentMode,
            long? currentCycleId, long? currentPhase, IReadOnlyList<AccuracyEvidenceRecord> accuracyEvidence)
        {
            BestSensitivity = bestSensitivity;
            LatestPerformanceScore = latestPerformanceScore;
            CurrentMode = currentMode;
            CurrentCycleId = currentCycleId;
            CurrentPhase = currentPhase;
            AccuracyEvidence = accuracyEvidence ?? throw new ArgumentNullException(nameof(accuracyEvidence));
        }

        public double? BestSensitivity { get; }
        public double? LatestPerformanceScore { get; }
        public string CurrentMode { get; }
        public long? CurrentCycleId { get; }
        public long? CurrentPhase { get; }
        public IReadOnlyList<AccuracyEvidenceRecord> AccuracyEvidence { get; }
    }
}
