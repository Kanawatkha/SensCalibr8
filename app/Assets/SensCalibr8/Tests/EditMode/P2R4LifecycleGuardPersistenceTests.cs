using System;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Services.Profiles;
using SensCalibr8.UI;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P2R4LifecycleGuardPersistenceTests
    {
        private string tempDirectory;
        private string databasePath;
        private string nativeLibraryPath;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p2r4-" + Guid.NewGuid().ToString("N"));
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
        public void ActiveProfileRestoresFromPersistentStateAfterApplicationReopens()
        {
            ProfileSetupApplicationService first = Open();
            ProfileSlotPresentation created = Create(first, "persistent-profile");
            first.SelectSlot(created.Id);

            ProfileSetupApplicationService reopened = Open();
            ProfileSetupPresentation active = reopened.GetActiveSetup();
            Assert.That(active, Is.Not.Null);
            Assert.That(active.Id, Is.EqualTo(created.Id));
            Assert.That(active.Name, Is.EqualTo("persistent-profile"));
        }

        [Test]
        public void PersistedSelectionRemainsScopedToTheProfileThatWasExplicitlySelected()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation first = Create(application, "first-profile");
            ProfileSlotPresentation second = Create(application, "second-profile");
            application.SelectSlot(second.Id);

            ProfileSetupPresentation active = Open().GetActiveSetup();
            Assert.That(active.Id, Is.EqualTo(second.Id));
            Assert.That(active.Id, Is.Not.EqualTo(first.Id));
            Assert.That(active.Name, Is.EqualTo("second-profile"));
        }

        [Test]
        public void EditResumesSetupAndPreservesTheLockedCrosshairColor()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation created = Create(application, "editable-profile");
            ProfileSetupPresentation existing = application.GetSetup(created.Id);
            var screen = new ProfileSetupScreenModel();
            screen.Load(existing);
            screen.Name = "edited-profile";
            screen.CurrentSensitivity = "0.2";

            Assert.That(screen.TryUpdate(application, created.Id, out ProfileSetupPresentation updated), Is.True, screen.StatusMessage);
            Assert.That(updated.Name, Is.EqualTo("edited-profile"));
            Assert.That(updated.CurrentSensitivity, Is.EqualTo(0.2d));
            Assert.That(updated.CrosshairColor, Is.EqualTo("#FFE600"));
        }

        [Test]
        public void InactiveDeletionRequiresConfirmationAndClearsPersistedSelectionAfterDeletion()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation created = Create(application, "delete-profile");
            application.SelectSlot(created.Id);
            AssertProfileError("active_profile_deletion_forbidden", () => application.BeginDeletion(created.Id));

            application.ExitActiveProfile();
            ProfileDeletionConfirmation confirmation = application.BeginDeletion(created.Id);
            Assert.That(confirmation.ProfileName, Is.EqualTo("delete-profile"));
            AssertProfileError("deletion_confirmation_required", () => application.ConfirmDeletion(null));
            application.ConfirmDeletion(confirmation);

            Assert.That(application.ListSlots(), Is.Empty);
            Assert.That(Open().GetActiveSetup(), Is.Null);
        }

        [Test]
        public void AConfirmationCannotDeleteAProfileWhoseIdentityChangedAfterThePrompt()
        {
            ProfileSetupApplicationService application = Open();
            ProfileSlotPresentation created = Create(application, "original-name");
            ProfileDeletionConfirmation confirmation = application.BeginDeletion(created.Id);
            var screen = new ProfileSetupScreenModel();
            screen.Load(application.GetSetup(created.Id));
            screen.Name = "renamed-after-prompt";
            Assert.That(screen.TryUpdate(application, created.Id, out _), Is.True, screen.StatusMessage);

            AssertProfileError("deletion_confirmation_invalid", () => application.ConfirmDeletion(confirmation));
            Assert.That(application.ListSlots(), Has.Count.EqualTo(1));
        }

        private ProfileSetupApplicationService Open() => ProfileSetupApplicationFactory.Open(databasePath, RepositoryRoot(), nativeLibraryPath);

        private static ProfileSlotPresentation Create(ProfileSetupApplicationService application, string name)
        {
            var screen = new ProfileSetupScreenModel
            {
                Name = name, HardwareDpi = "1600", CurrentSensitivity = "0.175", ConfiguredPollingRateHz = "1000",
                MousepadWidthCm = "45", MousepadHeightCm = "40", AdsMultiplier = "1"
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
