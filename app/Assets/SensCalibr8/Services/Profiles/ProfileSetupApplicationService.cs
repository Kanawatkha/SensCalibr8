using System;
using System.Collections.Generic;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Validation;

namespace SensCalibr8.Services.Profiles
{
    public sealed class ProfileSlotPresentation
    {
        public ProfileSlotPresentation(long id, string name, string lastActiveDate) { Id = id; Name = name; LastActiveDate = lastActiveDate; }
        public long Id { get; }
        public string Name { get; }
        public string LastActiveDate { get; }
    }

    public sealed class PhysicalRulerDpiPreview
    {
        public PhysicalRulerDpiPreview(double exactEstimatedDpi, int suggestedDpi) { ExactEstimatedDpi = exactEstimatedDpi; SuggestedDpi = suggestedDpi; }
        public double ExactEstimatedDpi { get; }
        public int SuggestedDpi { get; }
    }

    public sealed class ProfileSetupPresentation
    {
        public ProfileSetupPresentation(long id, string name, long mouseDpi, double currentSensitivity, double configuredPollingRateHz,
            string dominantHand, string crosshairColor, string gripStyle, string movementStrategy, double mousepadWidthCm,
            double mousepadHeightCm, double adsMultiplier)
        {
            Id = id; Name = name; MouseDpi = mouseDpi; CurrentSensitivity = currentSensitivity; ConfiguredPollingRateHz = configuredPollingRateHz;
            DominantHand = dominantHand; CrosshairColor = crosshairColor; GripStyle = gripStyle; MovementStrategy = movementStrategy;
            MousepadWidthCm = mousepadWidthCm; MousepadHeightCm = mousepadHeightCm; AdsMultiplier = adsMultiplier;
        }

        public long Id { get; } public string Name { get; } public long MouseDpi { get; } public double CurrentSensitivity { get; }
        public double ConfiguredPollingRateHz { get; } public string DominantHand { get; } public string CrosshairColor { get; }
        public string GripStyle { get; } public string MovementStrategy { get; } public double MousepadWidthCm { get; }
        public double MousepadHeightCm { get; } public double AdsMultiplier { get; }
    }

    public sealed class ProfileSetupApplicationService
    {
        private readonly ProfileLifecycleService lifecycle;
        private readonly SensitivityCalculationService calculations;
        private readonly ErgonomicWarningService ergonomicWarnings;
        private readonly ProfileDashboardService dashboard;

        public ProfileSetupApplicationService(ProfileLifecycleService lifecycle, SensitivityCalculationService calculations)
            : this(lifecycle, calculations, null) { }

        public ProfileSetupApplicationService(ProfileLifecycleService lifecycle, SensitivityCalculationService calculations, ErgonomicWarningService ergonomicWarnings)
            : this(lifecycle, calculations, ergonomicWarnings, null) { }

        public ProfileSetupApplicationService(ProfileLifecycleService lifecycle, SensitivityCalculationService calculations, ErgonomicWarningService ergonomicWarnings, ProfileDashboardService dashboard)
        {
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.calculations = calculations ?? throw new ArgumentNullException(nameof(calculations));
            this.ergonomicWarnings = ergonomicWarnings;
            this.dashboard = dashboard;
        }

        public IReadOnlyList<ProfileSlotPresentation> ListSlots()
        {
            var slots = new List<ProfileSlotPresentation>();
            foreach (ProfileRecord profile in lifecycle.List()) slots.Add(new ProfileSlotPresentation(profile.Id.Value, profile.Name, profile.LastActiveDate));
            return slots.AsReadOnly();
        }

        public ProfileSlotPresentation SelectSlot(long profileId)
        {
            ProfileRecord profile = lifecycle.Select(profileId);
            return new ProfileSlotPresentation(profile.Id.Value, profile.Name, profile.LastActiveDate);
        }

        public ProfileSetupPresentation GetActiveSetup()
        {
            ProfileRecord profile = lifecycle.GetActive();
            return profile == null ? null : Present(profile);
        }

        public ProfileSetupPresentation GetSetup(long profileId) => Present(lifecycle.Get(profileId));

        public void ExitActiveProfile() => lifecycle.ExitActiveProfile();

        public ProfileDeletionConfirmation BeginDeletion(long profileId) => lifecycle.BeginInactiveDeletion(profileId);

        public void ConfirmDeletion(ProfileDeletionConfirmation confirmation) => lifecycle.ConfirmInactiveDeletion(confirmation);

        public IReadOnlyList<ErgonomicWarningPresentation> RefreshActiveWarnings()
        {
            if (ergonomicWarnings == null) throw new InvalidOperationException("Ergonomic warning service is unavailable.");
            ProfileRecord profile = lifecycle.GetActive();
            return profile == null ? Array.Empty<ErgonomicWarningPresentation>() : ergonomicWarnings.EvaluateAndList(Present(profile));
        }

        public void AcknowledgeActiveWarning(long flagId)
        {
            if (ergonomicWarnings == null) throw new InvalidOperationException("Ergonomic warning service is unavailable.");
            ProfileRecord profile = lifecycle.GetActive();
            if (profile == null) throw new ProfileLifecycleException("active_profile_required");
            ergonomicWarnings.Acknowledge(profile.Id.Value, flagId);
        }

        public ProfileDashboardPresentation GetActiveDashboard()
        {
            if (dashboard == null) throw new InvalidOperationException("Profile dashboard service is unavailable.");
            ProfileRecord profile = lifecycle.GetActive();
            return profile == null ? null : dashboard.Load(Present(profile));
        }

        public PhysicalRulerDpiPreview PreviewPhysicalRuler(string counts, string distanceCm)
        {
            ValidationResult<long> countResult = SetupInputValidationService.PhysicalRulerCounts(counts);
            if (!countResult.IsValid) throw new ProfileLifecycleException(countResult.ErrorCode);
            ValidationResult<double> distanceResult = SetupInputValidationService.PhysicalRulerDistanceCm(distanceCm);
            if (!distanceResult.IsValid) throw new ProfileLifecycleException(distanceResult.ErrorCode);
            double exact = calculations.CalculatePhysicalRulerDpi(countResult.Value, distanceResult.Value);
            var selection = new PhysicalRulerHardwareDpiSelection(exact, SuggestedDpi(exact), false);
            return new PhysicalRulerDpiPreview(selection.ExactEstimatedDpi, selection.SuggestedDpi);
        }

        public ProfileSlotPresentation Create(ProfileSetupSubmission submission)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));
            if (!CrosshairPalette.IsSupported(submission.CrosshairColor)) throw new ProfileLifecycleException("crosshair_color_not_supported");
            ProfileRecord profile = lifecycle.Create(new ProfileSetupRequest(submission.Name, submission.HardwareDpi, submission.CurrentSensitivity,
                submission.ConfiguredPollingRateHz, submission.DominantHand, submission.CrosshairColor, submission.GripStyle,
                submission.MovementStrategy, submission.MousepadWidthCm, submission.MousepadHeightCm, submission.AdsMultiplier));
            return new ProfileSlotPresentation(profile.Id.Value, profile.Name, profile.LastActiveDate);
        }

        public ProfileSetupPresentation Update(long profileId, ProfileSetupUpdateSubmission submission)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));
            ProfileRecord profile = lifecycle.Update(profileId, new ProfileUpdateRequest(submission.Name, submission.HardwareDpi,
                submission.CurrentSensitivity, submission.ConfiguredPollingRateHz, submission.DominantHand, submission.GripStyle,
                submission.MovementStrategy, submission.MousepadWidthCm, submission.MousepadHeightCm, submission.AdsMultiplier));
            return Present(profile);
        }

        private static int SuggestedDpi(double exact)
        {
            if (double.IsNaN(exact) || double.IsInfinity(exact) || exact <= 0d || exact > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(exact));
            return (int)Math.Round(exact, MidpointRounding.AwayFromZero);
        }

        private static ProfileSetupPresentation Present(ProfileRecord profile) => new ProfileSetupPresentation(profile.Id.Value, profile.Name,
            profile.MouseDpi, profile.CurrentSensitivity, profile.ConfiguredPollingRateHz, profile.DominantHand, profile.CrosshairConfig,
            profile.GripStyle, profile.MovementStrategy, profile.MousepadWidthCm, profile.MousepadHeightCm, profile.AdsMultiplier);
    }

    public sealed class ProfileSetupSubmission
    {
        public ProfileSetupSubmission(string name, HardwareDpiSelection hardwareDpi, double currentSensitivity, double configuredPollingRateHz,
            DominantHand dominantHand, string crosshairColor, GripStyle gripStyle, MovementStrategy movementStrategy,
            double mousepadWidthCm, double mousepadHeightCm, double adsMultiplier)
        {
            Name = name; HardwareDpi = hardwareDpi; CurrentSensitivity = currentSensitivity; ConfiguredPollingRateHz = configuredPollingRateHz;
            DominantHand = dominantHand; CrosshairColor = crosshairColor; GripStyle = gripStyle; MovementStrategy = movementStrategy;
            MousepadWidthCm = mousepadWidthCm; MousepadHeightCm = mousepadHeightCm; AdsMultiplier = adsMultiplier;
        }

        public string Name { get; }
        public HardwareDpiSelection HardwareDpi { get; }
        public double CurrentSensitivity { get; }
        public double ConfiguredPollingRateHz { get; }
        public DominantHand DominantHand { get; }
        public string CrosshairColor { get; }
        public GripStyle GripStyle { get; }
        public MovementStrategy MovementStrategy { get; }
        public double MousepadWidthCm { get; }
        public double MousepadHeightCm { get; }
        public double AdsMultiplier { get; }
    }

    public sealed class ProfileSetupUpdateSubmission
    {
        public ProfileSetupUpdateSubmission(string name, HardwareDpiSelection hardwareDpi, double currentSensitivity, double configuredPollingRateHz,
            DominantHand dominantHand, GripStyle gripStyle, MovementStrategy movementStrategy, double mousepadWidthCm,
            double mousepadHeightCm, double adsMultiplier)
        {
            Name = name; HardwareDpi = hardwareDpi; CurrentSensitivity = currentSensitivity; ConfiguredPollingRateHz = configuredPollingRateHz;
            DominantHand = dominantHand; GripStyle = gripStyle; MovementStrategy = movementStrategy; MousepadWidthCm = mousepadWidthCm;
            MousepadHeightCm = mousepadHeightCm; AdsMultiplier = adsMultiplier;
        }

        public string Name { get; } public HardwareDpiSelection HardwareDpi { get; } public double CurrentSensitivity { get; }
        public double ConfiguredPollingRateHz { get; } public DominantHand DominantHand { get; } public GripStyle GripStyle { get; }
        public MovementStrategy MovementStrategy { get; } public double MousepadWidthCm { get; } public double MousepadHeightCm { get; }
        public double AdsMultiplier { get; }
    }
}
