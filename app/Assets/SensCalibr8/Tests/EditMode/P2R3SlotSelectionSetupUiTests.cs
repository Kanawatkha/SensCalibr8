using System;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Profiles;
using SensCalibr8.UI;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P2R3SlotSelectionSetupUiTests
    {
        private string tempDirectory;
        private ProfileSetupApplicationService application;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "senscalibr8-p2r3-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string databasePath = Path.Combine(tempDirectory, "profiles.sqlite3");
            string nativeLibraryPath = Path.Combine(RepositoryRoot(), "app", "Assets", "Plugins", "sqlite3.dll");
            FrozenCalibrationConfiguration configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            new SqliteDatabaseBootstrapper().Initialize(databasePath, configuration, nativeLibraryPath);
            var lifecycle = new ProfileLifecycleService(new ProfileRepository(new SqliteConnectionFactory(databasePath, nativeLibraryPath)), new InMemoryActiveProfileStore(), new FixedClock());
            application = new ProfileSetupApplicationService(lifecycle, new SensitivityCalculationService(ResearchConstantsLoader.LoadFromRepository(RepositoryRoot())));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void ManualSetupCreatesAndSelectsASlotThroughThePresentationContracts()
        {
            var screen = CompleteManualScreen();
            Assert.That(screen.TryCreate(application, "#FFE600", out ProfileSlotPresentation created), Is.True, screen.StatusMessage);
            Assert.That(created.Name, Is.EqualTo("UI profile"));
            Assert.That(application.ListSlots(), Has.Count.EqualTo(1));

            ProfileSlotPresentation selected = application.SelectSlot(created.Id);
            Assert.That(selected.Id, Is.EqualTo(created.Id));
            Assert.That(selected.LastActiveDate, Does.Contain("2026-07-16"));
        }

        [Test]
        public void SetupScreenRejectsInvalidRequiredInputBeforeItCallsTheProfileLifecycle()
        {
            var screen = CompleteManualScreen();
            screen.HardwareDpi = "1600.5";
            Assert.That(screen.TryCreate(application, "#FFE600", out _), Is.False);
            Assert.That(screen.StatusMessage, Is.EqualTo("hardware_dpi_positive_integer_required"));
            Assert.That(application.ListSlots(), Is.Empty);
        }

        [Test]
        public void PhysicalRulerShowsExactAndSuggestedDpiThenRequiresConfirmationBeforeCreating()
        {
            var screen = CompleteManualScreen();
            screen.UsePhysicalRuler = true;
            screen.PhysicalRulerCounts = "1600";
            screen.PhysicalRulerDistanceCm = "2.54";
            Assert.That(screen.TryPreviewPhysicalRuler(application), Is.True, screen.StatusMessage);
            Assert.That(screen.PhysicalRulerPreview.ExactEstimatedDpi, Is.EqualTo(1600d));
            Assert.That(screen.ConfirmedPhysicalRulerDpi, Is.EqualTo("1600"));

            Assert.That(screen.TryCreate(application, "#FFE600", out _), Is.False);
            Assert.That(screen.StatusMessage, Is.EqualTo("physical_ruler_dpi_confirmation_required"));
            screen.IsPhysicalRulerDpiConfirmed = true;
            Assert.That(screen.TryCreate(application, "#FFE600", out ProfileSlotPresentation created), Is.True, screen.StatusMessage);
            Assert.That(created.Name, Is.EqualTo("UI profile"));
        }

        [Test]
        public void SetupScreenRequiresAColorSelectionAndDoesNotExposeCrosshairStyleOrSizeAsInputs()
        {
            var screen = CompleteManualScreen();
            Assert.That(screen.TryCreate(application, string.Empty, out _), Is.False);
            Assert.That(screen.StatusMessage, Is.EqualTo("crosshair_color_selection_required"));
            Assert.That(typeof(ProfileSetupScreenModel).GetField("CrosshairStyle"), Is.Null);
            Assert.That(typeof(ProfileSetupScreenModel).GetField("CrosshairSize"), Is.Null);
        }

        [Test]
        public void SetupScreenPersistsOnlyTheOwnerApprovedPaletteValues()
        {
            Assert.That(CrosshairPalette.SupportedColors, Is.EquivalentTo(new[] { "#FFE600", "#FF00FF", "#FF3B30", "#FF9500" }));
            var screen = CompleteManualScreen();
            Assert.That(screen.TryCreate(application, "#00FFFF", out _), Is.False);
            Assert.That(screen.StatusMessage, Is.EqualTo("crosshair_color_not_supported"));
        }

        private static ProfileSetupScreenModel CompleteManualScreen()
        {
            return new ProfileSetupScreenModel
            {
                Name = "UI profile", HardwareDpi = "1600", CurrentSensitivity = "0.175", ConfiguredPollingRateHz = "1000",
                MousepadWidthCm = "45", MousepadHeightCm = "40", AdsMultiplier = "1"
            };
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class FixedClock : IProfileClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        }
    }
}
