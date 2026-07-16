using System;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Profiles;
using SensCalibr8.UI;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P2R5ErgonomicWarningsDashboardTests
    {
        private string tempDirectory;
        private string databasePath;
        private string nativeLibraryPath;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p2r5-" + Guid.NewGuid().ToString("N"));
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
        public void WristWarningUsesTheStrictResearchThresholdAndRemainsInformational()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation below = Create(application, "below-threshold", "0.124375", MovementStrategy.Wrist, "5000", "5000");
            application.SelectSlot(below.Id);
            ProfileDashboardPresentation dashboard = application.GetActiveDashboard();
            Assert.That(dashboard.Warnings, Has.Count.EqualTo(1));
            Assert.That(dashboard.Warnings[0].FlagType, Is.EqualTo(ErgonomicWarningService.LowEdpiWristStrain));
            Assert.That(dashboard.Warnings[0].Message, Does.Contain("Informational"));
            Assert.That(dashboard.Profile.Name, Is.EqualTo("below-threshold"));

            ProfileSlotPresentation boundary = Create(application, "at-threshold", "0.125", MovementStrategy.Wrist, "5000", "5000");
            application.SelectSlot(boundary.Id);
            Assert.That(application.GetActiveDashboard().Warnings, Is.Empty);
        }

        [Test]
        public void MousepadConstraintFlagPersistsOnceUntilAcknowledgedThenCanReappearIfStillTriggered()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation profile = Create(application, "small-pad", "0.175", MovementStrategy.Arm, "45", "40");
            application.SelectSlot(profile.Id);
            ProfileDashboardPresentation first = application.GetActiveDashboard();
            Assert.That(first.Warnings, Has.Count.EqualTo(1));
            Assert.That(first.Warnings[0].FlagType, Is.EqualTo(ErgonomicWarningService.MousepadConstraintViolation));

            Assert.That(application.GetActiveDashboard().Warnings, Has.Count.EqualTo(1));
            application.AcknowledgeActiveWarning(first.Warnings[0].Id);
            ProfileDashboardPresentation reTriggered = application.GetActiveDashboard();
            Assert.That(reTriggered.Warnings, Has.Count.EqualTo(2));
            Assert.That(reTriggered.Warnings[0].Acknowledged, Is.True);
            Assert.That(reTriggered.Warnings[1].Acknowledged, Is.False);
        }

        [Test]
        public void WarningAcknowledgementCannotCrossProfileBoundaries()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation warned = Create(application, "warned-profile", "0.124375", MovementStrategy.Wrist, "5000", "5000");
            application.SelectSlot(warned.Id);
            long warningId = application.GetActiveDashboard().Warnings[0].Id;
            ProfileSlotPresentation other = Create(application, "other-profile", "0.175", MovementStrategy.Arm, "5000", "5000");
            application.SelectSlot(other.Id);

            AssertProfileError("injury_risk_flag_not_found_or_acknowledged", () => application.AcknowledgeActiveWarning(warningId));
            application.SelectSlot(warned.Id);
            Assert.That(application.GetActiveDashboard().Warnings[0].Acknowledged, Is.False);
        }

        [Test]
        public void DashboardShellReportsOnlyProfileScopedAvailableActivity()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation first = Create(application, "first-dashboard", "0.175", MovementStrategy.Arm, "5000", "5000");
            ProfileSlotPresentation second = Create(application, "second-dashboard", "0.175", MovementStrategy.Arm, "5000", "5000");
            application.SelectSlot(second.Id);

            ProfileDashboardPresentation dashboard = application.GetActiveDashboard();
            Assert.That(dashboard.Profile.Id, Is.EqualTo(second.Id));
            Assert.That(dashboard.Profile.Id, Is.Not.EqualTo(first.Id));
            Assert.That(dashboard.CompletedSessionCount, Is.EqualTo(0));
            Assert.That(dashboard.LatestSessionDate, Is.Null);
            Assert.That(dashboard.LatestGrade, Is.Null);
        }

        private ProfileSetupApplicationService Open() => ProfileSetupApplicationFactory.Open(databasePath, RepositoryRoot(), nativeLibraryPath);

        private static ProfileSlotPresentation Create(ProfileSetupApplicationService application, string name, string sensitivity,
            MovementStrategy movement, string width, string height)
        {
            var screen = new ProfileSetupScreenModel
            {
                Name = name, HardwareDpi = "1600", CurrentSensitivity = sensitivity, ConfiguredPollingRateHz = "1000",
                MousepadWidthCm = width, MousepadHeightCm = height, AdsMultiplier = "1", MovementStrategy = movement
            };
            Assert.That(screen.TryCreate(application, "#FFE600", out ProfileSlotPresentation created), Is.True, screen.StatusMessage);
            return created;
        }

        private static void AssertProfileError(string expectedCode, TestDelegate action)
        {
            ProfileLifecycleException exception = Assert.Throws<ProfileLifecycleException>(action);
            Assert.That(exception.ErrorCode, Is.EqualTo(expectedCode));
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
