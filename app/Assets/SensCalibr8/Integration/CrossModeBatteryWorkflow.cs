using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Profiles;
using SensCalibr8.TestLogic;

namespace SensCalibr8.Integration
{
    public interface IBatteryTestModeFactory { ITestMode Create(TestMode mode,int batteryRepetitionOrdinal); }

    public sealed class ProductionBatteryTestModeFactory : IBatteryTestModeFactory
    {
        private readonly DeterministicTargetSequencer sequencer;
        public ProductionBatteryTestModeFactory(DeterministicTargetSequencer sequencer){this.sequencer=sequencer??throw new ArgumentNullException(nameof(sequencer));}
        public ITestMode Create(TestMode mode,int batteryRepetitionOrdinal)
        { switch(mode){case TestMode.FlickClose:return new CloseFlickMode(sequencer,batteryRepetitionOrdinal);case TestMode.FlickFar:return new FarFlickMode(sequencer,batteryRepetitionOrdinal);case TestMode.Tracking:return new TrackingMode(sequencer,batteryRepetitionOrdinal);case TestMode.MicroCorrection:return new MicroCorrectionMode(sequencer,batteryRepetitionOrdinal);default:throw new ArgumentOutOfRangeException(nameof(mode));} }
    }

    public sealed class CrossModeBatteryPlan
    {
        internal CrossModeBatteryPlan(EngineCycleContext cycle,EngineCandidateContext candidate,EngineBatteryContext battery,int ordinal,string blindLabel,IReadOnlyList<TestMode> modes)
        { Cycle=cycle;Candidate=candidate;Battery=battery;BatteryRepetitionOrdinal=ordinal;BlindCandidateLabel=Required(blindLabel);OrderedModes=new ReadOnlyCollection<TestMode>(new List<TestMode>(modes));if(OrderedModes.Count!=Enum.GetValues(typeof(TestMode)).Length||new HashSet<TestMode>(OrderedModes).Count!=OrderedModes.Count)throw new ArgumentException("A battery requires every mode exactly once.",nameof(modes)); }
        internal EngineCycleContext Cycle{get;}internal EngineCandidateContext Candidate{get;}internal EngineBatteryContext Battery{get;}public int BatteryRepetitionOrdinal{get;}public string BlindCandidateLabel{get;}public IReadOnlyList<TestMode> OrderedModes{get;}
        private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Blind candidate label is required.");
    }

    public sealed class CrossModeBatteryRun
    {
        internal CrossModeBatteryRun(CrossModeBatteryPlan plan,TestMode mode,SessionAttemptRecord attempt,TestEngineStateMachine engine,SessionSequenceAuditRecord audit)
        { Plan=plan;Mode=mode;Attempt=attempt;Engine=engine;Audit=audit; }
        public CrossModeBatteryPlan Plan{get;}public TestMode Mode{get;}public SessionAttemptRecord Attempt{get;}public TestEngineStateMachine Engine{get;}public SessionSequenceAuditRecord Audit{get;}
    }

    public sealed class CrossModeBatteryWorkflow
    {
        private readonly DeterministicTargetSequencer sequencer;private readonly IBatteryTestModeFactory factory;private readonly SessionBatteryPersistenceService persistence;private readonly FrozenCalibrationConfiguration configuration;private readonly HashSet<TestMode> completed=new HashSet<TestMode>();private CrossModeBatteryPlan plan;private CrossModeBatteryRun active;
        public CrossModeBatteryWorkflow(DeterministicTargetSequencer sequencer,IBatteryTestModeFactory factory,SessionBatteryPersistenceService persistence,FrozenCalibrationConfiguration configuration)
        { this.sequencer=sequencer??throw new ArgumentNullException(nameof(sequencer));this.factory=factory??throw new ArgumentNullException(nameof(factory));this.persistence=persistence??throw new ArgumentNullException(nameof(persistence));this.configuration=configuration??throw new ArgumentNullException(nameof(configuration)); }
        public CrossModeBatteryPlan Plan=>plan;public CrossModeBatteryRun ActiveRun=>active;public IReadOnlyCollection<TestMode> CompletedModes=>new ReadOnlyCollection<TestMode>(new List<TestMode>(completed));
        public CrossModeBatteryPlan CreatePlan(EngineCycleContext cycle,EngineCandidateContext candidate,EngineBatteryContext battery,int batteryRepetitionOrdinal,IReadOnlyList<long> phaseCandidateIds)
        {
            if(plan!=null)throw new TestEngineLifecycleException("battery_plan_already_created");if(cycle==null||candidate==null||battery==null)throw new ArgumentNullException("Battery plan context is required.");if(cycle.ProfileId!=candidate.ProfileId||cycle.CycleId!=candidate.CycleId||battery.ProfileId!=candidate.ProfileId||battery.CycleId!=candidate.CycleId||battery.CandidateId!=candidate.CandidateId||battery.Phase!=candidate.Phase||!battery.SensitivityValue.Equals(candidate.SensitivityValue))throw new TestEngineLifecycleException("battery_plan_lineage_mismatch");CounterbalancedOrder order=sequencer.CreateCounterbalancedOrder(cycle.ProfileId,cycle.CycleId,candidate.Phase,batteryRepetitionOrdinal,phaseCandidateIds);BlindCandidateAssignment assignment=null;foreach(BlindCandidateAssignment item in order.Candidates)if(item.CandidateId==candidate.CandidateId)assignment=item;if(assignment==null)throw new TestEngineLifecycleException("battery_candidate_not_in_blind_order");plan=new CrossModeBatteryPlan(cycle,candidate,battery,batteryRepetitionOrdinal,assignment.BlindLabel,order.Modes);return plan;
        }
        public CrossModeBatteryRun BeginNext(string startedDate)
        {
            RequirePlan();if(active!=null)throw new TestEngineLifecycleException("battery_mode_already_active");if(completed.Count==plan.OrderedModes.Count)throw new TestEngineLifecycleException("battery_all_modes_completed");TestMode mode=plan.OrderedModes[completed.Count];SequenceAuditMetadata auditMetadata=sequencer.Create(new SequenceSeedContext(plan.Cycle.ProfileId,plan.Cycle.CycleId,plan.Candidate.Phase,mode,plan.BatteryRepetitionOrdinal)).Audit;var audit=new SessionSequenceAuditRecord(auditMetadata.ContractVersion,auditMetadata.Generator,auditMetadata.SeedSha256,auditMetadata.SeedMaterial,plan.BlindCandidateLabel);long configId=RequireConfigurationId();var start=new SessionAttemptStartRecord(plan.Cycle.ProfileId,plan.Cycle.CycleId,plan.Candidate.CandidateId,plan.Battery.BatteryId,configId,ModeName(mode),Required(startedDate),audit);SessionAttemptRecord attempt=persistence.Begin(start);var session=new EngineSessionContext("battery-"+plan.Battery.BatteryId.ToString(CultureInfo.InvariantCulture)+"-"+ModeName(mode),plan.Cycle,plan.Candidate,plan.Battery,mode,configuration.ConfigVersion);var engine=new TestEngineStateMachine(factory.Create(mode,plan.BatteryRepetitionOrdinal),session,configuration);engine.Prepare();engine.Start();active=new CrossModeBatteryRun(plan,mode,attempt,engine,audit);return active;
        }
        public SessionFinalizationResult CompleteActive(SessionCaptureRequest capture,string completedDate)
        {
            if(active==null)throw new TestEngineLifecycleException("battery_no_active_mode");if(active.Engine.State!=TestEngineSessionState.Completed)throw new TestEngineLifecycleException("battery_mode_report_required");ValidateCapture(active,capture);SessionFinalizationResult result=persistence.Complete(active.Attempt,capture,active.Audit,Required(completedDate));completed.Add(active.Mode);active=null;return result;
        }
        private void ValidateCapture(CrossModeBatteryRun run,SessionCaptureRequest capture)
        {
            if(capture==null)throw new ArgumentNullException(nameof(capture));SessionRecord session=capture.Session;if(session.ProfileId!=run.Plan.Cycle.ProfileId||session.BatteryId!=run.Plan.Battery.BatteryId||session.CalibrationConfigId!=run.Attempt.Start.CalibrationConfigId||!string.Equals(session.Mode,ModeName(run.Mode),StringComparison.Ordinal))throw new TestEngineLifecycleException("battery_capture_lineage_mismatch");if(!capture.TimingDiagnostics.TimingContractPassed)throw new TestEngineLifecycleException("battery_timing_contract_failed");
            if(run.Mode==TestMode.Tracking){if(capture.Shots.Count!=0||capture.TrackingTrials.Count!=18||capture.TrackingWindows.Count!=108)throw new TestEngineLifecycleException("battery_tracking_completion_invalid");foreach(TrackingTrialCaptureRecord trial in capture.TrackingTrials)if(trial.IsAdaptationTrial.HasValue||!trial.SensitivityValue.Equals(run.Plan.Candidate.SensitivityValue))throw new TestEngineLifecycleException("battery_tracking_evidence_invalid");}
            else {if(capture.TrackingTrials.Count!=0||capture.TrackingWindows.Count!=0||capture.Shots.Count!=30)throw new TestEngineLifecycleException("battery_shot_completion_invalid");foreach(ShotCaptureRecord shot in capture.Shots)if(shot.IsAdaptationShot.HasValue||!shot.SensitivityValue.Equals(run.Plan.Candidate.SensitivityValue))throw new TestEngineLifecycleException("battery_shot_evidence_invalid");}
        }
        private long RequireConfigurationId(){if(configuration.Record==null||string.IsNullOrWhiteSpace(configuration.ConfigVersion.Value)||!string.Equals(configuration.Record.ConfigVersion,configuration.ConfigVersion.Value,StringComparison.Ordinal))throw new TestEngineLifecycleException("battery_configuration_invalid");return persistence.RequireAcceptedConfigurationId();}
        private void RequirePlan(){if(plan==null)throw new TestEngineLifecycleException("battery_plan_required");}private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Date is required.");private static string ModeName(TestMode mode){switch(mode){case TestMode.FlickClose:return "flick_close";case TestMode.FlickFar:return "flick_far";case TestMode.Tracking:return "tracking";case TestMode.MicroCorrection:return "micro_correction";default:throw new ArgumentOutOfRangeException(nameof(mode));}}
    }
}
