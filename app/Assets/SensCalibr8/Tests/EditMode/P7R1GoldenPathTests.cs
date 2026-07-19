using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Integration;
using SensCalibr8.Services.Analysis;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using SensCalibr8.TestLogic;
using SensCalibr8.UI;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P7R1GoldenPathTests
    {
        private string directory;
        private string database;
        private string nativeLibrary;
        private string root;
        private FrozenCalibrationConfiguration configuration;
        private SqliteConnectionFactory connections;
        private ProtocolRepository protocol;
        private ProfileRecord profile;
        private CycleRecord cycle;
        private long configurationId;
        private PhaseTwoProtocolContract phaseTwoContract;
        private PhaseThreeProtocolContract phaseThreeContract;
        private PhaseOneExploratoryProtocolService phaseOneService;

        [SetUp]
        public void SetUp()
        {
            root=RepositoryRoot();directory=Path.Combine(Path.GetTempPath(),"senscalibr8-p7r1-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            database=Path.Combine(directory,"golden-path.sqlite3");nativeLibrary=Path.Combine(root,"app","Assets","Plugins","sqlite3.dll");
            configuration=FrozenCalibrationConfigurationLoader.LoadFromRepository(root);new SqliteDatabaseBootstrapper().Initialize(database,configuration,nativeLibrary);
            connections=new SqliteConnectionFactory(database,nativeLibrary);protocol=new ProtocolRepository(connections);configurationId=new CalibrationConfigurationRepository(connections).RequireId(configuration.ConfigVersion.Value);
            phaseTwoContract=PhaseTwoProtocolContractLoader.LoadFromRepository(root);phaseThreeContract=PhaseThreeProtocolContractLoader.LoadFromRepository(root);
        }

        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

        [Test]
        public void GoldenPathConnectsSetupPhaseOneToThreeContinuousCycleAndAnalysisOutputs()
        {
            ProfileSetupApplicationService application=ProfileSetupApplicationFactory.Open(database,root,nativeLibrary);
            ProfileSlotPresentation created=CreateProfile(application,"golden-primary","0.175");
            ProfileRecord persisted=new ProfileRepository(connections).FindById(created.Id);
            Assert.That(persisted.MouseDpi,Is.EqualTo(1600));Assert.That(persisted.CurrentSensitivity,Is.EqualTo(0.175d));
            Assert.That(new SensitivityCalculationService(ResearchConstantsLoader.LoadFromRepository(root)).CalculateStartingSensitivity(1600),Is.EqualTo(0.175d));

            PhaseOneExploratoryWorkflow phaseOne=CreatePhaseOneWorkflow();
            PhaseOneExploratoryPlan phaseOnePlan=phaseOne.CreatePlan(persisted.Id.Value,1,1600,"2026-07-19");
            Assert.That(new NarrowingRepository(connections).ListPhaseCandidates(persisted.Id.Value,phaseOnePlan.CycleId,1).Count,Is.EqualTo(7));
            InsertSignificanceDecision(persisted.Id.Value,phaseOnePlan.CycleId);
            cycle=new CycleRecord(phaseOnePlan.CycleId,persisted.Id.Value,1,"2026-07-19",null,null);

            NarrowingRepository narrowing=new NarrowingRepository(connections);PhaseHistoryRepository history=new PhaseHistoryRepository(connections);
            NarrowingStabilizationService stabilization=new NarrowingStabilizationService(narrowing,new CalibrationConfigurationRepository(connections),configuration,phaseTwoContract);
            NarrowingWinnerSelectionService winner=new NarrowingWinnerSelectionService(stabilization,narrowing,history);
            PhaseTwoNarrowingWorkflow phaseTwo=new PhaseTwoNarrowingWorkflow(new PhaseTwoNarrowingService(ResearchConstantsLoader.LoadFromRepository(root),phaseTwoContract,protocol,narrowing),stabilization,new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)));
            PhaseTwoNarrowingPlan phaseTwoPlan=phaseTwo.CreatePlan(persisted.Id.Value,cycle.Id.Value,1600,"2026-07-19");
            IReadOnlyList<ProtocolCandidateRecord> phaseTwoCandidates=narrowing.ListPhaseCandidates(persisted.Id.Value,cycle.Id.Value,2);
            foreach(ProtocolCandidateRecord candidate in phaseTwoCandidates)for(int repetition=0;repetition<phaseTwoContract.MinimumCompleteBatteries;repetition++)SeedScoredBattery(candidate, candidate.Edpi==280d?77d:70d, "A");
            NarrowingWinnerSelection phaseTwoWinner=winner.SelectAndPersist(persisted.Id.Value,cycle.Id.Value,ProtocolPhase.PhaseTwo,"2026-07-19T01:00:00Z");
            Assert.That(phaseTwoWinner.HasWinner,Is.True,"Phase 2 must persist a unique stabilized Winner.");Assert.That(phaseTwoWinner.PersistedWinner.WinnerEdpi,Is.EqualTo(280d));Assert.That(phaseTwoPlan.Candidates.Count,Is.EqualTo(3));

            PhaseThreeFinalNarrowingWorkflow phaseThree=new PhaseThreeFinalNarrowingWorkflow(new PhaseThreeFinalNarrowingService(ResearchConstantsLoader.LoadFromRepository(root),phaseThreeContract,phaseTwoContract,protocol,narrowing,history),stabilization,new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)));
            PhaseThreeFinalPlan phaseThreePlan=phaseThree.CreatePlan(persisted.Id.Value,cycle.Id.Value,1600,"2026-07-19");
            IReadOnlyList<ProtocolCandidateRecord> phaseThreeCandidates=narrowing.ListPhaseCandidates(persisted.Id.Value,cycle.Id.Value,3);
            foreach(ProtocolCandidateRecord candidate in phaseThreeCandidates)for(int repetition=0;repetition<phaseTwoContract.MinimumCompleteBatteries;repetition++)SeedScoredBattery(candidate,candidate.Edpi==294d?81d:75d,"A");
            NarrowingWinnerSelection phaseThreeWinner=winner.SelectAndPersist(persisted.Id.Value,cycle.Id.Value,ProtocolPhase.PhaseThree,"2026-07-19T02:00:00Z");
            Assert.That(phaseThreeWinner.HasWinner,Is.True,"Phase 3 must persist a unique stabilized Winner.");Assert.That(phaseThreeWinner.PersistedWinner.WinnerEdpi,Is.EqualTo(294d));Assert.That(phaseThreePlan.Candidates.Count,Is.EqualTo(3));
            Assert.That(new PhaseHistoryRepository(connections).Require(persisted.Id.Value,cycle.Id.Value,3).WinnerEdpi,Is.EqualTo(294d));
            Assert.That(new ProfileRepository(connections).FindById(persisted.Id.Value).CurrentSensitivity,Is.EqualTo(0.175d));

            SeedTrainingEvidence(persisted.Id.Value,cycle.Id.Value,phaseThreeCandidates.Single(value=>value.Edpi==294d));
            ContinuousCycleService continuous=new ContinuousCycleService(new ContinuousCycleRepository(connections),ContinuousCycleContractLoader.LoadFromRepository(root),phaseOneService,new ProfileRepository(connections),configuration);
            continuous.FinalizeTraining(persisted.Id.Value,cycle.Id.Value,"2026-07-19");
            Assert.That(Scalar("SELECT COUNT(*) FROM cycle_checkpoints WHERE profile_id="+persisted.Id.Value),Is.EqualTo(1));

            AnalysisProfileDataset dataset=new AnalysisDatasetService(new AnalysisReadRepository(connections)).ReadProfileDataset(persisted.Id.Value);
            Assert.That(dataset.AuthoritativeScores.Count,Is.EqualTo(31));Assert.That(dataset.AuthoritativeScores.Any(value=>value.Phase==3&&value.Edpi==294d&&value.PerformanceScore==81d),Is.True,"Analysis must preserve the Phase 3 Winner score lineage.");
            HtmlReportInput report=new HtmlReportInputService(new HtmlReportInputRepository(connections)).Read(persisted.Id.Value);
            Assert.That(report.Scores.Count,Is.EqualTo(31));Assert.That(report.Winners.Any(value=>value.Phase==3&&value.Edpi==294d),Is.True,"HTML report input must preserve the Phase 3 Winner.");

            ProfileSlotPresentation comparisonProfile=CreateProfile(application,"golden-secondary","0.2");
            IReadOnlyList<ProfileComparisonPresentation> comparison=application.CompareExplicitProfiles(new[]{persisted.Id.Value,comparisonProfile.Id});
            Assert.That(comparison.Count,Is.EqualTo(2));Assert.That(comparison.Single(value=>value.ProfileId==persisted.Id.Value).HasComparableResult,Is.True,"Comparison must expose the completed primary profile.");Assert.That(comparison.Single(value=>value.ProfileId==comparisonProfile.Id).HasComparableResult,Is.False);
            ProfileDataExportResult export=new ProfileDataExportService(new ProfileDataExportRepository(connections)).Export(persisted.Id.Value,directory,new DateTime(2026,7,19,3,0,0,DateTimeKind.Utc));
            Assert.That(File.Exists(export.ManifestPath),Is.True);Assert.That(export.CsvPaths.Count,Is.GreaterThan(10));
            Assert.That(Scalar("SELECT COUNT(*) FROM protocol_candidates WHERE profile_id="+comparisonProfile.Id),Is.Zero);Assert.That(Scalar("SELECT COUNT(*) FROM sensitivity_tests WHERE profile_id="+comparisonProfile.Id),Is.Zero);
        }

        private PhaseOneExploratoryWorkflow CreatePhaseOneWorkflow()
        {
            phaseOneService=new PhaseOneExploratoryProtocolService(ResearchConstantsLoader.LoadFromRepository(root),ProtocolConstantsLoader.LoadFromRepository(root),protocol);
            return new PhaseOneExploratoryWorkflow(phaseOneService,new DeterministicTargetSequencer(FrozenSequenceContract.From(configuration)));
        }

        private void InsertSignificanceDecision(long profileId,long cycleId)
        {
            using(SqliteDatabaseConnection connection=connections.Open())connection.Execute(@"INSERT INTO significance_tests(profile_id,cycle_id,calibration_config_id,phase,candidate_a_edpi,candidate_b_edpi,test_method,alternative,alpha,p_value,effect_estimate,confidence_level,confidence_interval_lower,confidence_interval_upper,paired_sample_size,is_significant,formula_version,result)
VALUES(@profile,@cycle,@config,1,280,294,'exact-sign-flip','two-sided',0.05,0.001953125,5,0.95,5,5,10,1,@formula,'candidate_a');",new Dictionary<string,object>{{"@profile",profileId},{"@cycle",cycleId},{"@config",configurationId},{"@formula",configuration.FormulaVersion.Value}});
        }

        private void SeedScoredBattery(ProtocolCandidateRecord candidate,double score,string grade)
        {
            ProtocolBatteryRecord battery=protocol.CreateBattery(new ProtocolBatteryRecord(null,candidate.ProfileId,candidate.CycleId,candidate.Id.Value,candidate.SensitivityValue,candidate.Phase,"narrowing","2026-07-19","2026-07-19"));
            InsertFourModes(battery,candidate.ProfileId);
            SensitivityTestRecord stored=new SensitivityTestRepository(connections).Create(new SensitivityTestRecord(null,candidate.ProfileId,candidate.CycleId,configurationId,battery.Id.Value,candidate.Edpi,46.384d,score,"{}",grade,configuration.FormulaVersion.Value,candidate.Phase,1));
            UpdateComparisonMetrics(stored.Id.Value);
        }

        private void SeedTrainingEvidence(long profileId,long cycleId,ProtocolCandidateRecord candidate)
        {
            ProtocolBatteryRecord first=protocol.CreateBattery(new ProtocolBatteryRecord(null,profileId,cycleId,candidate.Id.Value,candidate.SensitivityValue,3,"training","2026-07-19","2026-07-19"));InsertFourModes(first,profileId);
            SensitivityTestRecord stored=new SensitivityTestRepository(connections).Create(new SensitivityTestRecord(null,profileId,cycleId,configurationId,first.Id.Value,candidate.Edpi,46.384d,81d,"{}","A",configuration.FormulaVersion.Value,3,1));
            UpdateComparisonMetrics(stored.Id.Value);
            ProtocolBatteryRecord second=protocol.CreateBattery(new ProtocolBatteryRecord(null,profileId,cycleId,candidate.Id.Value,candidate.SensitivityValue,3,"training","2026-07-19",null));
            using(SqliteDatabaseConnection connection=connections.Open())connection.Execute("INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag) VALUES(@p,@b,@c,'2026-07-19','flick_close',1,0);",new Dictionary<string,object>{{"@p",profileId},{"@b",second.Id.Value},{"@c",configurationId}});
        }

        private void InsertFourModes(ProtocolBatteryRecord battery,long profileId)
        {
            using(SqliteDatabaseConnection connection=connections.Open())foreach(string mode in new[]{"flick_close","flick_far","tracking","micro_correction"})connection.Execute("INSERT INTO sessions(profile_id,battery_id,calibration_config_id,date,mode,duration_sec,fatigue_flag) VALUES(@p,@b,@c,'2026-07-19',@m,1,0);",new Dictionary<string,object>{{"@p",profileId},{"@b",battery.Id.Value},{"@c",configurationId},{"@m",mode}});
        }

        private void UpdateComparisonMetrics(long sensitivityTestId)
        {
            using(SqliteDatabaseConnection connection=connections.Open())connection.Execute("UPDATE sensitivity_tests SET battery_consistency_utility=0.8,reaction_tier='A' WHERE id=@id;",new Dictionary<string,object>{{"@id",sensitivityTestId}});
        }

        private static ProfileSlotPresentation CreateProfile(ProfileSetupApplicationService application,string name,string sensitivity)
        {
            var screen=new ProfileSetupScreenModel{Name=name,HardwareDpi="1600",CurrentSensitivity=sensitivity,ConfiguredPollingRateHz="1000",MousepadWidthCm="50",MousepadHeightCm="50",AdsMultiplier="1",MovementStrategy=MovementStrategy.Arm};
            Assert.That(screen.TryCreate(application,"#FFE600",out ProfileSlotPresentation result),Is.True,screen.StatusMessage);return result;
        }

        private int Scalar(string sql){using(SqliteDatabaseConnection connection=connections.Open())return Convert.ToInt32(connection.Scalar(sql));}
        private static string RepositoryRoot()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
    }
}
