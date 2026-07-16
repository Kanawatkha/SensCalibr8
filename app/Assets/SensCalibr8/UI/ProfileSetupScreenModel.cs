using System;
using System.Globalization;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Profiles;
using SensCalibr8.Services.Validation;

namespace SensCalibr8.UI
{
    public sealed class ProfileSetupScreenModel
    {
        public string Name = string.Empty;
        public string HardwareDpi = string.Empty;
        public string CurrentSensitivity = string.Empty;
        public string ConfiguredPollingRateHz = string.Empty;
        public string MousepadWidthCm = string.Empty;
        public string MousepadHeightCm = string.Empty;
        public string AdsMultiplier = string.Empty;
        public string PhysicalRulerCounts = string.Empty;
        public string PhysicalRulerDistanceCm = string.Empty;
        public string ConfirmedPhysicalRulerDpi = string.Empty;
        public bool UsePhysicalRuler;
        public bool IsPhysicalRulerDpiConfirmed;
        public DominantHand DominantHand = DominantHand.Right;
        public GripStyle GripStyle = GripStyle.Claw;
        public MovementStrategy MovementStrategy = MovementStrategy.Wrist;
        public string StatusMessage { get; private set; } = string.Empty;
        public PhysicalRulerDpiPreview PhysicalRulerPreview { get; private set; }

        public void Load(ProfileSetupPresentation profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Name = profile.Name; HardwareDpi = profile.MouseDpi.ToString(CultureInfo.InvariantCulture);
            CurrentSensitivity = profile.CurrentSensitivity.ToString("G17", CultureInfo.InvariantCulture);
            ConfiguredPollingRateHz = profile.ConfiguredPollingRateHz.ToString("G17", CultureInfo.InvariantCulture);
            MousepadWidthCm = profile.MousepadWidthCm.ToString("G17", CultureInfo.InvariantCulture);
            MousepadHeightCm = profile.MousepadHeightCm.ToString("G17", CultureInfo.InvariantCulture);
            AdsMultiplier = profile.AdsMultiplier.ToString("G17", CultureInfo.InvariantCulture);
            DominantHand = (DominantHand)Enum.Parse(typeof(DominantHand), profile.DominantHand, true);
            GripStyle = (GripStyle)Enum.Parse(typeof(GripStyle), profile.GripStyle, true);
            MovementStrategy = (MovementStrategy)Enum.Parse(typeof(MovementStrategy), profile.MovementStrategy, true);
            UsePhysicalRuler = false; IsPhysicalRulerDpiConfirmed = false; PhysicalRulerPreview = null;
            StatusMessage = "Editing setup. Crosshair color remains locked.";
        }

        public bool TryPreviewPhysicalRuler(ProfileSetupApplicationService application)
        {
            try
            {
                PhysicalRulerPreview = application.PreviewPhysicalRuler(PhysicalRulerCounts, PhysicalRulerDistanceCm);
                ConfirmedPhysicalRulerDpi = PhysicalRulerPreview.SuggestedDpi.ToString(CultureInfo.InvariantCulture);
                StatusMessage = "Confirm or edit the suggested DPI before creating the profile.";
                return true;
            }
            catch (ProfileLifecycleException exception) { StatusMessage = exception.ErrorCode; return false; }
        }

        public bool TryCreate(ProfileSetupApplicationService application, string crosshairColor, out ProfileSlotPresentation created)
        {
            created = null;
            if (string.IsNullOrWhiteSpace(Name)) return Fail("profile_name_required");
            if (string.IsNullOrWhiteSpace(crosshairColor)) return Fail("crosshair_color_selection_required");
            if (!TryPositive(SetupInputValidationService.CurrentSensitivity(CurrentSensitivity), out double sensitivity)) return false;
            if (!TryPositive(SetupInputValidationService.ConfiguredPollingRateHz(ConfiguredPollingRateHz), out double polling)) return false;
            if (!TryPositive(SetupInputValidationService.MousepadWidthCm(MousepadWidthCm), out double width)) return false;
            if (!TryPositive(SetupInputValidationService.MousepadHeightCm(MousepadHeightCm), out double height)) return false;
            if (!double.TryParse(AdsMultiplier, NumberStyles.Float, CultureInfo.InvariantCulture, out double ads) || double.IsNaN(ads) || double.IsInfinity(ads)) return Fail("ads_multiplier_finite_number_required");

            HardwareDpiSelection dpi;
            if (UsePhysicalRuler)
            {
                if (PhysicalRulerPreview == null) return Fail("physical_ruler_preview_required");
                ValidationResult<int> confirmed = SetupInputValidationService.HardwareDpi(ConfirmedPhysicalRulerDpi);
                if (!confirmed.IsValid) return Fail(confirmed.ErrorCode);
                dpi = new PhysicalRulerHardwareDpiSelection(PhysicalRulerPreview.ExactEstimatedDpi, confirmed.Value, IsPhysicalRulerDpiConfirmed);
            }
            else
            {
                ValidationResult<int> manual = SetupInputValidationService.HardwareDpi(HardwareDpi);
                if (!manual.IsValid) return Fail(manual.ErrorCode);
                dpi = new ManualHardwareDpiSelection(manual.Value);
            }

            try
            {
                created = application.Create(new ProfileSetupSubmission(Name, dpi, sensitivity, polling, DominantHand, crosshairColor,
                    GripStyle, MovementStrategy, width, height, ads));
                StatusMessage = "Profile created.";
                return true;
            }
            catch (ProfileLifecycleException exception) { return Fail(exception.ErrorCode); }
        }

        public bool TryUpdate(ProfileSetupApplicationService application, long profileId, out ProfileSetupPresentation updated)
        {
            updated = null;
            if (string.IsNullOrWhiteSpace(Name)) return Fail("profile_name_required");
            if (!TryPositive(SetupInputValidationService.CurrentSensitivity(CurrentSensitivity), out double sensitivity)) return false;
            if (!TryPositive(SetupInputValidationService.ConfiguredPollingRateHz(ConfiguredPollingRateHz), out double polling)) return false;
            if (!TryPositive(SetupInputValidationService.MousepadWidthCm(MousepadWidthCm), out double width)) return false;
            if (!TryPositive(SetupInputValidationService.MousepadHeightCm(MousepadHeightCm), out double height)) return false;
            if (!double.TryParse(AdsMultiplier, NumberStyles.Float, CultureInfo.InvariantCulture, out double ads) || double.IsNaN(ads) || double.IsInfinity(ads)) return Fail("ads_multiplier_finite_number_required");

            HardwareDpiSelection dpi;
            if (UsePhysicalRuler)
            {
                if (PhysicalRulerPreview == null) return Fail("physical_ruler_preview_required");
                ValidationResult<int> confirmed = SetupInputValidationService.HardwareDpi(ConfirmedPhysicalRulerDpi);
                if (!confirmed.IsValid) return Fail(confirmed.ErrorCode);
                dpi = new PhysicalRulerHardwareDpiSelection(PhysicalRulerPreview.ExactEstimatedDpi, confirmed.Value, IsPhysicalRulerDpiConfirmed);
            }
            else
            {
                ValidationResult<int> manual = SetupInputValidationService.HardwareDpi(HardwareDpi);
                if (!manual.IsValid) return Fail(manual.ErrorCode);
                dpi = new ManualHardwareDpiSelection(manual.Value);
            }

            try
            {
                updated = application.Update(profileId, new ProfileSetupUpdateSubmission(Name, dpi, sensitivity, polling, DominantHand,
                    GripStyle, MovementStrategy, width, height, ads));
                StatusMessage = "Profile updated. Crosshair color remains locked.";
                return true;
            }
            catch (ProfileLifecycleException exception) { return Fail(exception.ErrorCode); }
        }

        private bool TryPositive(ValidationResult<double> result, out double value)
        {
            value = 0d;
            if (!result.IsValid) return Fail(result.ErrorCode);
            value = result.Value;
            return true;
        }

        private bool Fail(string message) { StatusMessage = message; return false; }
    }
}
