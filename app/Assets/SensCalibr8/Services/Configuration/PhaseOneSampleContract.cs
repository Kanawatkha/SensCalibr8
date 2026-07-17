using System;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.Services.Configuration
{
    public sealed class PhaseOneSampleContract
    {
        private PhaseOneSampleContract(int shotTotal,int shotAdaptation,int shotAuthoritative,int trackingTrials,int trackingAdaptationTrials,int trackingWindows,int trackingAuthoritativeWindows){ShotTotal=shotTotal;ShotAdaptation=shotAdaptation;ShotAuthoritative=shotAuthoritative;TrackingTrials=trackingTrials;TrackingAdaptationTrials=trackingAdaptationTrials;TrackingWindows=trackingWindows;TrackingAuthoritativeWindows=trackingAuthoritativeWindows;}
        public int ShotTotal{get;}public int ShotAdaptation{get;}public int ShotAuthoritative{get;}public int TrackingTrials{get;}public int TrackingAdaptationTrials{get;}public int TrackingWindows{get;}public int TrackingAuthoritativeWindows{get;}
        public static PhaseOneSampleContract From(FrozenCalibrationConfiguration configuration)
        {
            if(configuration==null)throw new ArgumentNullException(nameof(configuration));using JsonDocument document=JsonDocument.Parse(configuration.Record.TrackingContractJson);JsonElement root=document.RootElement,shared=root.GetProperty("shared_shot_contract"),tracking=root.GetProperty("modes").GetProperty("tracking");int shotTotal=shared.GetProperty("minimum_resolved_trials").GetInt32(),shotAdaptation=shared.GetProperty("adaptation_trials_at_minimum").GetInt32(),shotAuthoritative=configuration.ScoringFormula.AuthoritativeShotObservations,trackingTrials=tracking.GetProperty("total_trials").GetInt32(),adaptationBlocks=tracking.GetProperty("adaptation_blocks").GetInt32(),trialsPerBlock=tracking.GetProperty("trials_per_block").GetInt32(),duration=(int)tracking.GetProperty("trial_duration_seconds").GetDouble(),window=(int)tracking.GetProperty("metric_window_seconds").GetDouble(),authoritativeWindows=configuration.ScoringFormula.AuthoritativeTrackingWindows;if(shotTotal-shotAdaptation!=shotAuthoritative||duration<=0||window<=0||duration%window!=0)throw new InvalidDataException("Phase 1 sample contract is inconsistent.");return new PhaseOneSampleContract(shotTotal,shotAdaptation,shotAuthoritative,trackingTrials,adaptationBlocks*trialsPerBlock,trackingTrials*(duration/window),authoritativeWindows);
        }
        public bool IsSatisfied(TestMode mode,int total,int adaptation,int authoritative)
        { return mode==TestMode.Tracking?total==TrackingTrials&&adaptation==TrackingAdaptationTrials&&authoritative==TrackingTrials-TrackingAdaptationTrials:total==ShotTotal&&adaptation==ShotAdaptation&&authoritative==ShotAuthoritative; }
        public bool TrackingWindowsSatisfied(int total,int authoritative)=>total==TrackingWindows&&authoritative==TrackingAuthoritativeWindows;
    }
}
