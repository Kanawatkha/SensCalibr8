using System;
using System.Globalization;
using SensCalibr8.Core.Domain;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Profiles
{
    public sealed class ProfileLifecycleException : Exception
    {
        public ProfileLifecycleException(string errorCode, Exception innerException = null) : base(errorCode, innerException)
        { ErrorCode = !string.IsNullOrWhiteSpace(errorCode) ? errorCode : throw new ArgumentException("Error code is required.", nameof(errorCode)); }
        public string ErrorCode { get; }
    }

    public interface IActiveProfileStore
    {
        long? ActiveProfileId { get; }
        void Select(long profileId);
        void Clear();
    }

    public sealed class InMemoryActiveProfileStore : IActiveProfileStore
    {
        public long? ActiveProfileId { get; private set; }
        public void Select(long profileId)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            ActiveProfileId = profileId;
        }
        public void Clear() { ActiveProfileId = null; }
    }

    public interface IProfileClock { DateTimeOffset UtcNow { get; } }

    public sealed class SystemProfileClock : IProfileClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public sealed class PersistentActiveProfileStore : IActiveProfileStore
    {
        private readonly ActiveProfileStateRepository state;
        private readonly IProfileClock clock;

        public PersistentActiveProfileStore(ActiveProfileStateRepository state, IProfileClock clock)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public long? ActiveProfileId => state.GetActiveProfileId();

        public void Select(long profileId)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            state.Select(profileId, clock.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }

        public void Clear() { state.Clear(); }
    }

    public sealed class ProfileDeletionConfirmation
    {
        internal ProfileDeletionConfirmation(long profileId, string profileName)
        {
            ProfileId = profileId;
            ProfileName = profileName ?? throw new ArgumentNullException(nameof(profileName));
        }

        public long ProfileId { get; }
        public string ProfileName { get; }
    }

    public abstract class HardwareDpiSelection
    {
        public abstract int ResolveConfirmedDpi();
    }

    public sealed class ManualHardwareDpiSelection : HardwareDpiSelection
    {
        public ManualHardwareDpiSelection(int dpi) { Dpi = ProfileSetupRequest.RequirePositive(dpi, nameof(dpi)); }
        public int Dpi { get; }
        public override int ResolveConfirmedDpi() => Dpi;
    }

    public sealed class PhysicalRulerHardwareDpiSelection : HardwareDpiSelection
    {
        public PhysicalRulerHardwareDpiSelection(double exactEstimatedDpi, int confirmedDpi, bool isConfirmed)
        {
            if (double.IsNaN(exactEstimatedDpi) || double.IsInfinity(exactEstimatedDpi) || exactEstimatedDpi <= 0d) throw new ArgumentOutOfRangeException(nameof(exactEstimatedDpi));
            ExactEstimatedDpi = exactEstimatedDpi;
            SuggestedDpi = SuggestedNearestInteger(exactEstimatedDpi);
            ConfirmedDpi = ProfileSetupRequest.RequirePositive(confirmedDpi, nameof(confirmedDpi));
            IsConfirmed = isConfirmed;
        }
        public double ExactEstimatedDpi { get; }
        public int SuggestedDpi { get; }
        public int ConfirmedDpi { get; }
        public bool IsConfirmed { get; }

        public override int ResolveConfirmedDpi()
        {
            if (!IsConfirmed) throw new ProfileLifecycleException("physical_ruler_dpi_confirmation_required");
            return ConfirmedDpi;
        }

        private static int SuggestedNearestInteger(double value)
        {
            double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            return (int)rounded;
        }
    }

    public sealed class ProfileSetupRequest
    {
        public ProfileSetupRequest(string name, HardwareDpiSelection hardwareDpi, double currentSensitivity, double configuredPollingRateHz,
            DominantHand dominantHand, string crosshairColor, GripStyle gripStyle, MovementStrategy movementStrategy,
            double mousepadWidthCm, double mousepadHeightCm, double adsMultiplier)
        {
            Name = Required(name, nameof(name)); HardwareDpi = hardwareDpi ?? throw new ArgumentNullException(nameof(hardwareDpi));
            CurrentSensitivity = Positive(currentSensitivity, nameof(currentSensitivity)); ConfiguredPollingRateHz = Positive(configuredPollingRateHz, nameof(configuredPollingRateHz));
            DominantHand = dominantHand; CrosshairColor = Required(crosshairColor, nameof(crosshairColor)); GripStyle = gripStyle; MovementStrategy = movementStrategy;
            MousepadWidthCm = Positive(mousepadWidthCm, nameof(mousepadWidthCm)); MousepadHeightCm = Positive(mousepadHeightCm, nameof(mousepadHeightCm)); AdsMultiplier = Finite(adsMultiplier, nameof(adsMultiplier));
        }
        public string Name { get; } public HardwareDpiSelection HardwareDpi { get; } public double CurrentSensitivity { get; } public double ConfiguredPollingRateHz { get; }
        public DominantHand DominantHand { get; } public string CrosshairColor { get; } public GripStyle GripStyle { get; } public MovementStrategy MovementStrategy { get; }
        public double MousepadWidthCm { get; } public double MousepadHeightCm { get; } public double AdsMultiplier { get; }
        internal static int RequirePositive(int value, string field) => value > 0 ? value : throw new ArgumentOutOfRangeException(field);
        internal static string Required(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(field + " is required.", field);
        internal static double Positive(double value, string field) => Finite(value, field) > 0d ? value : throw new ArgumentOutOfRangeException(field);
        internal static double Finite(double value, string field) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(field);
    }

    public sealed class ProfileUpdateRequest
    {
        public ProfileUpdateRequest(string name, HardwareDpiSelection hardwareDpi, double currentSensitivity, double configuredPollingRateHz,
            DominantHand dominantHand, GripStyle gripStyle, MovementStrategy movementStrategy, double mousepadWidthCm, double mousepadHeightCm, double adsMultiplier)
        {
            Name = ProfileSetupRequest.Required(name, nameof(name)); HardwareDpi = hardwareDpi ?? throw new ArgumentNullException(nameof(hardwareDpi));
            CurrentSensitivity = ProfileSetupRequest.Positive(currentSensitivity, nameof(currentSensitivity)); ConfiguredPollingRateHz = ProfileSetupRequest.Positive(configuredPollingRateHz, nameof(configuredPollingRateHz));
            DominantHand = dominantHand; GripStyle = gripStyle; MovementStrategy = movementStrategy; MousepadWidthCm = ProfileSetupRequest.Positive(mousepadWidthCm, nameof(mousepadWidthCm)); MousepadHeightCm = ProfileSetupRequest.Positive(mousepadHeightCm, nameof(mousepadHeightCm)); AdsMultiplier = ProfileSetupRequest.Finite(adsMultiplier, nameof(adsMultiplier));
        }
        public string Name { get; } public HardwareDpiSelection HardwareDpi { get; } public double CurrentSensitivity { get; } public double ConfiguredPollingRateHz { get; }
        public DominantHand DominantHand { get; } public GripStyle GripStyle { get; } public MovementStrategy MovementStrategy { get; }
        public double MousepadWidthCm { get; } public double MousepadHeightCm { get; } public double AdsMultiplier { get; }
    }
}
