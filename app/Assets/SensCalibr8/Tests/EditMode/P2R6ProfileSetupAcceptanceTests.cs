using System;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using SensCalibr8.UI;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P2R6ProfileSetupAcceptanceTests
    {
        private string tempDirectory;
        private string databasePath;
        private string nativeLibraryPath;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p2r6-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            databasePath = Path.Combine(tempDirectory, "profiles.sqlite3");
            nativeLibraryPath = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void EndToEndProfileWorkflowSurvivesRestartAndKeepsProfilesIsolated()
        {
            ProfileSetupApplicationService first = Open();
            ProfileSlotPresentation alpha = Create(first, "alpha", "0.175", MovementStrategy.Arm, "5000", "5000");
            ProfileSlotPresentation beta = Create(first, "beta", "0.2", MovementStrategy.Wrist, "5000", "5000");
            first.SelectSlot(alpha.Id);

            ProfileSetupScreenModel edit = new ProfileSetupScreenModel();
            edit.Load(first.GetSetup(alpha.Id));
            edit.Name = "alpha-edited";
            edit.CurrentSensitivity = "0.2";
            Assert.That(edit.TryUpdate(first, alpha.Id, out ProfileSetupPresentation updated), Is.True, edit.StatusMessage);
            Assert.That(updated.Name, Is.EqualTo("alpha-edited"));
            Assert.That(updated.CrosshairColor, Is.EqualTo("#FFE600"));

            ProfileSetupApplicationService reopened = Open();
            Assert.That(reopened.GetActiveSetup().Id, Is.EqualTo(alpha.Id));
            Assert.That(reopened.GetActiveSetup().Name, Is.EqualTo("alpha-edited"));
            reopened.SelectSlot(beta.Id);
            Assert.That(reopened.GetActiveSetup().Name, Is.EqualTo("beta"));
            Assert.That(reopened.GetActiveDashboard().Warnings, Has.Count.EqualTo(0));
        }

        [Test]
        public void SetupValidationRejectsInvalidInputsAndDuplicateNamesBeforePersistence()
        {
            ProfileSetupApplicationService application = Open();
            Create(application, "unique", "0.175", MovementStrategy.Arm, "5000", "5000");

            ProfileSetupScreenModel duplicate = ValidScreen("unique", "0.175", MovementStrategy.Arm, "5000", "5000");
            Assert.That(duplicate.TryCreate(application, "#FFE600", out _), Is.False);
            Assert.That(duplicate.StatusMessage, Is.EqualTo("profile_name_duplicate"));

            foreach (Action<ProfileSetupScreenModel> mutate in new Action<ProfileSetupScreenModel>[]
            {
                screen => screen.HardwareDpi = "0",
                screen => screen.CurrentSensitivity = "-1",
                screen => screen.ConfiguredPollingRateHz = "0",
                screen => screen.MousepadWidthCm = "0",
                screen => screen.MousepadHeightCm = "NaN"
            })
            {
                ProfileSetupScreenModel invalid = ValidScreen(Guid.NewGuid().ToString("N"), "0.175", MovementStrategy.Arm, "5000", "5000");
                mutate(invalid);
                Assert.That(invalid.TryCreate(application, "#FFE600", out _), Is.False, invalid.StatusMessage);
                Assert.That(invalid.StatusMessage, Is.Not.Empty);
            }

            Assert.That(application.ListSlots(), Has.Count.EqualTo(1));
        }

        [Test]
        public void PhysicalRulerAcceptanceRequiresExplicitPositiveIntegerConfirmation()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSetupScreenModel screen = ValidScreen("ruler", "0.175", MovementStrategy.Arm, "5000", "5000");
            screen.UsePhysicalRuler = true;
            screen.PhysicalRulerCounts = "1600";
            screen.PhysicalRulerDistanceCm = "2.54";
            Assert.That(screen.TryCreate(application, "#FFE600", out _), Is.False);
            Assert.That(screen.StatusMessage, Is.EqualTo("physical_ruler_preview_required"));
            Assert.That(screen.TryPreviewPhysicalRuler(application), Is.True, screen.StatusMessage);
            Assert.That(screen.PhysicalRulerPreview.ExactEstimatedDpi, Is.EqualTo(1600d));
            Assert.That(screen.ConfirmedPhysicalRulerDpi, Is.EqualTo("1600"));
            Assert.That(screen.TryCreate(application, "#FFE600", out ProfileSlotPresentation created), Is.False);
            Assert.That(screen.StatusMessage, Is.EqualTo("physical_ruler_dpi_confirmation_required"));
            screen.IsPhysicalRulerDpiConfirmed = true;
            Assert.That(screen.TryCreate(application, "#FFE600", out created), Is.True, screen.StatusMessage);
            Assert.That(application.GetSetup(created.Id).MouseDpi, Is.EqualTo(1600));
        }

        [Test]
        public void FormulaWorkedExampleAndEdpiFloorRemainAvailableAtAcceptanceBoundary()
        {
            ResearchConstants constants = ResearchConstantsLoader.LoadFromRepository(RepositoryRoot());
            var calculations = new SensitivityCalculationService(constants);
            Assert.That(calculations.CalculateStartingSensitivity(1600), Is.EqualTo(0.175d));
            Assert.That(calculations.CalculateEdpi(1600, 0.175d), Is.EqualTo(280d));
            EdpiFloorResult floor = calculations.ApplyEdpiFloor(1600, 0.05d);
            Assert.That(floor.EffectiveEdpi, Is.EqualTo(160d));
            Assert.That(floor.WasAdjusted, Is.True);
        }

        [Test]
        public void ActiveDeletionIsBlockedAndConfirmedInactiveDeletionCascadesChildData()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation created = Create(application, "cascade", "0.175", MovementStrategy.Arm, "5000", "5000");
            application.SelectSlot(created.Id);
            AssertProfileError("active_profile_deletion_forbidden", () => application.BeginDeletion(created.Id));

            FrozenCalibrationConfiguration configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath);
            var connectionFactory = new SqliteConnectionFactory(databasePath, nativeLibraryPath);
            new ProtocolRepository(connectionFactory).CreateCycle(new CycleRecord(null, created.Id, 1, "2026-07-16", null, null));

            application.ExitActiveProfile();
            ProfileDeletionConfirmation confirmation = application.BeginDeletion(created.Id);
            application.ConfirmDeletion(confirmation);
            using (SqliteDatabaseConnection connection = connectionFactory.Open())
            {
                Assert.That(Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM profiles;")), Is.EqualTo(0));
                Assert.That(Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM cycles;")), Is.EqualTo(0));
            }
        }

        [Test]
        public void WarningAcknowledgementIsProfileScopedAndRemainsNonBlocking()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation warned = Create(application, "warned", "0.124375", MovementStrategy.Wrist, "5000", "5000");
            application.SelectSlot(warned.Id);
            ProfileDashboardPresentation dashboard = application.GetActiveDashboard();
            Assert.That(dashboard.Warnings, Has.Count.EqualTo(1));
            Assert.That(dashboard.Warnings[0].Message, Does.Contain("Informational"));
            long warningId = dashboard.Warnings[0].Id;

            ProfileSlotPresentation other = Create(application, "other", "0.175", MovementStrategy.Arm, "5000", "5000");
            application.SelectSlot(other.Id);
            AssertProfileError("injury_risk_flag_not_found_or_acknowledged", () => application.AcknowledgeActiveWarning(warningId));
            application.SelectSlot(warned.Id);
            application.AcknowledgeActiveWarning(warningId);
            Assert.That(application.GetActiveDashboard().Warnings, Has.Count.EqualTo(2));
        }

        private ProfileSetupApplicationService Open() => ProfileSetupApplicationFactory.Open(databasePath, RepositoryRoot(), nativeLibraryPath);

        private static ProfileSlotPresentation Create(ProfileSetupApplicationService application, string name, string sensitivity,
            MovementStrategy movement, string width, string height)
        {
            ProfileSetupScreenModel screen = ValidScreen(name, sensitivity, movement, width, height);
            Assert.That(screen.TryCreate(application, "#FFE600", out ProfileSlotPresentation created), Is.True, screen.StatusMessage);
            return created;
        }

        private static ProfileSetupScreenModel ValidScreen(string name, string sensitivity, MovementStrategy movement, string width, string height)
        {
            return new ProfileSetupScreenModel
            {
                Name = name, HardwareDpi = "1600", CurrentSensitivity = sensitivity, ConfiguredPollingRateHz = "1000",
                MousepadWidthCm = width, MousepadHeightCm = height, AdsMultiplier = "1", MovementStrategy = movement
            };
        }

        private static void AssertProfileError(string expectedCode, TestDelegate action)
        {
            ProfileLifecycleException exception = Assert.Throws<ProfileLifecycleException>(action);
            Assert.That(exception.ErrorCode, Is.EqualTo(expectedCode));
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
