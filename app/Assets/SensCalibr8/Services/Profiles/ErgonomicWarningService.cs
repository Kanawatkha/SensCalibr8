using System;
using System.Collections.Generic;
using System.Globalization;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Calculations;

namespace SensCalibr8.Services.Profiles
{
    public sealed class ErgonomicWarningPresentation
    {
        public ErgonomicWarningPresentation(long id, string flagType, string message, double edpiAtTrigger, bool acknowledged)
        { Id = id; FlagType = flagType; Message = message; EdpiAtTrigger = edpiAtTrigger; Acknowledged = acknowledged; }
        public long Id { get; } public string FlagType { get; } public string Message { get; } public double EdpiAtTrigger { get; } public bool Acknowledged { get; }
    }

    public sealed class ErgonomicWarningService
    {
        public const string LowEdpiWristStrain = "low_edpi_wrist_strain";
        public const string MousepadConstraintViolation = "mousepad_constraint_violation";

        private readonly InjuryRiskFlagRepository flags;
        private readonly SensitivityCalculationService calculations;
        private readonly ResearchConstants constants;
        private readonly IProfileClock clock;

        public ErgonomicWarningService(InjuryRiskFlagRepository flags, SensitivityCalculationService calculations, ResearchConstants constants, IProfileClock clock)
        {
            this.flags = flags ?? throw new ArgumentNullException(nameof(flags)); this.calculations = calculations ?? throw new ArgumentNullException(nameof(calculations));
            this.constants = constants ?? throw new ArgumentNullException(nameof(constants)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public IReadOnlyList<ErgonomicWarningPresentation> EvaluateAndList(ProfileSetupPresentation profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            double edpi = calculations.CalculateEdpi(checked((int)profile.MouseDpi), profile.CurrentSensitivity);
            if (edpi < constants.WristWarningEdpiExclusiveUpper && string.Equals(profile.MovementStrategy, "wrist", StringComparison.Ordinal))
                EnsureFlag(profile.Id, LowEdpiWristStrain, edpi);
            if (calculations.EvaluateMousepadConstraint(checked((int)profile.MouseDpi), profile.CurrentSensitivity, profile.MousepadWidthCm).WarningRequired)
                EnsureFlag(profile.Id, MousepadConstraintViolation, edpi);
            return List(profile.Id);
        }

        public IReadOnlyList<ErgonomicWarningPresentation> List(long profileId)
        {
            var result = new List<ErgonomicWarningPresentation>();
            foreach (InjuryRiskFlagRecord flag in flags.ListForProfile(profileId))
                result.Add(new ErgonomicWarningPresentation(flag.Id.Value, flag.FlagType, Message(flag.FlagType), flag.EdpiAtTrigger, flag.Acknowledged));
            return result.AsReadOnly();
        }

        public void Acknowledge(long profileId, long flagId)
        {
            if (!flags.Acknowledge(profileId, flagId)) throw new ProfileLifecycleException("injury_risk_flag_not_found_or_acknowledged");
        }

        private void EnsureFlag(long profileId, string type, double edpi)
        {
            if (!flags.ExistsUnacknowledged(profileId, type, edpi))
                flags.Create(new InjuryRiskFlagRecord(null, profileId, type, clock.UtcNow.ToString("O", CultureInfo.InvariantCulture), edpi, false));
        }

        private static string Message(string type)
        {
            if (type == LowEdpiWristStrain) return "Informational ergonomic notice: low eDPI with wrist movement may increase repetitive-strain concern. This is not medical advice.";
            if (type == MousepadConstraintViolation) return "Informational setup notice: the calculated cm/360 exceeds the configured mousepad width and may require frequent mouse lifting.";
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
