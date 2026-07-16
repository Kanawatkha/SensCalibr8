using System;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Configuration;

namespace SensCalibr8.Services.Profiles
{
    public sealed class SessionBatteryPersistenceService
    {
        private readonly SessionLifecycleRepository lifecycle;
        private readonly SessionCaptureRepository captures;
        private readonly CalibrationConfigurationRepository configurations;
        private readonly FrozenCalibrationConfiguration configuration;
        private readonly FrozenSessionLifecycleContract contract;

        public SessionBatteryPersistenceService(SessionLifecycleRepository lifecycle, SessionCaptureRepository captures, CalibrationConfigurationRepository configurations, FrozenCalibrationConfiguration configuration)
        { this.lifecycle=lifecycle??throw new ArgumentNullException(nameof(lifecycle));this.captures=captures??throw new ArgumentNullException(nameof(captures));this.configurations=configurations??throw new ArgumentNullException(nameof(configurations));this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));contract=FrozenSessionLifecycleContract.From(configuration); }

        public SessionAttemptRecord Begin(SessionAttemptStartRecord start)
        { RequireConfiguration(start); return lifecycle.Begin(start); }
        public SessionAttemptRecord Pause(SessionAttemptRecord attempt,string reason,string date) => lifecycle.Transition(attempt,"paused",Require(reason),Require(date));
        public SessionAttemptRecord Resume(SessionAttemptRecord attempt,string reason,string date) => lifecycle.Transition(attempt,"capturing",Require(reason),Require(date));
        public SessionAttemptRecord Cancel(SessionAttemptRecord attempt,string reason,string date) => lifecycle.Transition(attempt,"cancelled",Require(reason),Require(date));
        public SessionAttemptRecord Fault(SessionAttemptRecord attempt,string reason,string date) => lifecycle.Transition(attempt,"faulted",Require(reason),Require(date));
        public SessionFinalizationResult Complete(SessionAttemptRecord attempt,SessionCaptureRequest capture,SessionSequenceAuditRecord audit,string completedDate)
        {
            if(attempt==null)throw new ArgumentNullException(nameof(attempt));if(capture==null)throw new ArgumentNullException(nameof(capture));if(audit==null)throw new ArgumentNullException(nameof(audit));Require(completedDate);
            RequireConfiguration(attempt.Start);SessionRecord session=capture.Session;
            if(session.ProfileId!=attempt.Start.ProfileId||session.BatteryId!=attempt.Start.BatteryId||session.CalibrationConfigId!=attempt.Start.CalibrationConfigId||!string.Equals(session.Mode,attempt.Start.Mode,StringComparison.Ordinal))throw new InvalidOperationException("Completed capture does not match its active attempt.");
            if(!AuditEquals(attempt.Start.SequenceAudit,audit))throw new InvalidOperationException("Completed capture sequence audit does not match its active attempt.");
            return captures.PersistAndFinalize(new SessionFinalizationRequest(attempt.Id,capture,audit,contract.ToAdaptationPolicy(),completedDate));
        }
        private void RequireConfiguration(SessionAttemptStartRecord start)
        { if(start==null)throw new ArgumentNullException(nameof(start));if(start.CalibrationConfigId!=configurations.RequireId(configuration.ConfigVersion.Value))throw new InvalidOperationException("Session attempt must use the accepted frozen calibration configuration.");if(!string.Equals(start.SequenceAudit.ContractVersion,contract.ModeContractVersion,StringComparison.Ordinal))throw new InvalidOperationException("Session attempt sequence contract does not match the frozen mode contract."); }
        private static bool AuditEquals(SessionSequenceAuditRecord first,SessionSequenceAuditRecord second) => first.ContractVersion==second.ContractVersion&&first.Generator==second.Generator&&first.SeedSha256==second.SeedSha256&&first.SeedMaterial==second.SeedMaterial&&first.BlindCandidateLabel==second.BlindCandidateLabel;
        private static string Require(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Lifecycle disposition is required.");
    }
}
