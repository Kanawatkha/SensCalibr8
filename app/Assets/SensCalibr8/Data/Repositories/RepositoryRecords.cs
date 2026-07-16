using System;
using System.Collections.Generic;

namespace SensCalibr8.Data.Repositories
{
    public sealed class InjuryRiskFlagRecord
    {
        public InjuryRiskFlagRecord(long? id, long profileId, string flagType, string triggeredDate, double edpiAtTrigger, bool acknowledged)
        {
            Id = id; ProfileId = profileId > 0 ? profileId : throw new ArgumentOutOfRangeException(nameof(profileId));
            FlagType = Require(flagType, nameof(flagType)); TriggeredDate = Require(triggeredDate, nameof(triggeredDate));
            EdpiAtTrigger = edpiAtTrigger; Acknowledged = acknowledged;
        }
        public long? Id { get; } public long ProfileId { get; } public string FlagType { get; } public string TriggeredDate { get; }
        public double EdpiAtTrigger { get; } public bool Acknowledged { get; }
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class ProfileRecord
    {
        public ProfileRecord(long? id, string name, string createdDate, long mouseDpi, double currentSensitivity,
            double configuredPollingRateHz, string dominantHand, string crosshairConfig, string gripStyle,
            string movementStrategy, double mousepadWidthCm, double mousepadHeightCm, double adsMultiplier, string lastActiveDate)
        {
            Id = id; Name = Require(name, nameof(name)); CreatedDate = Require(createdDate, nameof(createdDate));
            MouseDpi = mouseDpi; CurrentSensitivity = currentSensitivity; ConfiguredPollingRateHz = configuredPollingRateHz;
            DominantHand = Require(dominantHand, nameof(dominantHand)); CrosshairConfig = Require(crosshairConfig, nameof(crosshairConfig));
            GripStyle = Require(gripStyle, nameof(gripStyle)); MovementStrategy = Require(movementStrategy, nameof(movementStrategy));
            MousepadWidthCm = mousepadWidthCm; MousepadHeightCm = mousepadHeightCm; AdsMultiplier = adsMultiplier;
            LastActiveDate = Require(lastActiveDate, nameof(lastActiveDate));
        }
        public long? Id { get; } public string Name { get; } public string CreatedDate { get; } public long MouseDpi { get; }
        public double CurrentSensitivity { get; } public double ConfiguredPollingRateHz { get; } public string DominantHand { get; }
        public string CrosshairConfig { get; } public string GripStyle { get; } public string MovementStrategy { get; }
        public double MousepadWidthCm { get; } public double MousepadHeightCm { get; } public double AdsMultiplier { get; } public string LastActiveDate { get; }
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class CycleRecord
    {
        public CycleRecord(long? id, long profileId, long cycleNumber, string startDate, string endDate, string outcome)
        {
            Id = id; ProfileId = Positive(profileId, nameof(profileId)); CycleNumber = Positive(cycleNumber, nameof(cycleNumber));
            StartDate = Require(startDate, nameof(startDate)); EndDate = endDate; Outcome = outcome;
        }
        public long? Id { get; } public long ProfileId { get; } public long CycleNumber { get; } public string StartDate { get; } public string EndDate { get; } public string Outcome { get; }
        private static long Positive(long value, string field) => value > 0 ? value : throw new ArgumentOutOfRangeException(field);
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class ProtocolCandidateRecord
    {
        public ProtocolCandidateRecord(long? id, long profileId, long cycleId, long phase, double edpi, double sensitivityValue, string generationRule, string createdDate)
        {
            Id = id; ProfileId = Positive(profileId, nameof(profileId)); CycleId = Positive(cycleId, nameof(cycleId)); Phase = Positive(phase, nameof(phase));
            Edpi = edpi; SensitivityValue = sensitivityValue; GenerationRule = Require(generationRule, nameof(generationRule)); CreatedDate = Require(createdDate, nameof(createdDate));
        }
        public long? Id { get; } public long ProfileId { get; } public long CycleId { get; } public long Phase { get; } public double Edpi { get; } public double SensitivityValue { get; } public string GenerationRule { get; } public string CreatedDate { get; }
        private static long Positive(long value, string field) => value > 0 ? value : throw new ArgumentOutOfRangeException(field);
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class ProtocolCandidateSourceRecord
    {
        public ProtocolCandidateSourceRecord(double anchorEdpi, double offsetPercent, double preFloorEdpi, bool floorApplied)
        { AnchorEdpi = anchorEdpi; OffsetPercent = offsetPercent; PreFloorEdpi = preFloorEdpi; FloorApplied = floorApplied; }
        public double AnchorEdpi { get; } public double OffsetPercent { get; } public double PreFloorEdpi { get; } public bool FloorApplied { get; }
    }

    public sealed class ProtocolBatteryRecord
    {
        public ProtocolBatteryRecord(long? id, long profileId, long cycleId, long candidateId, double sensitivityValue, long phase, string purpose, string startedDate, string completedDate)
        {
            Id = id; ProfileId = Positive(profileId, nameof(profileId)); CycleId = Positive(cycleId, nameof(cycleId)); CandidateId = Positive(candidateId, nameof(candidateId));
            SensitivityValue = sensitivityValue; Phase = Positive(phase, nameof(phase)); Purpose = Require(purpose, nameof(purpose)); StartedDate = Require(startedDate, nameof(startedDate)); CompletedDate = completedDate;
        }
        public long? Id { get; } public long ProfileId { get; } public long CycleId { get; } public long CandidateId { get; } public double SensitivityValue { get; } public long Phase { get; } public string Purpose { get; } public string StartedDate { get; } public string CompletedDate { get; }
        private static long Positive(long value, string field) => value > 0 ? value : throw new ArgumentOutOfRangeException(field);
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class SessionRecord
    {
        public SessionRecord(long profileId, long batteryId, long calibrationConfigId, string date, string mode, long durationSec, double? fatigueScoreChangePercentage, bool fatigueFlag)
        {
            ProfileId = Positive(profileId, nameof(profileId)); BatteryId = Positive(batteryId, nameof(batteryId)); CalibrationConfigId = Positive(calibrationConfigId, nameof(calibrationConfigId));
            Date = Require(date, nameof(date)); Mode = Require(mode, nameof(mode)); DurationSec = durationSec; FatigueScoreChangePercentage = fatigueScoreChangePercentage; FatigueFlag = fatigueFlag;
        }
        public long ProfileId { get; } public long BatteryId { get; } public long CalibrationConfigId { get; } public string Date { get; } public string Mode { get; } public long DurationSec { get; } public double? FatigueScoreChangePercentage { get; } public bool FatigueFlag { get; }
        private static long Positive(long value, string field) => value > 0 ? value : throw new ArgumentOutOfRangeException(field);
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class SessionTimingDiagnosticsRecord
    {
        public SessionTimingDiagnosticsRecord(string signalPipelineVersion, string inputDeviceIdentity, double configuredPollingRateHz, double measuredEventRateHz, double medianIntervalMs, double intervalMadMs, double p95IntervalMs, double p99IntervalMs, long duplicateTimestampCount, long reverseTimestampCount, long burstIntervalCount, long singleCadenceIntervalCount, long gapIntervalCount, double p99SingleCadenceResidualMs, bool timingContractPassed, string dispositionReason)
        {
            SignalPipelineVersion = Require(signalPipelineVersion, nameof(signalPipelineVersion)); InputDeviceIdentity = Require(inputDeviceIdentity, nameof(inputDeviceIdentity)); ConfiguredPollingRateHz = configuredPollingRateHz; MeasuredEventRateHz = measuredEventRateHz; MedianIntervalMs = medianIntervalMs; IntervalMadMs = intervalMadMs; P95IntervalMs = p95IntervalMs; P99IntervalMs = p99IntervalMs; DuplicateTimestampCount = duplicateTimestampCount; ReverseTimestampCount = reverseTimestampCount; BurstIntervalCount = burstIntervalCount; SingleCadenceIntervalCount = singleCadenceIntervalCount; GapIntervalCount = gapIntervalCount; P99SingleCadenceResidualMs = p99SingleCadenceResidualMs; TimingContractPassed = timingContractPassed; DispositionReason = Require(dispositionReason, nameof(dispositionReason));
        }
        public string SignalPipelineVersion { get; } public string InputDeviceIdentity { get; } public double ConfiguredPollingRateHz { get; } public double MeasuredEventRateHz { get; } public double MedianIntervalMs { get; } public double IntervalMadMs { get; } public double P95IntervalMs { get; } public double P99IntervalMs { get; } public long DuplicateTimestampCount { get; } public long ReverseTimestampCount { get; } public long BurstIntervalCount { get; } public long SingleCadenceIntervalCount { get; } public long GapIntervalCount { get; } public double P99SingleCadenceResidualMs { get; } public bool TimingContractPassed { get; } public string DispositionReason { get; }
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class ShotCaptureRecord
    {
        public ShotCaptureRecord(long targetId, string distanceZone, string targetSize, string spawnPosition, double spawnTimestamp, double? firstMouseMovementTimestamp, double resolutionTimestamp, double? hitTimestamp, string hitPosition, bool isHit, string outcomeReason, string finalAimPosition, bool? isOutlier, bool? isAdaptationShot, double sensitivityValue, double initialOffsetDistance, long? microAdjustmentCount, long? submovementCount, double finalPrecisionError, bool isCenterHit)
        { TargetId = targetId; DistanceZone = Require(distanceZone, nameof(distanceZone)); TargetSize = Require(targetSize, nameof(targetSize)); SpawnPosition = Require(spawnPosition, nameof(spawnPosition)); SpawnTimestamp = spawnTimestamp; FirstMouseMovementTimestamp = firstMouseMovementTimestamp; ResolutionTimestamp = resolutionTimestamp; HitTimestamp = hitTimestamp; HitPosition = hitPosition; IsHit = isHit; OutcomeReason = Require(outcomeReason, nameof(outcomeReason)); FinalAimPosition = Require(finalAimPosition, nameof(finalAimPosition)); IsOutlier = isOutlier; IsAdaptationShot = isAdaptationShot; SensitivityValue = sensitivityValue; InitialOffsetDistance = initialOffsetDistance; MicroAdjustmentCount = microAdjustmentCount; SubmovementCount = submovementCount; FinalPrecisionError = finalPrecisionError; IsCenterHit = isCenterHit; }
        public long TargetId { get; } public string DistanceZone { get; } public string TargetSize { get; } public string SpawnPosition { get; } public double SpawnTimestamp { get; } public double? FirstMouseMovementTimestamp { get; } public double ResolutionTimestamp { get; } public double? HitTimestamp { get; } public string HitPosition { get; } public bool IsHit { get; } public string OutcomeReason { get; } public string FinalAimPosition { get; } public bool? IsOutlier { get; } public bool? IsAdaptationShot { get; } public double SensitivityValue { get; } public double InitialOffsetDistance { get; } public long? MicroAdjustmentCount { get; } public long? SubmovementCount { get; } public double FinalPrecisionError { get; } public bool IsCenterHit { get; }
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class MouseSampleCaptureRecord
    {
        public MouseSampleCaptureRecord(long sampleIndex, double timestampSec, double rawDeltaX, double rawDeltaY, double azimuthDeg, double elevationDeg, int? shotCaptureIndex)
        { SampleIndex = sampleIndex; TimestampSec = timestampSec; RawDeltaX = rawDeltaX; RawDeltaY = rawDeltaY; AzimuthDeg = azimuthDeg; ElevationDeg = elevationDeg; ShotCaptureIndex = shotCaptureIndex; }
        public long SampleIndex { get; } public double TimestampSec { get; } public double RawDeltaX { get; } public double RawDeltaY { get; } public double AzimuthDeg { get; } public double ElevationDeg { get; } public int? ShotCaptureIndex { get; }
    }

    public sealed class TrackingTrialCaptureRecord
    {
        public TrackingTrialCaptureRecord(double sensitivityValue, long trialIndex, long blockIndex, bool? isAdaptationTrial, string patternType, string targetSize, string pathContractId, string pathParametersJson, long durationMs, string deviationSamples, double timeOnTargetMs, double timeOnTargetPercentage)
        { SensitivityValue = sensitivityValue; TrialIndex = trialIndex; BlockIndex = blockIndex; IsAdaptationTrial = isAdaptationTrial; PatternType = Require(patternType, nameof(patternType)); TargetSize = Require(targetSize, nameof(targetSize)); PathContractId = Require(pathContractId, nameof(pathContractId)); PathParametersJson = Require(pathParametersJson, nameof(pathParametersJson)); DurationMs = durationMs; DeviationSamples = Require(deviationSamples, nameof(deviationSamples)); TimeOnTargetMs = timeOnTargetMs; TimeOnTargetPercentage = timeOnTargetPercentage; }
        public double SensitivityValue { get; } public long TrialIndex { get; } public long BlockIndex { get; } public bool? IsAdaptationTrial { get; } public string PatternType { get; } public string TargetSize { get; } public string PathContractId { get; } public string PathParametersJson { get; } public long DurationMs { get; } public string DeviationSamples { get; } public double TimeOnTargetMs { get; } public double TimeOnTargetPercentage { get; }
        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
    }

    public sealed class TrackingWindowCaptureRecord
    {
        public TrackingWindowCaptureRecord(int trackingTrialCaptureIndex, long windowIndex, long windowStartMs, long windowEndMs, double timeOnTargetMs, double timeOnTargetPercentage, double trackingDeviationRmsDeg)
        { TrackingTrialCaptureIndex = trackingTrialCaptureIndex; WindowIndex = windowIndex; WindowStartMs = windowStartMs; WindowEndMs = windowEndMs; TimeOnTargetMs = timeOnTargetMs; TimeOnTargetPercentage = timeOnTargetPercentage; TrackingDeviationRmsDeg = trackingDeviationRmsDeg; }
        public int TrackingTrialCaptureIndex { get; } public long WindowIndex { get; } public long WindowStartMs { get; } public long WindowEndMs { get; } public double TimeOnTargetMs { get; } public double TimeOnTargetPercentage { get; } public double TrackingDeviationRmsDeg { get; }
    }

    public sealed class SessionCaptureRequest
    {
        public SessionCaptureRequest(SessionRecord session, SessionTimingDiagnosticsRecord timingDiagnostics, IReadOnlyList<ShotCaptureRecord> shots, IReadOnlyList<MouseSampleCaptureRecord> mouseSamples)
            : this(session, timingDiagnostics, shots, mouseSamples, Array.Empty<TrackingTrialCaptureRecord>(), Array.Empty<TrackingWindowCaptureRecord>()) { }
        public SessionCaptureRequest(SessionRecord session, SessionTimingDiagnosticsRecord timingDiagnostics, IReadOnlyList<ShotCaptureRecord> shots, IReadOnlyList<MouseSampleCaptureRecord> mouseSamples, IReadOnlyList<TrackingTrialCaptureRecord> trackingTrials, IReadOnlyList<TrackingWindowCaptureRecord> trackingWindows)
        { Session = session ?? throw new ArgumentNullException(nameof(session)); TimingDiagnostics = timingDiagnostics ?? throw new ArgumentNullException(nameof(timingDiagnostics)); Shots = shots ?? throw new ArgumentNullException(nameof(shots)); MouseSamples = mouseSamples ?? throw new ArgumentNullException(nameof(mouseSamples)); TrackingTrials = trackingTrials ?? throw new ArgumentNullException(nameof(trackingTrials)); TrackingWindows = trackingWindows ?? throw new ArgumentNullException(nameof(trackingWindows)); }
        public SessionRecord Session { get; } public SessionTimingDiagnosticsRecord TimingDiagnostics { get; } public IReadOnlyList<ShotCaptureRecord> Shots { get; } public IReadOnlyList<MouseSampleCaptureRecord> MouseSamples { get; } public IReadOnlyList<TrackingTrialCaptureRecord> TrackingTrials { get; } public IReadOnlyList<TrackingWindowCaptureRecord> TrackingWindows { get; }
    }
}
