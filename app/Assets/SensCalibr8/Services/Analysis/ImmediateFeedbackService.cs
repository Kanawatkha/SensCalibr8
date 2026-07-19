using System;
using System.Collections.Generic;
using System.Linq;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Analysis
{
    public sealed class AccuracyBarPresentation
    {
        public AccuracyBarPresentation(double sensitivityValue, long hitCount, long resolvedCount, double accuracyPercent, double fillFraction)
        {
            SensitivityValue = sensitivityValue;
            HitCount = hitCount;
            ResolvedCount = resolvedCount;
            AccuracyPercent = accuracyPercent;
            FillFraction = fillFraction;
        }

        public double SensitivityValue { get; }
        public long HitCount { get; }
        public long ResolvedCount { get; }
        public double AccuracyPercent { get; }
        public double FillFraction { get; }
    }

    public sealed class ImmediateFeedbackPresentation
    {
        public static ImmediateFeedbackPresentation Empty { get; } =
            new ImmediateFeedbackPresentation(null, null, null, null, null, Array.Empty<AccuracyBarPresentation>());

        public ImmediateFeedbackPresentation(double? bestSensitivity, double? latestPerformanceScore, string currentMode,
            long? currentCycleId, long? currentPhase, IReadOnlyList<AccuracyBarPresentation> accuracyBars)
        {
            BestSensitivity = bestSensitivity;
            LatestPerformanceScore = latestPerformanceScore;
            CurrentMode = currentMode;
            CurrentCycleId = currentCycleId;
            CurrentPhase = currentPhase;
            AccuracyBars = accuracyBars ?? throw new ArgumentNullException(nameof(accuracyBars));
        }

        public double? BestSensitivity { get; }
        public double? LatestPerformanceScore { get; }
        public string CurrentMode { get; }
        public long? CurrentCycleId { get; }
        public long? CurrentPhase { get; }
        public IReadOnlyList<AccuracyBarPresentation> AccuracyBars { get; }
    }

    public sealed class ImmediateFeedbackService
    {
        private const double PercentageScale = 100d;
        private readonly ImmediateFeedbackRepository repository;

        public ImmediateFeedbackService(ImmediateFeedbackRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public ImmediateFeedbackPresentation Load(long profileId)
        {
            ImmediateFeedbackDataRecord data = repository.ReadCurrentProtocolWindow(profileId);
            IReadOnlyList<AccuracyBarPresentation> bars = data.AccuracyEvidence.Select(value =>
            {
                double fraction = (double)value.HitCount / value.ResolvedCount;
                return new AccuracyBarPresentation(value.SensitivityValue, value.HitCount, value.ResolvedCount,
                    fraction * PercentageScale, fraction);
            }).ToArray();
            return new ImmediateFeedbackPresentation(data.BestSensitivity, data.LatestPerformanceScore, data.CurrentMode,
                data.CurrentCycleId, data.CurrentPhase, bars);
        }
    }
}
