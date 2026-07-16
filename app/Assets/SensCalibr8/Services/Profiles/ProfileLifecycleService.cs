using System;
using System.Collections.Generic;
using System.Globalization;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Profiles
{
    public sealed class ProfileLifecycleService
    {
        private readonly ProfileRepository profiles;
        private readonly IActiveProfileStore activeProfiles;
        private readonly IProfileClock clock;

        public ProfileLifecycleService(ProfileRepository profiles, IActiveProfileStore activeProfiles, IProfileClock clock)
        {
            this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            this.activeProfiles = activeProfiles ?? throw new ArgumentNullException(nameof(activeProfiles));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public ProfileRecord Create(ProfileSetupRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RejectDuplicateName(request.Name, null);
            string now = UtcNow();
            var record = new ProfileRecord(null, request.Name, now, request.HardwareDpi.ResolveConfirmedDpi(), request.CurrentSensitivity,
                request.ConfiguredPollingRateHz, ToStorage(request.DominantHand), request.CrosshairColor, ToStorage(request.GripStyle),
                ToStorage(request.MovementStrategy), request.MousepadWidthCm, request.MousepadHeightCm, request.AdsMultiplier, now);
            try { return profiles.Create(record); }
            catch (DataAccessException exception) when (exception.FailureKind == DataFailureKind.ConstraintViolation)
            { throw new ProfileLifecycleException("profile_name_duplicate", exception); }
        }

        public IReadOnlyList<ProfileRecord> List() => profiles.List();

        public ProfileRecord Get(long profileId) => RequireProfile(profileId);

        public ProfileRecord Select(long profileId)
        {
            ProfileRecord existing = RequireProfile(profileId);
            ProfileRecord touched = profiles.UpdatePreservingCrosshair(Copy(existing, existing.Name, existing.MouseDpi, existing.CurrentSensitivity,
                existing.ConfiguredPollingRateHz, existing.DominantHand, existing.CrosshairConfig, existing.GripStyle, existing.MovementStrategy, existing.MousepadWidthCm,
                existing.MousepadHeightCm, existing.AdsMultiplier, UtcNow()));
            activeProfiles.Select(touched.Id.Value);
            return touched;
        }

        public ProfileRecord GetActive()
        {
            if (!activeProfiles.ActiveProfileId.HasValue) return null;
            ProfileRecord profile = profiles.FindById(activeProfiles.ActiveProfileId.Value);
            if (profile == null) activeProfiles.Clear();
            return profile;
        }

        public void ExitActiveProfile() => activeProfiles.Clear();

        public ProfileRecord Update(long profileId, ProfileUpdateRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ProfileRecord existing = RequireProfile(profileId);
            RejectDuplicateName(request.Name, profileId);
            ProfileRecord updated = Copy(existing, request.Name, request.HardwareDpi.ResolveConfirmedDpi(), request.CurrentSensitivity,
                request.ConfiguredPollingRateHz, ToStorage(request.DominantHand), existing.CrosshairConfig, ToStorage(request.GripStyle),
                ToStorage(request.MovementStrategy), request.MousepadWidthCm, request.MousepadHeightCm, request.AdsMultiplier, existing.LastActiveDate);
            try { return profiles.UpdatePreservingCrosshair(updated); }
            catch (DataAccessException exception) when (exception.FailureKind == DataFailureKind.ConstraintViolation)
            { throw new ProfileLifecycleException("profile_name_duplicate", exception); }
        }

        public ProfileDeletionConfirmation BeginInactiveDeletion(long profileId)
        {
            if (activeProfiles.ActiveProfileId == profileId) throw new ProfileLifecycleException("active_profile_deletion_forbidden");
            ProfileRecord profile = RequireProfile(profileId);
            return new ProfileDeletionConfirmation(profile.Id.Value, profile.Name);
        }

        public void ConfirmInactiveDeletion(ProfileDeletionConfirmation confirmation)
        {
            if (confirmation == null) throw new ProfileLifecycleException("deletion_confirmation_required");
            if (activeProfiles.ActiveProfileId == confirmation.ProfileId) throw new ProfileLifecycleException("active_profile_deletion_forbidden");
            ProfileRecord profile = RequireProfile(confirmation.ProfileId);
            if (!string.Equals(profile.Name, confirmation.ProfileName, StringComparison.Ordinal)) throw new ProfileLifecycleException("deletion_confirmation_invalid");
            if (!profiles.DeleteById(confirmation.ProfileId)) throw new ProfileLifecycleException("profile_not_found");
        }

        private ProfileRecord RequireProfile(long profileId)
        {
            if (profileId <= 0) throw new ProfileLifecycleException("profile_not_found");
            ProfileRecord profile = profiles.FindById(profileId);
            return profile ?? throw new ProfileLifecycleException("profile_not_found");
        }

        private void RejectDuplicateName(string name, long? excludedId)
        {
            ProfileRecord existing = profiles.FindByName(name);
            if (existing != null && existing.Id != excludedId) throw new ProfileLifecycleException("profile_name_duplicate");
        }

        private string UtcNow() => clock.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        private static string ToStorage(object value) => value.ToString().ToLowerInvariant();
        private static ProfileRecord Copy(ProfileRecord existing, string name, long dpi, double sensitivity, double pollingRate, string dominantHand, string crosshair, string grip, string movement, double width, double height, double ads, string lastActiveDate) => new ProfileRecord(existing.Id, name, existing.CreatedDate, dpi, sensitivity, pollingRate, dominantHand, crosshair, grip, movement, width, height, ads, lastActiveDate);
    }
}
