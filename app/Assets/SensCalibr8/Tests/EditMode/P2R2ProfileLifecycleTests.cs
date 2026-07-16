using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P2R2ProfileLifecycleTests
    {
        private string tempDirectory;
        private SqliteConnectionFactory connectionFactory;
        private InMemoryActiveProfileStore activeProfiles;
        private ProfileLifecycleService service;
        private readonly FixedClock clock = new FixedClock(new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero));

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p2r2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string databasePath = Path.Combine(tempDirectory, "profiles.sqlite3");
            string nativeLibraryPath = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            FrozenCalibrationConfiguration configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath);
            connectionFactory = new SqliteConnectionFactory(databasePath, nativeLibraryPath);
            activeProfiles = new InMemoryActiveProfileStore();
            service = new ProfileLifecycleService(new ProfileRepository(connectionFactory), activeProfiles, clock);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void CreateListAndSelectMaintainTheActiveProfileAndRequiredFields()
        {
            ProfileRecord created = service.Create(ManualRequest("primary"));

            Assert.That(created.Id, Is.Not.Null);
            Assert.That(created.MouseDpi, Is.EqualTo(1600));
            Assert.That(created.CurrentSensitivity, Is.EqualTo(0.175d));
            Assert.That(created.ConfiguredPollingRateHz, Is.EqualTo(1000d));
            Assert.That(created.CreatedDate, Is.EqualTo(clock.UtcNow.ToString("O")));
            Assert.That(service.List(), Has.Count.EqualTo(1));

            ProfileRecord selected = service.Select(created.Id.Value);
            Assert.That(activeProfiles.ActiveProfileId, Is.EqualTo(created.Id));
            Assert.That(service.GetActive().Id, Is.EqualTo(created.Id));
            Assert.That(selected.LastActiveDate, Is.EqualTo(clock.UtcNow.ToString("O")));
        }

        [Test]
        public void DuplicateNamesAreRejectedBeforePersistence()
        {
            service.Create(ManualRequest("unique-name"));
            AssertProfileError("profile_name_duplicate", () => service.Create(ManualRequest("unique-name")));
            Assert.That(service.List(), Has.Count.EqualTo(1));
        }

        [Test]
        public void PhysicalRulerEstimateRequiresExplicitConfirmationAndPreservesUserConfirmedInteger()
        {
            var unconfirmed = new PhysicalRulerHardwareDpiSelection(1600.6d, 1601, false);
            Assert.That(unconfirmed.ExactEstimatedDpi, Is.EqualTo(1600.6d));
            Assert.That(unconfirmed.SuggestedDpi, Is.EqualTo(1601));
            AssertProfileError("physical_ruler_dpi_confirmation_required", () => service.Create(PhysicalRequest("unconfirmed", unconfirmed)));
            Assert.That(service.List(), Is.Empty);

            var confirmed = new PhysicalRulerHardwareDpiSelection(1600.6d, 1600, true);
            ProfileRecord persisted = service.Create(PhysicalRequest("confirmed", confirmed));
            Assert.That(persisted.MouseDpi, Is.EqualTo(1600));
        }

        [Test]
        public void UpdateChangesEditableFieldsButCannotChangeTheLockedCrosshairConfiguration()
        {
            ProfileRecord created = service.Create(ManualRequest("editable"));
            ProfileRecord updated = service.Update(created.Id.Value, new ProfileUpdateRequest("renamed", new ManualHardwareDpiSelection(800), 0.35d, 500d,
                DominantHand.Left, GripStyle.Palm, MovementStrategy.Arm, 50d, 45d, 1d));

            Assert.That(updated.Name, Is.EqualTo("renamed"));
            Assert.That(updated.MouseDpi, Is.EqualTo(800));
            Assert.That(updated.DominantHand, Is.EqualTo("left"));
            Assert.That(updated.CrosshairConfig, Is.EqualTo("#FFFFFF"));
            Assert.That(updated.GripStyle, Is.EqualTo("palm"));
            Assert.That(updated.MovementStrategy, Is.EqualTo("arm"));
            Assert.That(typeof(ProfileUpdateRequest).GetProperty("CrosshairColor", BindingFlags.Instance | BindingFlags.Public), Is.Null);
        }

        [Test]
        public void ActiveProfileCannotBeDeletedAndInactiveDeletionUsesTheSchemaCascade()
        {
            ProfileRecord created = service.Create(ManualRequest("deletable"));
            new ProtocolRepository(connectionFactory).CreateCycle(new CycleRecord(null, created.Id.Value, 1, "2026-07-16", null, null));
            service.Select(created.Id.Value);

            AssertProfileError("active_profile_deletion_forbidden", () => service.BeginInactiveDeletion(created.Id.Value));
            Assert.That(service.List(), Has.Count.EqualTo(1));

            service.ExitActiveProfile();
            ProfileDeletionConfirmation confirmation = service.BeginInactiveDeletion(created.Id.Value);
            service.ConfirmInactiveDeletion(confirmation);
            Assert.That(service.List(), Is.Empty);
            using SqliteDatabaseConnection connection = connectionFactory.Open();
            Assert.That(Convert.ToInt32(connection.Scalar("SELECT COUNT(*) FROM cycles;")), Is.EqualTo(0));
        }

        [Test]
        public void UnknownProfilesCannotBeSelectedUpdatedOrDeleted()
        {
            AssertProfileError("profile_not_found", () => service.Select(1));
            AssertProfileError("profile_not_found", () => service.Update(1, UpdateRequest("missing")));
            AssertProfileError("profile_not_found", () => service.BeginInactiveDeletion(1));
        }

        private static ProfileSetupRequest ManualRequest(string name) => new ProfileSetupRequest(name, new ManualHardwareDpiSelection(1600), 0.175d, 1000d,
            DominantHand.Right, "#FFFFFF", GripStyle.Claw, MovementStrategy.Wrist, 45d, 40d, 1d);

        private static ProfileSetupRequest PhysicalRequest(string name, HardwareDpiSelection selection) => new ProfileSetupRequest(name, selection, 0.175d, 1000d,
            DominantHand.Right, "#FFFFFF", GripStyle.Claw, MovementStrategy.Wrist, 45d, 40d, 1d);

        private static ProfileUpdateRequest UpdateRequest(string name) => new ProfileUpdateRequest(name, new ManualHardwareDpiSelection(1600), 0.175d, 1000d,
            DominantHand.Right, GripStyle.Claw, MovementStrategy.Wrist, 45d, 40d, 1d);

        private static void AssertProfileError(string expectedCode, TestDelegate action)
        {
            ProfileLifecycleException exception = Assert.Throws<ProfileLifecycleException>(action);
            Assert.That(exception.ErrorCode, Is.EqualTo(expectedCode));
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class FixedClock : IProfileClock
        {
            public FixedClock(DateTimeOffset utcNow) { UtcNow = utcNow; }
            public DateTimeOffset UtcNow { get; }
        }
    }
}
